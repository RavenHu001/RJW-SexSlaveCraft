# RJW-SexSlaveCraft · TieJin Modify

面向 RimWorld 1.6 的成人向 RimJobWorld（RJW）扩展模组，基于上游 **2.2.8** 继续维护。当前版本归属：**2.4.1 — PNA 重置、欢愉恩赐与绑定维护**（2026-10-16）。原 2.4.0.5 是开发版本，其全部内容及后续开发统一归入 2.4.1；正式下载版为 **2.4.1**。

[2.4.1 版本说明](Docs/Releases/2.4.1/2.4.1发布说明.md) · [正式版下载 2.4.1](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.1) · [模组介绍 / Mod overview](Docs/模组介绍.md) · [玩家阅读入口 / Player documentation](Docs/Players/README.md) · [文档总索引](Docs/README.md) · [English overview](#english-overview)

当前工作区已采用双版本目录：`1.6/` 保留 **2.4.1** 运行基线，`1.7/` 用于后续开发，`Common/Textures/` 共享基础贴图。1.7 本批重布局及兼容修复已获维护者确认测试未发现问题，见[验收记录](Docs/Development/Completed/双版本重布局与1.7兼容修复验收.md)；下方公开下载仍为原 1.6 正式版。构建和目录规则见[维护说明](https://github.com/RavenHu001/RJW-SexSlaveCraft/blob/main/Docs/Maintenance/%E5%8F%91%E5%B8%83%E4%B8%8E%E6%89%93%E5%8C%85.md)。

## 项目概况

SSC 提供角色培养、主从绑定、特化发展、人格转移和身体改造玩法：

- 日常调教、自我调教与契约仪式，连接恶堕、锁链和六项部位成长。
- 公交车、奶牛、训导官、宠物猫、宠物狗、战斗员六种特化。
- 人格凝胶与终极特化，以及局部和全身凝胶化改造。
- **2.4.0 引入**欲望之种、个人主人资格、欲望共生四阶段成长与主人面板；资格与成长随人格保留，克隆不继承。
- **2.4.1 归并**PNA 五档负荷与性奴活化、原版十天成瘾恢复、主人免疫、身体归属、来源性吸引、产出者性交补给及后续信息栏／研究／枪击成瘾修正。

2.4.1 同时包含已完成四批实机验收的[欢愉恩赐首版](Docs/Development/Completed/主人欲望侵染完成核对.md)及其后续四语文案、主从绑定关系和 CE 删除修复、任务文本与两类镜像统一检查；中英文使用说明见玩家入口。

完整功能概述见[模组介绍](Docs/模组介绍.md)。当前版本范围见[2.4.1 版本说明](Docs/Releases/2.4.1/2.4.1发布说明.md)，既有测试与本次文档检查边界见[版本验证](Docs/Releases/2.4.1/2.4.1版本验证.md)。历史 2.4.0 下载包不包含后续 PNA 重置。

## 安装

| 项目 | 要求 |
| --- | --- |
| 游戏版本 | RimWorld 1.6 |
| 必需模组 | Harmony、RimJobWorld（RJW），均加载在 SSC 之前 |
| 契约仪式与阶段进阶 | 需要 Ideology（文化）DLC 的仪式系统 |
| Royalty（皇权，可选） | 安装时调教研究需要蓝图；种子或原版独立蓝图可补齐，之后仍需研究 |

1. 从 [v2.4.1 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.1) 下载安装 ZIP 和 SHA-256 校验文件。
2. 退出游戏，将 ZIP 内的模组文件夹解压到 `RimWorld/Mods/`。
3. 更新时先将旧模组文件夹移出 `Mods`，再放入新版本，避免残留文件。
4. 启用依赖与本模组，按上表加载。2.4.0 的个人资格、当前主人身份和研究分别管理，再阅读[中文快速入门](读我，玩法介绍.md)或 [English quick start](QUICK_START_EN.md)。

2.4.1 安装包包含已编译的正式 DLL。宠物兔选择入口尚未开放，旧 RimTalk 对接暂停；其他限制与兼容说明见玩家指南。

## 文档导航

| 你想做什么 | 从这里开始 | 下一步 |
| --- | --- | --- |
| 了解模组功能 | [模组介绍](Docs/模组介绍.md) | [玩家阅读顺序](Docs/Players/README.md) |
| 安装、开局或查玩法 | [玩家阅读入口](Docs/Players/README.md) | 快速入门 → 对应机制 → 常见问题 |
| 查看版本变化 | [发布记录](Docs/Releases/README.md) | 发布说明 → 验证记录；跨版本检索见 [CHANGELOG](CHANGELOG.md) |
| 讨论后续开发 | [当前规划](Docs/Design/README.md) | 总体状态 → 专项方案 |
| 追溯已交付功能 | [开发资料入口](Docs/Development/README.md) | 完成记录、待验收、复盘或历史交接 |
| 编译、测试、打包 | [开发维护入口](Docs/Maintenance/README.md) | 环境与命令 → [测试说明](Tests/README.md) |
| 查找美术资料 | [美术索引](Docs/Art/README.md) | 当前资源与历史尝试 |

所有用途与目录说明集中在[文档总索引](Docs/README.md)。

## 开发与验证

1.7 开发工程为 `Sexslavecraft/SexSlaveCraft_1.7.csproj`，目标框架为 .NET 10，输出至 `1.7/Assemblies/`。1.6 的现有 2.4.1 DLL 按哈希冻结，旧工程保留源码清单但不用于覆盖基线。环境、构建、自动回归和本地打包命令统一见[发布与打包](Docs/Maintenance/发布与打包.md)，专项步骤见[测试说明](Tests/README.md)。

准备新功能先查[当前规划](Docs/Design/README.md)；接手已有功能先查[开发资料入口](Docs/Development/README.md)。已交付设计单独保存在[设计归档](Docs/Design/Archive/README.md)，文档新增与移动遵循[维护约定](Docs/Maintenance/文档维护约定.md)。

## 反馈与贡献

问题反馈请提供模组与游戏版本、模组列表及加载顺序、复现步骤和错误日志；涉及存档时说明新档或旧档。提交修改时说明影响范围和验证结果。

项目署名：Someone、Hajimi、TieJin。感谢上游作者及参与修复、翻译和验证的贡献者。

## English overview

The current checkout has separate `1.6/` and `1.7/` runtime directories with shared textures in `Common/Textures/`. The 1.6 content is frozen at 2.4.1; the maintainer tested the current 1.7 layout and compatibility-fix batch and reported no issues. The public 2.4.1 download below remains the original 1.6 release.

SexSlaveCraft (SSC) is an adult RimJobWorld expansion for **RimWorld 1.6**, continuing from upstream **2.2.8**. Version **2.4.1** consolidates the former 2.4.0.5 development milestone and subsequent PNA, Bestow Bliss, bond-maintenance, localization and validation work. The stable download is **2.4.1**. Harmony and RJW must load before SSC; Binding Rituals and their progression route require Ideology.

Start with the [mod overview](Docs/模组介绍.md#english), then follow [the player reading order](Docs/Players/README.md#english-reading-order). The Seed of Desire and Master progression introduced in 2.4.0 remain included. For the full 2.4.1 scope and verification boundaries, read the [2.4.1 notes](Docs/Releases/2.4.1/2.4.1发布说明.md#english) and [validation record](Docs/Releases/2.4.1/2.4.1版本验证.md).

[Latest public download: 2.4.1](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.1) · [Quick start](QUICK_START_EN.md) · [Full guide](PLAYER_GUIDE_EN.md) · [Documentation index](Docs/README.md)
