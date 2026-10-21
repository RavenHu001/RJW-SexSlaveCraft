# SSC 脉冲枪单槽分层贴图接入与检查

> 版本归属：**2.4.3，仅 1.7**。原实施日期、测试／实机反馈和构建版本保留；本版汇总见[版本说明](../../Releases/2.4.3/2.4.3发布说明.md)，现行状态优先看所属主题的完成核对。

2026-10-20，维护者明确选择 `exec-511d72eb-17c9-4617-9e2a-9fcc5483ccfe.png`，要求导出256×256并应用回游戏；随后按相同思路绘制投射枪新原稿。脉冲枪及长枪随后分别完成本轮贴图接入，维护者最新反馈“效果不错，提交”，本轮按授权统一提交。下文长枪首稿段落保留当时的候选状态，当前长条瞄具版见[接入记录](SSC投射枪长条瞄具重描边贴图接入与检查.md)。

## 精确回选与导出

[脉冲枪指定原稿](../../Art/SSC_PnaPulseGun/SSC_PnaPulseGun_Detailed_v4_HeavyDark_Source.png)为1983×793透明RGBA，SHA-256：`8cb15c0f99b667951bb32536a7504bca056e165bbba1ce78b2f75f49f7099b51`。与指定生成文件逐字节一致，未采用后来的补留白版 `exec-e82a6f4a`。原图右侧触边情况如实保留，不通过另一次生成替换维护者选图。

[统一导出脚本](../../Art/Export-PnaSpacerGuns-256.py)使用alpha>1边界、两像素源边距、预乘alpha Lanczos等比缩放，内容240×90，偏移(8,83)，放入256×256透明画布；没有拉伸或旋转。输出与已展示的v4预览一致，SHA-256：`63050f30d684bfc1826897e35406528a17bceec0e582ff71a813ef699a6db07c`。

运行路径为 `1.7/Textures/Things/Item/Equipment/SSC_PnaPulseGun.png`，drawSize仍为1.05；XML、texPath、握持配置、声音、武器数值、C#及DLL不变，无需重新编译或重复机制测试。旧原稿、各轮候选、提示词和来源记录保留，当前选择见[RuntimeSelection](../../Art/SSC_PnaPulseGun/RuntimeSelection.json)。

## 检查与同步

- Def镜像25/25、布局／冻结基线538项通过。
- 除脉冲枪运行PNG外，其余1067份1.7／1.6／Common文件哈希不变，包含未改的投射枪运行贴图；画布、透明度、预览及档案副本一致。
- 核对安装mod身份和修改前全部536份1.7文件，备份旧PNG后只复制一份新图；同步后全部536份文件与工作区哈希一致。
- 安装位置：`D:/STEAM/steamapps/common/RimWorld/Mods/RJW-SexSlaveCraft-TieJin-Modify`。
- 备份及报告：`.builds/pna-weapons/pulse-detail-art/game-backup-20261020-170012`、同目录层级的 `game-sync.json`、`checks.json`、`before-files.json` 及 `export.log`。

同步及自动检查报告保持生成时点的 `gameRuntimeVerified=false`；维护者最新认可本轮效果并授权统一提交，验收在文档登记，不回写原报告，也不扩写未逐项报告的专项组合。

## 长枪新原稿

[投射枪分层候选](../../Art/SSC_PnaProjectorGun/SSC_PnaProjectorGun_Detailed_v1_Source.png)使用内置imagegen，以现有长枪为构型、指定脉冲枪为系列材质／线条参考，辅以工业速射针枪和太空鞭柄。保留筒式瞄具、后置液舱及长枪定位，增加侧盖板、单条贯通紫槽、银灰接口及局部下护木分层。完整[提示词](../../Art/SSC_PnaProjectorGun/DetailRedraw_v1.prompt.txt)及[候选元数据](../../Art/SSC_PnaProjectorGun/DetailRedraw_v1.json)已保存；未替换运行图或进行游戏同步。
