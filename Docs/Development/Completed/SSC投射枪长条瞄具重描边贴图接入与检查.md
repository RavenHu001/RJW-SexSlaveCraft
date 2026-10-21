# SSC 投射枪长条瞄具重描边贴图接入与检查

> 版本归属：**2.4.3，仅 1.7**。原实施日期、测试／实机反馈和构建版本保留；本版汇总见[版本说明](../../Releases/2.4.3/2.4.3发布说明.md)，现行状态优先看所属主题的完成核对。

日期：2026-10-20。维护者认可 `exec-c42a1a6e-c05c-48b2-bf0f-80f0fc20f9cf.png` 并要求导出256×256、导入本机游戏。维护者随后反馈“效果不错，提交”，本轮外观已获认可并按授权提交；不扩大为未逐项报告的场景组合均已专项实测。

## 精确选图与导出

[指定原稿](../../Art/SSC_PnaProjectorGun/SSC_PnaProjectorGun_Detailed_v4_HeavyClean_Source.png)为2172×724透明RGBA，SHA-256：`117a49473db11d56cab0554003dfdb24ef2e45dad59b5c30c162409aa15cfcc9`，与指定生成文件逐字节一致。保留低矮长条光学舱与小型浅蓝视窗、单紫槽、侧盖板和局部护木分层；原稿由内置imagegen使用已确定构型和三张画风／材质参考重绘，本轮不再生成或改画。

[统一导出脚本](../../Art/Export-PnaSpacerGuns-256.py)按alpha>1边界及两像素源边距，以预乘alpha Lanczos等比缩放为240×64，偏移(8,96)，置入256×256透明画布。保留源图弱透明边缘情况，不补画或替换来源；输出SHA-256：`866a92e04d1aa0081b0f0f118064662801778f9cc55508876f9243617a5c22ee`，与已展示v4预览、档案副本一致。

运行位置为 `1.7/Textures/Things/Item/Equipment/SSC_PnaProjectorGun.png`，drawSize保持1.25。texPath、XML、装备角度、其他武器图、声音、数值、C#和DLL不变；本次仅PNG替换，无需重新编译或重复机制回归。

## 检查与本机同步

- Def镜像25/25，布局／冻结基线538项通过。
- 除投射枪PNG外，其余1067份1.7／1.6／Common文件哈希不变，包含已同步的新脉冲枪；尺寸、RGBA、透明范围、来源及导出哈希核对通过。
- 安装mod身份及修改前全部536份1.7文件一致，备份旧PNG后仅复制投射枪新图；同步后536份运行文件与工作区逐一哈希一致。
- 安装目录：`D:/STEAM/steamapps/common/RimWorld/Mods/RJW-SexSlaveCraft-TieJin-Modify`。
- 备份：`.builds/pna-weapons/projector-detail-art/game-backup-20261020-170846`。证据目录 `.builds/pna-weapons/projector-detail-art/` 中保存 `before-files.json`、`checks.json`、`export.log`、`game-sync.json`。

同步与自动检查报告保留生成时点的 `gameRuntimeVerified=false`；维护者后续已认可当前效果并授权提交，本批验收关闭，不回写原报告。此前筒镜版验收保持历史含义，各轮图稿及提示词保留；本轮不重绘、重新导出、编译、同步或发布。
