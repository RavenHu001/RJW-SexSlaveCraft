"""Export the three selected poses with a common handle size; requires Pillow."""
from pathlib import Path
import hashlib
import json

from PIL import Image

ROOT = Path(__file__).resolve().parent
OUTPUT = ROOT / "256"
# Butt left edge, emitter opening, horizontal handle axis. These are source
# landmarks, not whole-sprite bounds: a folded idle pose must not be enlarged.
POSES = {
    "Idle": ("SSC_PnaSilkWhip_Idle_v3_Source.png", 55, 1087, 307),
    "Attack": ("SSC_PnaSilkWhip_Attack_v2_Source.png", 40, 625, 546),
    "Recovery": ("SSC_PnaSilkWhip_Recovery_Approved.png", 48, 766, 198),
}
HANDLE_WIDTH = 64
HANDLE_CENTER = (50, 128)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def export():
    approved = json.loads((ROOT / "ApprovedPoses.json").read_text(encoding="utf-8"))
    hashes = {p["pose"]: p["SHA256"] for p in approved["poses"]}
    OUTPUT.mkdir(exist_ok=True)
    records = []
    for pose, (filename, butt, mouth, axis) in POSES.items():
        source = ROOT / filename
        if digest(source) != hashes[pose]:
            raise ValueError(f"Selected source changed: {filename}")
        original = Image.open(source).convert("RGBA")
        bounds = original.getchannel("A").point(lambda a: 255 if a > 1 else 0).getbbox()
        left, top, right, bottom = bounds
        crop = (max(0, left - 2), max(0, top - 2),
                min(original.width, right + 2), min(original.height, bottom + 2))
        cropped = original.crop(crop)
        scale = HANDLE_WIDTH / (mouth - butt)
        size = (round(cropped.width * scale), round(cropped.height * scale))
        resized = cropped.convert("RGBa").resize(size, Image.Resampling.LANCZOS).convert("RGBA")
        sx, sy = size[0] / cropped.width, size[1] / cropped.height
        center = (butt + mouth) / 2
        offset = (round(HANDLE_CENTER[0] - (center - crop[0]) * sx),
                  round(HANDLE_CENTER[1] - (axis - crop[1]) * sy))
        if not (0 <= offset[0] and 0 <= offset[1] and
                offset[0] + size[0] <= 256 and offset[1] + size[1] <= 256):
            raise ValueError(f"Pose would be clipped: {pose}")
        canvas = Image.new("RGBA", (256, 256))
        canvas.paste(resized, offset)
        output = OUTPUT / f"SSC_PnaSilkWhip_{pose}.png"
        canvas.save(output)
        records.append(dict(pose=pose, source=filename, source_sha256=hashes[pose],
            source_size=list(original.size), source_visible_bbox=list(bounds), crop_bbox=list(crop),
            source_handle_landmarks=dict(butt_left_x=butt, collar_mouth_x=mouth, grip_axis_y=axis),
            nominal_scale=scale, scaled_size=list(size), offset=list(offset),
            handle_width_px=(mouth-butt)*sx,
            handle_center_px=[offset[0]+(center-crop[0])*sx, offset[1]+(axis-crop[1])*sy],
            output=output.name, output_sha256=digest(output),
            alpha_extrema=list(canvas.getchannel("A").getextrema()),
            visible_bbox=list(canvas.getchannel("A").point(lambda a:255 if a>1 else 0).getbbox())))
    manifest = dict(canvas_size=[256,256], mode="RGBA", target_handle_width_px=HANDLE_WIDTH,
        target_handle_center_px=list(HANDLE_CENTER),
        method="Whole-pose premultiplied-alpha Lanczos; common handle landmarks; no redraw or rotation.",
        runtime_integrated=False, poses=records)
    (OUTPUT / "poses-export.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    for row in records:
        print(f"{row['pose']}: handle {row['handle_width_px']:.2f}px, center {row['handle_center_px']}, content {row['scaled_size']}")


if __name__ == "__main__":
    export()
