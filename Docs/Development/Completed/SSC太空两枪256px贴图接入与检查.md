# SSC 太空两枪256px贴图接入与检查

> 版本归属：**2.4.3，仅 1.7，开发未发布**。原实施日期、测试／实机反馈和构建版本保留；本版汇总见[版本说明](../../Releases/2.4.3/2.4.3发布说明.md)，现行状态优先看所属主题的完成核对。

日期：2026-10-20。基线：`6d12dda`。**状态：已按指定原稿导出、接入并同步本机，维护者认可当前效果并授权提交，本批贴图验收关闭。** 本机版本为1.7.4982 rev1280。本批只改变美术资源和两枪texPath，不重新编译C#。

## 明确回选与确定性导出

| 武器 | 指定生成文件 | 当前图稿与导出 | 有效等比尺寸／偏移 | drawSize |
| --- | --- | --- | --- | --- |
| 脉冲枪 | `exec-36869d38-822e-4bda-b3a8-a642ab1d605b.png` | [原稿／记录](../../Art/SSC_PnaPulseGun/README.md) | 240×90／(8,83) | 1.05 |
| 投射枪 | `exec-a4365b18-2e50-4033-878e-9fee23c8cf03.png` | [原稿／记录](../../Art/SSC_PnaProjectorGun/README.md) | 240×64／(8,96) | 1.25 |

维护者明确指定两张图，覆盖此前未定稿的各轮试验：脉冲枪为完全侧视重描边、上槽贯通而下槽封闭的版本；投射枪保留传统瞄准镜，不采用最后生成的梯形瞄具。

通过[导出脚本](../../Art/Export-PnaSpacerGuns-256.py)进行固定转换，校验选图哈希，按alpha>1边界加两像素源边距，预乘alpha Lanczos等比缩放一次、居中放入256×256透明RGBA画布；保留水平朝右，不重绘、不旋转或拉伸。两源图实体均未触边；输出四角透明、alpha范围0～255。源图、256px档案副本、运行图和哈希报告均保存在仓库。

运行图SHA-256：

- 脉冲枪：`1a2c99db3a365a4ee0f41ac4cce956124647379a9ea529a939a19a5bc6815f9d`。
- 投射枪：`41a5fd716bb992c5a7cee5120c1df1e05f9844bbcbf063dc08fbc00c177dd38e`。

## XML与检查

`1.7/Defs/ThingsDefs/SSC_PnaSpacerWeapons.xml`及源码镜像中，两枪的texPath分别从工业占位改为`Things/Item/Equipment/SSC_PnaPulseGun`和`Things/Item/Equipment/SSC_PnaProjectorGun`，并更新文件注释。drawSize保持1.05／1.25，握持参数、伤害、射速、科技、配方、投射物、声音、特效及丝鞭不变。

检查结果：

- 与HEAD解析后的XML比较，只允许上述两个texPath变化，其他Def数据相同。
- 25/25份Def镜像逐字节一致；布局及1.6／Common冻结基线538项通过。
- 指定源文件和仓库原稿哈希一致；两张运行图均为256×256透明RGBA，与各自档案副本逐字节相同；已查看实际尺寸图。
- 本批无C#、DLL或机制变更，不重跑机制回归或重新编译。布局脚本中的“1.7 binary: rebuilt”仅描述既有DLL来源，不表示本轮重编译。

资源检查证据位于`.builds/pna-weapons/spacer-guns-art-256/resource-checks.json`；导出参数分别见两资源目录的`export-256.json`。

## 游戏同步及验收边界

已核对安装mod身份，检查非本轮运行文件无差异，再同步两张PNG及武器XML共3份文件到`D:/STEAM/steamapps/common/RimWorld/Mods/RJW-SexSlaveCraft-TieJin-Modify`。既有XML先备份，新PNG记录为新增。全部545份1.7运行文件与工作区哈希一致。

备份：`.builds/pna-weapons/spacer-guns-art-256/game-sync/backup-20261020-130919-8647`；同步报告：同目录`game-sync.json`。报告保持`gameRuntimeVerified=false`，不把复制成功当成实机验收。旧工业图保留供工业武器使用，没有删除或覆盖。

初次同步时列出的地面／信息卡、手持／瞄准、各方向镜像、描边、占幅和枪口辨识作为观察参照保留。维护者随后反馈“可以，提交”，据此记录当前效果认可并关闭本批美术验收；未逐项报告的场景不补记为专项实测。

本轮按授权提交两枪源图、导出脚本／报告、运行PNG、XML引用和相关文档；不改变已经测试的资源或DLL，不重新编译或同步。原导出和同步报告中的`gameRuntimeVerified=false`保留其生成时点含义，本次认可仅记录在文档。声音另行分析，本批不改音效，不打包发布。
