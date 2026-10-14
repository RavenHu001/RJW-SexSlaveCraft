# Progression: Education 与调教任务抢占核查

核查日期：2026-09-23（本地）。2026-09-24 已按维护者要求实施最小修复：自动调教避让课程活动。下文核查证据保留；实际实施范围见末节。

## 结论

这是教育模组与 SSC 日常调教之间的任务调度冲突。教育模组在课程期间持续强制停止学生的非学习任务；SSC 又允许自动调教选择正在听课的对象，并且在接收任务被外部打断后没有重试等待。条件持续满足时，两边形成循环。

与此前的“原版奴隶不能上课”问题不同，这条循环要求学生能通过教育模组的集会检查。原版殖民者形式的 SSC 性奴即可满足这个前提，无需修改原版奴隶兼容逻辑。

不能把它完全归为教育模组的问题：强制拉回来自教育模组，课堂避让及中断后的重试控制也是 SSC 可以处理的兼容边界。

## 运行日志证据

调查期间游戏重启，原 `Player.log` 轮换成了 `Player-prev.log`。为避免继续轮换导致证据丢失，已将两者只读复制至工作区 `.builds/education-job-contention/`，没有修改原日志。

含问题的快照：`.builds/education-job-contention/Player-prev.log`。

- 源日志修改时间：2026-09-23 23:50:12（本地）。
- `actor=M-1, partner=S-2` 的 `Receiver job started`：**733 次**，快照第 218–2310 行之间。
- 同一日志中 `Start() called` 总计 735 次；该日志行不携带参与者，不能把全部 735 次都归给 S-2。
- S-2 的 `Daily training completed`：**0 次**。
- 全日志只有 1 次日常调教完成，目标为 S-1-公车，与 S-2 不同。
- 没有匹配到 `10 jobs` / `too many jobs` 的任务频率错误。这不否定循环，因为跨 tick 重启不必触发同一 tick 的次数保护。
- 快照 SHA256：`F73D5C4C449F31CA985FAD02458C63052205C1078A9761FF25A7C90E1FD23D83`。

日志直接证明大量重复启动且 S-2 没有完成。它没有每次中断的调用栈、Job ID 或 tick 时间，因此不能仅凭这些行计算每秒频率，也不能逐条证明中断来源。下述源码和 DLL 调用链解释了用户观察到的听课／接受调教切换。

## 完整循环

1. 学生原本属于活动课程，持有学习 Duty 并执行学习任务。
2. SSC 的自动工作扫描通过身份、指派、许可、排班、冷却、预约和身体检查。`TrainerAssignmentUtility.TryPrepareTrainingTarget` 没有“正在参加课程”的排除条件。
3. 调教师到达后，`TrainingJobUtility.TryStartReceiverJobCore` 调用 `partner.jobs.StartJob(receiverJob, JobCondition.InterruptForced)`，学生的学习任务被 `SSC_TrainingReceiver` 替换。此处没有退出或暂停学生所属的课程。
4. 教育的 `LordToil_AttendClass.LordToilTick` 发现学生当前任务不是 `JobDriver_AttendClass`；若教室 `interruptJobs=true`，便调用 `student.jobs.StopAll()`。
5. 该调用强制清理接受调教任务及任务队列。课堂关系仍在，后续 AI 可以根据原课堂职责再次领取听课任务。
6. 调教师的 `JobDriver_Training` 在实际调教阶段通过 `FailOn(!IsValidTrainingReceiver(...))` 发现目标已失去接收任务，结束本次调教并清理 `isBeingTrained`。
7. 这不是完整调教结束，不会走 `Notify_TrainingCompleted` 写入日常冷却；也不是启动失败或身体校验失败，不会触发已有的 300 tick 校验失败等待。
8. 如果自动调教仍开启、指定调教师仍可接单且其他条件不变，工作扫描再次选择同一目标，回到步骤 2。

这解释了为何现象不是“一次正常中断后回去听课”，而是不断重新开始。

## 教育模组的强制拉回

本机目录：`D:/STEAM/steamapps/workshop/content/294100/3590863552/1.6/Source`。

`AI/LordToil_AttendClass.cs:46–63` 的筛选条件是：

```text
学生通过集会检查
且当前有 Job
且当前驱动不是 JobDriver_AttendClass
且（学生空闲 或 教室 interruptJobs 已开启）
    -> StopAll()
    -> 再检查是否已在 ownedPawns 中，必要时补入课堂
```

关键细节：

- `StopAll()` 在 `lord.ownedPawns.Contains(student)` 判断之前。已经属于课堂的学生也照样会被强制停止当前工作。
- 没有针对调教接收任务、成对任务或 `casualInterruptible` 的判断。
- 没有在该循环外设置 tick 间隔；游戏 `Lord.LordTick()` 每次都会调用当前 `LordToilTick()`。因此它是持续检查，不仅在铃响时执行。
- `ClassLogic/Classroom.cs:13,44` 中 `interruptJobs` 的初始值和存档默认值都是 true。
- 教室设置使用 `PE_InterruptJobsDuringClass`。英文提示说明铃响时打断，但实际持续拉回逻辑覆盖整个上课阶段。
- `LordJob_AttendClass.CancelOtherJobs` 还会在铃响转换时单次停止符合条件的参与者任务。因此兼容时需要考虑“调教过程中开课”和“上课过程中开始调教”两个方向。

已用 Mono.Cecil 检查实际教育 DLL：`LordToilTick` 中的 `StopAll` 调用位于 `ownedPawns` 检查之前；其筛选闭包确实检查集会资格、学习驱动类型、IsIdle 和 interruptJobs。相关 IL 保存在 `.builds/education-job-contention/education-tick.il.txt`。

## SSC 当前保护为什么无效

### 不可随意中断标志不拦截 StopAll

`Defs/JobDef/JobDriver_TrainingReceiver.xml:8` 已设置 `casualInterruptible=false`，发起者的 JobDef 也设置了相同标志。这不是漏填 XML。

游戏实际 DLL 中，`Pawn_JobTracker.StopAll()` 直接调用 `CleanupCurrentJob(JobCondition.InterruptForced, ...)` 并清空排队任务，不先检查 casualInterruptible。因此修改一般任务优先级或重复设置这个标志无法阻止教育模组的强制中断。

### isBeingTrained 不是跨模组任务锁

`TrainingJobUtility.MarkTrainingStarted` 设置的标志用于 SSC 自己的目标筛选，教育模组不会读取它。中断清理会解除标志，这是避免对象永久卡住所必需的；不能通过永不清理标志解决抢占。

`TrainerAssignmentUtility.RecoverTrainingTargetState` 也会在目标已经没有接收任务时清理失效标志，因而它不能代替明确的课堂避让或重试策略。

### 当前中断路径不进入冷却

- `JobDriver_Train.cs:174`：接收任务不再有效时触发失败。
- `JobDriver_Train.cs:192–200`：退出清理训练占用，没有记录外部中断的重试等待。
- `Comp_Train.cs:178–188`：完成才更新 `lastTrainingTick`；中止只清理状态。
- `TrainingJobUtility.MarkValidationFailure` 的 300 tick 等待用于身体校验失败或接收任务当场启动失败，不覆盖“成功启动后被课堂抢走”。

`JobGiver_SlaveKeepJob` 只挂在 SSC 仪式的 `SSC_SlaveWaitDuty` 上，并不是日常调教的全局保活器。这次不能据其名称推断为它在每 tick 重发接收任务。

## 修复方向建议

建议先明确业务优先级，再做针对性修复：

1. **自动调教避让正在进行的课程。** 检查活动课程成员关系／职责，并在候选筛选及真正替换接收任务前复查，覆盖调教师行走过程中课程开始的情况。不能仅检查瞬时 `CurJobDef==PE_AttendClass`，否则学生短暂处于空任务或走路状态时仍可能被抢走；也不能把所有 Lord 一概当课堂。
2. **外部中断后有短暂重试等待。** 只针对本次日常调教因接收任务被替换而失败的情况记录等待。不要冒充成功结算并消耗正常完成冷却，也不要影响仪式阶段推进或正常结束。此项是防止高速重复的兜底，单独实施不能解决优先级冲突。
3. **若手动命令允许调教优先，需要教育侧临时避让。** 只标记 playerForced 或一次移出课堂不够，因为学生仍在 studyGroup.students 中，课堂会继续扫描、打断和补加。需要让实际调教期间的课堂拉回暂停，结束后再恢复正常参与。
4. 若希望所有调教都服从课程，则手动入口也应明确拒绝并显示原因，避免命令表面成功后立即被课堂撤销。这属于产品规则选择，本次没有擅自实施。

临时验证／规避方式：关闭教室“打断工作上课”选项，或将 SSC 自动调教时段与课程错开。关闭选项会改变普通工作的课堂打断行为，而且空闲分支仍然存在，所以它是对照与临时规避手段，不作为完整修复结论。

后续应验证：课程已开始时自动尝试调教、调教师行走时课程开始、调教进行时铃响、明确手动命令、学生任务被第三方替换、下课恢复自动调教，以及正常完成是否仍只结算一次。观察参与者双方 Job、课程成员关系、占用标志、重试 tick 与完成冷却。

## 本次边界

最初核查只新增本文档及忽略目录中的日志／IL 快照，没有修改功能代码。后续修复范围如下；教育模组文件和存档仍未修改。

此前[课程兼容核查](ProgressionEducation课程兼容核查.md)记录的是 23:27 的存档快照；本次调查时用户测试已将同名存档更新到 23:47，角色身份和课程名单均发生变化。不能把更新后的名单倒推为此前测试或本次循环发生瞬间的名单。

## 已实施：自动调教避让课程（2026-09-24）

- 新增独立可选兼容组件 `ProgressionEducationCompatibility`，通过目标实际所属 Lord 的完整类型名识别 `ProgressionEducation.LordJob_AttendClass`。没有对 Education 的编译依赖，未安装时不会匹配。
- 自动候选筛选及完整任务创建检查均跳过课程成员。判断不依赖瞬时学习 Job，因此听课、授课及课程成员临时无任务时都受保护；仅有课表、尚未加入活动的对象不在此最小规则范围内。
- 日常调教驱动增加执行期间的失败条件。目标在调教师行走或执行期间加入课程时，自动调教退出并沿用现有占用清理；课程结束并退出 Lord 后可以再次自动调教。
- 玩家手动强制命令、其他 Lord、绑定仪式、教育自身的学生资格检查均保持原行为。手动命令仍可能被教育的强制拉回打断，这不在本次自动避让修复范围内。
- 没有增加新设置、重试冷却或 Education Harmony 补丁，也没有实现上文其他修复建议。

验证：TrainerIdentity 55/55、RitualLifecycle 52/52；包括 4 个新增回归场景，覆盖候选查询后入课、课程成员无学习 Job、手动命令、其他 Lord、在途开课及退出清理、下课恢复。使用本机游戏/RJW 引用的 Release 编译通过，工作区 `Assemblies/Sexslavecraft.dll` 已更新。已备份并同步游戏安装目录的 DLL 与调试符号，文件哈希校验一致；2026-09-24 维护者确认实机修复有效。
