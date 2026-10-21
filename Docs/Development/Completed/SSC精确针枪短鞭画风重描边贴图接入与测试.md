# SSC 精确针枪短鞭画风重描边贴图接入与测试

> 版本归属：**2.4.3，仅 1.7，开发未发布**。原实施日期、测试／实机反馈和构建版本保留；本版汇总见[版本说明](../../Releases/2.4.3/2.4.3发布说明.md)，现行状态优先看所属主题的完成核对。

维护者已认可当前游戏效果并授权提交：速射针枪此前反馈“效果很好”，精确针枪最新反馈“可以了，提交”。本篇约定的贴图范围验收关闭；未逐项报告的方向／场景不另记为专项实测。原始检查、构建和同步报告保持原字段，不回写历史实测状态。未打包发布。

日期：2026-10-19。基线：`f026c5a`。状态：维护者明确回选基础原图并要求制作重描边版，现已按要求导出、接入及同步本机，维护者已认可当前效果并授权提交。

[前版两枪验收](SSC工业气枪粗描边重绘与缩放测试.md) · [速射针枪同画风记录](SSC速射针枪短鞭画风贴图接入与测试.md) · [资源表](../../Art/SSC纳米催情液武器资源.md) · [工业规划](../../Design/Archive/SSC工业时代纳米催情液武器开发规划.md)

## 本轮选图

基础版为维护者明确回选的 `exec-c9b1a161-bc8b-4531-b91e-0ab7bed36f43.png`。以[短鞭物品原图](../../Art/SSC_PnaShortWhip/SSC_PnaShortWhip_Item_Approved.png)和原始精确针枪 `exec-0ba16143-cbf8-4639-9089-f2f654233739.png` 作参考，新的局部护木结构通过文字描述生成，未将此前否决的下置护木草图作为参考图。此后更小视窗试稿未被回选，本次以明确指定的基础图为准。

重描边版为 `exec-c9c5aef5-70e9-47bd-8464-aadf07e04b5b.png`，2168×725 RGBA，SHA256 `19c4bbf68979e50dfb451ea223c02f9dc63063a1e59eb7bcaafc0fb1126e887c`。保留指定图的护木构型、配色、视窗及内部细节，强化黑色轮廓；深灰紫枪身、银灰高光、气罐枪托、定位夹具、压力表和低矮瞄准镜保持同系列画风。

项目保存[基础原图](../../Art/SSC_PnaNeedleRifle/SSC_PnaNeedleRifle_WhipStyle.png)、[当前重描边原图](../../Art/SSC_PnaNeedleRifle/SSC_PnaNeedleRifle_WhipStyleHeavy.png)、[提示词与来源](../../Art/SSC_PnaNeedleRifle/prompt-whipstyle-heavy.txt)、[导出脚本](../../Art/SSC_PnaNeedleRifle/Export-256.py)及[导出参数](../../Art/SSC_PnaNeedleRifle/export.json)。生成文件源目录为 `C:/Users/24196/.codex/generated_images/01a14d5a-bbd6-79f3-9356-75f2d8ad5113/`，旧Approved／Redraw原图保留。

## 导出、接入与检查

只做确定性尺寸处理：alpha>1取边界、加两像素源图边距，裁切(32,76,2151,670)，预乘alpha Lanczos等比缩为240×67，偏移(8,94)，放入256×256透明画布，水平朝右，不拉伸或旋转。输出：[SSC_PnaNeedleRifle.png](../../../1.7/Textures/Things/Item/Equipment/SSC_PnaNeedleRifle.png)，SHA256 `10af69bf9497db0bf8026bdfaed090ecc9cc45b7358900bc11d3936f7c2be493`。

只替换精确针枪PNG，XML／镜像内容及texPath不变，drawSize保持1.25，原数值、握持角度、弹丸及特效不变。本轮前维护者已反馈速射针枪重描边版“效果很好”；本轮未改动速射针枪PNG，继续保留其当前版本。

资源／引用检查通过：256×256 RGBA、alpha范围0～255、四角透明、有效边界(8,94,248,161)，源图实体未触边；项目源图与指定生成文件哈希匹配，XML解析内容与HEAD一致、镜像字节一致，drawSize=1.25。已核对速射针枪本轮前后哈希不变，为 `3c50288910b6558604667c63e3f2da97cf34a2d03527cb74d89b914c5e2031c6`。无XML／布局／C#／DLL改动，不重复运行前版套件或编译。

## 同步与实机边界

已备份并替换 `D:/STEAM/steamapps/common/RimWorld/Mods/RJW-SexSlaveCraft-TieJin-Modify` 中精确针枪PNG一份；514份1.7运行文件哈希匹配，532份1.6／Common文件及LoadFolders.xml不变。原PNG、脚本／参数备份，以及资源检查、同步清单和结果保存于 `.builds/pna-weapons/industrial-art/needle-rifle-whipstyle-heavy-256/`。

同步报告保持 `gameRuntimeVerified=false`，等待新精确针枪贴图的地面／信息卡、持枪／瞄准、朝向、遮挡、尺寸与护木可见度反馈。此前图稿的实机认可不自动覆盖本轮；暂不提交或打包发布。

## 验收与提交

当前WhipStyleHeavy原图及256px导出已获维护者认可，drawSize继续为速射针枪1.05／精确针枪1.25。按授权提交PNG、原图、提示词、导出脚本／参数及文档；本次无新运行文件变化，不重复编译、测试或同步。上文等待反馈的内容属于各次接入时的历史记录，以本段及文首状态为准。
