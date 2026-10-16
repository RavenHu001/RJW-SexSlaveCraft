# PNA 摄入数量配置修正

> 现行归属（2026-10-16）：**2.4.1，原 2.4.0.5 开发里程碑统一纳入，已纳入正式发布。** PNA 约定范围已获分批实机确认；本页保留该批原日期、参数、提交、测试数和当时的后续任务。`ea448b0`／`2ec5cb6` 是前置 PNA 批次基线，不是当前全部代码；先读[完成核对](../Completed/PNA重置与玩法补全完成核对.md)及[2.4.1 验证](../../Releases/2.4.1/2.4.1版本验证.md)。

日期：2026-10-14。分支：`codex/pna-reset`。基线提交 `680963c` 已确认恢复和再次服药正常；本批仅改 XML 摄入数量及营养值，维护者已确认每次只吃一份、零营养服药正常，批准提交，未发布。

## 原因与证据

维护者确认：普通液，手动服药，一次实际吃了 14 份；要求每次只能吃一份。

两种 PNA 继承 `ResourceBase`，没有原版 `DrugBase` 的数量配置。本机 `RimWorld.IngestibleProperties` 默认 `defaultNumToIngestAtOnce=20`，`maxNumToIngestAtOnce=0`（不设上限）。

原版手动菜单调用 `FoodUtility.WillIngestStackCountOf`，药物直接采用默认份数；任务默认取最多 20 份。`Toils_Ingest.FinalizeIngest` 用实际食物需求计算希望摄入的营养，`Thing.IngestedCalculateAmounts` 按营养需要向上取整并限制在持有堆叠内，但只有 maxNumToIngestAtOnce>0 才进一步限制单次摄入。

因此默认取 20 份并不保证实际吃 20 份，可能按饥饿程度吃 14 份。当前专用 Doer 正确按实际份数计算：普通液 14×20%=280%，钳制到 100%，立即进入末期。这与成瘾是否已有恢复进度无关；成瘾严重度的服药增加与身体负荷剂量是两条独立逻辑。

证据为本机 Assembly-CSharp.dll，MVID `61e4173561894da49d210260257b5097`，上述类型和方法，以及 Core `Defs/Drugs/DrugBases.xml` 和 `Defs/ThingDefs_Items/Items_Resource_Base.xml`。未取得运行时合并 Def 快照，当前启用模组的其他数量补丁仍需实机复测。

## 无营养药物核查

维护者进一步希望去除营养值，要求先确认原版允许无营养药物摄入。已核对本机 Core 的 WakeUp、Penoxycyline、GoJuice 及它们的 DrugBase 继承链，均没有 Nutrition；GoJuice 同样使用 `Processed, Fluid`。Nutrition 的原版 StatDef 默认值为 0，ResourceBase 也不添加营养，因此 PNA 删除显式值后作者定义为零营养。

`ThingDef.IsIngestible` 仅检查 ingestible 不为 null；`Thing.IngestibleNow` 增加燃烧等检查而不要求营养。`RaceProperties.CanEverEat` 检查食性、食物类型与准入，`FoodUtility.DrugIsSuitable` 检查特殊角色药物限制，不要求正营养。原版化学需求找药校验 Chemical、成瘾 Def、可达性、禁用与药物政策，不检查正营养。PNA 的 `preferability=NeverForNutrition` 也符合原版零营养配置校验。

实际服用流程仍执行药物组件及摄入 Doer。`Thing.IngestedCalculateAmounts` 的上限与至少一份夹取配合 maxNumToIngestAtOnce=1，令合法堆叠每次服用一份；输出营养为 0，不增加 food.CurLevel。无需新增零营养 C# 补丁。

依据仍为本机游戏 DLL，补充核对 ThingDef.IsIngestible、Thing.IngestibleNow、RaceProperties.CanEverEat、FoodUtility.DrugIsSuitable、JobGiver_SatisfyChemicalNeed 及 Core `Defs/Stats/Stats_Basics_General.xml`。当前合并 Def 和第三方补丁未代替实机复测。

## 修正与验证

仅对 `Defs/ThingsDefs/Nanofluid.xml` 的普通液和浓缩液各增加：

```xml
<defaultNumToIngestAtOnce>1</defaultNumToIngestAtOnce>
<maxNumToIngestAtOnce>1</maxNumToIngestAtOnce>
```

另外移除两种物品的 `<Nutrition>0.05</Nutrition>`，沿用原版属性默认零营养，服用不补充食物需求。

前者令手动指令默认取一份，后者保证实际一次摄入最多一份；与原版药物基类一致。保留资源继承、除营养外的原物品其他属性、普通／浓缩每份 +20%／+70%、真实份数计算和原版成瘾逻辑，不加新的数量补丁或自动补给安全过滤。重复下达服药指令仍会逐次增加负荷，末期和再次服药造成恢复回退的行为保留。

按维护者约定只核对 XML 格式、两个 Def 的数量字段和既有剂量、四语镜像、生产 C# 与 DLL 均未变化；不重编 DLL、不重新运行代码回归。1932/1932 是基线 `680963c` 的历史代码验证，本批不把它标为新运行结果。395 份 XML 格式检查通过，323 份语言 XML 镜像一致，单份配置／剂量／无显式营养及未变化代码的核对通过；当前检查、测试包及同步报告位于 `.builds/pna-ingest-quantity/`；同步前校验游戏关闭、packageId，备份差异资源且不删除文件或修改存档。

首次本机同步尝试因检测到 RimWorld 正在运行而在任何覆盖前停止。随后检测游戏已关闭，单份与零营养配置检查通过；实际安装同步结果、逐项哈希与覆盖前备份目录以 `.builds/pna-ingest-quantity/sync-result.json` 为准。重启游戏才能加载更新 XML。

## 实机确认与复测范围

2026-10-14 维护者确认每次只吃一份，移除营养值后服药正常，批准更新文档并提交。此前原版恢复和再次服药亦已确认正常。此次反馈不扩大为旅队自动补给、完整存读档或其他尚未逐项反馈的验收。

以下保留进一步核验步骤：

1. 重启游戏加载更新 XML，从一大堆普通液手动服用一次，整堆及角色所持合计应只消耗一份，负荷约增加 20 个百分点（受正常清除和上限影响）。
2. 浓缩液同样只消耗一份，负荷约增加 70 个百分点；已有负荷较高时仍可能合法进入末期。
3. 改变饥饿程度并使用部分恢复的成瘾角色重复检查，单次份数仍为一；再次服药加深已有成瘾保持原版行为。
4. 原版自动补给也应单次一份；库存携带数量不等于实际摄入份数。
5. 饥饿与饱腹角色都能正常服药，服药本身不补充食物需求；化学需求补充、负荷加量与已有成瘾加深仍正常。
