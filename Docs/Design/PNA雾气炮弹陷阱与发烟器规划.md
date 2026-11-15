# PNA 雾气炮弹、陷阱与发烟器规划

**记录日期** 2026-11-14。**规划基线** SSC 2.5.0，面向 RimWorld 1.7。炮弹、陷阱与发烟器测试版均通过维护者实机验收。

[总体规划第 16 项](未来内容开发规划.md#pna-mist) · [当前专项一览](README.md#当前专项规划)

## 炮弹和陷阱

**炮弹和陷阱共用同一个爆炸定义。** 爆炸半径、受波及格判定、剂量与表现对两种载体完全一致。炮弹沿用原版迫击炮的装填和发射流程，在实际爆炸位置结算。陷阱沿用原版 IED 的触发流程，在陷阱所在位置结算。原版烟雾炮弹和烟雾 IED 共用 `Smoke` 爆炸与 `BlindSmoke` 结果，可作为两条触发路径的参考。

- 爆炸时，波及范围内每个有效目标即时增加 **25 个百分点 PNA 负荷**。例如 20% 变为 45%，接近满值时沿用现有负荷上限。
- 同一爆炸对同一目标结算一次。雾气体现在设定、描述和瞬时爆炸表现里，全部剂量在爆炸时结算。
- 实测版使用 5 格半径和零物理伤害。爆心落在实心阻挡格时只处理中心格。炮弹与陷阱均由「工业纳米催情液武器」解锁，陷阱另需原版 IED 研究。

### 原版触发链与现行接入

以下接口根据本机 RimWorld **1.7.5005 rev1082** 的 Core XML 与 `Assembly-CSharp.dll` 核对。游戏程序集 MVID 为 `3523c04c-c298-43ae-9571-20caaef22acc`。两种载体已通过本批实机验收。

| 载体 | 原版入口 | SSC 现行接入 |
| --- | --- | --- |
| 迫击炮弹 | Core 的 `Shell_Smoke` 继承 `MakeableShellBase`，通过 `MortarShells` 分类入装填列表。原版按 `projectileWhenLoaded` 取得 `Bullet_Shell_Smoke`，由 `Projectile_Explosive.Explode` 在实际落点调用 `GenExplosion.DoExplosion`。 | 弹药物品与弹体分别由各自的 `ThingDef` 定义，沿用原版装填与发射。弹体的 `projectile.damageDef` 使用 SSC 共用的爆炸定义。 |
| IED 陷阱 | Core 的 `TrapIED_Smoke` 继承 `TrapIEDBase`。`Building_TrapExplosive.SpringSub` 启动 `CompExplosive` 引信，`Detonate` 在陷阱位置调用 `GenExplosion.DoExplosion`。 | 陷阱的 `CompExplosive.explosiveDamageType` 引用同一个爆炸定义，引信为 15 tick。踩踏或符合条件的受损启动引信。 |

两种载体从抽象父定义接入，参照 Core 的 `Defs/ThingDefs_Items/Items_Resource_Shell.xml`、`Defs/ThingDefs_Buildings/Buildings_Security.xml` 和 `Defs/DamageDefs/Damages_Misc.xml`。弹体和陷阱通过共享 `DamageDef` 播放瞬时紫雾，`postExplosionGasType` 留空。库存炮弹作为普通物品处理，起爆由飞行弹体负责。

### 共享范围与一次性结算

两种载体引用同一个 `DamageDef` 和专用 `DamageWorker`。

1. **统一入射方向与遮挡。** `Projectile_Explosive.Explode` 会传入弹道方向，`CompExplosive.Detonate` 的方向为空。共用处理器覆盖六参数版本的 `ExplosionCellsToHit`。可越过的爆心按无方向的原版视线与障碍规则计算，实心阻挡爆心只处理中心格。
2. **对齐原版参数。** `projectile.explosionRadius` 和 `CompExplosive.explosiveRadius` 都设为 5 格。`DamageDef.expolosionPropagationSpeed` 和 `CompExplosive.propagationSpeed` 都设为 10，使整圈在爆炸的首个处理 tick 生效。
3. **使用原版逐格入口和去重。** 零伤害爆炸仍会从 `Explosion.AffectCell` 进入 `DamageWorker.ExplosionAffectCell`，在同格遮挡筛选后调用 `ExplosionDamageThing`。专用覆盖方法核对有效 `Pawn` 和 `ignoredThings`，用该次爆炸的 `damagedThings` 去重，再调用公共 PNA 入口。原版 `Explosion` 保存剩余格和已处理目标，跨 tick 或读档继续使用各次爆炸自己的记录。
4. **保持爆发为负荷输入。** 专用覆盖方法直接写入 PNA 负荷，跳过基类的 `TakeDamage` 和伤害战报。原版爆炸仍会发出对象通知，并能移除带 `removedByExplosions` 的临时地形。SSC 已有的爆炸击杀归因补丁及武器伤害分摊继续沿各自入口运行。 [现有爆炸击杀归因](../../Sexslavecraft/ThePatches/Harmony_CombatantExperience.cs) · [PNA 伤害分摊](../../Sexslavecraft/ThePatches/Harmony_SSCPnaDamageShare.cs)

### PNA 来源规则与验证

处理器以 `Blast` 来源和归一化剂量 `.25f` 调用 `SSCPnaExposureUtility.ApplyExposure`。爆炸复用普通负荷、有效锁链正面活化、主人免疫及普通目标满负荷的首次成瘾判定。已有需求补充、已有成瘾基础增量及末期额外 `+0.20` 成瘾增量在 `Blast` 路径上均为零。 [PNA 来源结算](../../Sexslavecraft/common/SSCPnaExposureUtility.cs)

三个 `ThingDef`、共享 `DamageDef` 和 C# 处理器已纳入 1.7 的资源与编译入口。单次剂量、满值与身份分流可从 [PnaLoad 专项](../../Tests/PnaLoad/README.md) 查阅，实机反馈按下方批次记录。

### 爆炸物第一批测试版

- 2026-11-14 接通 [炮弹与陷阱 Def](../../1.7/Defs/ThingsDefs/SSC_PnaMistExplosives.xml)、[共享爆炸 Def](../../1.7/Defs/DemageDefs/SSC_PnaMistBlast.xml) 和 [爆炸处理器](../../Sexslavecraft/DamageWorker/DamageWorker_SSCPnaBlast.cs)。测试参数为 5 格、传播速度 10、零物理伤害、实心爆心仅处理中心格。IED 引信 15 tick。库存弹药为普通物品，落点和陷阱位置均通过各自的原版入口起爆。
- 炮弹在机械加工台制造，每发消耗 25 钢、2 份浓缩 PNA。陷阱建造消耗 30 钢、4 份浓缩 PNA。两者使用工业 PNA 武器研究，陷阱同时继承原版 IED 研究门槛。雾效来自一次爆炸的紫色 Fleck。
- `Blast` 来源按每目标 +25 个百分点结算。有效锁链目标获得活化，主人免疫，普通目标满值可首次成瘾。已有需求及已有成瘾进度沿用本次输入前的值。
- 自动验证完成 1.7 构建（零警告、零错误）及 PnaLoad 专项 **584／584**。Def、四语语言镜像和共享爆炸接线通过静态检查。
- 2026-11-14 同步 12 份运行文件并逐份核对哈希。维护者确认炮弹和陷阱实机测试无问题，关闭本批实机验收。安装与工作区 DLL 的 SHA-256 均为 `d7951f07a6320d515ef272a373025fe1e791aeae8a6749974dae8424094ab59b`，目标游戏为 RimWorld 1.7.5005 rev1082。

## PNA 发烟器

**半径和档位可分别调节。** 半径控制覆盖范围，开关与档位控制是否运行及对单位目标的输入速率。至少提供低档和高档。覆盖预览随所选半径更新。

| 档位 | 已确认的行为 |
| --- | --- |
| 低档 | 适合日常使用。持续暴露时，输入和负荷越高就越快的自然消退共同决定长期平衡点。低于平衡点时逐渐增长，高于平衡点时逐渐下降。 |
| 高档 | 提高单位时间的 PNA 输入，持续暴露时能跨过普通目标的失能门槛并达到满负荷。 |

低档的平衡点由**输入速率和现有清除公式**决定。普通目标在 97% 负荷进入末期，意识上限降至 10%，达到 100% 时按现行规则判定首次成瘾。高档速率须能克服末期自然消退。[负荷数学](../../Sexslavecraft/common/SSCPnaMath.cs) · [五档负荷与成瘾规则](../../机制详解.md#11-pna-物品与武器)

### 发烟器测试版

2026-11-14 实现 [1.7 建筑定义](../../1.7/Defs/ThingsDefs/SSC_PnaMistEmitter.xml)与 [PNA 运行组件](../../Sexslavecraft/Comps/CompSSCPnaMistEmitter.cs)。发烟器复用工业催情发烟器的雾效和房间规则，使用普通 PNA 补料。

- 初建为关闭。档位与半径独立保存，半径 **5～10 格**、默认 **5 格**。实际覆盖为半径内、同房间的有效目标。断电或缺料暂停运行，保留玩家所选档位及半径。
- 低档每游戏日毛输入 **10 个百分点**。单台长期覆盖约趋近 **35%**。无现存负荷时，先累积到 **0.75%** 才首次入账，入账后每 **60 tick** 投送。
- 高档每 **60 tick** 投送 **2.5 个百分点**，零负荷目标在一倍速下约 **40 秒**进入末期，继续暴露可达 100%。沿用主人免疫、有效锁链活化与普通目标首次成瘾。已有依赖和已有需求随原状态变化，雾气只增加负荷。
- 低档耗电为 `100 + 4 × 半径²` W，高档为其两倍。5 格时低档每日消耗 1 份普通 PNA、高档 12 份，其余半径按面积比例换算。以上参数用于首轮实机调试。
- 未入账剂量随档保存，读档先复查当前覆盖，随后继续采样。离开覆盖、停机、断电、改半径或切换档位会清除受影响的未入账剂量，已入账负荷沿用自然消退。
- 自动验证完成 1.7 构建（零警告、零错误）、发烟器原生专项 **23／23** 和 PnaLoad 专项 **585／585**。原生专项覆盖低档从零与高值趋同、高档满值、档位与半径独立、实际电网及启动余额的 Scribe 往返。[发烟器测试边界](../../Tests/AphrodisiacEmitterNative/README.md)
- 2026-11-14，维护者确认发烟器实机无问题，本批验收关闭。[实机验收记录](../Development/Completed/催情发烟器/PNA发烟器测试版实机验收.md)
- 同日接入独立的单格发烟器贴图，维护者确认最终黑边版实机显示无问题。[贴图接入与实机验收](../Development/Completed/催情发烟器/PNA发烟器测试版实机验收.md#独立贴图与黑边验收)

## 美术收尾

炮弹物品、飞行弹体和 IED 最终沿用原版烟雾系列贴图并应用紫色色调，发烟器采用经实机验收的单格独立贴图。[最终外观与资源路径](../Development/Completed/催情发烟器/PNA发烟器测试版实机验收.md#pna-雾气系列美术收尾)

## 实现依据

- 爆炸和发烟器以 `Blast`、`MistEmitter` 来源调用 [PNA 公共入口](../../Sexslavecraft/common/SSCPnaExposureUtility.cs)。两者共用普通侵蚀、有效锁链活化、主人免疫及身体负荷保存。两种来源的已有需求补充和已有成瘾增量均为零。
- PNA 发烟器复用现有建筑的半径配置、范围预览和房间判定，并投送 PNA 身体负荷。原三档催情发烟器施加 `HumpShroomEffect`。[现有发烟器设计](Archive/单格电力催情发烟器开发规划.md#2-驼峰菇药效与暴露语义) · [现有发烟器组件](../../Sexslavecraft/Comps/CompSSCAphrodisiacEmitter.cs)
- 发烟器按有效游戏 Tick 换算暴露量。低档把首次小剂量攒至 0.75% 入账，越过 0.5% 的负荷清除阈值。有负荷记录后按采样直接投送，已入账的负荷由同一组件自然消退。[PNA 负荷组件](../../Sexslavecraft/Hediff/HediffComp_SSCPnaLoad.cs)

## 验收核对项

以下条目用于复核。两批测试版的实机反馈见各自的验收记录。

- 炮弹和陷阱分别触发一次，验证实际爆炸位置、共同覆盖边界和每个有效目标的 25 个百分点增量。覆盖同一目标只结算一次，以及接近满负荷时的钳制。
- 调节半径与切换档位，确认范围预览和实际覆盖同步，核对停机、离开覆盖和存读档后的行为。
- 分别让零负荷和高于预期平衡点的普通目标处于低档覆盖，观察双方从各自方向趋近平衡点。高档验证从零持续暴露可进入失能档并达到满负荷。
- 选择代表性普通目标、有效锁链目标和免疫目标，核对公共负荷入口的分流。自动检查与实机反馈按[测试编写与执行准则](../Development/Reference/测试编写与执行准则.md)记录。
