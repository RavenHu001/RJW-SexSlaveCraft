# 绑定仪式的 RJW 行为与动画部位适配规划

**状态：2026-11-22，RimWorld 1.7 女女绑定仪式实机验收通过。** 首批适用于 RimWorld 1.7 双人绑定仪式。 [总体规划第 23.1 项](未来内容开发规划.md#ssc-training-anatomy-compatibility) · [女女实机验收](#ssc-binding-anatomy-female-acceptance)

优化前的固定动作版本在 RimWorld 1.7.5014、RJW 6.2.2 上完成了女女六阶段。第 3 阶段启动动画，其余五个阶段未找到匹配动画。第 6 阶段保留 `Sex_Vaginal` 进入 RJW 流程，RJW Menstruation 按交互类型处理体液，当时记录了 33 条 `0 fluid` 警告。 [阶段映射及空接触路径](../../Sexslavecraft/common/RitualTrainingUtility.cs)

加入动作筛选后的女女存档交互记录依次是 `Sex_Fingering`、`Sex_Footjob`、`Sex_Cunnilingus`、`Sex_Breastjob`，以及连续两次 `Sex_MutualMasturbation`。后两段的通用部位标签产生了肛部和阴部候选，解析结果缺少接触，定义也未指定对应部位。筛选按下述规则排除这类候选。

## 六阶段筛选

**性奴的目标部位参与是自动筛选的硬条件，调教员的阴部参与是排序偏好。** SSC 保留调教员主持、性奴受训的仪式角色及原有准入检查。每阶段维护性奴侧的部位规则，候选动作由 RJW 提供。调教员以任何能通过 RJW 校验并参与本次交互的部位配合。自动筛选无结果时，强制采用该阶段原有的固定动作保底。

| 阶段 | 性奴侧目标部位 | 可用于确认参与的结果 | 原有保底动作 |
| --- | --- | --- | --- |
| 1 手部 | `Hand` | 候选明确选用了性奴的手。若有配对，从对应端点确认手的归属。 | `Sex_Handjob` |
| 2 足部 | `Foot` | 候选明确选用了性奴的脚。调教员侧按候选本身的要求匹配。 | `Sex_Footjob` |
| 3 口部 | `Mouth`、`Tongue` 或交互明确支持的 `Beak` | 性奴口部出现在其参与部位或明确的接触端点中。`Sixtynine` 有时只返回生殖器参与部位，阶段仍须取得口部参与证据。 | `Sex_Sixtynine` |
| 4 胸部 | `Breasts` | 候选明确选用了性奴的胸部，部位可用性及交互的严重度门槛由 RJW 判定。 | `Sex_Breastjob` |
| 5 肛部 | `Anus` | 确认候选用到性奴的肛部。若以插入配对证明参与，核对来源是调教员、接纳端是性奴的肛部。经 RJW 解析的手或舌配合也可入选。 | `Sex_Anal` |
| 6 阴部 | `Vagina` | 确认候选用到性奴的 `Vagina`。双方阴部接触、调教员用手或舌配合均可入选。若以插入配对证明参与，核对接纳端属于性奴。 | `Sex_Vaginal` |

### 选取与部位归属

1. 在阶段启动前用本场双方及交互情境调用 `SexInteractionFinder.FindInteractions`。每条 `SexInteractionResolved` 提供一个具体交互及双方的一组可用部位。同一 Def 可能有多个部位组合。先过滤每条结果，再进行排序和选取。
2. 用 `Initiator`、`Recipient`、部位的 `Owner` 和 `Family` 核对性奴侧是否参与了目标部位。有 `Penetrations` 时按各端点的实际归属和方向判断。若目标部位只出现在参与列表里，还须由该动作的接收方部位类别要求明确指定。例如 `Sex_Scissoring` 明确要求双方的 `Vagina`。只靠 `CanPenetrate` 或 `CanBePenetrated` 一类通用标签枚举出来的部位，需要实际接触配对才能证明参与。SSC 详细日志会打印自动选中的 Def、双方参与部位及接触方向。
3. 在合格候选中，优先选择调教员的阴部也实际参与的组合。调教员侧使用其他部位的合格组合继续可选。阴部偏好按实际部位类别判断，RJW 的通用 `CouldBePenetrator()` 也接受手，因此仅这个判定不足以认定阴部参与。同优先级取 RJW 返回的首个候选。
4. 采用选中的具体 Def 和同一份 `resolved`，保留交互的 `Reverse` 标记及其 RJW 语义。SSC 的筛选依据是本场角色和部位归属。例如 `Sex_Fingering` 用到性奴的手，可用于手部阶段。其反向版本用到性奴的 `Vagina`，可用于阴部阶段。两者都继续遵守本场许可。 [现有交互字段校验](../../Sexslavecraft/common/RJWSexPropsUtility.cs) · [部位和接触归属](../../Sexslavecraft/common/SSCOrdinarySexPartsUtility.cs)
5. 没有通过阶段规则的候选时，复用 `RitualTrainingUtility.GetPhaseData` 的原有指定动作映射，以该 Def 强制启动保底。用本场双方解析这个固定动作。解析成功时使用实际结果，解析不到时沿用 `allowEmptyParts` 记录当前 Def、当前双方及空接触。保底仍须通过仪式原有许可和归属复查，缺少固定 Def 或初始化失败沿原失败清理流程处理。 [原有阶段保底映射](../../Sexslavecraft/common/RitualTrainingUtility.cs)

## 阶段执行与动画

**优先保证有效仪式完整跑完六阶段。** 动画由现有框架随 RJW 场景自行匹配和播放。1.7 绑定仪式移除 SSC 的手动动画回退调用。缺少匹配动画时，RJW 动作按原流程继续，SSC 按阶段时钟和仪式归属推进。动画状态不参与动作筛选、暂停、重新选取或完成判定。 [当前手动回退调用点](../../Sexslavecraft/JobDriver/JobDriver_RitualTraining.cs)

| 匹配结果 | 推进方式 |
| --- | --- |
| 解析出实际部位 | 执行自动选中或固定保底的 RJW 交互，沿场景时钟推进。 |
| 固定保底动作没有解析出部位 | 保留原动作 Def，正常启动接收任务并执行 RJW `Start`、`SexTick`、`ProcessSex` 和 `End`。沿 RJW 时钟推进，兼容模组仍可能记录 `0 fluid` 等黄色警告。 |

阶段切换后按当前身体重新筛选。已开始阶段沿用已选交互及 RJW 阶段时钟，读档恢复本场角色、所选部位或保底空接触状态及相应进度。仍由同一仪式 `Lord` 及当前阶段 Job 认领阶段推进和完成资格。许可失效、角色变化、取消或中断沿原有清理流程处理。缺少完整阶段部位快照或原身体引用失效时，保留动作和 RJW 剩余时间，按空接触继续执行。旧档若已保存开场时的合格胃部开发凭据，正常完成阶段时仍按原角色、动作及器官附着情况复查该凭据。新版部位快照失效后的空接触状态会跨再次读档保留。旧版已经进入等待阶段的存档若尚未启动 RJW 场景，在恢复首 tick 时启动原场景。 [阶段 Job 与恢复](../../Sexslavecraft/JobDriver/JobDriver_RitualTraining.cs) · [仪式生命周期](../../Sexslavecraft/common/BindingRitualStateUtility.cs)

## 开发与整场结算

- **普通部位开发。** 完成整场后，按原规则一次性发放手、足、口、胸、肛、阴部开发，使用原有身体条件、研究门槛、仪式质量及进度上限。固定动作保底和缺动画均按同一规则发放。 [整场发放](../../Sexslavecraft/common/ConditioningUtility.cs) · [普通部位经验](../../Sexslavecraft/common/TrainingExpUtility.cs)
- **专门部位开发。** 子宫按原有阴部开发资格和仪式质量结算。胃部按原有的严格口部接触凭据结算。 [口部接触证据](../../Sexslavecraft/Comps/Comp_Train.StomachDevelopment.cs) · [子宫与胃部结算](../../Sexslavecraft/Ritual/RitualOutcomeEffectWorker_SSCBinding.cs)
- **整场奖励。** 完成六阶段后按现有规则领取一次整场结算资格，绑定、关系、观礼及记忆继续走原有流程。 [整场资格与结算](../../Sexslavecraft/common/BindingRitualStateUtility.cs)

## 验收范围

- 对照双方均有、仅一方有及双方均无可用阴茎的组合。验证性奴目标部位缺失或被封锁时该候选被排除，调教员阴部参与时优先，只有手或舌等配合时仍能选中合格动作。覆盖同类型正反向动作的部位归属差异。
- 用六阶段各一个明确目标案例核对选中的部位，重点检查 `Fingering` 在手部和阴部阶段的不同归属、`Sixtynine` 口部证据不足和双方阴部接触的情况。
- 验证自动筛选无结果时采用本阶段原有的固定 Def，并分别检验保底解析到部位和保底为空的行为。覆盖有动画与无动画的整场仪式，核对六阶段均能推进、接收任务正常清理，结束后沿用原整场开发奖励。用无法解析第 6 阶段部位的组合验证 `Sex_Vaginal` 照常驱动 RJW 场景，出现 `0 fluid` 警告时阶段仍可完成。
- 验证阶段中断、换阶段及读档恢复。核对本场角色、阶段进度和原有的胃部接触凭据，以及胃部、子宫和整场奖励各自的资格。

<a id="ssc-binding-anatomy-validation"></a>

## 本轮自动验证

RimWorld **1.7.5014 rev1195**、RJW **6.2.2** 隔离构建通过，生产候选和原生测试宿主均为零警告、零错误。`UterusDevelopmentNative` **768／768**，含肛部和阴部阶段排除仅有通用标签的空接触候选，保留有接触或明确指定部位的动作。六阶段空接触保底回归执行实际 `SexTick` 并观测 `ProcessSex` 入口，验证整场只结算一次，发起六类普通部位发放，并按原资格写入实际部位与子宫进度。旧档回归经原生 Scribe 恢复后检查 RJW 时钟和原交互 Def。宿主隔离地图切换、图形及第三方结算副作用。

恢复结算时的相关回归通过 `RitualLifecycle` **226／226**、`RitualProgression` **38／38** 和 `TrainingExperience` **71／71**。

<a id="ssc-binding-anatomy-female-acceptance"></a>

## 女女实机验收

2026-11-22，维护者确认女女绑定仪式验收通过。受测环境为 RimWorld **1.7.5014 rev1195**、RJW **6.2.2**，SSC 分支提交 `f95f339b`。同一女女组合运行两次，存档中的 RJW 交互记录顺序一致。下表的部位和动画记录来自最后一场的 `Player.log`（01:55:45–01:56:14），动作 Def 已与存档交互记录交叉核对。

| 阶段 | 实际执行的 RJW 动作 | 选择方式及部位 | 动画框架 |
| --- | --- | --- | --- |
| 1 手部 | `Sex_Fingering` | 自动选中，性奴的手与调教员的阴部参与。 | 已启动 |
| 2 足部 | `Sex_Footjob` | 使用原固定保底，RJW 场景正常启动并完成。 | 无启动记录 |
| 3 口部 | `Sex_Cunnilingus` | 自动选中，性奴的舌与调教员的阴部参与。 | 无启动记录 |
| 4 胸部 | `Sex_Breastjob` | 使用原固定保底，RJW 场景正常启动并完成。 | 无启动记录 |
| 5 肛部 | `Sex_Reverse_Rimming` | 自动选中，调教员的舌配合性奴的肛部。 | 无启动记录 |
| 6 阴部 | `Sex_Scissoring` | 自动选中，双方阴部参与。 | 已启动 |

第 5、6 阶段的目标部位均由动作定义明确指定，并出现在对应角色的解析结果中。`contacts=[]` 表示 RJW 未生成插入配对。最后一场逐阶段记录了 `Start ritual scene` 和完成状态，最终达到 `6/6` 并记录 `Binding Ritual finished`。
