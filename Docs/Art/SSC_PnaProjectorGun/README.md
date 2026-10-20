# 纳米催情液投射枪贴图

2026-10-20：已按维护者明确回选原稿导出、接入并同步本机；维护者认可当前效果并授权提交，贴图验收关闭。当前版本是**完全侧视、传统瞄准镜、单条分段紫槽贯通枪口**，没有采用后来生成的梯形瞄具试稿。

[当前原稿](SSC_PnaProjectorGun_Source.png)来自`exec-a4365b18-2e50-4033-878e-9fee23c8cf03.png`，2172×724 RGBA，SHA-256为`1524ae1a344689809712507a05d67cc4476dc3ee07880f622a6a20097004392e`，仓库副本与指定文件一致。原稿由内置imagegen生成：长枪侧视版`exec-754879e2`提供构型，脉冲枪重描边版`exec-36869d38`提供侧槽贯通和线条参考，太空丝鞭物品v2提供材质参考。

保留长枪前部投送模块、深蓝灰局部护木、独立低位瞄准镜、竖置可拆液舱，以及冷灰白／银灰／深紫色块。描边、槽内分段及实际显示大小须由本轮游戏验证确认，原稿生成时的线宽目标不作为已测量结果。

使用[统一导出脚本](../Export-PnaSpacerGuns-256.py)，按alpha>1边界加两像素源边距，预乘alpha Lanczos等比缩为240×64，居中偏移(8,96)，放入[256×256透明画布](256/SSC_PnaProjectorGun.png)。运行图为[SSC_PnaProjectorGun.png](../../../1.7/Textures/Things/Item/Equipment/SSC_PnaProjectorGun.png)，drawSize保持1.25。没有拉伸、旋转或重绘。

[来源清单](RuntimeSelection.json) · [导出参数与输出哈希](export-256.json) · [接入／同步与检查](../../Development/Completed/SSC太空两枪256px贴图接入与检查.md)。原稿身份以完整生成文件名与哈希为准，后续试稿不自动覆盖回选。
