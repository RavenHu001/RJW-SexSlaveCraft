# 调教受方倒地原生专项

仅 1.7，登记 **18 项 Core**。宿主使用完整生产候选、原版游戏程序集、RJW 和游戏 Harmony。

实际执行以下入口：

- 原 `Pawn_HealthTracker.MakeDowned` 的健康状态变更、职责判断与原 `ClearMind_NewTemp → StopAll` 分支。有效已开始的日常／仪式受方保留原接收Job；普通RJW、准备态、施教者倒地和旧配对均沿原中断。
- 原 `JobDriver_Sex.ConditionsToAbortSex` 生成的闭包，确认后续受方倒地不触发中断、调教者倒地仍中断、受方死亡仍触发原死亡条件。器官列表为固定外部输入，不宣称本套验证了器官变化。
- 真实 `TrainingJobUtility.TryStartBindingRitualReceiver` 的下一阶段交接、新Job创建与归属确认。持续倒地的受方可重建阶段接收器并生成原生中断条件。
- 从生产XML读取slave的 `allowDowned`，以真实 `RitualRoleAssignments` 执行原 `LordJob_Ritual.ShouldRemovePawn`。真实 `Lord.Notify_PawnLost` 对Incapped受方保留角色，主持者倒地和死亡的原移除判定保持。
- 多调教员第二阶段新增四项。覆盖双发起者争用及延后清理、读档后走位占用重建与接收配对优先、接收交接保留原版预约，以及原版排队撤销释放精确预约。宿主预置地图预约，接收任务安装仍使用现有隔离边界。 [实现与验证](../../Docs/Development/Verification/多调教员指派第二阶段实现与验证.md)
- 第三阶段核对仪式主持槽为空时，倒地的首位成员被跳过、第二位成员通过配对许可，移除第二位后找不到可用主持者。 [实现与验证](../../Docs/Development/Verification/多调教员指派第三阶段实现与验证.md)
- 整体复查增加原版点名预约争用、结束的旧接收器和交接期间替换组件三个用例，另在原用例验证重复恢复和固定主持槽。 [复查记录](../../Docs/Development/Verification/多调教员指派第三阶段实现与验证.md#2026-11-12-整体复查)

夹具使用真实Pawn／健康／Job／Driver／Comp／Lord与角色表，开始凭据由原 `SSCRestrictionJobGuard.TryMarkStarted` 建立。环境只提供地图存在／同图、床位、固定器官列表、图形与思想／故事通知终端。`MakeDowned` 使用不记录传说的健康原因；测试进程未进入完整Playing世界，其地图／派系通知不冒充实机覆盖。普通中断路径的 `CleanupCurrentJob` 为可观察安装／清理终端，原职责分支、ClearMind和StopAll仍执行。

新阶段 `StartJob` 仅隔离引擎安装终端，记录真实新Job及接收Driver；本套不宣称执行了完整原版任务安装、预约、寻路或Toil调度。六阶段的倒地持续、计时完成、一次性结算与末阶段memo另由 [RitualLifecycle](../RitualLifecycle/README.md) 的生产阶段驱动回归验证；该套是引擎模型边界，不是原生完整仪式。

不验证GPU／动画、完整地图调度、世界存档往返或所有第三方组合。角色初始选人仍由既有角色资格检查负责，本套没有放开倒地候选准入。

运行与构建方式同其他net10原生宿主，向统一runner提供本机 `GameRoot`、`RjwAssemblyPath`、`AnimationsAssemblyPath`、`GameHarmonyAssemblyPath`；可用 `-p:SscAssemblyPath=<隔离候选绝对路径>` 直接验证候选，避免改正式DLL。可执行参数为仓库根目录和游戏根目录。宿主输出所加载程序集路径、MVID和SHA256。

2026-10-28实际执行：**10／10通过**，宿主零警告／零错误；游戏 `1.7.4990 rev1069`，RJW MVID `83b5bb8057c941b086031c2d1cbbf59c`。受测候选SHA256 `9078fb2ca95ac92b1cab196792f42bee5b008098c0c816dcab1146c0793b6728`，日志 `.builds/training-downed/native.log`。本记录是自动验证，实机状态另记。

2026-10-28：维护者确认倒地修复“实机测试没有问题”，整体验收关闭并按授权提交，见[验收记录](../../Docs/Development/Completed/受调教者倒地不中断调教修复与验证.md#实机验收关闭2026-10-28)。本轮未重跑自动回归；上述自动结果和隔离边界保留原时点，不补记逐项第三方组合通过。
