# PNA 近战品质伤害回归

直接链接生产 [品质补丁](../../Sexslavecraft/ThePatches/Harmony_SSCPnaMeleeQuality.cs)，使用本机 1.7 的真实 `StatPart_Quality`、`StatRequest`、`ThingDef` 和测试用 Harmony；不模拟原版品质曲线。当前 **31/31**。

覆盖七种品质、短矛／短鞭标记、抽象／实物请求、其他输入倍率保留、普通近战及射针枪防身品质放行、其他属性的品质修正、空请求与 XML 标记范围。信息卡通过真实说明入口核对，不启动游戏 UI 或完整 Def 加载；维护者已确认本轮实机正常，未逐项报告的场景不补记为专项实测。

已登记进 [Test-All](../../scripts/Test-All.ps1)。该项目需要 .NET 10 SDK 与当前游戏程序集，其他边界宿主继续使用 .NET 9。统一入口可以从 `1.7/BUILD-INFO.json` 推导游戏目录、从 `.builds/version-layout/sdk/dotnet/dotnet.exe` 找本机 SDK；也可传 `-NativeGameRoot`／`-NativeDotnetPath`。

单独运行：

```powershell
& '<.NET 10 SDK 的 dotnet.exe>' run --project Tests/PnaMeleeQuality/PnaMeleeQuality.csproj -c Release `
  -p:GameRoot='<本机 RimWorld 1.7 目录>' `
  -p:HarmonyAssemblyPath='<测试用 net9.0 0Harmony.dll>' -- '<仓库目录>' '<本机 RimWorld 1.7 目录>'
```

完整证据与游戏验收清单见[工业步骤一](../../Docs/Development/Completed/SSC工业武器步骤一实现与测试.md)。
