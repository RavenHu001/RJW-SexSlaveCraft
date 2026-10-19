"""AI 辅助生成：已选精确针枪原图的等比透明导出，不重绘、不旋转。"""
from pathlib import Path
import hashlib
import json
from PIL import Image
root = Path(__file__).resolve().parents[3]
folder = Path(__file__).resolve().parent
source = folder / "SSC_PnaNeedleRifle_Redraw.png"
image = Image.open(source).convert("RGBA")
bounds = image.getchannel("A").point(lambda value: 255 if value > 1 else 0).getbbox()
if bounds is None:
    raise ValueError("Empty source sprite")
left, top, right, bottom = bounds
crop = (max(0, left-2), max(0, top-2), min(image.width, right+2), min(image.height, bottom+2))
sprite = image.crop(crop).convert("RGBa")
sprite.thumbnail((240, 240), Image.Resampling.LANCZOS)
sprite = sprite.convert("RGBA")
canvas = Image.new("RGBA", (256, 256))
offset = ((256-sprite.width)//2, (256-sprite.height)//2)
canvas.paste(sprite, offset)
destination = root / "1.7/Textures/Things/Item/Equipment/SSC_PnaNeedleRifle.png"
canvas.save(destination)
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
report = dict(source=source.name, sourceSize=list(image.size), sourceSha256=sha(source),
              alphaThreshold=1, sourcePadding=2, crop=list(crop), resampling="premultiplied-alpha Lanczos",
              fittedSize=list(sprite.size), offset=list(offset), outputSize=list(canvas.size),
              output=destination.relative_to(root).as_posix(), outputSha256=sha(destination))
(folder / "export.json").write_text(json.dumps(report, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
print(json.dumps(report, ensure_ascii=False))
