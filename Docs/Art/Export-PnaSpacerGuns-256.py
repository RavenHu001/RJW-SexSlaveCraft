"""AI 辅助生成：按明确回选原稿等比导出太空两枪，不重绘、拉伸或旋转。"""
from pathlib import Path
import hashlib
import json
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SPECS = (
    ("SSC_PnaPulseGun", "SSC_PnaPulseGun_SideHeavy_Source.png", "1.05"),
    ("SSC_PnaProjectorGun", "SSC_PnaProjectorGun_Source.png", "1.25"),
)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


for name, source_name, draw_size in SPECS:
    folder = ROOT / "Docs/Art" / name
    source = folder / source_name
    selection = json.loads((folder / "RuntimeSelection.json").read_text(encoding="utf-8"))
    if sha(source) != selection["sourceSha256"]:
        raise ValueError(f"Selected source changed: {source}")
    image = Image.open(source).convert("RGBA")
    alpha = image.getchannel("A")
    bounds = alpha.point(lambda value: 255 if value > 1 else 0).getbbox()
    if bounds is None:
        raise ValueError(f"Empty sprite: {source}")
    left, top, right, bottom = bounds
    crop = (max(0, left - 2), max(0, top - 2),
            min(image.width, right + 2), min(image.height, bottom + 2))
    # 预乘透明度后只缩放一次，避免细边出现暗色或透明混色杂边。
    sprite = image.crop(crop).convert("RGBa")
    sprite.thumbnail((240, 240), Image.Resampling.LANCZOS)
    sprite = sprite.convert("RGBA")
    canvas = Image.new("RGBA", (256, 256))
    offset = ((256 - sprite.width) // 2, (256 - sprite.height) // 2)
    canvas.paste(sprite, offset)
    archive = folder / "256" / f"{name}.png"
    archive.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(archive)
    runtime = ROOT / "1.7/Textures/Things/Item/Equipment" / f"{name}.png"
    runtime.write_bytes(archive.read_bytes())
    report = {
        "source": source.relative_to(ROOT).as_posix(),
        "originalGeneratedFile": selection["originalGeneratedFile"],
        "sourceSha256": sha(source), "sourceSize": list(image.size),
        "sourceAlphaExtrema": list(alpha.getextrema()),
        "sourceTouchesEdge": left == 0 or top == 0 or right == image.width or bottom == image.height,
        "alphaThreshold": 1, "sourcePadding": 2, "crop": list(crop),
        "resampling": "premultiplied-alpha Lanczos", "fittedSize": list(sprite.size),
        "offset": list(offset), "outputSize": list(canvas.size),
        "outputAlphaBBox": list(canvas.getchannel("A").getbbox()),
        "outputAlphaExtrema": list(canvas.getchannel("A").getextrema()),
        "output": runtime.relative_to(ROOT).as_posix(), "outputSha256": sha(runtime),
        "archive": archive.relative_to(ROOT).as_posix(), "drawSize": draw_size,
        "gameRuntimeVerified": False,
    }
    (folder / "export-256.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False))
