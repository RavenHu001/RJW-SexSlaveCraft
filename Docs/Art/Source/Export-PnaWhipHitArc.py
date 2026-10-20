"""AI 辅助生成：导出已否决小弧痕试验的 128×128 归档图；不写入游戏。"""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[3]
source = Path(__file__).with_name("SSC_PnaWhipHitArc_Source.png")
image = Image.open(source).convert("RGBA")
# 忽略几乎透明的外围散点，只用有效轮廓确定裁切框；保留框内原始 alpha。
bounds = image.getchannel("A").point(lambda value: 255 if value > 4 else 0).getbbox()
if bounds is None:
    raise ValueError("Empty arc image")
sprite = image.crop(bounds).convert("RGBa")
sprite.thumbnail((112, 112), Image.Resampling.LANCZOS)
sprite = sprite.convert("RGBA")
canvas = Image.new("RGBA", (128, 128))
canvas.paste(sprite, ((128 - sprite.width) // 2, (128 - sprite.height) // 2))
destination = Path(__file__).with_name("SSC_PnaWhipHitArc_Rejected_128.png")
canvas.save(destination)
print(destination)
