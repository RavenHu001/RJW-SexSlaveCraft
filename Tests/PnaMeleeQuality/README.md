# PNA 武器原版品质回归

2026-10-19 维护者决定全部武器恢复原版品质规则。当前 **103/103**，直接调用本机 1.7 的 `StatPart_Quality`、`StatWorker.FinalizeValue`、`VerbProperties`、信息卡穿甲入口和 `ProjectileProperties`，不加载已废弃的品质适配。

覆盖三近战×七档品质的伤害与穿甲、抽象／实物请求、外部倍率、零穿甲与自动穿甲、信息卡加权数值与详细说明；六远程×七档品质的弹丸伤害／穿甲／精度及普通防身伤害。另验证废案源码有用途说明，活动源码、XML 标记、生产和测试编译入口均已清理。信息卡检查调用真实说明入口，不启动完整游戏 UI 或地图；属性基础输入／缓存仍由边界提供。

旧套件 104 项中的“品质例外空请求放行”已随补丁撤下，不再验证废弃接口；其余断言改为原版品质预期。历史批次计数和 SHA 保留在[旧适配记录](../../Docs/Development/Completed/SSC近战品质穿甲修正与远程品质核对.md)，当前构建、同步及待实机范围见[恢复记录](../../Docs/Development/Completed/SSC武器恢复原版品质规则与测试.md)。废案源码见[归档说明](../../Archive/PnaMeleeQuality/README.md)。

已登记进 [Test-All](../../Scripts/Test-All.ps1)。该项目需要 .NET 10 SDK 与当前游戏程序集，其他边界宿主继续使用 .NET 9。统一入口可以从 `1.7/BUILD-INFO.json` 推导游戏目录、从 `.builds/version-layout/sdk/dotnet/dotnet.exe` 找本机 SDK；也可传 `-NativeGameRoot`／`-NativeDotnetPath`。

单独运行：

```powershell
& '<.NET 10 SDK 的 dotnet.exe>' run --project Tests/PnaMeleeQuality/PnaMeleeQuality.csproj -c Release `
  -p:GameRoot='<本机 RimWorld 1.7 目录>' `
  -p:HarmonyAssemblyPath='<测试用 net9.0 0Harmony.dll>' -- '<仓库目录>' '<本机 RimWorld 1.7 目录>'
```

完整证据与游戏验收清单见[工业步骤一](../../Docs/Development/Completed/SSC工业武器步骤一实现与测试.md)。
