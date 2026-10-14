# Progression: Education 课程兼容核查

核查日期：2026-09-23（本地时区）。状态：仅分析，未实施修复。

## 结论

1. 玩家截图确实对应 `ProgressionEducation.EducationManager.TryInitiateClassForStudyGroup` 中的教师检查。本机随模组提供的 `1.6/Source/EducationManager.cs:272–276` 与截图一致；第 274 行输出 `is unavailable. Suspending class.`。它不是学生入课失败的日志。
2. 本机 RimWorld 的 `GatheringsUtility.PawnCanStartOrContinueGathering` 明确拒绝 `pawn.IsPrisoner || pawn.IsSlave`。这里读取的是原版 `Pawn_GuestTracker.guestStatusInt`，与 SSC 的 `pawnIdentity` 独立。
3. 用户最终确认：本机测试的教师是原版殖民者，两个学生都是原版奴隶；三人都可以有 SSC 性奴身份。最近的测试存档也符合这个状态。因此“性奴能教、不能学”在这次测试中实际比较了不同的原版身份，不代表教育模组专门放行性奴教师。
4. SSC 现有补丁只清除工作类型限制，没有放行教育系统调用的集会检查。这是已确认的兼容缺口；现有证据不足以认定为 SSC 2.3.1 新增回归。
5. 另发现教育模组添加学生时的集会条件取反。本机源文件和 DLL 均包含该问题，需独立处理；它不是本次两名奴隶无法学习的唯一原因。

## 核查范围与证据级别

- 读取两张附件，将其中的代码作为待核验材料。
- 检查 SSC 当前源码、`v2.3.1` 标签、相关 Git 历史及 `v2.3.0..v2.3.1` 差异。
- 读取本机教育模组源码，并用 Mono.Cecil 只读检查实际 DLL 的 IL，覆盖编译器生成的闭包方法。
- 读取本机游戏 DLL 的集会检查、奴隶身份 getter 和身份存档方法。
- 只读解析 `SSC测试.rws`、`SSC测试1.rws`，检查课程、角色身份、当前任务和 Lord 成员。
- 对照教育模组作者的公开源码。网页可能有缓存，实际本机结论以本机 DLL 为依据。
- 没有启动游戏重演，没有执行游戏程序集，没有获取运行中的 Harmony 补丁链。未修改源码、DLL、模组安装目录或存档，也未实施补丁。

## 截图能证明什么

调用顺序为：

```text
EducationManager.WorldComponentTick
  -> TryInitiateClassForStudyGroup
     -> 检查课程、时段、教室、铃和已有 Lord
     -> PawnCanStartOrContinueGathering(studyGroup.teacher)
     -> 失败时记录教师 unavailable，通知暂停并 return
     -> 成功时才用教师创建课程 Lord
```

因此截图中的小人“米拉”是那门课的教师。日志是 `Log.Message` 主动输出的诊断消息，附带调用栈；不等于抛出了代码异常。

截图本身没有给出教师的原版身份、健康或征召状态，不能仅凭该消息断言一定由奴隶身份导致。原函数还会拒绝征召、出血率大于 0.3、失血严重度大于 0.2、野人、非人化、亚人、未生成、倒地或精神异常等状态。但如果教师原版身份确实为奴隶，原版奴隶检查已足以令它失败。

调用栈中的 `/Users/ferny/.../Source/...` 是编译时记录的源码路径，不能据此判定玩家运行在 macOS。

## 本机学生失败的完整路径

课程不是普通 WorkGiver 工作，也不是 `LordJob_VoluntarilyJoinable`：实际 `LordJob_AttendClass` 直接继承 `Verse.AI.Group.LordJob`，通过 Duty 分配教学和学习任务。

本机教育 DLL 中共查到 **15 个调用点**，包括闭包中的调用：

| 用途 | 调用点 |
| --- | --- |
| 创建课程 | `EducationManager.TryInitiateClassForStudyGroup` |
| 教师持续资格 | `LordJob_AttendClass.LordJobTick` |
| 响铃与教师任务 | `LordToil_RingBell.UpdateAllDuties`、`JobGiver_RingBell.TryGiveJob`、`JobDriver_RingBell.MakeNewToils` 的失败条件、`JobGiver_Teach.TryGiveJob` |
| 开课时打断已有任务 | `LordJob_AttendClass.CancelOtherJobs` 的过滤闭包 |
| 首次添加学生 | `TransitionAction_AddStudentsToLord.DoAction` 的过滤闭包 |
| 后续补加学生 | `LordToil_AttendClass.LordToilTick` 的过滤闭包 |
| 学生职责分配 | `LordToil_AttendClass.UpdateAllDuties` |
| 学生领取任务 | `JobGiver_AttendClass.TryGiveJob` |
| 学习过程失败条件 | `JobDriver_AttendClass`、`JobDriver_AttendMeleeClass`、`JobDriver_AttendShootingClass` 中的闭包 |
| 出勤与教学收益 | `StudyGroup.IsStudentPresentAndAttending` |

对于原版奴隶学生：

- 分配职责时会走 `lord.RemovePawn(student)`，而不是正常得到学习职责。
- 后续补加学生的扫描要求集会检查为真，奴隶不会被补入。
- 即使其他方式把学生塞回 Lord，`JobGiver_AttendClass` 仍会返回 `null`。
- 即使直接派发学习任务，任务的 `FailOn` 仍会使其失败。
- 即使绕过任务失败，出勤判定仍会把学生算作未上课，影响课程进度／收益。

因此，仅修改截图中的教师检查，或仅让学生进入课程名单，不能完整修复。

教师也没有一般性的奴隶豁免：创建课程、持续检查和 `JobGiver_Teach` 都检查集会资格。当前教师能工作，是因为其原版身份不是奴隶。

## 存档交叉验证

读取文件：`C:/Users/24196/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Saves/SSC测试.rws`。

该文件修改时间为 2026-09-23 23:27:48（本地）。课程为 `StudyGroup_2`，名称 `S-1-公车 (高科技精通)`。

| 小人 | ID | SSC 身份 | 原版 guestStatus | 保存时任务／职责 |
| --- | --- | --- | --- | --- |
| S-1-公车 | Human432 | Slave | 未保存 Slave；默认 Guest，属于玩家派系 | `PE_Teach` / `PE_Teach` |
| S-2 | Human52352 | Slave | Slave | `GotoWander` / 无课程 Duty |
| S-3-奴隶 | Human228280 | Slave | Slave | `GotoWander` / 无课程 Duty |

课程 Lord_25 的 `ownedPawns` 只有教师 Human432，当前处于第 1 个 LordToil（AttendClass）；`numPawnsEverGained=3`。这些字段与“教师留在课堂，两个学生曾加入后又被移出”的路径相符；累计人数不是精确事件日志，不能仅凭它断言移除过程的全部细节。

S-2 的 `joinStatus=JoinAsColonist` 不会覆盖 `guestStatus=Slave`：游戏 `IsSlave` getter 只看后者。S-1 反而保存了 `joinStatus=JoinAsSlave`，但没有原版奴隶 guestStatus，仍可担任教师。不能用 joinStatus 或 SSC 性奴标记代替当前原版身份。

更早的 `SSC测试1.rws` 中，这三人的 guestStatus 分布相同。用户随后确认测试使用的就是上述存档，并修正了“两名学生中有一名殖民者”的初步描述。

## 额外发现：开课添加学生时条件取反

本机 `1.6/Source/AI/LordJob_AttendClass.cs:234`：

```csharp
.Where(s => !GatheringsUtility.PawnCanStartOrContinueGathering(s)
            && s.GetLord() == null)
```

这会跳过符合集会条件的学生，反而选中不符合条件的学生。本机 DLL 的生成方法 `<DoAction>b__2_0` 在集会结果为真时直接跳至返回 false，确认不是“源码有误但 DLL 已经修好”。

它与后续 `UpdateAllDuties` 中“不符合条件就 RemovePawn”的逻辑相冲突。正常学生存在后续 `LordToilTick` 补加路径，但该路径还要求已有当前 Job、不是学习驱动，并且空闲或开启打断工作。因此不能把所有正常殖民者入课问题都无条件归因于取反，也不能因为有补加就忽略这个缺陷。

本次用户最终确认两个学生均为原版奴隶，故不需要另立“原版殖民者同样必定失败”的结论。

## 与 SSC 2.3.1 的关系

`Sexslavecraft/ThePatches/Harmony_SlaveWorkRestrictions.cs:14–33` 修改的是 `GuestUtility.GetDisabledWorkTypes`。清除工作类型禁用列表不会改变 `Pawn.IsSlave`，也不会改变集会资格。

- 当前源码和 `v2.3.1` 均未找到针对 `GatheringsUtility`、`PawnCanStartOrContinueGathering` 或 `ProgressionEducation` 的兼容补丁。
- 对上述符号的全部本地 Git 历史检索没有发现曾经添加后又删除的补丁。工作类型放行文件的历史仅显示初始提交；它和 `SSCIdentityUtility.cs` 在 `v2.3.0..v2.3.1` 间没有变化。
- 新限制系统的全局 AI Hook 虽然会经过教育 JobGiver，但只处理 RJW 性行为驱动、原版 Lovin 和已知 Seduced 驱动。教育 `JobDriver_LessonBase` 直接继承原版 `JobDriver`，不在这些类别内。
- 当前工作区 HEAD 是 `53f518e`，已包含发布后的仪式选人修复；本机安装 DLL 与工作区 DLL SHA256 相同，二者文件版本仍为 `2.3.1.0`。本机不是仅凭版本号即可认定的原始标签构建，因此同时核对了 `v2.3.1` 的源码。

据此，更准确的定性是“SSC 尚未覆盖的教育／原版奴隶兼容问题”。玩家声称“最近更新后又出现”可能涉及其历史身份、教育模组版本或其他补丁变化，但缺少旧环境对照，不能作为 SSC 更新导致回归的证据。

## 建议修复范围与后续验证

建议采用只作用于 Progression: Education 的可选兼容层：

1. 明确允许参加课程的 SSC 性奴范围，沿用统一身份判定，并限制到适当的玩家所属对象。
2. 只在教育检查中豁免目标对象的原版奴隶身份限制，保留征召、健康、精神状态等其余规则。不要全局把所有性奴的集会检查强制返回 true，否则会同时开放宴会等无关行为并绕过健康检查。
3. 覆盖完整教学生命周期及编译器生成的闭包／学习失败条件，不能只修改创建课程入口。
4. 独立修正首次添加学生的反向条件。还需核对补加学生是否已经属于其他 Lord、是否仍在对应地图及课表，避免兼容代码抢走正在其他活动中的 Pawn。
5. 兼容层使用可选依赖探测；教育模组缺席时不加载，对目标签名或 IL 不匹配时给出可定位诊断。

后续实机验收至少区分：SSC 性奴且原版殖民者、SSC 性奴且原版奴隶、普通殖民者、普通奴隶；分别作为教师和学生，覆盖普通／近战／射击课程、铃响入课、持续学习、实际收益、下课清理及读档。普通奴隶的预期结果由修复范围决定。征召、倒地、出血／失血和精神异常等对照仍应被正确拒绝。

## 本机二进制指纹与上游位置

本机游戏 `Version.txt`：`1.6.4871 rev590`。

| 程序集 | SHA256 |
| --- | --- |
| 游戏 `RimWorldWin64_Data/Managed/Assembly-CSharp.dll` | `5CF1B5BE399D5B1C9C56CA72C9D35B4ECF307FEACF5859D04AC5A1AA5926356A` |
| 教育 `3590863552/1.6/Assemblies/ProgressionEducation.dll` | `EF42E932F625F92F186C11EE30FEB98279B1A2C051260DDE1AD0E867737775A9` |
| 工作区及本机安装的 `Sexslavecraft.dll` | `866E3C8B2FBE5A519FC4C52558F3E61D624946D63318255B592EEBA77BA956D8` |

教育模组本机根目录：`D:/STEAM/steamapps/workshop/content/294100/3590863552`。

上游同类代码：

- [EducationManager：教师可用性与初始参与者](https://github.com/fernyrepos/Progression-Education/blob/main/1.6/Source/EducationManager.cs)
- [LordJob_AttendClass：铃响转换与学生过滤](https://github.com/fernyrepos/Progression-Education/blob/main/1.6/Source/AI/LordJob_AttendClass.cs)
- [LordToil_AttendClass：补加学生与职责更新](https://github.com/fernyrepos/Progression-Education/blob/main/1.6/Source/AI/LordToil_AttendClass.cs)

以上上游链接指向可变化的 main 分支；复核本次本机结论时应优先使用记录的 DLL 指纹和本机源文件。
