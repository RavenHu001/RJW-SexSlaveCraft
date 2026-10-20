# SSC 轻弩贴图接入与测试

归档说明（2026-10-18）：本批约定范围已交付。当前实现、最终参数及分批验收边界统一见[首批完成核对](SSC武器拓展首批完成核对.md)。下文保留当时的试验参数、测试计数和同步状态，旧的“待实机／未实现／暂不提交”不代表当前待办。

日期：2026-10-18。基线：`2e0fb3f`。状态：维护者反馈“实机测试正常”，本轮轻弩与独立弩矢贴图／XML 验收关闭，整理提交。最终采用 256×256 轻弩和 128×128 弩矢（drawSize=0.6）；效力显示暂缓。前述各阶段记录保留。

## 资源与显示

以短矛、当前吹箭筒和堂丸模组的 PNG 为画风参考，通过内置 image_gen 生成轻弩，再将初版弩矢上的纤维尾塞局部改为普通浅色尾羽。主体为暖色木质弩床、深灰绑带、灰色金属件与可辨认弩弦，弩矢箭头全紫，弩床水平朝右；高清图位于 `.builds/pna-light-crossbow/preview-v2-hd/`。

从高清源图等比缩放并透明居中补成精确 512×512 RGBA，Alpha 范围 0～255，不拉伸长宽比。运行资源为 [SSC_PnaLightCrossbow.png](../../../1.7/Textures/Things/Item/Equipment/SSC_PnaLightCrossbow.png)，转换脚本、输入及结果位于 `.builds/pna-light-crossbow/layout-512-v2/`。

现有 `SSC_Weapon_PnaLightCrossbow` 的 texPath 改为 `Things/Item/Equipment/SSC_PnaLightCrossbow`，保留 Graphic_Single，设置 drawSize=1、equippedAngleOffset=0。移除原临时图的全局紫色 tint，保留木材、尾羽及金属原色；使用原版待命／瞄准流程，不新增渲染补丁。

当前只接入武器本体图，`SSC_Projectile_PnaBolt` 的飞行贴图仍为原版 Arrow_Small 加紫色 tint。已装填弩矢是武器图的一部分，不能替代独立弹丸贴图。战斗、制作、PNA 参数、Def ID、四语文本、C#／DLL、版本与 1.6／Common 均不改；效力显示未开始。

## 验证与同步

- 图片检查：512×512、32 位 RGBA、Alpha 0～255，已查看最终水平居中图。
- XML 结构对比：与 HEAD 相比，仅轻弩 graphicData／equippedAngleOffset 变化；其他武器、弹丸和所有机制参数不变。
- PnaLoad 470/470、版本布局与冻结基线 538 项通过。无代码变化，不编译或重复全量回归。
- 已备份后同步本机 SSC 目录：覆盖 1 个武器 XML，新增 1 张轻弩 PNG；465 个 1.7 文件一致，532 个冻结文件未变。
- 同步备份：`.builds/pna-light-crossbow/layout-512-v2/game-sync/backup-20261018-201021-3f964e6c/`，含旧 XML 及新增资源清单。

重启游戏后检查四朝向待命／瞄准、水平及斜向持握、世界尺寸和图标、弩弦与箭头辨识度。设计确认不等于最终画面验收；本轮未提交或发布。

## 实机反馈：比例与小尺寸辨识调整

维护者反馈初次接入仍偏写实细长、游戏内结构不清。以第二版高清图为基础生成第三版：缩短弩床相对弩臂的比例，加粗弩臂、弩弦及已装填弩矢，扩大普通尾羽和紫色箭头的可读性，简化细碎细节。保留暖色木材、深灰绑带、水平朝右及透明背景。生成式调整不宣称指定比例精确或其他部位逐像素不变。

重新等比导出 512×512 RGBA，Alpha 0～255，并查看最终图。处理产物和前版 PNG 位于 `.builds/pna-light-crossbow/layout-512-v3/`。仅替换图片，不改 drawSize=1、角度 0、XML 或机制参数；本轮不重复机制回归、编译或提交，初次接入的测试证据保留为此前结果。

已备份后同步本机单张 PNG，465 个 1.7 文件一致、532 个冻结文件未变；备份为 `.builds/pna-light-crossbow/layout-512-v3/game-sync/backup-20261018-201519-0dbc6f76/`。第三版实际结构辨识和握持仍待重启游戏比较。

## 第三版改用 256×256 试装

按维护者要求，从第三版高清原图重新等比导出 256×256 RGBA，Alpha 范围 0～255，水平居中；保留紧凑比例及游戏内 drawSize=1、equippedAngleOffset=0，不从旧的小图放大或改变机制。已查看最终图，前一张 512 PNG 与脚本／输入保留于 `.builds/pna-light-crossbow/layout-256-v3/`。

备份后同步本机 PNG 与仅对齐分辨率注释的 XML 两个文件；465 个 1.7 文件一致、532 个冻结文件未变。备份为 `.builds/pna-light-crossbow/layout-256-v3/game-sync/backup-20261018-202418-c2dd1740/`。无行为代码改动，不重复机制测试或编译，重启后观察当前分辨率表现，暂不提交。

## 独立弩矢绘制与接入

维护者授权参考弩上装填弩矢绘制独立投射物，并明确将建议的 drawSize=0.75 改为 0.6。通过内置 image_gen，以第三版高清轻弩为参考，生成单支竖直弩矢：全紫箭头朝上、暖棕木杆、箭头根部深灰固定段、普通浅色平整尾羽朝下，不使用吹箭的纤维尾塞。高清图及确定性等比导出脚本位于 `.builds/pna-bolt/layout-128-v1/`。

新增 [SSC_PnaBolt.png](../../../1.7/Textures/Things/Projectile/SSC_PnaBolt.png)，128×128 RGBA、Alpha 范围 0～255，已查看最终图。`SSC_Projectile_PnaBolt` 的 texPath 改为 `Things/Projectile/SSC_PnaBolt`，保留 Graphic_Single，设置 drawSize=(0.6,0.6)，移除全局紫色 tint。原版飞行旋转与 Transparent shader 继承保留，速度 55、伤害 7、穿甲 18%、PNA 结算及 C#／DLL 不变；本体贴图保持 256×256、显示尺寸 1 和角度 0。前文“临时原版弩矢图”是本次接入前的历史状态。

验证：PNG 精确尺寸／RGBA 与 XML 引用／尺寸／染色检查通过，PnaLoad 470/470、布局 538 项通过。无生产代码改动，不编译或重复全量机制回归。

已备份后同步本机 XML 与新增弹丸 PNG，466 个 1.7 文件一致、532 个冻结文件未变；备份为 `.builds/pna-bolt/layout-128-v1/game-sync/backup-20261018-202812-a91feb1d/`。重启游戏观察弹丸箭头是否沿飞行方向、0.6 尺寸下箭头与尾羽是否清楚；尚未登记弹丸实机通过，暂不提交。效力显示未开始。

## 实机确认与提交收尾

维护者确认本轮实机正常并授权提交轻弩与弩矢贴图、XML 及文档，关闭本批验收。最终版本为第三版紧凑轻弩 256×256（drawSize=1、equippedAngleOffset=0）和普通尾羽弩矢 128×128（drawSize=0.6）。源图均为透明 RGBA；轻弩水平朝右、弹丸箭头朝上，使用原版显示与旋转流程。

收尾沿用此前 PnaLoad 470/470、布局 538 项及本次实机反馈，核对最终图像／安装副本与差异，不重复机制测试或编译。保留机制、Def ID、版本和冻结内容；效力显示未开始。此前“待实机／不提交”为阶段历史，不作为当前状态。未逐项反馈的情境不补记独立专项通过。
