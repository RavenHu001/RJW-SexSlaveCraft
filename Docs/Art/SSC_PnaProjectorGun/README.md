# 纳米催情液投射枪贴图

## 当前接入：长条瞄具与干净重描边v4

2026-10-20，维护者认可 `exec-c42a1a6e-c05c-48b2-bf0f-80f0fc20f9cf.png` 并要求处理为256×256导入游戏。当前采用[重描边原稿](SSC_PnaProjectorGun_Detailed_v4_HeavyClean_Source.png)，保留低矮长条光学舱、单紫槽、侧盖板与局部护木分层，替换旧传统筒镜运行图。

通过[统一导出脚本](../Export-PnaSpacerGuns-256.py)导出256×256透明RGBA，内容240×64、偏移(8,96)，drawSize=1.25。运行图、档案副本及已展示的v4预览逐字节一致。仅替换一张PNG，XML、装备配置及DLL保持；已备份同步本机，维护者已认可本轮效果并授权提交，验收关闭。[当前选择](RuntimeSelection.json) · [导出报告](export-256.json) · [检查与同步](../../Development/Completed/SSC投射枪长条瞄具重描边贴图接入与检查.md)。

以下保留此前的试绘、原稿认可和旧运行版本记录，不代表当前接入状态。


2026-10-20：维护者确定v3长条光学舱的设计，要求结合其他参考图重绘并进一步加粗黑边。已使用内置imagegen，以v3构型、指定脉冲枪、工业速射针枪和太空鞭柄生成v4干净重描边候选。[高清原稿](SSC_PnaProjectorGun_Detailed_v4_HeavyClean_Source.png) · [256px预览](SSC_PnaProjectorGun_Detailed_v4_HeavyClean_256.png) · [提示词](DetailRedraw_v4_HeavyClean.prompt.txt) · [来源与参数](DetailRedraw_v4_HeavyClean.json)。本轮只生成候选，未接入游戏或提交，原运行图及各轮原稿保留。

2026-10-20 后续瞄具试绘：维护者认为v2形状不合适，另生成低矮长条光学舱候选v3，使用内置imagegen从v1枪体重新编辑。[高清原稿](SSC_PnaProjectorGun_Detailed_v3_PodSight_Source.png) · [256px预览](SSC_PnaProjectorGun_Detailed_v3_PodSight_256.png) · [提示词](DetailRedraw_v3_PodSight.prompt.txt) · [来源记录](DetailRedraw_v3_PodSight.json)。仅候选预览，未接入游戏或提交。

## 新候选：与上壳融合的科幻瞄具

2026-10-20，按维护者要求将分层候选的独立筒镜改为低矮梯形／分面瞄具模块，底座与枪上层衔接，保留浅蓝小视窗。使用内置imagegen编辑，其余枪体沿用v1构型。[高清原稿](SSC_PnaProjectorGun_Detailed_v2_IntegratedSight_Source.png) · [256px预览](SSC_PnaProjectorGun_Detailed_v2_IntegratedSight_256.png) · [提示词](DetailRedraw_v2_IntegratedSight.prompt.txt) · [来源与参数](DetailRedraw_v2_IntegratedSight.json)。仅候选预览，未替换长枪运行图或同步游戏。

## 候选重绘：单槽分层与重描边

2026-10-20：以现有长枪构型、明确选定的暗色重描边脉冲枪、工业速射针枪及太空鞭柄为参考，生成[新版高清原稿](SSC_PnaProjectorGun_Detailed_v1_Source.png)与[256px预览](SSC_PnaProjectorGun_Detailed_v1_256.png)。本轮采用内置imagegen；保留长枪及筒式瞄具，增加接收机侧盖板、单紫槽、银灰接口和局部下护木分层。来源、尺寸、哈希见[候选记录](DetailRedraw_v1.json)，完整[提示词](DetailRedraw_v1.prompt.txt)已保存。候选尚未认可、未接入或同步游戏，下文仍是当前运行版本。

2026-10-20：已按维护者明确回选原稿导出、接入并同步本机；维护者认可当前效果并授权提交，贴图验收关闭。当前版本是**完全侧视、传统瞄准镜、单条分段紫槽贯通枪口**，没有采用后来生成的梯形瞄具试稿。

[当前原稿](SSC_PnaProjectorGun_Source.png)来自`exec-a4365b18-2e50-4033-878e-9fee23c8cf03.png`，2172×724 RGBA，SHA-256为`1524ae1a344689809712507a05d67cc4476dc3ee07880f622a6a20097004392e`，仓库副本与指定文件一致。原稿由内置imagegen生成：长枪侧视版`exec-754879e2`提供构型，脉冲枪重描边版`exec-36869d38`提供侧槽贯通和线条参考，太空丝鞭物品v2提供材质参考。

保留长枪前部投送模块、深蓝灰局部护木、独立低位瞄准镜、竖置可拆液舱，以及冷灰白／银灰／深紫色块。描边、槽内分段及实际显示大小须由本轮游戏验证确认，原稿生成时的线宽目标不作为已测量结果。

使用[统一导出脚本](../Export-PnaSpacerGuns-256.py)，按alpha>1边界加两像素源边距，预乘alpha Lanczos等比缩为240×64，居中偏移(8,96)，放入[256×256透明画布](256/SSC_PnaProjectorGun.png)。运行图为[SSC_PnaProjectorGun.png](../../../1.7/Textures/Things/Item/Equipment/SSC_PnaProjectorGun.png)，drawSize保持1.25。没有拉伸、旋转或重绘。

[来源清单](RuntimeSelection.json) · [导出参数与输出哈希](export-256.json) · [接入／同步与检查](../../Development/Completed/SSC太空两枪256px贴图接入与检查.md)。原稿身份以完整生成文件名与哈希为准，后续试稿不自动覆盖回选。
