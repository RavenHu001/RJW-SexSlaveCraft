# SSC 武器拓展步骤二 XML 实现与测试

归档说明（2026-10-18）：本批约定范围已交付。当前实现、最终参数及分批验收边界统一见[首批完成核对](SSC武器拓展首批完成核对.md)。下文保留当时的试验参数、测试计数和同步状态，旧的“待实机／未实现／暂不提交”不代表当前待办。

日期：2026-10-18。分支：`codex/ssc-weapon-expansion`。实现基线：命名／耗液／后续显示规划提交 `4ed9e09`。

**状态：第二步 XML 已完成并获维护者实机确认。** 2026-10-18，同步本机游戏后维护者反馈“实机测试正常”，本轮 XML 范围验收关闭；自动与原生检查范围保留如下。 维护者授权研究和制作参数先采用建议值。正式贴图和“纳米催情液效力”属性不在本批范围。未打包或指定新发布版本。

[首批规划](../../Design/Archive/SSC武器拓展首批开发规划.md) · [共用剂量工具](PNA武器共用抗性与剂量工具实现与测试.md) · [待验收索引](../Verification/README.md) · [PNA 回归](../../../Tests/PnaLoad/README.md)

## 1. Def 与接线

本批新增 11 个显式 Def，以及由三个武器 `recipeMaker` 在游戏加载时生成的三份配方；不手写重复 RecipeDef。

| 类型 | defName | 名称或职责 |
| --- | --- | --- |
| ResearchProjectDef | `SSC_RES_PNAPrimitiveWeapons` | 原始纳米催情液武器 |
| ThingDef | `SSC_Weapon_PnaSpikeStaff` | 纳米催情液刺棍 |
| ThingDef | `SSC_Weapon_PnaBlowpipe` | 纳米催情液吹箭筒 |
| ThingDef | `SSC_Weapon_PnaLightCrossbow` | 纳米催情液轻弩 |
| ThingDef | `SSC_Projectile_PnaDart` | 纳米催情液吹箭，飞行投射物 |
| ThingDef | `SSC_Projectile_PnaBolt` | 纳米催情液弩矢，飞行投射物 |
| DamageDef | `SSC_Damage_PnaStaffStab` | 纳米催情液刺击，`baseLoad=0.15` |
| DamageDef | `SSC_Damage_PnaDartStab` | 纳米催情液吹箭，`baseLoad=0.06` |
| DamageDef | `SSC_Damage_PnaBolt` | 纳米催情液弩矢，`baseLoad=0.10` |
| ToolCapacityDef | `SSC_ToolCapacity_PnaStab` | 刺棍专用携液刺击能力 |
| ManeuverDef | `SSC_Maneuver_PnaStab` | 将专用能力连接至携液主伤害 |

运行文件为：

- `1.7/Defs/ThingsDefs/SSC_PnaPrimitiveWeapons.xml`：三件武器及两种投射物。
- `1.7/Defs/DemageDefs/SSC_PnaPrimitiveDamage.xml`：三类携液伤害和一份不实例化的公共 XML 刺伤基类。
- `1.7/Defs/ManeuverDefs/SSC_PnaPrimitiveManeuvers.xml`：近战专用能力及动作。
- `1.7/Defs/ResearchDefs/ResearchProjectDef.xml`：追加一项研究，既有研究字段保留。

三种 DamageDef 均调用已编译的 `SexSlaveCraft.DamageWorker_SSCPnaCoated`，使用 `SSCPnaCoatingExtension.baseLoad` 和 `Hediff_PNA_Consciousness_Record`。刺棍／吹箭对应原版 `Stab` 伤势，弩矢通过 Arrow XML 基类保留 `Cut`、箭伤传播、远程标记及锐器护甲；实体部位伤势为 `Crack`。专用能力不修改原版 Stab 动作，也没有额外近战伤害或第二份零伤害投药。

武器与伤害显示名采用维护者确认的六个名称。简中、繁中、英文、俄文分别新增 ThingDef／DamageDef／ToolCapacityDef／ResearchProjectDef 四份 DefInjected 文件，每语 21 个键（含工具部位与死亡信息）。16 份语言文件同步到 `Sexslavecraft/Languages`。既有 Def 镜像为部分集合，本批不扩大其范围。

## 2. 参数与制作

研究为 Medieval、800 点，前置仅 `SSC_RES_PNASimpleApp`，SSC 研究页坐标 `(2, 4.5)`；不添加浓缩、精密膛线或新蓝图条件。旧发射器及人格发展线保留原前置。

| 参数 | 刺棍 | 吹箭筒 | 轻弩 |
| --- | --- | --- | --- |
| 伤害／穿甲 | 5／12% | 3／5% | 7／18% |
| 攻击间隔 | 主刺击 1.8 秒 | 0.5 秒预热＋1 秒冷却 | 1.5 秒预热＋2.5 秒冷却 |
| 射程 | 相邻 | 17.9 | 30.9 |
| 命中配置 | 近战命中评分＋2 | 四档 65／45／20／10% | 四档 50／80／80／60% |
| 基础归一化 PNA | 0.15 | 0.06 | 0.10 |
| 木材／钢铁／普通 PNA | 30／15／10 | 20／5／6 | 45／25／10 |
| WorkToMake／Crafting | 4000／3 | 2400／2 | 8000／4 |
| 工作台 | 燃料／电力锻造台 | 手工制作点、燃料／电力锻造台 | 燃料／电力锻造台 |
| 重量 | 1.2 kg | 0.5 kg | 2.2 kg |

成本只在制造时扣除，不加入库存弹药、剩余药液、补液或充能机制。三种成品均为 Medieval，固定材料且保留原版品质，不开放自由材质；PNA 基础剂量不随品质变化。未覆盖 MarketValue，使用原版估值流程。移除继承的商人／NPC 装备标签并设为不可交易、无遗物权重，首批通过玩家制作获取。

刺棍显式替换继承的 `defaultIngredientFilter`，避免 BaseMeleeWeapon_Sharp 默认禁木材使账单不可制作。所有 `recipeUsers` 明确替换继承列表，未引入 TableMachining 或工业研究。自动配方预期为 `Make_SSC_Weapon_PnaSpikeStaff`、`Make_SSC_Weapon_PnaBlowpipe`、`Make_SSC_Weapon_PnaLightCrossbow`；本轮已按维护者“实机测试正常”的反馈验收，不将未逐项报告的场景补记为专项实测。

三件武器的柄击均为 2 点普通钝伤、0 穿甲、2 秒冷却，不施药。刺棍主刺击设 `chanceFactor=2`：原版 `InitialVerbWeight` 采用 DPS 与选择系数，而原版人类拳击为 8.2／2 秒，未提高选择系数时可能抢占低伤害主刺击。该字段只改变动作选择权重，不能当成命中率修正。对普通品质基础效率进行实际 1.7 分档函数核对，主刺击为 Best、拳击为 Mid、柄击为 Worst；不承诺第三方种族、品质、植入攻击或角色修正后的固定动作概率。

## 3. 临时外观与范围

武器暂引用原版 Club、Pilum、BowRecurve，投射物引用 Arrow_Small，配置紫色 tint；只引用原版资源路径，没有复制或生成贴图资产。吹箭飞行速度 35、弩矢 55，作为开发起点，持握、飞行显示与视觉辨识待第三步。

后续按维护者反馈，删除三件武器描述中“制造时浸覆、成品无需补液”的末段，四语及镜像同步。制造耗液机制与规划保留，界面描述仅交代武器设定和使用特点；对应段落数量断言一并移除。

本批不新增效力属性、StatDef、显示 C# 或对应显示键；正式贴图与“纳米催情液效力”任务继续保留在规划。生产 C#、项目源码清单、1.7 DLL／BUILD-INFO、1.6／Common、版本号和 About 均未改动。步骤一的实际 1.7 编译结果为历史产物依据，本轮只编译独立原生验证宿主，没有重编译或替换生产 DLL。

## 4. 验证与证据边界

目标游戏 `D:/STEAM/steamapps/common/RimWorld/Version.txt` 为 **1.7.4981 rev136**。实际程序集 SHA-256 为 `EB0826B85AF430D13962E5C097EF53D869CF6A031656FB6A3673168A761C9F23`，MVID 为 `f1e6382600a84191bbc6d8a184fd6b57`，与步骤一取证一致。

| 检查 | 本批结果 |
| --- | --- |
| PnaLoad | 470/470；新增 21 项 XML 到生产处理器的接线、配方／副攻击／四语检查 |
| LowTechAdaptation | 39/39；新增研究，既有点数／前置及浓缩配方保持 |
| 实际 1.7 原生探针 | 52/52；11 个 Def、289 个继承后 XML 字段及相关类型／枚举／Def 引用检查 |
| 全量回归 | 36/36 套、3284/3284 项；0 失败、0 未执行 |
| 资源镜像 | 2/2；既有 Def 镜像 16 份一致，语言镜像 367 份一致 |
| 版本布局／冻结基线 | 538 项通过 |

PnaLoad 读取实际运行 XML 的三档剂量，再调用生产涂覆处理器，确认成功开放伤口后才输入、被阻挡不建快照；物理伤口由宿主边界提供，不模拟原版护甲随机。既有 449 项继续覆盖原发射器、意识／快照和身份分流。低科技研究数相应为 19 项（2 Neolithic／16 Medieval／1 Industrial），既有研究不改写。

独立 net10.0 探针使用实际 1.7 `XmlInheritance` 合并所需原版基类与新增定义，以当前程序集检查字段、枚举、类及作者 XML 中的引用；运行真实 `RecipeDefGenerator.SetIngredients` 检查三项固定成本与筛选、`Tool.Maneuvers` 检查专用动作隔离、动作选择分档检查主刺击权重。探针仅初始化内存偏好供 DeepProfiler 使用，不读取或保存玩家配置。它没有加载完整 Def、应用第三方补丁、启动地图或执行实际账单，不能替代完整游戏测试。

本地证据目录 `.builds/pna-primitive-xml/`（忽略的开发产物）：

- `PnaLoad.log` 与 `validation/`：专项及统一回归日志，完整汇总为 `validation/summary.json`。
- `native/`、`native.log`、`native-result.json`：实际 API 探针源码、构建与执行结果。
- `native-merged-defs.xml`：实际 XML 继承器输出的 11 个 Def，不是游戏运行时快照。
- `scope-evidence.json`：与 `4ed9e09` 对照的未变文件哈希、18 项既有研究语义对照及 16 份新语言镜像核对。

未导出本批新的运行时 Def 快照。上述原生探针与自动回归仍按其原范围登记；后续维护者的“实机测试正常”反馈作为本轮 XML 实机验收依据，不改写原生探针或同步时记录中的 gameRuntimeVerified=false，也不扩大为所有模组组合、种族或长期存读档专项均已实测。冗余补液文案删减后仅重跑 PnaLoad，470/470 通过，XML 与语言镜像检查通过；全量 36 套结果保留接入阶段的实际运行范围。

## 5. 原游戏内验收清单与确认范围

以下为实现阶段制定的验收情境，留作追溯参考。维护者已反馈本轮实机测试正常，不再将第二步 XML 列为待验收；正式贴图和效力显示继续留待开发。

1. 在 1.7 启用 SSC，检查启动日志中新增 Def、引用、语言与临时资源无加载错误；SSC 页出现 800 点研究，既有研究位置及前置正常。
2. 完成新研究后，三件武器在指定工作台出现；木材／钢铁／PNA 的默认筛选可用，消耗和工艺要求正确，不出现重复配方，品质正常生成。
3. 对普通肉体目标测试三件武器：成功开放伤口产生对应负荷；未命中、完全阻挡、转钝伤和普通柄击不输入。核对真实传播、自然断肢、人工部位及高意识快照。
4. 对主人与有效锁链目标验证：物理伤口保留，主人药效免疫，有效链活化且不治疗；同 tick 独立命中分别结算。
5. 使用同环境原发射器对照，确认其固定创伤、有效链无伤／治疗和原研究条件仍正常；检查存档／读档后新武器与新研究保留。
6. 观察刺棍动作频率、吹箭中近距离和轻弩远距离表现，重点记录轻弩误杀；临时原版外观只用于辨认功能，正式图与最终持握另行验收。

## 6. 本机同步

2026-10-18 按维护者要求同步至 `D:/STEAM/steamapps/common/RimWorld/Mods/RJW-SexSlaveCraft-TieJin-Modify`。先核对相同 packageId 和 1.7.4981 rev136，再备份 5 个被覆盖文件；复制 25 个文件（覆盖 5、新增 20），包括三件武器 XML／四语、所需当前 1.7 处理器 DLL／调试符号／构建信息，以及安装目录原有的旧 About／VERSION 与工作区 2.4.2 元信息对齐。没有修改工作区版本或创建新发布版本。

同步后 461 个 1.7 文件与工作区逐文件 SHA-256 一致；安装目录 1.6／Common 的 532 个文件保持原哈希，LoadFolders 未改，无删除操作。备份及新增文件清单位于 `.builds/pna-primitive-xml/game-sync/backup-20261018-175412-4dfb4fdf/manifest.json`，同步结果为同目录上一级的 `result.json`。本次同步已包含后续删除的冗余补液说明。

同步记录本身不等于游戏内验收；同步后维护者另行反馈“实机测试正常”，据此关闭第二步 XML 验收。正式贴图与效力显示仍未制作。
