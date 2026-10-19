# RJW-SexSlaveCraft · TieJin Modify

> 当前分支进展（2026-10-19）：1.7 的低科技适配、关系概览、中世纪与工业 PNA 三武器已交付。工业短鞭／两枪机制、正式图稿、效力、姿态／特效及枪声均已分批验收，见[工业完成核对](Docs/Development/Completed/SSC工业时代纳米催情液武器完成核对.md)、[中世纪完成核对](Docs/Development/Completed/SSC武器拓展首批完成核对.md)及[未发布增量](CHANGELOG.md#未发布开发增量--unreleased-development)。尚未重新打包进既有 2.4.2 下载，1.6／Common 冻结不变。

面向RimWorld的成人向RimJobWorld（RJW）扩展模组，基于上游 **2.2.8** 继续维护。当前版本：**2.4.2 — 双版本目录、1.7迁移与个体凝胶外观**（2026-10-17）。v2.4.1发布后的开发统一纳入本版；1.6运行内容继续冻结在2.4.1。

[2.4.2 版本说明](Docs/Releases/2.4.2/2.4.2发布说明.md) · [正式版下载 2.4.2](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.2) · [模组介绍 / Mod overview](Docs/模组介绍.md) · [玩家阅读入口 / Player documentation](Docs/Players/README.md) · [文档总索引](Docs/README.md) · [English overview](#english-overview)

当前工作区采用双版本目录：`1.6/` 保留 **2.4.1** 冻结运行基线，`1.7/` 承载 **2.4.2** 的后续开发，`Common/Textures/` 共享基线贴图。双版本重布局、1.7兼容修复及凝胶渲染／外观／个人开关均归入2.4.2；功能与实机反馈见[本版范围](Docs/Releases/2.4.2/2.4.2发布说明.md)，各批保留原始验证日期。当前公开下载为2.4.2双版本包，构建与版本对齐要求见[维护说明](https://github.com/RavenHu001/RJW-SexSlaveCraft/blob/main/Docs/Maintenance/%E5%8F%91%E5%B8%83%E4%B8%8E%E6%89%93%E5%8C%85.md)。

## 项目概况

SSC 提供角色培养、主从绑定、特化发展、人格转移和身体改造玩法：

- 日常调教、自我调教与契约仪式，连接恶堕、锁链和六项部位成长。
- 公交车、奶牛、训导官、宠物猫、宠物狗、战斗员六种特化。
- 人格凝胶与终极特化，以及局部和全身凝胶化改造。
- **2.4.0 引入**欲望之种、个人主人资格、欲望共生四阶段成长与主人面板；资格与成长随人格保留，克隆不继承。
- **2.4.1 归并**PNA 五档负荷与性奴活化、原版十天成瘾恢复、主人免疫、身体归属、来源性吸引、产出者性交补给及后续信息栏／研究／枪击成瘾修正。

2.4.1 同时包含已完成四批实机验收的[欢愉恩赐首版](Docs/Development/Completed/主人欲望侵染完成核对.md)及其后续四语文案、主从绑定关系和 CE 删除修复、任务文本与两类镜像统一检查；中英文使用说明见玩家入口。

完整功能概述见[模组介绍](Docs/模组介绍.md)。当前版本范围见[2.4.2说明](Docs/Releases/2.4.2/2.4.2发布说明.md)与[版本登记/验证边界](Docs/Releases/2.4.2/2.4.2版本验证.md)；已发布PNA等基础内容保留[2.4.1说明](Docs/Releases/2.4.1/2.4.1发布说明.md)。历史2.4.0下载包不包含后续PNA重置。

## 安装

| 项目 | 要求 |
| --- | --- |
| 游戏版本 | RimWorld 1.6／1.7；1.6冻结2.4.1，新功能在1.7 |
| 必需模组 | Harmony、RimJobWorld（RJW），均加载在 SSC 之前 |
| 契约仪式与阶段进阶 | 需要 Ideology（文化）DLC 的仪式系统 |
| Royalty（皇权，可选） | 安装时调教研究需要蓝图；种子或原版独立蓝图可补齐，之后仍需研究 |

1. 从 [v2.4.2 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.2) 下载安装 ZIP 和 SHA-256 校验文件。
2. 退出游戏，将 ZIP 内的模组文件夹解压到 `RimWorld/Mods/`。
3. 更新时先将旧模组文件夹移出 `Mods`，再放入新版本，避免残留文件。
4. 启用依赖与本模组，按上表加载。2.4.0 的个人资格、当前主人身份和研究分别管理，再阅读[中文快速入门](读我，玩法介绍.md)或 [English quick start](QUICK_START_EN.md)。

2.4.2安装包包含两版DLL与显式加载配置：1.6保留2.4.1，1.7使用2.4.2。宠物兔选择入口尚未开放，旧 RimTalk 对接暂停；其他限制与兼容说明见玩家指南。

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

The 1.7 development branch also includes accepted low-tech adaptation, the bond overview and three medieval PNA-coated weapons, with dedicated art, potency displays and visual feedback. These branch changes have not been repackaged into the existing 2.4.2 download. See the [completion audit](Docs/Development/Completed/SSC武器拓展首批完成核对.md).

The current checkout has separate `1.6/` and `1.7/` runtime directories with shared textures in `Common/Textures/`. The 1.6 content is frozen at 2.4.1; the maintainer tested the current 1.7 layout and compatibility-fix batch and reported no issues. The current 2.4.2 package includes both version directories.

SexSlaveCraft (SSC) is an adult RimJobWorld expansion for **RimWorld** (1.7 and the frozen 1.6 runtime), continuing from upstream **2.2.8**. The current release is **2.4.2**, covering all work after the published v2.4.1 tag, including the versioned layout and 1.7 gel rendering/UI. The former 2.4.0.5 milestone and work completed through the v2.4.1 release remain part of 2.4.1. The release package is **2.4.2**. Harmony and RJW must load before SSC; Binding Rituals and their progression route require Ideology.

Start with the [mod overview](Docs/模组介绍.md#english), then follow [the player reading order](Docs/Players/README.md#english-reading-order). The Seed of Desire and Master progression introduced in 2.4.0 remain included. For the current release scope, read the [2.4.2 notes](Docs/Releases/2.4.2/2.4.2发布说明.md#english) and [version-registration limits](Docs/Releases/2.4.2/2.4.2版本验证.md). For the published base, read the [2.4.1 notes](Docs/Releases/2.4.1/2.4.1发布说明.md#english) and [validation record](Docs/Releases/2.4.1/2.4.1版本验证.md).

[Download 2.4.2](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.2) · [Quick start](QUICK_START_EN.md) · [Full guide](PLAYER_GUIDE_EN.md) · [Documentation index](Docs/README.md)
