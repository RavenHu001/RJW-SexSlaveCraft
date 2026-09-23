# 仪式选人性能与提交边界回归

使用真实 Harmony 补丁和生产角色、资格、选人代码；游戏 UI、地图/RJW 查询使用依据 RimWorld 1.6 调用顺序编写的计数模型。完整限制规则另由 TrainerIdentity、RestrictionCore 和 InteractionProtection 覆盖。本套件证明调用边界与次数，不测量实际游戏帧时间。

```powershell
dotnet run --project Tests/RitualSelection/RitualSelection.csproj --configuration Release `
  -p:HarmonyAssemblyPath='C:\Dependencies\Harmony\net9.0\0Harmony.dll' `
  -p:RestoreConfigFile=Tests/NuGet.Config -- .
```

在仓库根目录执行，最后的 `.` 用于四语 XML 检查。也可直接运行 `Scripts/Test-All.ps1`，该套件已纳入统一验证。需要 .NET 9 运行时与 net9.0 Harmony，不能使用游戏的 .NET Framework Harmony。

覆盖：

- 20、100、300、1000 人开窗、自动填充与多次高亮；SSC 完整身体、配对、可达性查询和诊断报告均为零，原版观众保留。
- 单次资格查询最多调用一次 RJW；设置门槛、空目标、布尔入口及显式诊断兼容。
- 两种选人顺序、替换、两角色交换、移至空槽位、取消及观众分配。
- 失败与异常恢复原角色、观众及候选顺序；交换第二步被原版拒绝时恢复品质预览。
- 右键菜单在后续事件才执行时重新验证；普通菜单和其他泛型角色控件不受影响。
- 开始前的身体、权限、指派、倒地及可达性变化；直接提交/启动、强制角色、取消重开和异常作用域清理。
- 运行中仪式仍完整验证；非 SSC 仪式保留自动填充；窗口提示不修改调用方共享列表。

实机还需用大人口存档检查开窗及悬停耗时、点击/拖拽/右键选人、实际开始与中途取消，并复测常用 UI/仪式兼容模组组合。
