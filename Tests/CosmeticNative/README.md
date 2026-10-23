# 纳米催情液医美原生专项

面向本机 RimWorld **1.7.4985 rev1269**（MVID `d368493b52bd4740bc7507c1b0386895`）。测试通过 ProjectReference 构建当前生产工程，使用实际游戏类型和生产 XML；不启动游戏，不修改用户配置或安装副本。

统一入口已登记 `CosmeticNative`：

```powershell
pwsh -File Scripts/Test-All.ps1 -GameVersion 1.7 -HarmonyAssemblyPath '<net9.0/0Harmony.dll>'
```

单独构建 `CosmeticNative.csproj` 时传入 `GameRoot`、`HarmonyAssemblyPath` 和 `BUILD-INFO.json` 中的三个生产依赖路径；使用 .NET 10 SDK。构建后执行：

```powershell
dotnet Tests/CosmeticNative/bin/Release/net10.0/CosmeticNative.dll '<repositoryRoot>' '<gameRoot>'
```

38 项覆盖原版 XML 继承／交叉引用、Beauty 四步升级与保守兼容、原版 TraitSet 增删、手术／仪式身份、医术门槛、共用反应三阶段和四种自然恢复、原版权重概率、失效固定失败、跨堆预检／消费、缺料后的原版取消防重、DLC 门控及四语键／参数。

无界面宿主隔离了图形、通知、完整地图寻路／活动范围／禁止／可预留判定和 Lord 移除的地图／信号投递；上述判定的生产调用保留为原版 API，测试只注入边界结果。材料仍执行实际地面列表、足量判断、`SplitOff`／`Destroy`；取消执行原版 `ApplyOutcome` 的 `ended`／`cancelled` 及 `Lord.Cleanup`，通过实际 `Transition.Execute`／`GotoToil` 与测试用职责探针验证 postAction 清理后不再分配职责。技能禁用例通过真实 `SkillDef.IsDisabled` 验证，不替换 `TotallyDisabled`。

这不覆盖游戏内清单刷新、真实医疗麻醉／政策／扣药、完整仪式 AI 站位、地图预留与搬运竞争、整场存读档。实机清单见[实施与验收](../../Docs/Development/Verification/纳米催情液医美实现与测试.md)。
