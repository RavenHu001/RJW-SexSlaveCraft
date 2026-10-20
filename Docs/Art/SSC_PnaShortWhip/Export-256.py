"""Export approved whip poses without redrawing. Requires Pillow; run from any cwd."""
from pathlib import Path
import hashlib
import json

from PIL import Image


ROOT = Path(__file__).resolve().parent
OUTPUT = ROOT / "256"
# Source landmarks: butt's left edge, collar mouth, and the grip's horizontal axis.
# Calibrate each original independently; never size poses by their total silhouette.
POSES = {
    "Item": (31, 744, 480, "b3340419cab16291fe6d2bd4275b988e60d3df8ed9cd0baf9c18967c10e87c55"),
    "Idle": (206, 1089, 385.5, "e03309d810569fc1d3248a774096344e88b80ca05df9a134ef04f57704073bc7"),
    "Attack": (20, 719, 367, "8afc9fe5c66c224c9b6a6eaf97bc9ee3330942620aab8ad751f7a8d683d0fa33"),
    "Recovery": (24, 768, 204, "082e755d0d1466f5abc6b062519c9ae17b61490fcb64532e83cfb34ac0fd2203"),
}
HANDLE_WIDTH = 80
HANDLE_CENTER = (50, 128)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def export():
    OUTPUT.mkdir(exist_ok=True)
    records = []
    for pose, (butt_x, collar_x, axis_y, expected_hash) in POSES.items():
        source = ROOT / f"SSC_PnaShortWhip_{pose}_Approved.png"
        if digest(source) != expected_hash:
            raise ValueError(f"Approved original changed: {source.name}")
        original = Image.open(source).convert("RGBA")
        # Ignore near-invisible alpha=1 specks when finding layout bounds; retain
        # the original interior alpha and add two source pixels of edge padding.
        bbox = original.getchannel("A").point(lambda a: 255 if a > 1 else 0).getbbox()
        left, top, right, bottom = bbox
        crop = (max(0, left - 2), max(0, top - 2),
                min(original.width, right + 2), min(original.height, bottom + 2))
        scale = HANDLE_WIDTH / (collar_x - butt_x)
        cropped = original.crop(crop)
        size = (round(cropped.width * scale), round(cropped.height * scale))
        # Premultiplied alpha avoids dark fringes during Lanczos downsampling.
        resized = cropped.convert("RGBa").resize(size, Image.Resampling.LANCZOS).convert("RGBA")
        # Use the actual rounded raster dimensions to keep each anchor within
        # half a destination pixel. All parts of each pose share the transform.
        sx, sy = size[0] / cropped.width, size[1] / cropped.height
        center_x = (butt_x + collar_x) / 2
        offset = (round(HANDLE_CENTER[0] - (center_x - crop[0]) * sx),
                  round(HANDLE_CENTER[1] - (axis_y - crop[1]) * sy))
        if not (0 <= offset[0] and 0 <= offset[1]
                and offset[0] + size[0] <= 256 and offset[1] + size[1] <= 256):
            raise ValueError(f"Pose would be clipped: {pose}")
        canvas = Image.new("RGBA", (256, 256))
        canvas.paste(resized, offset)  # No mask: preserve RGBA without squaring alpha.
        output = OUTPUT / f"SSC_PnaShortWhip_{pose}.png"
        canvas.save(output)
        records.append({
            "pose": pose, "source": source.name, "source_sha256": expected_hash,
            "source_size": list(original.size), "crop_bbox": list(crop),
            "source_handle_landmarks": {"butt_left_x": butt_x, "collar_mouth_x": collar_x,
                                        "grip_axis_y": axis_y},
            "nominal_scale": scale, "scaled_size": list(size), "offset": list(offset),
            "handle_width_px": (collar_x - butt_x) * sx,
            "handle_center_px": [offset[0] + (center_x - crop[0]) * sx,
                                 offset[1] + (axis_y - crop[1]) * sy],
            "output": output.name, "output_sha256": digest(output),
            "alpha_extrema": list(canvas.getchannel("A").getextrema()),
            "visible_bbox": list(canvas.getchannel("A").point(lambda a: 255 if a > 1 else 0).getbbox()),
        })
    manifest = {
        "canvas_size": [256, 256], "mode": "RGBA",
        "target_handle_width_px": HANDLE_WIDTH, "target_handle_center_px": list(HANDLE_CENTER),
        "method": "Whole-pose proportional premultiplied-alpha Lanczos; shared handle landmarks; no redraw or rotation.",
        "runtime_integrated": False, "poses": records,
    }
    (OUTPUT / "export.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    for record in records:
        print(f"{record['pose']}: 256x256 RGBA, handle {record['handle_width_px']:.2f}px, "
              f"center {record['handle_center_px']}")


if __name__ == "__main__":
    export()
