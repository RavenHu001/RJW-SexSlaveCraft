# 统一验证脚本的流程回归

验证 `Scripts/Test-All.ps1` 和 `Scripts/New-Release.ps1` 的编排及打包阻断行为，**不是额外的游戏机制套件，也不计入机制回归总数**。

## 浪漫文案模式隔离专项（2026-10-26）

```powershell
pwsh -NoProfile -File Tests/ValidationRunner/RomanceScope.ps1
```

该入口只需 PowerShell 7，执行真实 `Test-All.ps1` AST 中的环境准备／恢复和输出判定／异常处理区块，以轻量子进程及模拟宿主输出验证，不执行下述完整编排夹具或任何游戏机制套件。覆盖五种调用者环境值的成功／异常恢复、子进程继承、仅文案范围拒绝、动态摘要及已有失败规则；另静态核对发布入口和独立文案选择接线。本次56／56，日志位于 `.builds/romance-gate-isolation/`；详见[完成记录](../../Docs/Development/Completed/浪漫文案专项与总回归门禁隔离修复.md)。它不代表完整编排、发布或游戏验收通过。

## 完整编排夹具

需要 Windows、PowerShell 7、Git，以及两份本地 Harmony：net9.0 和用于拒绝场景的 net472。替换示例路径后运行：

```powershell
pwsh -File Tests/ValidationRunner/Run.ps1 `
  -HarmonyAssemblyPath 'C:\Dependencies\Harmony\net9.0\0Harmony.dll' `
  -FrameworkHarmonyAssemblyPath 'C:\Dependencies\Harmony\net472\0Harmony.dll'
```

脚本在 `.builds/validation-runner/` 创建带空格路径的隔离目录，复制编排脚本、完整的 `1.6/` 冻结基线与 `Common/` 共享贴图，以及 `1.7/` 打包所需的最小资源，用临时 `dotnet.cmd` 模拟 SDK 命令及套件输出，从仓库外执行检查。布局与基线检查使用真实的 `Test-ModLayout.ps1`、`ModLayout.ps1` 及原始基线哈希，不使用替身。预期套件数从当前测试项目清单读取。不会修改生产源码、游戏安装目录或全局环境变量；保留场景日志供排查。真实机制回归需另行执行 `Test-All.ps1 -GameVersion 1.7`。

历史开发里程碑 2.4.0.5 已归入正式 2.4.1；当前正式版本为 **2.4.3**，生产 About／1.7 DLL／源码版本已对齐。夹具仍将隔离 VERSION、AssemblyInfo、About 与 CHANGELOG 对齐复制 DLL 的实际三段产品版本，使编排场景能够执行；不修改仓库版本或绕过真实发布元数据校验。

Def 镜像场景使用隔离目录内的小型 XML，验证内容差异、根源文件缺失、相同但损坏的 XML、目录缺失、空镜像集合、检查脚本缺失及根目录独有文件；检查只读、报告独立、失败阻止 ZIP 生成，成功检查写入发布清单。含空格与中文的路径也在夹具内执行。

开发资源源目录为 `1.7/Defs`、`1.7/Languages`，源码项目镜像仍为 `Sexslavecraft/Defs`、`Sexslavecraft/Languages`；夹具中的“根源文件”指所选版本的运行资源，不表示资源仍存储在仓库根目录。

Languages 镜像场景另外验证内容差异、缺失镜像、镜像独有文件、缺目录、空源集合与缺检查脚本；Def 和 Languages 同时出错时两项均独立记录。统一入口保持只读并阻止打包，显式 `-Sync` 仍能修复缺失／差异文件，遇到镜像独有文件则在复制或删除前拒绝。发布清单分别记录两项检查，机制用例计数保持不变。

覆盖：全部通过且计数不写死、缺少 Harmony、错用 .NET Framework Harmony、恢复/构建/测试失败、零退出但缺少摘要或未全部通过、缺少 SDK/运行时、未登记项目、失败后继续运行后续套件、失败阻止生成安装包、环境变量传递、打包清单中的验证结果，以及测试依赖不进入安装包。

发布文档场景检查 ZIP 内 `Docs` 精确包含模组介绍、玩家入口与当前版发布说明，开发资料、本地备份和历史验证不入包；内联与引用式链接默认转换为 `main` 分支的仓库 URL，保留中文锚点、空格、尖括号和标题，目录使用 `tree`，清单记录 `repositoryRef`。正式发布可通过 `New-Release.ps1 -RepositoryRef v版本号` 选择已有的对应标签。随包文件链接、外部 URL 和页内锚点保持原样，源文档哈希不变。

双版本场景检查冻结的 1.6 资源、共享贴图和 1.7 DLL 来源哈希发生变化时均阻止打包，`LoadFolders.xml` 不能将 1.7 指向 1.6。ZIP 必须包含 `LoadFolders.xml`、`1.6/BASELINE.json`、`1.7/BUILD-INFO.json`，每版只包含一个本模组 DLL；版本目录内的开发文件不随运行目录白名单复制。发布清单分别记录两版 DLL 元数据和布局验证，并明确 .NET 9 回归不等于游戏内 1.7 验证。

2.4.3 发布检查补齐原生套件的隔离 .NET 10 命令入口与游戏路径哨兵，继续由模拟工具返回套件结果，不执行真实游戏代码。缺 Harmony／错误框架场景按当前九个依赖套件检查；进程结束时恢复新增的原生 SDK／游戏路径环境变量。
