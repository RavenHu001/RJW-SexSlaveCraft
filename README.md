# RJW-SexSlaveCraft · TieJin Modify

**2.4.4 正式版 — 2026-10-23。** [下载 v2.4.4](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.4)。1.7 运行元数据对齐 2.4.4，1.6／Common 冻结于 2.4.1；此前开发登记与分批测试保留原时点，发布结果见版本验证。

面向 RimWorld 的成人向 RimJobWorld（RJW）扩展，基于上游 **2.2.8** 继续维护，提供角色培养、主从绑定、六种特化、人格转移和身体改造。

[2.4.4 开发范围](Docs/Releases/2.4.4/2.4.4发布说明.md) · [正式下载 2.4.4](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.4) · [双语模组简介](Docs/模组介绍.md) · [玩家阅读入口](Docs/Players/README.md) · [文档总索引](Docs/README.md) · [English](#english-overview)

## 2.4.4 新增内容（仅 1.7）

| 主题 | 当前交付 | 详细说明 |
| --- | --- | --- |
| SSC 文化拓展 | 主奴秩序模因、六组 28 戒律、心情／社交／领袖／数量／性别反馈，七张图标、四语、命名与动态背景 | [基本完成核对](Docs/Development/Completed/SSC文化拓展基本开发完成核对.md) |
| 仪式演化点 | 默认关闭的全局开关，原版条件／曲线／上限，整场一次性结算 | [实现与验收](Docs/Development/Completed/仪式演化点开关实现与测试.md) |
| 医美手术与塑貌仪式 | Beauty 单档提升至 +2，工业手术／主奴秩序仪式、共用恢复、原版供料、紫雾与预设名 | [完成核对](Docs/Development/Completed/纳米催情液医美应用完成核对.md) |
| 人格排泄修复 | 固定动作空解析时继续既有空接触流程，原报告组合已实机确认 | [修复与验收](Docs/Development/Completed/人格排泄固定动作空解析修复与测试.md) |

2.4.3 已发布的关系概览、低科技、三代九把 PNA 武器与恩赐剂量继续保留，详见[历史发布范围](Docs/Releases/2.4.3/2.4.3发布说明.md)。**1.7 旧简易 PNA 发射器及相关定义已在 2.4.3 移除，不提供引用旧定义的存档迁移。**

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

正式游玩从 [v2.4.3 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.3) 下载 ZIP 与 SHA-256。退出游戏，移出旧文件夹后完整解压新文件夹，保留加载配置及两套版本目录。该下载的功能范围见[2.4.3 发布说明](Docs/Releases/2.4.3/2.4.3发布说明.md)，历史说明与附件保留各自版本。

宠物兔选择入口未开放，旧 RimTalk 对接暂停。受训目标须为 SSC 性奴身份，原版奴隶身份不能替代。

## 文档导航

| 用途 | 阅读顺序 |
| --- | --- |
| 了解与游玩 | [模组介绍](Docs/模组介绍.md) → [玩家阅读入口](Docs/Players/README.md) → 快速入门 → 完整机制 |
| 查看版本变化 | [2.4.4 说明](Docs/Releases/2.4.4/2.4.4发布说明.md) → [提交归并核对](Docs/Releases/2.4.4/2.4.4开发归并核对.md) → [验证边界](Docs/Releases/2.4.4/2.4.4版本验证.md) |
| 接手开发 | [已交付记录](Docs/Development/Completed/README.md) → [专项待验收](Docs/Development/Verification/README.md) → [当前规划](Docs/Design/README.md) |
| 编译、测试、打包 | [维护入口](Docs/Maintenance/README.md) → [测试说明](Tests/README.md) → [发布与打包](Docs/Maintenance/发布与打包.md) |
| 美术与历史依据 | [资源索引](Docs/Art/README.md) · [设计归档](Docs/Design/Archive/README.md) · [开发日志](Docs/Development/开发更新记录.md) |

1.7 工程为 `Sexslavecraft/SexSlaveCraft_1.7.csproj`（.NET 10），输出到 `1.7/Assemblies/`。1.6 成品按哈希冻结，不能用当前源码回归冒充旧版验证。文档新增与移动遵循[维护约定](Docs/Maintenance/文档维护约定.md)。

问题反馈请附模组／游戏版本、加载顺序、复现步骤、日志及新旧档情况。项目署名：Someone、Hajimi、TieJin。

## English overview

SexSlaveCraft is an adult RJW expansion for pawn development, bonds, six specializations, personality transfer and body modification. **Current release: 2.4.4 (2026-10-23).** [Download v2.4.4](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.4). New work targets 1.7; the 1.6/Common baseline stays frozen at 2.4.1.

2.4.4 groups post-v2.4.3 work through `f3e47c9`: Master-slave Order with six groups/28 precepts, cultural feedback/icons/four-language names and dynamic backgrounds; opt-in ritual development points; cosmetic surgery and Reshaping Rituals; and a Personality Excretion interruption fix. The 1.7 binary/About metadata are aligned to 2.4.4. The 2.4.3 bond overview, low-tech route and nine weapons remain available in development; saves referencing the removed old 1.7 launcher still have no migration path.

Load Harmony and RJW before SSC; Binding Rituals and cultural features require Ideology. Start with the [English mod overview](Docs/模组介绍.md#english), [player reading order](Docs/Players/README.md#english-reading-order), [quick start](QUICK_START_EN.md) and [full guide](PLAYER_GUIDE_EN.md). Compare the [2.4.4 development scope](Docs/Releases/2.4.4/2.4.4发布说明.md#english) with the [published 2.4.3 scope](Docs/Releases/2.4.3/2.4.3发布说明.md#english).
