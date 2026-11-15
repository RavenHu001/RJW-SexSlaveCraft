# SSC PatchAll 启动注册排查与分批修复方案

2026-11-15。以 SSC 2.5.0 当前开发分支和 RimWorld 1.7 为范围，包含 Issue #44 的 PNA 短矛绘制容错提交 `e63681e1`。第一批功能已通过自动验证及本机开 CE 的实机验收，鞭束显示问题单独留待排查。第二、三批待实施。1.6／Common 产物沿用冻结基线。

## 启动失败如何波及调教

`ModStartup` 的静态构造先执行 `harmony.PatchAll()`。补丁的准备、目标解析、Transpiler 执行或 Harmony 生成替换方法时抛出异常，会中断这次扫描。先前已装的补丁可能仍在，后续补丁不会继续注册。高潮采样、UAP、主人吸引力和倒地调教的安装回调在该调用返回后才排队，后面的仪式修正与全凝胶化绘制也因此可能走不到。现有 `Harmony patches loaded successfully` 只标记注解扫描完成，出现在手动注册和延迟安装实际完成之前。 [启动入口](../../../Sexslavecraft/ThePatches/HarmonyStartUp.cs#L17)

日常调教先启动接收任务，再调用 RJW 原生 `Start`。SSC 依赖 `Start_Postfix` 记录开始凭据，返回后校验凭据才进入正常推进。如果这个补丁因批次中断而缺失，接收任务仍可能反复启动，调教却无法推进和结算。该故障链已在 Issue #44 的诊断中观察到。绘制容错版已通过维护者开 CE 的注册、警告去重及结算验收，本机 Cat Hero 对照也确认修复版能继续注册并到达开始确认。其他候选风险目前来自源码，汇报者复测状态继续记在原调查记录。 [开始凭据写入](../../../Sexslavecraft/ThePatches/Harmony_ProtectSexSlave.cs#L40) · [调教校验](../../../Sexslavecraft/JobDriver/JobDriver_Train.cs#L313) · [Issue #44 调查](SSC-Issue-44日常调教循环调查.md)

## 风险清单

| 补丁 | 注册阶段的触发条件 | 批次 |
| --- | --- | --- |
| 绑定仪式角色选择 | `FillPawns` 必须恰好有两处 `AllRolesForReading` 调用，数量变化会抛异常。窗口构造函数靠 `GetConstructors().Single()` 定位，构造函数数量变化会失败。角色变更钩子也直接返回三个反射结果。 [手选与角色变更](../../../Sexslavecraft/ThePatches/Harmony_BindingRitualSelection.cs#L34) | 第一批 |
| PNA 鞭束协作 | `Verb_MeleeAttack.TryCastShot` 对 `meleeThreat` 和 `lastMeleeThreatHarmTick` 必须各写一次，字段或写入失配会抛异常。免闪避由另一个补丁启用，单边安装会改变协作行为。 [威胁改写](../../../Sexslavecraft/ThePatches/Harmony_SSCPnaWhipCooperation.cs#L58) | 第一批 |
| 原版、RJW 和 RJW Menstruation 出生入口 | 原版与 Menstruation 入口会先按名字解析 `mother`、`father` 或 `birtherThing`，再进入已有的 `GeneratePawn` 锚点回退。即使外部方法参数类型未变，改名也会抛异常。RJW 的 `GenerateBabies` 按方法名绑定，也受上游签名变化影响。 [参数解析与出生入口](../../../Sexslavecraft/ThePatches/Harmony_FertilityOffspring.cs#L47) | 第二批 |
| Human Cattle、Equal Milking | Human Cattle 只确认外部类型存在，再按方法名查找 `Prefix`／`Postfix`。Equal Milking 先确认模组已加载，随后假定对应类型和方法可用。目标缺失、新增重载或注入签名变化都可能使注册失败。 [Human Cattle](../../../Sexslavecraft/ThePatches/Harmony_HumanCattleCompatibility.cs#L41) · [Equal Milking](../../../Sexslavecraft/ThePatches/Harmony_EqualMilkingCompatibility.cs#L15) | 第二批 |
| 浪漫命令、原生心情和共用床界面 | 部分动态目标直接返回反射结果。目标缺失或签名变化会在运行时 `Ready` 检查之前影响注册。 [浪漫命令](../../../Sexslavecraft/ThePatches/Harmony_RomanceCommand.cs#L34) · [原生心情](../../../Sexslavecraft/ThePatches/Harmony_RomanceNativeMood.cs#L53) · [共用床界面](../../../Sexslavecraft/ThePatches/Harmony_SharedBedAssignmentUI.cs#L24) | 第二批 |
| RJW 预约与准备阶段守卫 | 预约扫描覆盖已加载的 RJW 派生驱动，某个第三方目标解析或生成补丁失败可波及整批。准备钩子直接返回两处 `MakeNewToils` 的反射结果，方法迁移或由基类提供后可能出现空目标。 [预约扫描](../../../Sexslavecraft/common/Restrictions/SSCRestrictionJobHooks.cs#L12) · [准备阶段](../../../Sexslavecraft/common/Restrictions/SSCRestrictionJobHooks.cs#L64) | 第三批 |

## 第一批　绑定仪式与 PNA 鞭束

**改动范围**

- 绑定仪式按完整签名解析窗口构造函数和手选流程必需的角色变更钩子。改写 `FillPawns` 前确认两个锚点与替换方法均可用，手选入口以整组就绪为准。
- 鞭束同时验证两处威胁写入和两个替换方法。免闪避与不登记本次威胁共用就绪条件，保持原有合格目标判定。

**失败回退**

- 锚点或目标失配时保留进入该 Transpiler 的完整指令流，记录一次具体失败原因。绑定仪式停用依赖改写的手选入口，沿用原生填充和已有的执行前资格校验。若执行前安全检查本身不可用，关闭受影响的 SSC 仪式入口。
- 鞭束整组回到原版命中、闪避与威胁处理。目标或签名不可用时跳过对应注册，已经安装的协作入口由就绪条件停用。

**验收**

- 正常布局下，确认绑定仪式手选、拖放与提交拒绝正确，合格鞭束仍同时免闪避和避免新增威胁，普通近战行为正常。复用相关的 [RitualSelection](../../../Tests/RitualSelection/README.md) 和 [PnaSpacerNative](../../../Tests/PnaSpacerNative/README.md) 回归。
- 分别制造一个仪式锚点失配和一个鞭束锚点失配。检查原指令保留、整项能力回退、同一失败只警告一次，后续补丁继续注册，日常调教仍可完成并结算。

**本批实现与验证**

- 绑定仪式使用已核对的 1.7 构造函数、赋值及角色变更签名。手选启用前检查改写和必要钩子，失配时保留原生填充与独立开场验证。鞭束的两个钩子由同一注册入口安装，注册异常撤回本组钩子，IL 失配时免闪避和威胁改写同时停用。
- `RitualSelection` 145／145 通过，包含模拟上游接管一次角色读取后恢复原生填充、重复失配警告去重、拒绝无效执行者。该套件使用真实 Harmony 和游戏边界模型。`PnaSpacerNative` 88／88 通过，包含在真实 1.7 近战方法中替换一处威胁写入，验证原生闪避与威胁写回恢复，并验证撤回注入后鞭束协作恢复。
- 使用 RimWorld `1.7.5005 rev1082`、正式游戏 Harmony 及第一批 DLL 做独立进程启动验证。常规布局和双失配布局各 5／5。常规布局下两组就绪，双失配时两组各警告一次，SSC 注册继续并正常确认调教原生 `Start`。两组均带入 CE 的绘制改写作为 Issue #44 回归。日志见 `.builds/patchall-batch1/startup.log`。
- 1.7 隔离构建零警告、零错误。测试 DLL 位于 `.builds/patchall-batch1/dll/Sexslavecraft.dll`，SHA-256 为 `df64635b750ea2416cedf26e239c4b11498993b351e5c7b47419b40695588afc`。

### 第一批 CE 实机验收

2026-11-15，RimWorld `1.7.5005 rev1082`，SSC 2.5.0 本体重新同步第一批测试 DLL，安装指纹与上文一致。以下行号对应本机 Waylith `Logs/Player.log` 中 17:57:36 开始的会话。

| 验收项 | 实机反馈与日志 |
| --- | --- |
| CE 启用与 SSC 注册 | CE `16.7.4.0` 于 2529、2567 行加载和初始化。SSC 在 3968 行报告 Harmony 注册成功，4278 行报告仪式修正 `9/9`。全会话只有 3926 行出现绘制失配警告，未见 SSC 静态构造失败或本批新增的两组回退警告。 |
| 绑定仪式 | 维护者确认正常完成，14045 行记录 `[SSC_RITUAL] Binding Ritual finished`。 |
| 日常调教 | 维护者确认正常完成和结算。12679 行记录开始确认，12780 行记录完成，`score=73.00`、`cooldownStarted=True`。 |
| PNA 鞭束 | 维护者确认功能正常，报告鞭束不显示。功能结论来自实机反馈。 |

鞭束手持绘制复用 `DrawEquipmentAiming` 的 SSC 替换入口。3926 行的透传会跳过 `SSCPnaWhipRenderer.TryDraw`，与 SSC 手持姿态缺失吻合。本次日志还记录丝鞭的 CE 近战统计支持错误（14202、14223 行）。确切的不可见原因仍需绘制路径对照，外观验收保留。 [共享绘制入口](../../../Sexslavecraft/ThePatches/Harmony_SSCPnaSpearGlow.cs#L24) · [鞭束绘制器](../../../Sexslavecraft/ThePatches/Harmony_SSCPnaWhip.cs#L61)

开始确认前还有两条同 tick 重派红字（12372、12489 行，`SSC_Training_SexSlave`）。该会话的启动注册及后续调教结算均成功，两条红字的具体触发条件仍未确认。

## 第二批　外部兼容与动态目标解析

**改动范围**

- 将 Human Cattle、Equal Milking 和 RJW Menstruation 的可选接入各自隔离。检查实际目标、参数和返回类型及补丁所需注入参数。原版和 RJW 的出生入口同样补足签名及参数校验，出生参数语义有歧义时跳过该入口。
- 校验浪漫命令、原生心情和共用床行的动态目标，复用浪漫功能已有的就绪判断。移出全局扫描的补丁只从自己的安装入口注册，复核现有优先级和相对顺序。

**失败回退**

- 可选依赖未加载时正常跳过。已加载但接口不兼容时，记录该适配的跳过或失败原因。互相依赖的钩子以整组为单位撤销已装部分或关闭入口，其他适配继续安装。
- 出生入口保留原生生成，该入口依赖改写的 SSC 扩展停用。浪漫命令或心情只关闭缺关键钩子的功能，保留现有原生回退路径。床位行恢复原生姓名绘制。

**验收**

- 覆盖可选依赖缺席和至少一个兼容版本，确认无依赖也能启动，兼容版本接入仍生效。
- 用缺失或签名不符的外部目标，以及一个无法可靠解析语义的出生参数作故障对照。确认受影响的适配回退、原生出生继续，其他适配和日常调教可用。按实际改动选跑 [UterusDevelopmentNative 出生回归](../../../Tests/UterusDevelopmentNative/README.md)、[RomanceControlNative](../../../Tests/RomanceControlNative/README.md) 和 [SharedBed](../../../Tests/SharedBed/README.md)。

## 第三批　启动隔离与调教就绪检查

**改动范围**

- 将仍由无保护的 `PatchAll()` 批量注册的补丁改为逐类或按相互依赖的功能组安装。把目标解析、Harmony 安装和结果核对放进同一个错误边界。按组记录已安装、跳过和失败，并复核现有 owner、优先级和先后约束。仪式修正、绘制及各延迟安装仍按其实际初始化时机调度。
- 将 RJW 基础预约、准备、开始、收尾及存档钩子与第三方派生驱动扩展分别核对。派生目标单独验证。调教就绪检查确认必要钩子已实际安装，依赖 IL 改写的功能还要确认锚点匹配。可以复用倒地调教的独立安装、就绪和清理做法。 [倒地调教安装器](../../../Sexslavecraft/ThePatches/Harmony_TrainingDowned.cs#L23)
- 在设置右下角的「诊断与兼容」加入「补丁注册状态」。沿用窗口的刷新和复制报告，按功能组显示已就绪、已回退、已跳过、注册失败或等待安装。异常详情包含补丁、目标、失败阶段和影响。Harmony 注册清单用于核对实际挂钩，安装流程另存 IL 匹配及回退结果。诊断读取独立的内存记录，窗口的刷新不重跑安装。 [现有诊断报告](../../../Sexslavecraft/common/SSCDiagnostics.cs#L37)

**失败回退**

- 安装失败时保持本组未就绪，撤销本组本次已装且不应单独生效的补丁。若撤销也失败，记录两个失败原因，保持本组相关入口关闭，其他组及延迟安装继续处理。单独派生驱动失败时停止该驱动对应的扩展。依赖该守卫的 SSC 入口保持关闭。
- 日常调教必需钩子缺失时，在派发前阻止受影响的自动工作及对应手动入口，记录一次缺失原因。保留原生 `Start` 确实运行后才接受凭据的规则。未开始的 SSC 调教不得结算。

**验收**

- 注入一个可选补丁注册异常，检查同批后续补丁、手动注册和延迟安装均可继续，并实际完成一次日常调教及结算。确认日志标明失败组、目标、阶段和结果。
- 注入一个必要的开始钩子注册失败，确认不会反复产生接收任务，手动入口同样不能绕过就绪检查，其他已就绪功能仍正常。恢复正常注册后，确认实际开始、首次 `SexTick`、完成与结算均可达。按改动选择 [InteractionProtection](../../../Tests/InteractionProtection/README.md) 和调教生命周期相关回归。
- 在同一次启动中验证短矛绘制原样透传时显示「已回退」，日常调教显示「已就绪」且可结算。另验证缺少可选依赖显示「已跳过」、安装异常显示补丁和失败阶段、延迟组完成后刷新更新状态。复制结果和窗口一致，无需开启详细日志或开发者模式。

## 分批交付与复测记录

- 每批保留上一批通过验收的 1.7 DLL。若当前测试版引入回归，退出游戏，恢复上一批通过的 DLL 后重启，以同一模组清单、加载顺序和存档复核。第一批的恢复基线包含 Issue #44 绘制容错。
- 专项回归、故障注入与实机结果分别记录。收集游戏构建、测试 DLL 指纹、模组版本和顺序，以及对应的注册、状态切换和任务结算日志。最终实机保留正常组合和维护者已能复现 Issue #44 的 CE 组合。原汇报组合按实际回传结果更新原调查记录。
- 第一批结果按上述实际范围登记。第二、三批的验收条目待执行，完整发布门禁按后续发布范围另行记录。 [测试编写与执行准则](../Reference/测试编写与执行准则.md)
