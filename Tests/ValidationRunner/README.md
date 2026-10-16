# 统一验证脚本的流程回归

验证 `Scripts/Test-All.ps1` 和 `Scripts/New-Release.ps1` 的编排及打包阻断行为，**不是额外的游戏机制套件，也不计入机制回归总数**。

需要 Windows、PowerShell 7、Git，以及两份本地 Harmony：net9.0 和用于拒绝场景的 net472。替换示例路径后运行：

```powershell
pwsh -File Tests/ValidationRunner/Run.ps1 `
  -HarmonyAssemblyPath 'C:\Dependencies\Harmony\net9.0\0Harmony.dll' `
  -FrameworkHarmonyAssemblyPath 'C:\Dependencies\Harmony\net472\0Harmony.dll'
```

脚本在 `.builds/validation-runner/` 创建带空格路径的隔离目录，复制编排脚本与打包所需的最小文件，用临时 `dotnet.cmd` 模拟 SDK 命令及套件输出，从仓库外执行检查。预期套件数从当前测试项目清单读取。不会修改生产源码、游戏安装目录或全局环境变量；保留场景日志供排查。真实机制回归需另行执行 `Test-All.ps1`。

历史开发里程碑曾用四段 `2.4.0.5`，现已统一归入三段 `2.4.1`，正式发布脚本继续要求三段版本和完整元数据一致。夹具只将隔离目录的 VERSION 对齐复制 DLL 的真实产品版本，使编排测试能够执行；正式 2.4.1 已对齐生产元数据；夹具仍以实际 DLL 版本为准，不绕过校验。复制的 AssemblyInfo／About／CHANGELOG 仍须满足该正式版本条件，夹具不改仓库版本或运行产物。

Def 镜像场景使用隔离目录内的小型 XML，验证内容差异、根源文件缺失、相同但损坏的 XML、目录缺失、空镜像集合、检查脚本缺失及根目录独有文件；检查只读、报告独立、失败阻止 ZIP 生成，成功检查写入发布清单。含空格与中文的路径也在夹具内执行。

Languages 镜像场景另外验证内容差异、缺失镜像、镜像独有文件、缺目录、空源集合与缺检查脚本；Def 和 Languages 同时出错时两项均独立记录。统一入口保持只读并阻止打包，显式 `-Sync` 仍能修复缺失／差异文件，遇到镜像独有文件则在复制或删除前拒绝。发布清单分别记录两项检查，机制用例计数保持不变。

覆盖：全部通过且计数不写死、缺少 Harmony、错用 .NET Framework Harmony、恢复/构建/测试失败、零退出但缺少摘要或未全部通过、缺少 SDK/运行时、未登记项目、失败后继续运行后续套件、失败阻止生成安装包、环境变量传递、打包清单中的验证结果，以及测试依赖不进入安装包。

发布文档场景检查 ZIP 内 `Docs` 精确包含模组介绍、玩家入口与当前版发布说明，开发资料、本地备份和历史验证不入包；内联与引用式链接转换为对应版本的仓库 URL，保留中文锚点、空格、尖括号和标题，目录使用 `tree`。随包文件链接、外部 URL 和页内锚点保持原样，源文档哈希不变。
