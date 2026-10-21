# RJW-SexSlaveCraft · TieJin Modify

面向 RimWorld 的成人向 RimJobWorld（RJW）扩展，基于上游 **2.2.8** 继续维护，提供角色培养、主从绑定、六种特化、人格转移和身体改造。

**当前开发版本：2.4.3（2026-10-20 登记，未发布）。** `v2.4.2` 发布后截至 `dea5c78` 的开发统一归入本版；最新公开下载仍为 **2.4.2**。1.7 承载新增功能，1.6／Common 保留 **2.4.1** 冻结基线。当前 DLL 与 About 等产物元数据仍为 2.4.2，正式发布前须版本对齐重编，详见[验证与发布边界](Docs/Releases/2.4.3/2.4.3版本验证.md)。

[2.4.3 版本范围](Docs/Releases/2.4.3/2.4.3发布说明.md) · [正式下载 2.4.2](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.2) · [双语模组介绍](Docs/模组介绍.md) · [玩家阅读入口](Docs/Players/README.md) · [文档总索引](Docs/README.md) · [English](#english-overview)

## 2.4.3 开发成果（仅 1.7）

| 方向 | 当前交付 | 详细说明 |
| --- | --- | --- |
| 主从关系概览 | 关系卡片、头像定位、调教员指派、当前地图批选、局部刷新与显式开发诊断 | [完成核对](Docs/Development/Completed/主从关系概览完成核对.md) |
| 低科技发展 | 研究分级调整、篝火／灶台 10→1 粗放浓缩及兼容烹饪工作台接入 | [实现与验收](Docs/Development/Completed/低科技适配首批实现与测试.md) |
| 三代九把 PNA 武器 | 中世纪、工业、太空各三把；研究／配方、效力、原版品质、正式美术与反馈；太空有效链无伤吸收及即时修复 | [版本清单](Docs/Releases/2.4.3/2.4.3发布说明.md#三代九把-pna-武器) |
| 结算与清理 | 分摊伤害不传药、两代鞭束配合受药、旧品质适配归档与旧发射器退役 | [太空完成核对](Docs/Development/Completed/SSC太空时代纳米催情液武器完成核对.md) |
| 欢愉恩赐 | 负荷按主人身体产液能力折算 3～100 点，参考产量 20 对应 8 点；其余结算沿用原规则 | [剂量验收](Docs/Development/Completed/欲望侵染体液产量与PNA剂量接入.md) |

**升级注意：1.7 已移除旧简易 PNA 发射器、弹丸、配方与专属研究，不兼容引用这些旧定义的存档，不提供迁移或替代。** 1.6 冻结版保留原内容。上述开发增量尚未进入既有 2.4.2 下载附件。

## 核心玩法与版本沿革

- 日常调教、手动命令、自我调教与契约仪式，连接恶堕、锁链及六项部位成长。
- 公交车、奶牛、训导官、宠物猫、宠物狗、战斗员六种特化；普通培养完成后，经人格凝胶加工／植入获得终极形态。
- 欲望之种与永久主人资格、欲望共生四阶段和主人面板由 **2.4.0** 引入。
- PNA 五档、身体归属、来源偏好、欢愉恩赐首版及绑定维护归入 **2.4.1**。
- 双版本布局、1.7 兼容与个体凝胶颜色／不透明度／渲染开关归入 **2.4.2**，本版继续保留。

新功能、继承内容和未完成提案分别登记，完整范围见[版本索引](Docs/Releases/README.md)与[更新日志](CHANGELOG.md)。

## 安装与依赖

| 项目 | 要求 |
| --- | --- |
| 游戏版本 | RimWorld 1.7；同时保留 1.6／SSC 2.4.1 冻结运行内容 |
| 必需模组 | 匹配游戏的 Harmony、RJW，加载在 SSC 之前 |
| 契约仪式与阶段进阶 | 需要 Ideology（文化）DLC |
| Royalty（可选） | 安装时调教研究需要蓝图；种子或原版独立蓝图可补齐，之后仍需研究 |

正式游玩从 [v2.4.2 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.2) 下载 ZIP 与 SHA-256。退出游戏，移出旧文件夹后完整解压新文件夹，保留加载配置及两套版本目录。该下载的具体功能范围见[2.4.2 发布说明](Docs/Releases/2.4.2/2.4.2发布说明.md)；当前仓库指南另含 2.4.3 开发内容。

宠物兔选择入口未开放，旧 RimTalk 对接暂停。受训目标须为 SSC 性奴身份，原版奴隶身份不能替代。

## 文档导航

| 用途 | 阅读顺序 |
| --- | --- |
| 了解与游玩 | [模组介绍](Docs/模组介绍.md) → [玩家阅读入口](Docs/Players/README.md) → 快速入门 → 完整机制 |
| 查看版本变化 | [2.4.3 说明](Docs/Releases/2.4.3/2.4.3发布说明.md) → [提交归并核对](Docs/Releases/2.4.3/2.4.3开发归并核对.md) → [验证边界](Docs/Releases/2.4.3/2.4.3版本验证.md) |
| 接手开发 | [已交付记录](Docs/Development/Completed/README.md) → [专项待验收](Docs/Development/Verification/README.md) → [当前规划](Docs/Design/README.md) |
| 编译、测试、打包 | [维护入口](Docs/Maintenance/README.md) → [测试说明](Tests/README.md) → [发布与打包](Docs/Maintenance/发布与打包.md) |
| 美术与历史依据 | [资源索引](Docs/Art/README.md) · [设计归档](Docs/Design/Archive/README.md) · [开发日志](Docs/Development/开发更新记录.md) |

1.7 工程为 `Sexslavecraft/SexSlaveCraft_1.7.csproj`（.NET 10），输出到 `1.7/Assemblies/`。1.6 成品按哈希冻结，不能用当前源码回归冒充旧版验证。文档新增与移动遵循[维护约定](Docs/Maintenance/文档维护约定.md)。

问题反馈请附模组／游戏版本、加载顺序、复现步骤、日志及新旧档情况。项目署名：Someone、Hajimi、TieJin。

## English overview

SexSlaveCraft is an adult RJW expansion for pawn development, bonds, six specializations, personality transfer and body modification. **The current development version is 2.4.3, registered on 2026-10-20 and not yet released. The public download remains 2.4.2.** New work targets 1.7; the 1.6/Common baseline stays frozen at 2.4.1.

2.4.3 groups all post-v2.4.2 development through `dea5c78`: the bond overview, low-tech research and rough concentration, nine PNA weapons across three eras, dedicated art/feedback, native quality scaling, isolated damage sharing, cooperative whip hits, retirement of the old launcher, and Bestow Bliss doses based on body fluid output. **1.7 saves referencing the removed launcher definitions have no migration path.** Current binary/About metadata still says 2.4.2 and requires alignment before a 2.4.3 release.

Load Harmony and RJW before SSC; Binding Rituals require Ideology. Start with the [English mod overview](Docs/模组介绍.md#english), [player reading order](Docs/Players/README.md#english-reading-order), [quick start](QUICK_START_EN.md) and [full guide](PLAYER_GUIDE_EN.md). Read the [2.4.3 scope](Docs/Releases/2.4.3/2.4.3发布说明.md#english) for development changes, or the [2.4.2 notes](Docs/Releases/2.4.2/2.4.2发布说明.md#english) for the existing download.
