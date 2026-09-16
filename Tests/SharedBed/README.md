# 主奴同床回归

直接编译生产规则、记忆和界面补丁，用真实 Harmony `PatchAll` 接入最小原版生命周期模型。
模型依据本机 RimWorld 1.6.4871 的床位方法建立，不运行 Unity 或完整游戏。

需要 .NET 9 SDK，以及适用于 **net9.0** 的本地 `0Harmony.dll`（已验证 Harmony 2.4.2）。
不要传入游戏使用的 net472/Unity 版本：它不能用于 .NET 9 运行时。

从仓库根目录执行：

```powershell
dotnet build Tests/SharedBed/SharedBed.csproj --configuration Release --target:Rebuild `
  -p:RestoreConfigFile=Tests/NuGet.Config `
  -p:HarmonyAssemblyPath="本机/net9.0/0Harmony.dll"
dotnet Tests/SharedBed/bin/Release/net9.0/SharedBed.dll .
```

此套件需显式提供 Harmony，独立运行，不改变现有无外部依赖的发布测试入口。
覆盖身份隔离、绑定优先、恋人判定不变、医疗/死眠、原版检查调用、实际 transpiler 执行、
分配标记及提示、共同睡眠结束结算、双方先后起床、记录存档字段和 XML 数值。
同时覆盖 DLL/XML 不配套时缺失的另一种记忆定义、缺失的当前定义和旧版情境心情定义。
记忆删除模型按原版先访问 `def.IsMemory`，避免把传入空定义的错误静默吞掉。

Dubs Mint Menus 行模型覆盖已分配/未分配标签、悬停提示、右侧按钮留白、普通奴隶和其他建筑保持原样，
以及拒绝原因和独立的意识形态说明。生产补丁按类型名反射定位，不引用 Mint 程序集。

存档测试只验证记录的序列化字段契约；界面测试只验证实际补丁的绘制调用、区域和状态恢复。
不能代替真实 Scribe 存档往返、游戏内字体布局或其他模组补丁顺序测试。
