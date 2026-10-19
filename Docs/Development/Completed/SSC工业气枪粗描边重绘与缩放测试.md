# SSC 工业气枪粗描边重绘与缩放测试

日期：2026-10-19。基线：`11b1011`。状态：维护者否决后续过度简化的版本，明确回选本轮最初生成的两张原图。已重新导出、接入并同步本机；维护者明确确认“这版可以，实机测试正常”，按授权提交，未打包发布。

[工业规划](../../Design/SSC工业时代纳米催情液武器开发规划.md) · [资源表](../../Art/SSC纳米催情液武器资源.md) · [精确针枪首次接入](SSC工业精确针枪贴图接入与测试.md) · [速射针枪前版验收](SSC工业速射针枪贴图接入与测试.md)

## 当前选图

采用维护者明确指定的首轮内置 imagegen 原图，不再加粗或继续简化。保留枪托内气罐、定位夹具、压力表、工程塑料枪身与较完整内部结构；速射针枪保留弹匣内横排紫色载药管，精确针枪保留低矮瞄准镜、贴腮板及拉柄槽／单针观察窗／枪管轴线对齐。

| 项目 | 速射针枪 | 精确针枪 |
| --- | --- | --- |
| 指定生成文件 | `exec-ba86cd96-9f97-4c97-99f4-1767f97de16a.png` | `exec-0ba16143-cbf8-4639-9089-f2f654233739.png` |
| 现行原图 | [SSC_PnaNeedleRepeater_Redraw.png](../../Art/SSC_PnaNeedleRepeater/SSC_PnaNeedleRepeater_Redraw.png) | [SSC_PnaNeedleRifle_Redraw.png](../../Art/SSC_PnaNeedleRifle/SSC_PnaNeedleRifle_Redraw.png) |
| 原图尺寸 | 1774×887 | 2169×725 |
| 参数 | [export.json](../../Art/SSC_PnaNeedleRepeater/export.json) | [export.json](../../Art/SSC_PnaNeedleRifle/export.json) |
| 提示词 | [prompt-redraw.txt](../../Art/SSC_PnaNeedleRepeater/prompt-redraw.txt) | [prompt-redraw.txt](../../Art/SSC_PnaNeedleRifle/prompt-redraw.txt) |
| 透明画布 | 256×256 RGBA | 256×256 RGBA |
| 等比图形与偏移 | 240×101，(8,77) | 240×68，(8,94) |
| drawSize | **1.05** | **1.25** |

来源目录为 `C:/Users/24196/.codex/generated_images/01a14d5a-bbd6-79f3-9356-75f2d8ad5113/`；用户附件路径开头的 `/C:/` 按实际 Windows 路径 `C:/` 读取。只做确定性尺寸处理：alpha>1取边界、两像素源图边距、预乘alpha Lanczos等比缩放，不重绘、不旋转或拉伸。历史 `*_Approved.png` 保留；已否决的 `*_Bold.png` 及多轮提示词移入本机阶段备份，不作为当前运行来源。

## 接入与检查

运行路径保持：[速射针枪](../../../1.7/Textures/Things/Item/Equipment/SSC_PnaNeedleRepeater.png)、[精确针枪](../../../1.7/Textures/Things/Item/Equipment/SSC_PnaNeedleRifle.png)。XML及镜像沿用上一轮drawSize=1.05／1.25；相对HEAD仍仅有这两个尺寸字段变化。本次回选只替换两张PNG，不改武器数值、角度、投射物、声音或特效。

两图资源检查通过：256×256 RGBA、alpha范围0～255、四角透明、引用有效、源图实体轮廓未触及边界，XML镜像匹配。上一轮21份Def镜像／538项布局检查结果沿用，本次未再改XML或布局；资源检查已按新图重跑。无C#、DLL或BUILD-INFO变动，不重新编译或重跑机制套件。

运行图SHA256：速射针枪 `a1148e2e908d3adeada61a8be1964fe4fbaf89c5beb7dbd80f757b74a20bfb81`；精确针枪 `d1d8331efa41c49b66f17527ec4891577ae03438dff3826117646df03275ffb1`。完整源图哈希与裁切见各自export.json。

## 同步与实机反馈

本机1.7.4982 rev1280。已备份并替换 `D:/STEAM/steamapps/common/RimWorld/Mods/RJW-SexSlaveCraft-TieJin-Modify` 中两张PNG；514份1.7运行文件哈希匹配，532份1.6／Common文件和LoadFolders.xml不变。最新清单、备份、资源和同步检查位于 `.builds/pna-weapons/industrial-art/needleguns-first-redraw-256/`；上轮加粗尝试记录留在 `.builds/pna-weapons/industrial-art/needleguns-bold-256/`，不得充作当前选图证据。

最初曾因首轮描边较细继续生成更简化版本，后经维护者明确否决，现以回选为准。维护者已确认本轮实机测试正常，当前两枪贴图及缩放的约定范围验收关闭；未单独报告的方向组合不补记为专项实测。原始同步报告保持 `gameRuntimeVerified=false`，不回写历史证据。本轮按授权提交，未打包发布。
