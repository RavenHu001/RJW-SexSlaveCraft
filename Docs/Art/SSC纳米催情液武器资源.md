# SSC 纳米催情液武器资源

当前资源核对：2026-10-19，仅用于 1.7 开发分支；1.6／Common 基线不变。所有下表 PNG 均为真实透明 RGBA。原始图、生成提示词、导出脚本与阶段备份的具体记录见对应实现文档，不将本机 `.builds/` 临时文件视为发布资源。

[首批完成核对](../Development/Completed/SSC武器拓展首批完成核对.md) · [美术索引](README.md)

| 资源 | 画布与方向 | 游戏参数 | 说明 |
| --- | --- | --- | --- |
| [工业短鞭](../../1.7/Textures/Things/Item/Equipment/SSC_PnaShortWhip.png) | 256×256，握柄在左、三股鞭束向右 | drawSize=1，沿用现有装备角度 | 深灰握柄、独立编织鞭束与紫色金属刃片、可见紫色液面的透明储液柄尾；已接入并获维护者实机认可，见[本批记录](../Development/Completed/SSC工业短鞭贴图接入与测试.md) |
| [短矛](../../1.7/Textures/Things/Item/Equipment/SSC_PnaShortSpear.png) | 256×256，水平朝右 | drawSize=1，无额外装备角度 | 紫色金属阔刃、小圆形尾端、暖木杆、深色绑带；最初参考图由维护者提供，后续横置、构型与粗细调整保留历史来源 |
| [吹箭筒](../../1.7/Textures/Things/Item/Equipment/SSC_PnaBlowpipe.png) | 256×256，水平朝右 | drawSize=1，角度 0 | 紧凑木筒、浸紫直筒口，两支备用箭为紫色金属箭头与浅米色纤维尾塞 |
| [轻弩](../../1.7/Textures/Things/Item/Equipment/SSC_PnaLightCrossbow.png) | 256×256，水平朝右 | drawSize=1，角度 0 | 紧凑木质弩床，清楚弩臂／弦，装填箭保留普通尾羽与紫色金属箭头 |
| [吹箭投射物](../../1.7/Textures/Things/Projectile/SSC_PnaDart.png) | 128×128，箭头朝上 | drawSize=0.5 | 紫色金属头、木杆、纤维尾塞 |
| [弩矢投射物](../../1.7/Textures/Things/Projectile/SSC_PnaBolt.png) | 128×128，箭头朝上 | drawSize=0.6 | 紫色金属头、木杆、普通尾羽 |

中世纪三件武器沿用粗黑轮廓、清晰色块与有限材质明暗，按缩小后的游戏辨识度设计。多轮修改产生噪点的远程本体已整体重绘；未整件染紫，木材、绑带与尾部保留自身颜色。飞行方向由原版弹丸绘制旋转，画布像素尺寸与 drawSize 分别管理。

## 短鞭后续多姿态原稿

物品图与待机图已选定并保存，具体文件、生成来源、SHA-256 与预览见[原稿选定记录](SSC短鞭多姿态原稿选定记录.md)。待机采用柄尾在左、三股鞭束朝柄尾回垂并交叠的版本。两张均为 1774×887 高清原稿，尚未替换上表运行贴图；攻击／回落图与多姿态切换仍待后续。

## 特效与音效

- 三类远程弹丸尾迹复用 Core `Things/Mote/BrightFlash`，使用普通 Mote 材质、不创建地图灯光；不随模组复制原版图。
- 受药反馈复用 Core `Things/Mote/Heart`，旋转 180°，持续 1.2 秒；没有新增爱心 PNG。
- 短矛命中采用原矛头纹理的刃部遮罩＋Core BrightFlash 柔边光晕。光晕长宽为矛头的 4.5 倍，峰值不透明度 0.8，18 tick 满亮＋30 tick 淡出；仅材质叠画，不增加原版动作或地图灯光。
- 旧尖梭 `SSC_PnaStabSpindle.png` 和对应 Mote 已撤下；它们是历史试验，不应重新打入运行目录。
- 吹箭发射引用原版 `UI/FlickWhoosh`；独立 SoundDef 为 MapOnly，音量 65～75、音调 0.95～1.05、距离 10～45；不复制原版音频、不更改原 UI SoundDef。

## 来源与阶段记录

[短矛改名与横置](../Development/Completed/SSC近战短矛贴图与载体调整.md) · [吹箭筒、紧凑化与声音](../Development/Completed/SSC吹箭筒贴图接入与测试.md) · [轻弩与尾羽弩矢](../Development/Completed/SSC轻弩贴图接入与测试.md) · [金属箭头与整体重绘](../Development/Completed/SSC箭矢金属质感调整与测试.md) · [尾迹](../Development/Completed/SSC投射物紫色拖尾实现与测试.md) · [泛光最终验收](../Development/Completed/SSC短矛命中泛光实现与测试.md)。各阶段的设计认可与实机反馈按原范围保留。
