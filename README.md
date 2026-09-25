# RJW-SexSlaveCraft · TieJin Modify

RimWorld 1.6 的 RimJobWorld（RJW）扩展模组，基于上游 **2.2.8** 继续维护。此仓库包含模组源码、游戏资源、回归测试及中英文文档，当前仓库版本为 **2.3.1**。

[下载安装包](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.1) · [更新日志](CHANGELOG.md) · [文档索引](Docs/README.md) · [English quick start](QUICK_START_EN.md)

## 项目概况

SexSlaveCraft（SSC）围绕角色培养、身份与绑定关系、特化发展、人格转移及身体改造提供一套玩法系统。本接续版在原有内容基础上维护游戏兼容性、修复问题，并完善交互界面与本地化。

2.3.1 的主要更新包括：

- 实际绑定后生效的六项个体行为许可，主人发起在统一入口直接允许。
- 装备、特化与个人配置统一解析；普通行为、调教、仪式及已支持事件/兼容入口统一接入。
- 旧档自动迁移，指定非主人调教员时同步授权调教。
- 右侧勾叉规则栏、强制项锁定、默认配置子窗口及批量覆盖警示。

完整变更与验证记录见 [2.3.1 发布说明](Docs/Releases/2.3.1/2.3.1发布说明.md) 与 [版本验证](Docs/Releases/2.3.1/2.3.1版本验证.md)。

2.3.1 发布后的开发更新已归入 **2.3.2（待发布）**：包含训导官特化与属性加成、仪式选人优化、Education 兼容，以及连续调教、预约和界面修复。详见 [2.3.2 中英更新说明](Docs/Releases/2.3.2/2.3.2发布说明.md)及[整理与验证状态](Docs/Releases/2.3.2/2.3.2版本验证.md)。本次仅整理文档，这些更新尚未包含在上方的 2.3.1 安装包中。

## 安装

| 项目 | 要求 |
| --- | --- |
| 游戏版本 | RimWorld 1.6 |
| 必需模组 | Harmony、RimJobWorld（RJW） |
| 加载顺序 | Harmony、RimWorld 本体及 RJW 位于本模组之前 |
| 绑定仪式 | 需要可用的 Ideology 仪式系统 |

1. 从 [v2.3.1 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.1) 下载模组安装 ZIP；同页提供 SHA-256 校验文件。
2. 退出游戏，将 ZIP 内的 `RJW-SexSlaveCraft-TieJin-Modify` 文件夹解压到 `RimWorld/Mods/`。
3. 更新已有安装时，先将旧模组文件夹移出 `Mods`，再放入新版本，以免残留已删除的文件。
4. 在游戏中启用依赖与本模组，按上述顺序加载。

安装包包含已编译的模组 DLL，无需自行编译；游戏与第三方模组需要另行安装。开始游玩请阅读 [中文快速入门](读我，玩法介绍.md) 或 [English quick start](QUICK_START_EN.md)。

## 文档导航

| 文档 | 内容 |
| --- | --- |
| [中文快速入门](读我，玩法介绍.md) | 基本操作、玩法流程与常见问题 |
| [中文机制详解](机制详解.md) | 系统条件、公式和数值 |
| [English quick start](QUICK_START_EN.md) | English gameplay introduction |
| [English full guide](PLAYER_GUIDE_EN.md) | Detailed mechanics and reference |
| [更新日志](CHANGELOG.md) | 中英双语版本变更 |
| [文档索引](Docs/README.md) | 开发记录、设计规划、版本验证及测试说明 |
| [发布与打包](Docs/Maintenance/发布与打包.md) | 编译环境、依赖配置、回归检查与安装包生成 |
| [未来开发规划](Docs/Design/未来内容开发规划.md) | 后续需求与尚未实现的设计 |

当前旧 RimTalk 兼容处于暂停状态，旧实现保存在 `Archive/RimTalk/`；宠物猫、宠物兔的未完成入口仍禁用。其他兼容说明及具体玩法限制见玩家指南。

## 开发与验证

主工程为 `Sexslavecraft/SexSlaveCraft_Alpha.csproj`，目标框架为 **.NET Framework 4.7.2**。源码编译需要 Visual Studio MSBuild、对应开发组件，以及本机 RimWorld 和模组依赖程序集。工程引用需按本机环境配置，具体方式见 [发布与打包](Docs/Maintenance/发布与打包.md)。

统一验证需要 PowerShell 7、.NET SDK 9（或更新 SDK）、.NET 9 运行时，以及适用于 net9.0 的本地 Harmony DLL。先设置路径（示例路径需替换为本机位置），再运行全部 17 套回归：

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

This repository continues RJW-SexSlaveCraft from upstream **2.2.8** for **RimWorld 1.6**. The repository version is **2.3.1**. Harmony and RimJobWorld are required and must load before this mod.

Download the installation ZIP from the [v2.3.1 release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.1), then extract its mod folder into `RimWorld/Mods/`. When updating, move the old mod folder out before installing the replacement. The package includes the compiled mod DLL.

For gameplay, read the [English quick start](QUICK_START_EN.md) or [full guide](PLAYER_GUIDE_EN.md). See the [bilingual changelog](CHANGELOG.md) for release history. Legacy RimTalk integration remains suspended, and unfinished Pet Cat and Pet Rabbit choices remain disabled.
