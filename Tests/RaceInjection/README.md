# 种族分页注入回归

运行：

```powershell
dotnet run --project Tests/RaceInjection/RaceInjection.csproj
```

此项目使用 .NET 9，无外部测试包，直接链接生产文件 `Sexslavecraft/common/ITabAddForRace.cs`。测试先触发真实静态构造、验证启动队列，再使用该队列提供的生产注入委托执行各场景。`GameStubs.cs` 仅提供 Def 数据库、共享分页管理器、启动队列与日志边界，没有另行实现注入逻辑。

八个场景覆盖：启动排队；已解析列表与原有实例、顺序、其他模组分页保留；已有非共享 SSC 分页按类型防重；重复注入时组件与分页不增加；不进入 HAR 式 `ResolveReferences` 覆盖方法；空已解析列表按原始类型顺序恢复共享实例并去重；缺少类型列表的恢复；类人、九莲两个白名单与非目标 Def 的筛选规则。

HAR 式替身的解析方法一旦被调用就计数并抛出异常，用于发现重复解析；它不模拟 HAR 内部实现。上述回归不代替游戏实测，实际保存加载、普通与 HAR 种族的新旧角色分页显示，以及其他模组组合下的行为仍需在 RimWorld 中验证。
