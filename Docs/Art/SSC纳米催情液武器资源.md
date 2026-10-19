# SSC 纳米催情液武器资源

当前资源核对：2026-10-19，仅用于 1.7 开发分支；1.6／Common 基线不变。所有下表 PNG 均为真实透明 RGBA。原始图、生成提示词、导出脚本与阶段备份的具体记录见对应实现文档，不将本机 `.builds/` 临时文件视为发布资源。

[首批完成核对](../Development/Completed/SSC武器拓展首批完成核对.md) · [美术索引](README.md)

| 资源 | 画布与方向 | 游戏参数 | 说明 |
| --- | --- | --- | --- |
| [工业精确针枪](../../1.7/Textures/Things/Item/Equipment/SSC_PnaNeedleRifle.png) | 256×256，水平朝右；枪体240×68 | drawSize=1.25，既有握持配置 | 首轮重绘回选版，同轴拉柄／单针观察窗／枪管，见[本轮检查](../Development/Completed/SSC工业气枪粗描边重绘与缩放测试.md)；已获维护者实机正常确认并授权提交 |
| [工业速射针](../../1.7/Textures/Things/Projectile/SSC_PnaRepeaterNeedle.png) | 128×128，针尖朝上；图形24×106 | drawSize=0.4 | 银灰针尖、紫色载药管、深灰硬质尾翼，见[接入记录](../Development/Completed/SSC工业针弹贴图接入与测试.md)；已获维护者认可并授权提交 |
| [工业精确针](../../1.7/Textures/Things/Projectile/SSC_PnaRifleNeedle.png) | 128×128，针尖朝上；图形26×112 | drawSize=0.4 | 同系列较长载药段，统一缩放倍率，见[接入记录](../Development/Completed/SSC工业针弹贴图接入与测试.md)；已获维护者认可并授权提交 |
| [工业速射针枪](../../1.7/Textures/Things/Item/Equipment/SSC_PnaNeedleRepeater.png) | 256×256，水平朝右；枪体240×101 | drawSize=1.05，既有握持配置 | 首轮重绘回选版，枪托储气罐／侧开夹具／压力表／横排载药管保留，见[本轮检查](../Development/Completed/SSC工业气枪粗描边重绘与缩放测试.md)；已获维护者实机正常确认并授权提交 |
| [工业短鞭](../../1.7/Textures/Things/Item/Equipment/SSC_PnaShortWhip.png) | 256×256，握柄在左、三股鞭束向右 | drawSize=1，沿用现有装备角度 | 当前为新版物品图，另接待机／挥击／回落；柄部共同标定，见[本轮实现](../Development/Completed/SSC短鞭四姿态接入与测试.md)，已获本轮实机认可；[旧单图验收](../Development/Completed/SSC工业短鞭贴图接入与测试.md)保留原范围 |
| [短矛](../../1.7/Textures/Things/Item/Equipment/SSC_PnaShortSpear.png) | 256×256，水平朝右 | drawSize=1，无额外装备角度 | 紫色金属阔刃、小圆形尾端、暖木杆、深色绑带；最初参考图由维护者提供，后续横置、构型与粗细调整保留历史来源 |
| [吹箭筒](../../1.7/Textures/Things/Item/Equipment/SSC_PnaBlowpipe.png) | 256×256，水平朝右 | drawSize=1，角度 0 | 紧凑木筒、浸紫直筒口，两支备用箭为紫色金属箭头与浅米色纤维尾塞 |
| [轻弩](../../1.7/Textures/Things/Item/Equipment/SSC_PnaLightCrossbow.png) | 256×256，水平朝右 | drawSize=1，角度 0 | 紧凑木质弩床，清楚弩臂／弦，装填箭保留普通尾羽与紫色金属箭头 |
| [吹箭投射物](../../1.7/Textures/Things/Projectile/SSC_PnaDart.png) | 128×128，箭头朝上 | drawSize=0.5 | 紫色金属头、木杆、纤维尾塞 |
| [弩矢投射物](../../1.7/Textures/Things/Projectile/SSC_PnaBolt.png) | 128×128，箭头朝上 | drawSize=0.6 | 紫色金属头、木杆、普通尾羽 |

中世纪三件武器沿用粗黑轮廓、清晰色块与有限材质明暗，按缩小后的游戏辨识度设计。多轮修改产生噪点的远程本体已整体重绘；未整件染紫，木材、绑带与尾部保留自身颜色。飞行方向由原版弹丸绘制旋转，画布像素尺寸与 drawSize 分别管理。

## 短鞭多姿态原稿与接入

物品、待机、抽击与回落四张原稿已选定并保存，具体文件、生成来源、SHA-256 与预览见[原稿选定记录](SSC短鞭多姿态原稿选定记录.md)。待机采用柄尾在左、三股鞭束朝柄尾回垂并交叠的版本。物品／待机为 1774×887，抽击／回落为 2172×724；回落最终采用维护者回选的 `exec-e2027163-803d-4eb9-af25-260d312b597b.png`。已分别导出 256×256 透明版本，按柄长约 80 px、柄轴中心 (50,128) 标定对齐；原稿保留，已替换物品运行图并接入三种手持姿态：出手后 6 tick 挥击、12 tick 回落，再恢复待机；待机仅绕共同柄心改为柄尾向下斜 45°，挥击／回落保留原版方向与镜像；持鞭近战每次实际出手均播放，含打空、柄击和拳击，但后两者仍不施药；不额外发光，修正批次已获实机认可。

## 特效与音效

- 三类远程弹丸尾迹复用 Core `Things/Mote/BrightFlash`，使用普通 Mote 材质、不创建地图灯光；不随模组复制原版图。
- 短鞭命中改为束根为轴的 180° 上半弧向下抽落残影，宽 0.60、峰值不透明度 0.95，复用 Core BrightFlash 和普通 Mote，跟随武器最终矩阵、随回落在 18 tick 内淡出；无新增运行 PNG。原目标前方 128px 弧痕已撤下，原图只作否决试验归档，见[实现记录](../Development/Completed/SSC短鞭紫色弧形命中特效实现与测试.md)，最终上半弧加宽提亮版已获实机正常确认。
- 受药反馈复用 Core `Things/Mote/Heart`，旋转 180°，持续 1.2 秒；没有新增爱心 PNG。
- 短矛命中采用原矛头纹理的刃部遮罩＋Core BrightFlash 柔边光晕。光晕长宽为矛头的 4.5 倍，峰值不透明度 0.8，18 tick 满亮＋30 tick 淡出；仅材质叠画，不增加原版动作或地图灯光。
- 旧尖梭 `SSC_PnaStabSpindle.png` 和对应 Mote 已撤下；它们是历史试验，不应重新打入运行目录。
- 吹箭发射引用原版 `UI/FlickWhoosh`；独立 SoundDef 为 MapOnly，音量 65～75、音调 0.95～1.05、距离 10～45；不复制原版音频、不更改原 UI SoundDef。

## 来源与阶段记录

[短矛改名与横置](../Development/Completed/SSC近战短矛贴图与载体调整.md) · [吹箭筒、紧凑化与声音](../Development/Completed/SSC吹箭筒贴图接入与测试.md) · [轻弩与尾羽弩矢](../Development/Completed/SSC轻弩贴图接入与测试.md) · [金属箭头与整体重绘](../Development/Completed/SSC箭矢金属质感调整与测试.md) · [尾迹](../Development/Completed/SSC投射物紫色拖尾实现与测试.md) · [泛光最终验收](../Development/Completed/SSC短矛命中泛光实现与测试.md)。各阶段的设计认可与实机反馈按原范围保留。
