# 新限制系统阶段 1 回归

从仓库根目录运行（.NET 9，无第三方依赖）：

```powershell
dotnet run --project Tests/RestrictionCore/RestrictionCore.csproj --configuration Release `
  -p:RestoreConfigFile="完整仓库路径/Tests/NuGet.Config" -- .
```

套件直接编译生产配置、文件定义、条目解析器、唯一许可入口和两个存档扩展，已加入 `Scripts/Test-All.ps1`。

55 项覆盖主人整次行为直接放行（包括缺配置/未知用途）、方向和双向许可、单人规则、日常/仪式独立条目、首次绑定准备、人格专用任务、公交车两项覆盖、装备优先级、同层冲突稳定性、总开关、模板隔离、只读预览、配置缺失/损坏和序列化往返。加载真实 XML 内容并检查运行目录与源码内嵌副本一致。

游戏对象及 Scribe 使用边界模型，XML 字段由测试读取，不声称代替 Unity 中的原生 XML 加载、Scribe 引用恢复、Harmony 时序或界面操作。旧 `InteractionProtection` 套件继续验证尚未接管的旧运行路径。

阶段 1 不自动创建或迁移角色配置，也不替换实际任务。游戏内只读预览及后续入口清单见[阶段 1 开发记录](../../Docs/Development/新限制系统阶段1.md)。
