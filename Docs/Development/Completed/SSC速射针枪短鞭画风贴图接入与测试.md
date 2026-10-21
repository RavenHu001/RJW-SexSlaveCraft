# SSC 速射针枪短鞭画风贴图接入与测试

> 版本归属：**2.4.3，仅 1.7**。原实施日期、测试／实机反馈和构建版本保留；本版汇总见[版本说明](../../Releases/2.4.3/2.4.3发布说明.md)，现行状态优先看所属主题的完成核对。

维护者已认可当前游戏效果并授权提交：速射针枪此前反馈“效果很好”，精确针枪最新反馈“可以了，提交”。本篇约定的贴图范围验收关闭；未逐项报告的方向／场景不另记为专项实测。原始检查、构建和同步报告保持原字段，不回写历史实测状态。未打包发布。

日期：2026-10-19。基线：`f026c5a`。状态：短鞭画风参考版及后续重描边版已按要求导出、接入及同步本机；当前采用重描边版，维护者已认可游戏效果并授权提交。

[前版两枪验收](SSC工业气枪粗描边重绘与缩放测试.md) · [资源表](../../Art/SSC纳米催情液武器资源.md) · [工业规划](../../Design/Archive/SSC工业时代纳米催情液武器开发规划.md)

## 选图与导出

采用内置 imagegen 生成的短鞭画风参考版，深灰紫工程塑料枪身、连续黑色描边和银灰局部高光；保留枪托内浅色气罐、定位夹具、压力表、护木及横排紫色针弹视窗。参考[短鞭物品原图](../../Art/SSC_PnaShortWhip/SSC_PnaShortWhip_Item_Approved.png)的画风，使用前版速射针枪作为构型参考，未采用此前的堂丸画风试验图。

选定源图：`C:/Users/24196/.codex/generated_images/01a14d5a-bbd6-79f3-9356-75f2d8ad5113/exec-a3dbf3cd-8236-4bdc-9d08-7f0f294c9f1a.png`，1774×887 RGBA，SHA256 `c4f2c803e8f8f81f30795ac0f27d51a739418eb82d42e32573a9e19d5fc07b42`。保存[原图](../../Art/SSC_PnaNeedleRepeater/SSC_PnaNeedleRepeater_WhipStyle.png)、[提示词](../../Art/SSC_PnaNeedleRepeater/prompt-whipstyle.txt)、[导出脚本](../../Art/SSC_PnaNeedleRepeater/Export-256.py)及[参数](../../Art/SSC_PnaNeedleRepeater/export.json)。历史Approved及Redraw原图保留。

只做尺寸处理：alpha>1取有效边界，加两像素源图边距，裁切(26,136,1762,868)，预乘alpha Lanczos等比导出240×101，偏移(8,77)，居中置于256×256透明画布。水平朝右，不重绘、拉伸或旋转。

## 接入与检查

替换既有运行PNG：[SSC_PnaNeedleRepeater.png](../../../1.7/Textures/Things/Item/Equipment/SSC_PnaNeedleRepeater.png)，SHA256 `4ffbebff22eafdff380592b1f6180c4e261d21512b9e106159c2b10ef850f37c`。drawSize=1.05及texPath保持，不改XML、源码镜像、握持角度、数值、声音或特效；精确针枪仍为已验收版本。

资源／引用检查通过：256×256 RGBA、alpha范围0～255、四角透明、边界(8,77,248,178)；源图实体轮廓未触及画布边界。已核对原图与指定文件哈希一致、XML解析内容与HEAD一致、镜像字节一致、drawSize=1.05、精确针枪PNG与HEAD一致。XML与布局未改，不重复运行前批的镜像／布局套件；无C#、DLL或BUILD-INFO变动，不重新编译。

## 同步与待验证

已备份并替换 `D:/STEAM/steamapps/common/RimWorld/Mods/RJW-SexSlaveCraft-TieJin-Modify` 中速射针枪PNG一份；514份1.7运行文件哈希匹配，532份1.6／Common文件及LoadFolders.xml不变。原运行PNG、导出脚本和参数，以及资源检查、同步清单和备份保存于 `.builds/pna-weapons/industrial-art/needle-repeater-whipstyle-256/`。

原图认可不等于新贴图已在游戏内验证；同步报告保持 `gameRuntimeVerified=false`。等待地面、信息卡、持枪／瞄准、朝向、遮挡以及高光可见度反馈，暂不提交或打包发布。前版实机认可保持其原范围。

## 后续重描边版接入（当前版本）

维护者在重描边预览后要求“把这版应用回游戏”。当前采用 `exec-2f4d15db-b4a6-43f6-ac6e-0cc658b63221.png`，1774×887 RGBA，SHA256 `fb12781d991fd254d116b88d65f333545dfa85ffce63ad45e83609094e199580`；保留原图构型、配色和内部细节，生成目标为强化外描边。原图认可与接入不等于实机效果已确认，也不将提示词线宽目标记作全图实测达标。

保存[当前原图](../../Art/SSC_PnaNeedleRepeater/SSC_PnaNeedleRepeater_WhipStyleHeavy.png)与[重描边提示词](../../Art/SSC_PnaNeedleRepeater/prompt-whipstyle-heavy.txt)，此前WhipStyle原图仍保留。导出脚本及export.json已指向WhipStyleHeavy：裁切(11,115,1774,885)，等比240×105、偏移(8,75)，256×256透明画布，水平朝右。源图右边缘存在60个alpha≥128的近黑描边像素，彩色区域未触边；按指定原图保留，不另行补画或生成新版本。输出画布四角透明、有效边界(8,75,248,180)。

当前运行图SHA256为 `3c50288910b6558604667c63e3f2da97cf34a2d03527cb74d89b914c5e2031c6`。已检查256px RGBA、透明度范围、引用、源文件对应、XML解析内容及镜像一致性；drawSize仍为1.05，精确针枪PNG与HEAD一致。只替换速射针枪PNG，无XML、C#或DLL变动，不重复编译或重跑机制套件。

已备份替换本机游戏PNG一份，514份1.7运行文件哈希匹配，532份冻结资源及LoadFolders.xml不变。最新原PNG／脚本／参数备份及检查、清单、同步结果位于 `.builds/pna-weapons/industrial-art/needle-repeater-whipstyle-heavy-256/`；上文首次短鞭画风版记录保留历史范围。`gameRuntimeVerified=false`，待实机反馈，暂不提交。

## 验收与提交

当前WhipStyleHeavy原图及256px导出已获维护者认可，drawSize继续为速射针枪1.05／精确针枪1.25。按授权提交PNG、原图、提示词、导出脚本／参数及文档；本次无新运行文件变化，不重复编译、测试或同步。上文等待反馈的内容属于各次接入时的历史记录，以本段及文首状态为准。
