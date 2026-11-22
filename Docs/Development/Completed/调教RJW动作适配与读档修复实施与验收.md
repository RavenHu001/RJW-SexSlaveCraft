# 调教 RJW 动作适配与读档修复实施与验收

2026-11-22，1.7 首批随 [PR #49](https://github.com/RavenHu001/RJW-SexSlaveCraft/pull/49) 合入 `main`，合并提交为 `d50aa9f8`。覆盖双人绑定仪式、日常调教指定姿势回退，以及两条在途读档恢复路径。以下分批验收沿用各次受测候选及环境。本次归档记录的是合并后的开发状态，正式版仍为 SSC 2.5.3。

[总体规划第 23.1 项](../../Design/未来内容开发规划.md#ssc-training-anatomy-compatibility) · [绑定仪式设计归档](../../Design/Archive/绑定仪式RJW行为与动画部位适配规划.md)

## 绑定仪式实现

六阶段按性奴实际参与的目标部位筛选 RJW 候选，调教员阴部参与只影响符合条件的候选排序。筛不到候选时强制执行原固定动作，包括部位解析为空的场景。缺动画时继续执行 RJW 和阶段推进，SSC 照原规则进行整场结算。 [筛选、保底和结算规则](../../Design/Archive/绑定仪式RJW行为与动画部位适配规划.md#六阶段筛选)

<a id="ssc-binding-anatomy-validation"></a>

## 绑定仪式专项自动验证

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

在途旧档的胃部开发凭据保留已通过实机复测。 [读档修复验收](#ssc-training-save-recovery-verification)

<a id="ssc-daily-training-fallback-acceptance"></a>

## 日常调教指定姿势回退

1.7 指定姿势继续先使用原来的固定交互。有效的指定动作解析不到适用于双方的部位时，本次由 RJW 按普通自动模式重新选取，保存的下拉选项留待下次尝试。自动选中且正常完成时按实际动作走原结算，自动选不到可用动作时沿现有中止、清理与重试冷却。重要日志 `[SSC_TRAIN_ACT]` 记录原姿势、失配原因、自动动作和启动或中止结果。 [日常调教入口](../../../Sexslavecraft/JobDriver/JobDriver_Train.cs) · [RJW 交互解析](../../../Sexslavecraft/common/RJWSexPropsUtility.cs)

2026-11-22，日常调教回退实机验收通过。两次指定本番分别回退到 `Sex_Cunnilingus` 和 `Sex_Reverse_Fingering`，一次指定乳交回退到 `Sex_Cunnilingus`。三次 `[SSC_TRAIN_ACT]` 均记录 `NoCompatibleParts` 和 `Started`，动作与存档交互记录一致。1.7 构建及三套相关自动回归通过，共 1026／1026 项。

<a id="ssc-training-save-recovery-verification"></a>

## 两处读档修复

- **日常调教。** 从在途任务实际执行的 RJW 动作 Def 恢复 `Reverse`，并同步给读档后认领的同一场接收任务。 [日常调教 Job](../../../Sexslavecraft/JobDriver/JobDriver_Train.cs)
- **绑定仪式。** 旧档已有的开场胃部开发凭据随在途阶段恢复，完成时按原角色、动作及器官附着情况复查。新版部位快照失效后保持空接触状态，重复存读档仍沿用该状态。 [仪式 Job](../../../Sexslavecraft/JobDriver/JobDriver_RitualTraining.cs) · [胃部开发凭据](../../../Sexslavecraft/Comps/Comp_Train.StomachDevelopment.cs)

2026-11-22，维护者确认本机 RimWorld 1.7.5014 rev1195 的读档修复候选 `c715cd2c` 实机复测通过。验收范围为日常调教反向动作的恢复，以及旧档绑定仪式已有胃部开发凭据的保留。该候选的 1.7 构建零警告、零错误，五套相关自动回归 **1318／1318** 项通过，包含原生 Scribe 的双方方向恢复和仪式重复存读档。

| 回归套件 | 通过 |
| --- | ---: |
| `UterusDevelopmentNative` | 774／774 |
| `InteractionProtection` | 218／218 |
| `TrainingExperience` | 71／71 |
| `ForcedSexProps` | 29／29 |
| `RitualLifecycle` | 226／226 |

征召后的重复派工仍暂缓处理。
