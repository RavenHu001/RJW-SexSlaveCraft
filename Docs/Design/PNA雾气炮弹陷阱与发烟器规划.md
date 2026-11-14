# PNA 雾气炮弹、陷阱与发烟器规划

**记录日期** 2026-11-14。**规划基线** SSC 2.5.0，面向 RimWorld 1.7。当前已确认核心玩法，待实现与实机调参。

[总体规划第 16 项](未来内容开发规划.md#pna-mist) · [当前专项一览](README.md#当前专项规划)

## 炮弹和陷阱

**炮弹和陷阱共用同一个爆炸定义。** 爆炸半径、受波及格判定、剂量与表现对两种载体完全一致。炮弹沿用原版迫击炮的装填和发射流程，在实际爆炸位置结算。陷阱沿用原版 IED 的触发流程，在陷阱所在位置结算。原版烟雾炮弹和烟雾 IED 共用 `Smoke` 爆炸与 `BlindSmoke` 结果，可作为两条触发路径的参考。

- 爆炸时，波及范围内每个有效目标即时增加 **25 个百分点 PNA 负荷**。例如 20% 变为 45%，接近满值时沿用现有负荷上限。
- 同一爆炸对同一目标结算一次。雾气体现在设定、描述和瞬时爆炸表现里，全部剂量在爆炸时结算。
- 共享半径、遮挡细则、制作与研究条件待定。首轮可用半径 5 格、物理伤害为 0 试做，这两个值属于试调方案。

### 原版触发链与接入方案

以下接口根据本机 RimWorld **1.7.5005 rev1082** 的 Core XML 与 `Assembly-CSharp.dll` 核对。游戏程序集 MVID 为 `3523c04c-c298-43ae-9571-20caaef22acc`。本节为实施方案，接入效果待编译与实机验证。

| 载体 | 原版入口 | SSC 接入方案 |
| --- | --- | --- |
| 迫击炮弹 | Core 的 `Shell_Smoke` 继承 `MakeableShellBase`，通过 `MortarShells` 分类入装填列表。原版按 `projectileWhenLoaded` 取得 `Bullet_Shell_Smoke`，由 `Projectile_Explosive.Explode` 在实际落点调用 `GenExplosion.DoExplosion`。 | 新增弹药物品和弹体两个 `ThingDef`，沿用原版装填与发射。弹体的 `projectile.damageDef` 使用 SSC 共用的爆炸定义。 |
| IED 陷阱 | Core 的 `TrapIED_Smoke` 继承 `TrapIEDBase`。`Building_TrapExplosive.SpringSub` 启动 `CompExplosive` 引信，`Detonate` 在陷阱位置调用 `GenExplosion.DoExplosion`。 | 新增陷阱 `ThingDef`，`CompExplosive.explosiveDamageType` 引用同一个爆炸定义。原版烟雾 IED 的 15 tick 引信可作为试做参考。 |

从抽象父定义接入，参照 Core 的 `Defs/ThingDefs_Items/Items_Resource_Shell.xml`、`Defs/ThingDefs_Buildings/Buildings_Security.xml` 和 `Defs/DamageDefs/Damages_Misc.xml`。弹体和陷阱的 `postExplosionGasType` 留空，通过共享 `DamageDef` 配置瞬时紫雾表现。库存中的炮弹是否因受损引爆仍待定，原版将这一行为配置在弹药物品自己的 `CompExplosive` 上。

### 共享范围与一次性结算

新增一个 `DamageDef` 和对应的专用 `DamageWorker`，由两种载体引用。以下为建议实现。

1. **统一入射方向与遮挡。** `Projectile_Explosive.Explode` 会传入弹道方向，而 `CompExplosive.Detonate` 的方向为空。爆心所在格不可越过时，原版会根据弹道方向附加视线约束，导致同一位置的范围可能不同。共用处理器可覆盖六参数版本的 `ExplosionCellsToHit`，为两种载体使用同一套以爆心为起点的原版视线和障碍规则。实心爆心建议只处理爆心格，具体规则待定。
2. **对齐原版参数。** 弹体从 `projectile.explosionRadius` 读取半径，陷阱从 `CompExplosive.explosiveRadius` 读取半径。两处使用相同值，由轻量资源检查核对。弹体的传播速度取自 `DamageDef.expolosionPropagationSpeed`，陷阱取自 `CompExplosive.propagationSpeed`。若以 5 格试做，建议两条路径设为 10，使整圈在爆炸的首个处理 tick 生效。
3. **使用原版逐格入口和去重。** 零伤害爆炸仍会从 `Explosion.AffectCell` 进入 `DamageWorker.ExplosionAffectCell`，在同格遮挡筛选后调用 `ExplosionDamageThing`。专用覆盖方法核对有效 `Pawn` 和 `ignoredThings`，用该次爆炸的 `damagedThings` 去重，再调用公共 PNA 入口。原版 `Explosion` 保存剩余格和已处理目标，跨 tick 或读档继续使用各次爆炸自己的记录。
4. **保持爆发为负荷输入。** 专用覆盖方法直接写入 PNA 负荷，跳过基类的 `TakeDamage` 和伤害战报。原版爆炸仍会发出对象通知，并能移除带 `removedByExplosions` 的临时地形。SSC 已有的爆炸击杀归因补丁及武器伤害分摊继续沿各自入口运行。 [现有爆炸击杀归因](../../Sexslavecraft/ThePatches/Harmony_CombatantExperience.cs) · [PNA 伤害分摊](../../Sexslavecraft/ThePatches/Harmony_SSCPnaDamageShare.cs)

### PNA 来源规则与验证

建议在 `SSCPnaExposureSource` 末尾新增 `Blast`，扩展公共入口的来源校验，并以归一化剂量 `.25f` 调用 `SSCPnaExposureUtility.ApplyExposure`。共用普通负荷、有效锁链正面活化、主人免疫及普通目标满负荷首次成瘾。现有涂覆武器按开放伤口和意识抗性算剂量，`Weapon` 来源还补充 PNA 需求、加深已有成瘾。爆炸来源建议保持固定剂量，并将已有需求补充、已有成瘾基础增量及末期额外 `+0.20` 成瘾增量均设为零。这些成瘾与需求细则待定。 [现有涂覆处理器](../../Sexslavecraft/DamageWorker/DamageWorker_SSCPnaCoated.cs) · [PNA 来源结算](../../Sexslavecraft/common/SSCPnaExposureUtility.cs)

建议把新增的三个 `ThingDef`、共享 `DamageDef` 和 C# 处理器纳入 1.7 的资源与编译入口。复用 [PnaLoad 专项](../../Tests/PnaLoad/README.md) 核对单次剂量、满值与身份分流。实机验证覆盖两种载体的实际爆心、同一空地与墙门布置下的范围一致性、倒地目标、单次去重及瞬时效果。迫击炮另验厚岩顶挡弹，陷阱另验踩踏与受损引信。

## PNA 发烟器

**半径和档位可分别调节。** 半径控制覆盖范围，开关与档位控制是否运行及对单位目标的输入速率。至少提供低档和高档。覆盖预览随所选半径更新。

| 档位 | 已确认的行为 |
| --- | --- |
| 低档 | 适合日常使用。持续暴露时，输入和负荷越高就越快的自然消退共同决定长期平衡点。低于平衡点时逐渐增长，高于平衡点时逐渐下降。 |
| 高档 | 提高单位时间的 PNA 输入，持续暴露时能跨过普通目标的失能门槛并达到满负荷。 |

低档的平衡点由**输入速率和现有清除公式**决定。普通目标在 97% 负荷进入末期，意识上限降至 10%，达到 100% 时按现行规则判定首次成瘾。高档速率须能克服末期自然消退。[负荷数学](../../Sexslavecraft/common/SSCPnaMath.cs) · [五档负荷与成瘾规则](../../机制详解.md#11-pna-物品与武器)

**试调参考。** 低档毛输入若为每游戏日 10 个百分点，按连续输入近似计算，长期平衡点约为 35%。高档若每 60 tick 输入 2.5 个百分点，零负荷的普通目标持续暴露约 40 秒可进入末期，按一倍速计。这些数字用于首轮试调。半径范围与默认值、覆盖边界、耗电和 PNA 耗材方案均待定。

## 实现依据

- 爆炸与持续暴露使用 [PNA 公共入口](../../Sexslavecraft/common/SSCPnaExposureUtility.cs)，复用普通侵蚀、有效锁链活化、主人免疫和身体负荷保存。新来源的需求补充和已有成瘾结算单独定义，尤其要核对持续输入的结算频率。
- 发烟器可复用现有建筑的半径配置、范围预览和房间判定方式。现有三档催情发烟器实际施加 `HumpShroomEffect`，新建筑施加 PNA 身体负荷。[现有发烟器设计](Archive/单格电力催情发烟器开发规划.md#2-驼峰菇药效与暴露语义) · [现有发烟器组件](../../Sexslavecraft/Comps/CompSSCAphrodisiacEmitter.cs)
- 发烟器按经过的有效游戏 Tick 换算暴露量。低档单次输入可能小于约 0.5% 的负荷清除阈值，可将同一目标的小剂量先累计、再入账，并用实测检查长期平衡点。离开覆盖或设备停机后，已入账的负荷继续自然消退。[PNA 负荷组件](../../Sexslavecraft/Hediff/HediffComp_SSCPnaLoad.cs)

## 验收重点

- 炮弹和陷阱分别触发一次，验证实际爆炸位置、共同覆盖边界和每个有效目标的 25 个百分点增量。覆盖同一目标只结算一次，以及接近满负荷时的钳制。
- 调节半径与切换档位，确认范围预览和实际覆盖同步，核对停机、离开覆盖和存读档后的行为。
- 分别让零负荷和高于预期平衡点的普通目标处于低档覆盖，观察双方从各自方向趋近平衡点。高档验证从零持续暴露可进入失能档并达到满负荷。
- 选择代表性普通目标、有效锁链目标和免疫目标，核对公共负荷入口的分流。自动检查与实机反馈按[测试编写与执行准则](../Development/Reference/测试编写与执行准则.md)记录。
