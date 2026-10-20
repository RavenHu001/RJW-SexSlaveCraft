"""AI 辅助生成：两种工业针弹统一倍率等比导出；保留原图形状、朝向与规格差异。"""
from pathlib import Path
from PIL import Image
import hashlib
import json
folder = Path(__file__).resolve().parent
root = folder.parents[2]
names = ["SSC_PnaRepeaterNeedle", "SSC_PnaRifleNeedle"]
items = []
for name in names:
    source = folder / (name + "_Approved.png")
    image = Image.open(source).convert("RGBA")
    bounds = image.getchannel("A").point(lambda v: 255 if v > 1 else 0).getbbox()
    if bounds is None:
        raise ValueError("Empty sprite: " + name)
    x0, y0, x1, y1 = bounds
    crop = (max(0,x0-2),max(0,y0-2),min(image.width,x1+2),min(image.height,y1+2))
    items.append((name,source,image,crop))
# 原图画布尺度一致；共用倍率，避免将较短的速射针强行放大到精确针的长度。
scale = min(112 / max(crop[2]-crop[0],crop[3]-crop[1]) for _,_,_,crop in items)
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
records = []
for name,source,image,crop in items:
    size = (round((crop[2]-crop[0])*scale),round((crop[3]-crop[1])*scale))
    sprite = image.crop(crop).convert("RGBa").resize(size, Image.Resampling.LANCZOS).convert("RGBA")
    canvas = Image.new("RGBA", (128,128))
    offset = ((128-size[0])//2,(128-size[1])//2)
    canvas.paste(sprite,offset)
    output = root / "1.7/Textures/Things/Projectile" / (name+".png")
    canvas.save(output)
    records.append(dict(name=name,source=source.name,sourceSize=list(image.size),sourceSha256=sha(source),
                        crop=list(crop),fittedSize=list(size),offset=list(offset),output=output.relative_to(root).as_posix(),outputSha256=sha(output)))
report = dict(canvas=[128,128],alphaThreshold=1,sourcePadding=2,sharedScale=scale,resampling="premultiplied-alpha Lanczos",items=records)
(folder/"export.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
print(json.dumps(report,ensure_ascii=False))
