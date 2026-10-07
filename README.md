# RJW-SexSlaveCraft · TieJin Modify

RimWorld 1.6 的 RimJobWorld（RJW）扩展模组，基于上游 **2.2.8** 继续维护。此仓库包含模组源码、游戏资源、回归测试及中英文文档，当前仓库版本为 **2.3.7**。

[2.3.7 更新说明](Docs/Releases/2.3.7/2.3.7发布说明.md) · [下载安装包](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.7) · [更新日志](CHANGELOG.md) · [文档索引](Docs/README.md) · [English quick start](QUICK_START_EN.md)

2.3.7 已正式发布，汇总 2.3.6 发布后的猫狗特化与宠物互动开发，功能基线为 `a24562e`，并包含版本归并与发布文档。安装 ZIP 与 SHA-256 校验文件见 [v2.3.7 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.7)。历史 2.3.6 ZIP 不包含本轮猫狗扩展。正式发布门禁 24 套、1289/1289 项通过，来源与验收范围见[2.3.7 版本验证](Docs/Releases/2.3.7/2.3.7版本验证.md)。

2.3.7 包含猫狗普通培养、宠物互斥、凝胶终极化及主动技能：终极猫使用“安抚共鸣”，终极狗使用“定向驯导”。附近空闲的宠物会接近实际绑定主人，完成两秒可见亲昵；当前普通猫额外获得 1 个百分点，完成后冷却一天。成年猫狗在双方自愿条件与许可允许时，以 20% 概率向实际主人发起后续互动；该后续已实现，待实机验收。猫狗进度区分普通完成与终极成果，普通完成后悬停可查看凝胶终极化步骤。兔入口仍禁用。

## 项目概况

SexSlaveCraft（SSC）围绕角色培养、身份与绑定关系、特化发展、人格转移及身体改造提供一套玩法系统。本接续版在原有内容基础上维护游戏兼容性、修复问题，并完善交互界面与本地化。

2.3.7 相对 2.3.6 的更新：

- 开放猫普通培养及亲昵特色经验，统一猫狗兔互斥、普通历史进度、跨方向终极效果与加工消耗前保护。
- 终极猫以“安抚共鸣”恢复玩家阵营目标当前精神状态或给予半天鼓舞；终极狗以“定向驯导”直接驯服野生动物或完整完成己方动物一个有效勾选训练项目。两项技能支持全地图选取、同格两秒读条、目标提示与特效，各自冷却一天，并保留人格迁移中的冷却。
- 狗普通培养按实际动物训练互动结算；宠物亲昵增加可见接近、读条、爱心和任务保护，成年猫狗具备固定 20% 自愿后续。猫狗培养显示和四语凝胶终极化引导同步更新。

本版继承 2.3.6 的统一日常调教／完整绑定仪式基础特化经验，以及 2.3.5 的自我调教和任务交接修复；这些既有功能不重复计为本轮新增。

完整变更见 [2.3.7 中英发布说明](Docs/Releases/2.3.7/2.3.7发布说明.md)，构建与验证见[版本验证](Docs/Releases/2.3.7/2.3.7版本验证.md)。历史统一经验及自我调教说明分别见 [2.3.6](Docs/Releases/2.3.6/2.3.6发布说明.md)、[2.3.5](Docs/Releases/2.3.5/2.3.5发布说明.md)。

## 安装

| 项目 | 要求 |
| --- | --- |
| 游戏版本 | RimWorld 1.6 |
| 必需模组 | Harmony、RimJobWorld（RJW） |
| 加载顺序 | Harmony、RimWorld 本体及 RJW 位于本模组之前 |
| 绑定仪式 | 需要可用的 Ideology 仪式系统 |

1. 从 [v2.3.7 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.7) 下载 2.3.7 安装 ZIP 和 SHA-256 校验文件。
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

当前旧 RimTalk 兼容处于暂停状态，旧实现保存在 `Archive/RimTalk/`；2.3.7 的猫、狗可正常培养，宠物兔选择入口仍禁用。其他兼容说明及具体玩法限制见玩家指南。

## 开发与验证

主工程为 `Sexslavecraft/SexSlaveCraft_Alpha.csproj`，目标框架为 **.NET Framework 4.7.2**。源码编译需要 Visual Studio MSBuild、对应开发组件，以及本机 RimWorld 和模组依赖程序集。工程引用需按本机环境配置，具体方式见 [发布与打包](Docs/Maintenance/发布与打包.md)。

统一验证需要 PowerShell 7、.NET SDK 9（或更新 SDK）、.NET 9 运行时，以及适用于 net9.0 的本地 Harmony DLL。先设置路径（示例路径需替换为本机位置），再运行当前全部 24 套回归：

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

产物输出至根目录 `Releases/`。打包脚本调用同一个统一验证入口，包含 `TrainerIdentity` 与 `SharedBed`；只有全部套件通过才继续打包。脚本只执行本地流程。测试入口见 [文档索引](Docs/README.md#随目录维护的说明)，自动回归的覆盖范围及游戏内验证要求见发布文档。

## 反馈与贡献

问题反馈请提供模组版本、RimWorld 版本、相关模组及加载顺序、复现步骤和错误日志；涉及存档时说明是在新档还是旧档中出现。

提交修改时，请说明影响范围和验证结果。玩家可见的变化记录在 `CHANGELOG.md`，技术说明、规划及发布记录按 [文档维护约定](Docs/README.md#文档维护约定) 分类维护。

项目作者信息见 `About/About.xml`：Someone、Hajimi、TieJin。感谢上游作者及参与修复、翻译和验证的贡献者。

## English overview

This repository continues RJW-SexSlaveCraft from upstream **2.2.8** for **RimWorld 1.6**. The repository version is **2.3.7**. Harmony and RimJobWorld are required and must load before this mod.

Version 2.3.7 consolidates all development after released 2.3.6 through feature commit `a24562e`. It includes Cat/Dog training, pet exclusivity, protected gel finalization, Soothing Resonance for final Cats and Directed Taming for final Dogs. Both abilities use map-wide targeting, a two-second interaction on the target cell and their own one-day cooldowns. Dog work progress follows actual training interactions. Idle pets near their actual master approach for visible affection; a current ordinary Cat gains 1 percentage point, with a one-day affection cooldown. Eligible adult Cats/Dogs have a 20% chance to initiate a consensual follow-up with that master; this follow-up awaits in-game acceptance. Progress distinguishes ordinary completion from finalization and offers gel-processing tooltips. Rabbit selection remains disabled.

Version 2.3.7 is released. Download the installation ZIP and SHA-256 checksum from [v2.3.7 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.7). The publication gate passed all 24 suites and 1289/1289 cases. Historical 2.3.6 packages do not contain the new Cat/Dog features. See the [2.3.7 bilingual notes](Docs/Releases/2.3.7/2.3.7发布说明.md) and [validation record](Docs/Releases/2.3.7/2.3.7版本验证.md). Shared base specialization progress from 2.3.6 and self-training/handoff fixes from 2.3.5 are retained.

For gameplay, read the [English quick start](QUICK_START_EN.md) or [full guide](PLAYER_GUIDE_EN.md). See the [bilingual changelog](CHANGELOG.md) for release history. Legacy RimTalk integration remains suspended. When updating an installation, move the old mod folder out of `RimWorld/Mods/` before installing the replacement.
