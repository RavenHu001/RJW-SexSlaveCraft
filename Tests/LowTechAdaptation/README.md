# 低科技适配回归

直接链接生产 `SSCPnaConcentrationRecipeUtility.cs`，检查模组工作台接入、已生成缓存、重复注入和缺失定义边界；同时检查实际 1.7 研究树、浓缩配方及四语文本。共 38 项，已纳入 `Scripts/Test-All.ps1`。

```powershell
dotnet run --project Tests/LowTechAdaptation/LowTechAdaptation.csproj --configuration Release -- 'E:\Games\RimWorld\modDevelopment\RJW-SexSlaveCraft-TieJin-Modify'
```

资源通过 `Tests/Shared/TestPaths.cs` 定位，遵循统一入口的环境变量和 1.7 运行目录。1.6 继续只检查冻结资源。

工作台边界模型覆盖：原始 `recipes`、配方反向 `recipeUsers`、工作台派生类、只有批量简易餐、已有缓存、缓存重建、显式粗放配方、重复执行、非烹饪建筑和缺失定义。以原版简易餐 Def 引用识别烹饪能力，不使用显示名或 DefName 片段。

资源检查固定 18 项研究的原点数及全部前置，确认 2 项 Neolithic／15 项 Medieval／1 项 Industrial；粗放配方 10→1、工作量 600、Cooking／CookSpeed、三个原版入口及研究门槛；药物台 5→1／450、20→4／1350 的参数和入口保持。

模型通过不代表 XML 继承、全部第三方启动代码或真实烹饪任务已经运行。2026-10-18 另用本机 1.7.4981 实际程序集、编译后的生产 DLL 和三份 GloomyFurniture 作者 XML 样本完成 13 项独立检查；2026-10-18 维护者已确认低科技研究及粗放浓缩正常，首批验收关闭；历史证据、反馈范围与原实机步骤见[实现与测试记录](../../Docs/Development/Completed/低科技适配首批实现与测试.md)。
