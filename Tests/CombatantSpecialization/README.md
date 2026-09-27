# 普通战斗员回归

在仓库根目录执行：

```powershell
dotnet run --project Tests/CombatantSpecialization/CombatantSpecialization.csproj --configuration Release --property:RestoreConfigFile=Tests/NuGet.Config -- .
```

本套件直接编译生产方向数据、健康对账、战斗员工具、研究检查和界面文字逻辑；游戏健康容器、研究完成状态、旧方向维护器和翻译使用最小边界模型。已登记到 `Scripts/Test-All.ps1`。

覆盖 17 组场景：枚举兼容、研究与身份矩阵、旧方向入口限制、零进度重复同步、主人对账、重复及陈旧状态、普通完成、即时切换、明确留空、异常浮点值、保存字段重建、加载时序、完成显示、真实 XML 的阶段边界与全部属性、研究及四语资源。

保存字段通过 JSON 边界模型重建，再执行生产规范化和健康对账；这不是 RimWorld Scribe 的实机存读档。阶段验证读取发布用 XML，不模拟实际伤害、命中曲线或 Unity 布局。仍须在游戏中验证存读档、窗口布局及实际属性计算。
