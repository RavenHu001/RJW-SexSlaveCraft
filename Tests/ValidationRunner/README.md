# 统一验证脚本的流程回归

验证 `Scripts/Test-All.ps1` 和 `Scripts/New-Release.ps1` 的编排及打包阻断行为，**不是额外的游戏机制套件，也不计入 390 项**。

需要 Windows、PowerShell 7、Git，以及两份本地 Harmony：net9.0 和用于拒绝场景的 net472。替换示例路径后运行：

```powershell
pwsh -File Tests/ValidationRunner/Run.ps1 `
  -HarmonyAssemblyPath 'C:\Dependencies\Harmony\net9.0\0Harmony.dll' `
  -FrameworkHarmonyAssemblyPath 'C:\Dependencies\Harmony\net472\0Harmony.dll'
```

脚本在 `.builds/validation-runner/` 创建带空格路径的隔离目录，复制编排脚本与打包所需的最小文件，用临时 `dotnet.cmd` 模拟 SDK 命令及套件输出，从仓库外执行检查。不会修改生产源码、游戏安装目录或全局环境变量；保留场景日志供排查。正常的 390 项验证需另行执行真实 `Test-All.ps1`。

覆盖：全部通过且计数不写死、缺少 Harmony、错用 .NET Framework Harmony、恢复/构建/测试失败、零退出但缺少摘要或未全部通过、缺少 SDK/运行时、未登记项目、失败后继续运行后续套件、失败阻止生成安装包、环境变量传递、打包清单中的验证结果，以及测试依赖不进入安装包。
