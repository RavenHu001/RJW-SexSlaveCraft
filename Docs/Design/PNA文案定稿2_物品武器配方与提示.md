# PNA 文案定稿 二 物品、武器、配方、研究与提示

> 当前完成状态（2026-10-16）：定稿、四语 XML、后续调试旧称及心情标签已落地并获分批实机确认，原 2.4.0.5 开发成果归入 **2.4.1**。下文保留交接与编写历史，不作为待执行任务；见[完成核对](../Development/Completed/PNA重置与玩法补全完成核对.md)及[本版说明](../Releases/2.4.1/2.4.1发布说明.md)。

配套[交接文档](PNA文案重写交接.md)与[定稿一](PNA文案定稿1_药效与成瘾.md)。本稿覆盖余下全部玩家可见文本：分泌状态、需求、化学品、两种药液、发射器与弹丸、伤害名、两份配方、三项研究、短提示与调试反馈。

术语按定稿一第 1 节执行。数值名“负荷”不再出现在玩家可见文本中；界面统一改用**作用强度**，技术文本（调试、日志、机制资料）继续用负荷。

## 1 分泌状态

`SSC_Purple_HyperLactation`（[HediffOfTrain.xml](../../1.7/Defs/HediffDefs/HediffOfTrain.xml)）

- label：**纳米催情液分泌**
- description：
  > 全身腺体在紫色微械的作用下持续生成纳米催情液，并把它送往体表与体液之中。此人性交时的分泌物会让伴侣一同少量摄入。

要点：档名写明分泌的是纳米催情液，且不再点明部位，因为实际供给来自全身腺体；明确“性交过程中”而不绑定任何姿势或插入行为。

## 2 需求

`Need_PNA`（[NeedDefs.xml](../../1.7/Defs/NeedDefs/NeedDefs.xml)）

- label：**纳米催情液渴求**
- description：
  > 依赖纳米催情液带来的刺激之后，身体会持续索要它。满足度随时间下降，见底即进入戒断；取得纳米催情液可以补回，但也让依赖更深。

要点：让玩家看懂“条越满＝越得到满足”。删去“身体负荷”与身体归属句。

## 3 化学品

`PNA_Chemical`（[ChemicalDef.xml](../../1.7/Defs/ChemicalDefs/ChemicalDef.xml)）

- label：**紫色纳米催情液**

要点：化学品名与物品正式名对齐，不再是“紫色纳米流体”。

## 4 两种药液

`Purple_Aphrodisiac_Nanofluid`（[Nanofluid.xml](../../1.7/Defs/ThingsDefs/Nanofluid.xml)）

- label：**紫色纳米催情液**
- description：
  > 悬浮着活性紫色微械的紫色液体，能强烈唤起情欲。它也是配制浓缩品的原料。

要点：删除“可用来制造有强效强化剂的盔甲”——该用途没有对应配方，属开发期承诺。

`Purple_Aphrodisiac_Nanofluid_Plus`

- label：**浓缩紫色纳米催情液**
- description：
  > 经减压蒸去多余液体制成的浓缩品，单位体积内的紫色微械更多，作用也更强、更快。对普通人而言，过量摄入足以让人在连续高潮中失去意识。

要点：交代浓缩工艺与强度差别，并保留关键风险；替换原有口语化占位句。

## 5 发射器与弹丸

`Gun_PNA_Low`（[SSC_Gun.xml](../../1.7/Defs/ThingsDefs/SSC_Gun.xml)）

- label：**PNA 发射器**
- description：
  > 用气压把纳米催情液弹丸射向目标。普通人被连续命中会失去意识；身体已经与紫色微械共存的性奴则会被活化，同时伤口自行收拢。对主人无效。

`Bullet_PNA_Low`

- label：**PNA 弹丸**

`Damage_PNA_Damage_LowTier`（[DemageDef_Gun.xml](../../1.7/Defs/DemageDefs/DemageDef_Gun.xml)）

- label：**低强度 PNA 作用**

要点：武器、弹丸、伤害三处不再都读作“纳米机械子弹”；症状按分流结果陈述，不再复述分支判断。

## 6 两份配方

[RecipeDef_PNA_PLUS.xml](../../1.7/Defs/RecipeDefs/RecipeDef_PNA_PLUS.xml)，DefName 保持不变。

`SSC_Make_SuperDrug`

- label：**浓缩纳米催情液**
- description：**把 5 份纳米催情液减压蒸去多余液体，得到 1 份浓缩品。**
- jobString：**正在浓缩纳米催情液。**

`SSC_Make_SuperDrug_forth`

- label：**浓缩纳米催情液 ×4**
- description：**把 20 份纳米催情液减压蒸去多余液体，一次得到 4 份浓缩品。**
- jobString：**正在批量浓缩纳米催情液。**

要点：统一用正式名；不再出现“纳米机械液体”“浓缩PNA。”“super drug”“第四个”。

## 7 三项研究与一项顺带修正

[ResearchProjectDef.xml](../../1.7/Defs/ResearchDefs/ResearchProjectDef.xml)

`SSC_RES_PNASimpleApp`

- label：**PNA 简易应用**
- description：**掌握把纳米催情液用于外科的初步手法，可在手术中注射纳米催情液，用于改造与调教。**

`SSC_RES_PNASimpleLauncher`

- label：**PNA 简易发射器**
- description：**把纳米催情液做成可发射的弹丸。低成本、低强度，适合在近处压制或活化目标。**（原无描述，新增）

`SSC_RES_PNAConcentration`

- label：**PNA 浓缩技术**
- description：**用减压蒸发提高单位体积内的紫色微械含量，得到作用更强、更快的浓缩纳米催情液。**（原无描述，新增）

`SSC_RES_Combatant`（§4 清单外，因描述写实呈现 PNA 改造而顺带统一用词）

- description 中“引导紫色纳米机械液进一步改造……”保留原意，仅替换物名与“活化体内微械”为“活化体内紫色微械”。

## 8 短提示与调试反馈

四语 `Keyed/SSC_Pna.xml`。键名与占位符不变。

| 键 | 新文本 | 约束 |
| --- | --- | --- |
| `SSC_Pna_LoadTooltip` | 纳米催情液侵蚀：{0}% | 保留百分号，不换算，不再复述效果 |
| `SSC_Pna_RootedLoadTooltip` | 纳米催情液活化：{0}% | 同上 |
| `SSC_Pna_NewDependence` | {0} 已经离不开纳米催情液了。 | `{0}` 为角色简称，中文语序需通顺 |
| `SSC_Pna_WeaponHealed` | 伤口收拢 | 飘字，短 |
| `SSC_Pna_WeaponActivated` | 活化 | 飘字，短 |
| `SSC_Pna_AttractionExplanation` | 渴求对方分泌的纳米催情液：×{0} ⇒ {1} | `{0}` 为本项乘数、`{1}` 为运算后 factor，不是概率或总吸引值 |

## 9 调试文本（保留技术表达，仅清旧称）

四语 `Keyed/SSC_Pna.xml` 与 [DebugActions_SSCPna.cs](../../Sexslavecraft/common/DebugActions_SSCPna.cs)。这部分不需要沉浸化，只把过期的 P1／P2 换成当前分档说法。

| 位置 | 新文本 |
| --- | --- |
| `SSC_Pna_Rooted` | 调试：性奴活化负荷 {0}%，不建立普通依赖。 |
| `SSC_Pna_DebugResult` | 调试：当前负荷 {0}%。普通身体作用达满时形成依赖，已有恢复进度不重置。 |
| C# `AddOrdinary` | `PNA: add ordinary dose (+8%)` |
| C# `AddTwenty` | `PNA: add 20% test dose` |
| C# `AddSeventy` | `PNA: add 70% test dose` |
| C# `Fill` | `PNA: fill severity (can create dependence)` |
| C# `Clear` | `PNA: clear severity` |

C# 五处须重新编译；XML 八处（四语 × 两键）可即时生效。

## 10 需整句删除的身体归属声明

除定稿一第 9 节列出的位置外，另有两处：

- `HediffOfTrain.xml`：`SSC_Purple_HyperLactation`
- `SSC_HediffDefs_PetSpecializations.xml`：`SSC_Hediff_RabbitCloneLowPNA`（废案条目，仅随手删除该句，不改写其余内容）

## 11 四语覆盖与落地顺序

1. 先按本稿与定稿一改中文源 Def。
2. 再改四语语言层：简中 5 个、繁中 13 个、英文 13 个、俄文 12 个（俄文缺 `ThoughtDef/HediffOfAddiction.xml`，如要补齐属新增，不因对称强求）。
3. 简中需**新增** `Hediff_PNA_Consciousness_Record` 词条。
4. 最后同步 [Sexslavecraft/Languages](../../Sexslavecraft/Languages) 镜像（326 个 XML 逐文件一致）。
5. 重编译 DLL 以生效 C# 五处调试名。

## 12 替换后将成为历史说法的文档

以下文档引用了旧档名（微量／活跃／侵蚀／深度影响／末期过载、微弱活化／稳定活化／深度活化／强烈活化／极限活化），重写后属历史记录，本次不回写：

- [PNA机制重置开发规划.md](PNA机制重置开发规划.md)
- [PNA玩法效果与性交补给补全分析.md](PNA玩法效果与性交补给补全分析.md)
- [开发更新记录.md](../Development/开发更新记录.md)、[CHANGELOG.md](../../CHANGELOG.md)
