# RJW-SexSlaveCraft · TieJin Modify

RimWorld 1.6 的 RimJobWorld（RJW）扩展模组，基于上游 **2.2.8** 继续维护。此仓库包含模组源码、游戏资源、回归测试及中英文文档，当前仓库版本为 **2.4.0**。

[2.4.0 更新说明](Docs/Releases/2.4.0/2.4.0发布说明.md) · [下载安装包](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.0) · [更新日志](CHANGELOG.md) · [文档索引](Docs/README.md) · [English quick start](QUICK_START_EN.md)

2.4.0 归并 2.3.9 之后的主人身份拓展：欲望之种、个人永久资格、首次免费任务、人格流转、欲望共生四阶段成长及主人专属面板。安装与验证见[2.4.0 发布说明](Docs/Releases/2.4.0/2.4.0发布说明.md)和[版本验证](Docs/Releases/2.4.0/2.4.0版本验证.md)。

## 2.4.0：欲望之种与主人成长

- **主人资格**：欲望之种赋予永久资格；默认仍允许直接手动选择主人，关闭该设置后须先取得资格。有 Royalty 时种子同时补齐“调教”研究蓝图，研究仍需正常完成；原版蓝图保留为独立研究来源。
- **道具获取**：满足条件时提供一次免费领取任务，按存档去重；后续种子来自稀有任务与遗迹奖励，商人库存暂不出售。设置内可生成道具或手动创建领取任务。
- **欲望共生**：萌芽 → 觉醒 → 掌控 → 升华，本人完成日常调教 +1 经验、整场绑定仪式 +3，训导官代劳不转记。提供意识、视觉、听觉、社交影响和伤口愈合五项加成。
- **主人面板与人格保存**：面板显示阶段、经验及进度，并操作原缰绳的伤害分担开关。资格与成长随人格凝胶迁移，克隆不继承主人身份、资格或成长。退出主人身份时隐藏成长条目并停用加成，返回后恢复原阶段与经验。
- **文案与资源**：欲望之种独立贴图、四语说明、任务叙事和投放通知，以及精简后的角色与凝胶资格展示。

用法见[中文快速入门](读我，玩法介绍.md#主人资格入口240)和[English quick start](QUICK_START_EN.md#master-qualification-entry-240)。PNA 重置与欲望侵染仍为后续规划；实机验收边界与自动回归结果以版本验证记录为准。

## 项目概况

SexSlaveCraft（SSC）围绕角色培养、身份与绑定关系、特化发展、人格转移及身体改造提供一套玩法系统。本接续版在原有内容基础上维护游戏兼容性、修复问题，并完善交互界面与本地化。

继承 2.3.9 的更新：

- **统一受训身份**：日常自动与手动调教、绑定仪式和调教员指派统一要求目标为 SSC 性奴。原版奴隶身份、关闭行为限制开关和强制命令都不能豁免；候选、实际选入、执行与结算都会复查，并保留各入口原有的身体、许可与通行条件。读档不改写已有身份与成长，不合资格的旧任务停止；未设身份但保留有效历史锁链者可在面板明确恢复身份。
- **训导官职责反馈**：有效任职的训导官正常完成一次日常调教或整场仪式后获得“履行训导职责”+3 心情一天，重复完成只刷新一份记忆；实际主人对其有 +5“受委任的训导官”评价，训导官同时对这位主人获得 +5 反向好感和 +3 常驻心情。后两条是条件性想法而不是记忆，随任职、绑定与资格实时生效或消失，不写记忆列表、不进人格凝胶。
- **训导官弱化记忆套**：训导官作为执行者发放受训者记忆时使用独立弱化套（K=0.5，心情 −5／+1／+4／+8），与主人原套互不覆盖、各自计时；主人与其他合法执行者仍用原套，单次结算只发一套。弱值四舍五入到整数、非零基准保底 1，半值远离零取整。
- **训导官文案重写**：训导官相关心情、评价、弱化记忆与同床文案整体重写，与主人原套同一基调，并全文去除性别指向；补齐简体中文原先缺失的同床条目。

本版保留 2.3.8 的绑定仪式修复与选人提示：

- 修复主持与性奴仍被计入观众的问题；最终观众助兴按本场实际时间和有效出席记录结算，观看至少半场才计入，零人时也显示结果分项。
- 金色鞭子表示主持候选、紫色项圈表示性奴候选，保留已选勾选、不可选斜线与悬停拒绝原因；双资格人物的图标横向排列，四语文案精简。

本版继承 2.3.7 的猫狗内容：

- 开放猫普通培养及亲昵特色经验，统一猫狗兔互斥、普通历史进度、跨方向终极效果与加工消耗前保护。
- 终极猫以“安抚共鸣”恢复玩家阵营目标当前精神状态或给予半天鼓舞；终极狗以“定向驯导”直接驯服野生动物或完整完成己方动物一个有效勾选训练项目。两项技能支持全地图选取、同格两秒读条、目标提示与特效，各自冷却一天，并保留人格迁移中的冷却。
- 狗普通培养按实际动物训练互动结算；宠物亲昵增加可见接近、读条、爱心和任务保护，成年猫狗具备固定 20% 自愿后续。猫狗培养显示和四语凝胶终极化引导同步更新。

本版继承 2.3.6 的统一日常调教／完整绑定仪式基础特化经验，以及 2.3.5 的自我调教和任务交接修复；这些既有功能不重复计为本轮新增。

2.3.9 的完整变更见 [2.3.9 中英发布说明](Docs/Releases/2.3.9/2.3.9发布说明.md)，构建与验证见[版本验证](Docs/Releases/2.3.9/2.3.9版本验证.md)。历史发布说明见 [2.3.8](Docs/Releases/2.3.8/2.3.8发布说明.md)、[2.3.7](Docs/Releases/2.3.7/2.3.7发布说明.md)、[2.3.6](Docs/Releases/2.3.6/2.3.6发布说明.md)、[2.3.5](Docs/Releases/2.3.5/2.3.5发布说明.md)。

## 安装

| 项目 | 要求 |
| --- | --- |
| 游戏版本 | RimWorld 1.6 |
| 必需模组 | Harmony、RimJobWorld（RJW） |
| 加载顺序 | Harmony、RimWorld 本体及 RJW 位于本模组之前 |
| 绑定仪式 | 需要可用的 Ideology 仪式系统 |

1. 从 [v2.4.0 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.0) 下载 2.4.0 安装 ZIP 和 SHA-256 校验文件。
2. 退出游戏，将 ZIP 内的 `RJW-SexSlaveCraft-TieJin-Modify` 文件夹解压到 `RimWorld/Mods/`。
3. 更新已有安装时，先将旧模组文件夹移出 `Mods`，再放入新版本，以免残留已删除的文件。
4. 在游戏中启用依赖与本模组，按上述顺序加载。

安装包包含已编译的模组 DLL，无需自行编译；游戏与第三方模组需要另行安装。开始游玩请阅读 [中文快速入门](读我，玩法介绍.md) 或 [English quick start](QUICK_START_EN.md)。

## 文档导航

| 文档 | 内容 |
| --- | --- |
| [模组介绍 / Mod overview](Docs/模组介绍.md) | 中英双语主题、内容与玩法概述，可用于其他平台介绍 / Bilingual overview for external platforms |
| [中文快速入门](读我，玩法介绍.md) | 基本操作、玩法流程与常见问题 |
| [中文机制详解](机制详解.md) | 系统条件、公式和数值 |
| [English quick start](QUICK_START_EN.md) | English gameplay introduction |
| [English full guide](PLAYER_GUIDE_EN.md) | Detailed mechanics and reference |
| [更新日志](CHANGELOG.md) | 中英双语版本变更 |
| [文档索引](Docs/README.md) | 开发记录、设计规划、版本验证及测试说明 |
| [发布与打包](Docs/Maintenance/发布与打包.md) | 编译环境、依赖配置、回归检查与安装包生成 |
| [未来开发规划](Docs/Design/未来内容开发规划.md) | 后续需求与尚未实现的设计 |

当前旧 RimTalk 兼容处于暂停状态，旧实现保存在 `Archive/RimTalk/`；猫、狗可正常培养，宠物兔选择入口仍禁用。其他兼容说明及具体玩法限制见玩家指南。

## 开发与验证

主工程为 `Sexslavecraft/SexSlaveCraft_Alpha.csproj`，目标框架为 **.NET Framework 4.7.2**。源码编译需要 Visual Studio MSBuild、对应开发组件，以及本机 RimWorld 和模组依赖程序集。工程引用需按本机环境配置，具体方式见 [发布与打包](Docs/Maintenance/发布与打包.md)。

统一验证需要 PowerShell 7、.NET SDK 9（或更新 SDK）、.NET 9 运行时，以及适用于 net9.0 的本地 Harmony DLL。先设置路径（示例路径需替换为本机位置），再运行当前全部 28 套回归（2.3.9 发布时为 25 套）：

```powershell
$env:SSC_TEST_HARMONY_PATH = 'C:\Dependencies\Harmony\net9.0\0Harmony.dll'
pwsh -File Scripts/Test-All.ps1
```

也可直接传入 `-HarmonyAssemblyPath`。各套件的通过、失败或未执行状态，以及逐套件日志和 `summary.json`，保存在 `.builds/validation/` 的本次运行目录。任何失败或未执行都会使整体验证失败；SharedBed 不接受游戏使用的 .NET Framework Harmony。

使用已有 DLL 进行版本校验和本地打包，另外需要 Git。配置好上述 Harmony 路径后运行：

```powershell
pwsh -File Scripts/New-Release.ps1
```

修改 C# 源码后，配置好本机依赖，再使用 `-Build` 重新编译并打包：

```powershell
pwsh -File Scripts/New-Release.ps1 -Build
```

产物输出至根目录 `Releases/`。打包脚本调用同一个统一验证入口，包含 `TrainerIdentity` 与 `SharedBed`；只有全部套件通过才继续打包。脚本只执行本地流程。测试入口见 [测试说明索引](Tests/README.md)，自动回归的覆盖范围及游戏内验证要求见发布文档。

## 反馈与贡献

问题反馈请提供模组版本、RimWorld 版本、相关模组及加载顺序、复现步骤和错误日志；涉及存档时说明是在新档还是旧档中出现。

提交修改时，请说明影响范围和验证结果。玩家可见的变化记录在 `CHANGELOG.md`，技术说明、规划及发布记录按 [文档维护约定](Docs/README.md#文档维护约定) 分类维护。

项目作者信息见 `About/About.xml`：Someone、Hajimi、TieJin。感谢上游作者及参与修复、翻译和验证的贡献者。

## English overview

This repository continues RJW-SexSlaveCraft from upstream **2.2.8** for **RimWorld 1.6**. The repository version is **2.4.0**. Harmony and RimJobWorld are required and must load before this mod.

Version 2.4.0 adds the Seed of Desire, permanent personal Master qualification, a one-time free claim quest, rare quest/ancient ruin rewards and personality transfer. Direct Master selection remains enabled by default. With Royalty, seeds also supply missing Training techprints; the vanilla techprint remains a separate research source.

Symbiosis of Desire grows through personally completed daily Training (+1 experience) and whole Binding Rituals (+3), with four named stages and bonuses to Consciousness, Sight, Hearing, Social Impact and Injury Healing. The Master panel shows growth and hosts the existing damage-sharing toggle. Qualification and growth transfer with Personality Gel; clones inherit neither Master identity nor qualification or growth. Leaving the role hides the growth entry and suspends bonuses, while returning restores the saved stage and experience. Four-language text, item art and delivery notifications are included. Download the ZIP and SHA-256 checksum from [v2.4.0 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.0).

Version 2.3.9 receiver identity rules and Trainer Officer feedback are retained. PNA rework and Desire Infusion remain future plans.

Version 2.3.8 consolidates Binding Ritual fixes and selection hints developed after 2.3.7 through feature baseline `88a8b87`. Both protagonists are excluded from the audience; final spectator quality counts eligible attendance for at least half the ritual's actual elapsed time and reports the factor even at zero. Gold whip and purple collar icons identify host and sex slave candidates, with assignment checks, unavailable slashes and hover reasons.

Version 2.3.7 consolidates all development after released 2.3.6 through feature commit `a24562e`. It includes Cat/Dog training, pet exclusivity, protected gel finalization, Soothing Resonance for final Cats and Directed Taming for final Dogs. Both abilities use map-wide targeting, a two-second interaction on the target cell and their own one-day cooldowns. Dog work progress follows actual training interactions. Idle pets near their actual master approach for visible affection; a current ordinary Cat gains 1 percentage point, with a one-day affection cooldown. Eligible adult Cats/Dogs have a 20% chance to initiate a consensual follow-up with that master; this follow-up awaits in-game acceptance. Progress distinguishes ordinary completion from finalization and offers gel-processing tooltips. Rabbit selection remains disabled.

The publication gate runs all 28 registered suites, including Master qualification, quest entry and panel checks. See the [test index](Tests/README.md), [2.4.0 bilingual notes](Docs/Releases/2.4.0/2.4.0发布说明.md) and [validation record](Docs/Releases/2.4.0/2.4.0版本验证.md) for actual results and in-game acceptance boundaries. Earlier specialization, self-training and ritual improvements are retained.

For gameplay, read the [English quick start](QUICK_START_EN.md) or [full guide](PLAYER_GUIDE_EN.md). See the [bilingual changelog](CHANGELOG.md) for release history. Legacy RimTalk integration remains suspended. When updating an installation, move the old mod folder out of `RimWorld/Mods/` before installing the replacement.
