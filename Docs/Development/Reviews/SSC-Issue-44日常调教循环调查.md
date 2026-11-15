# SSC Issue #44 日常调教循环调查

2026-11-15，RimWorld **1.7.5005 rev1082**，SSC **2.5.0**。已用发布 DLL 验证开始确认失败可以触发即时重派。汇报环境中首次开始确认失败的原因仍待定位，本文记录的修复建议尚未实施。回退对照为汇报者实际使用的 **2.4.9**。征召受训者时出现的同 tick 红字是另一条待排查路径，暂缓处理。

## 汇报材料与版本差异

原始材料保存在本地「SSC-无限调教乱切动画循环」目录，包括 `Player.log`、`1000+.xml` 和复现视频。关键事件可用下列日志行复核。

| 日志行 | 观察 |
| --- | --- |
| 76677–76706 | SSC 长矛发光 Transpiler 遇到预期之外的 `DrawEquipmentAiming` 指令布局。异常从 `PatchAll()` 传出并中断 `ModStartup`。 |
| 324501 | 后续补丁清单登记了 SSC 的 `JobDriver_SexBaseInitiator.Start` Prefix 和 Postfix，以及动画框架等后置补丁。清单无法说明这次调用是否执行了原方法或写入了凭据。 |
| 328811、328836 | 14:18:23.392 安装接收任务，35 毫秒后动画后置输出 `InteractionDef: Sex_Vaginal`。首轮从正常寻路抵达进入 `Start` 调用链。 |
| 328862、328878–328880 | 下一轮已安装新的接收任务，调用栈经过 `TryFindAndStartJob`，确认后续由工作扫描自动重派。 |
| 328811–344735 | 同一配对共 269 次接收任务启动和 269 次相同的 `InteractionDef`，两次重派间隔中位数为 109 毫秒。整份日志没有 SSC 的 `Start() called.` 或 `Daily training completed:`。 |

- **2.4.9** 在 RJW `Start()` 返回且原 Job 和目标仍有效后直接标记场景开始。
- **2.5.0** 的 1.7 多调教员互斥提交 `72226ce6` 增加 `HasStartedScene` 校验。凭据无法确认时，原任务以 `Incompletable` 结束。这条分支未限制下一次自动派工。`startNewJob: false` 只阻止结束调用当场再找工作。
- 两版的发光 Transpiler、SSC 初始化入口和 RJW 开始钩子源码相同。回退后的启动日志及完整调教完成记录仍待核对。

[2.4.9 发布归档](../../Releases/2.4.9/2.4.9版本验证.md) · [2.5.0 发布说明](../../Releases/2.5.0/2.5.0发布说明.md) · [日常调教驱动](../../../Sexslavecraft/JobDriver/JobDriver_Train.cs#L305) · [SSC 开始钩子](../../../Sexslavecraft/ThePatches/Harmony_ProtectSexSlave.cs#L36)

## RJW 场景的触发时点

1. 调教员走到目标身边后，`startPartnerJob` 给受训者安装 `SSC_TrainingReceiver`。该 Toil 立即完成，首轮调用栈显示它与后续场景初始化在同一次寻路抵达回调中推进。
2. 下一步 `sexToil.initAction` 同步位置、准备 `SexProps` 并通过最后一次身体与兼容性校验，随后调用继承自 RJW 的 `JobDriver_SexBaseInitiator.Start()`。本机 RJW 的原方法会调用 `SexInteraction.AddToPlayLog`。本机动画框架则在 `Start` 的 Postfix 中调用 `StartGroupAnimation` 后输出 `InteractionDef`。
3. 返回后，SSC 检查原任务、开始凭据、占用及身份。全部通过时才到达无条件的 `[SSC Debug] Start() called.` 日志。在随后的任务 tick 中，原 SSC 调教驱动调用 RJW 的 `SexTick` 推进场景。计时结束后才进入 `ProcessSex` 和 SSC 结算。

复现视频约 8 秒起能看到姿态切换和反复刷新的互动气泡，其中有与本机 RJW `VaginalSexRP` 互动记录一致的文案。本机 RJW 程序集内，`SexInteraction.AddToPlayLog` 的直接调用者为 `Start()`。若气泡来自对应的游戏互动记录，它支持原生 `Start` 至少有一部分尝试执行到了写日志的步骤。录像未提供原方法执行标记，也无法逐次对应 269 条启动记录。结合汇报日志缺少 SSC 开始确认和完成记录，当前现象更符合场景反复初始化，未进入持续的 `SexTick` 推进。

[接收任务交接](../../../Sexslavecraft/JobDriver/JobDriver_Train.cs#L181) · [场景初始化](../../../Sexslavecraft/JobDriver/JobDriver_Train.cs#L228) · [逐 tick 推进](../../../Sexslavecraft/JobDriver/JobDriver_Train.cs#L341) · [结算](../../../Sexslavecraft/JobDriver/JobDriver_Train.cs#L393)

## 隔离探针与生命周期边界

使用 2.5.0 标签对应的 1.7 发布 DLL 与本机 RJW、Harmony 和动画框架运行隔离探针，四组检查全部通过。探针位于本地 `.builds/issue-44-lifecycle/`。探针隔离了渲染和其他游戏环境副作用，验证范围是驱动回调、接收任务交接、凭据失败和接单资格。

| 探针场景 | 观察结果 |
| --- | --- |
| 正常调用 | 原生 `Start` 执行，SSC 确认开始，双方任务保持有效。 |
| Prefix 跳过原生 `Start` | 动画后置仍打印 `InteractionDef`，缺少 SSC 开始确认。 |
| 缺少 SSC `Start_Postfix` | 原生 `Start` 执行，动画后置仍输出，SSC 缺少凭据。 |
| SSC 写入凭据后改变交互方向 | 原生 `Start` 执行，变更前凭据有效，返回后匹配失败。 |

三个异常组均以 `Incompletable` 结束。接收任务及占用均已释放，配对立即再次通过自动准入。接收任务和交互数据在调用原生 `Start` 前已经准备好，因此动画后置日志可以出现在原方法被跳过时。

`Start()` 返回后，原任务被替换、开始凭据校验失败、最终占用或身份校验失败，均会跳过成功日志。两个实际执行原生 `Start` 的异常组均未调用 RJW `End()`。现有 `sceneStarted` 同时控制 SSC 结算和原生收尾，可能遗漏发起方的状态清理。RJW `End()` 含有排入后续任务的逻辑，后续需要核对可以按原任务归属安全回收的状态。

[开始凭据校验](../../../Sexslavecraft/common/Restrictions/SSCRestrictionJobGuard.cs#L93) · [原生收尾入口](../../../Sexslavecraft/JobDriver/JobDriver_Train.cs#L440) · [占用释放](../../../Sexslavecraft/common/SSCDailyTrainingOccupancy.cs#L71)

## 后续诊断与候选修复

1. **捕获首次失败。** 优先在同一模组列表与存档下核对 SSC 的 `phase=Start` 详细日志。需要诊断 DLL 时，在现有 Prefix、Postfix 和调用返回点记录一次准入结果、`__runOriginal`、原 Job `loadID`、接收任务、占用、凭据写入及方向匹配结果。按原 Job 记录首次实际调用 `SexTick`，区分初始化与持续推进。回退到 2.4.9 的对照需同时核对发光异常和实际完成。
2. **限制同一配对的自动重试。** 开始确认失败且仍持有原 Job 占用时，结束前记录只对该调教员和受训者生效的短暂运行时退避。让共用准入在创建下一份自动 Job 前读取该状态。现有 300 tick 身体校验冷却会写入存档，并影响其他调教员和手动命令。若确认 SSC 的开始后置钩子全局缺失，在安装接收任务前暂停自动日常调教并记录原因。
3. **隔离可选发光补丁。** 指令布局不匹配时，透传原指令及标签和异常块，记录一次警告，使 `PatchAll()` 可继续注册。单独验证这项修复能否消除汇报环境中的启动异常和调教循环。
4. **核对开始失败后的原生状态。** 用原 Job 和参与者归属判断哪些状态需要回收，继续用有效的开始凭据控制结算。若实机或探针证实有残留，为这条路径单独设计清理。

[共用准入](../../../Sexslavecraft/common/TrainerAssignmentUtility.cs#L219) · [发光补丁](../../../Sexslavecraft/ThePatches/Harmony_SSCPnaSpearGlow.cs#L20)
