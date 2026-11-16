# SSC Issue #44 日常调教循环调查

2026-11-15，RimWorld **1.7.5005 rev1082**，SSC **2.5.0**。诊断版已确认汇报现场在开始凭据校验处退出，采样未观察到 SSC 开始后置执行。Combat Extended 开关对照和本机 IL 探针确认它能触发同样的启动异常。武器绘制容错测试版已通过专项、隔离探针和维护者开启 CE 的实机验收。原汇报整合包未列出 CE，具体改写者和该组合的修复版结果仍待确认。回退对照为汇报者实际使用的 **2.4.9**。征召受训者时的同 tick 红字暂缓处理。

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

1. **隔离可选武器绘制补丁。** 四参绘制方法无法解析或匹配锚点数量不为一时，透传原指令、标签及异常块并记录一次警告，让 `PatchAll()` 继续注册。已匹配的布局沿用原绘制适配。透传布局上的 SSC 矛尖叠画和共用该钩子的鞭类绘制适配会跳过。单独复测 CE 组合与原汇报模组列表，检查 SSC 初始化成功、开始凭据写入、首个 `SexTick` 和实际完成。
2. **将自动重试作为后续加固。** 开始确认失败且仍持有原 Job 占用时，可以在退出前记录只对该配对及自动工作的短暂运行时退避。现有 300 tick 身体校验冷却会写入存档，并影响其他调教员和手动命令。若证实 SSC 开始钩子在进程内全局缺失，可以在自动任务安装接收任务前单独拦截。
3. **核对开始失败后的原生状态。** 用原 Job 和参与者归属判断哪些状态需要回收，继续用有效的开始凭据控制结算。若实机或探针证实有残留，为这条路径单独设计清理。

[共用准入](../../../Sexslavecraft/common/TrainerAssignmentUtility.cs#L219) · [发光补丁](../../../Sexslavecraft/ThePatches/Harmony_SSCPnaSpearGlow.cs#L20)

## 2.5.0 诊断版复测

诊断 DLL 基于发布标签 `v2.5.0`（`e75bca63`），本地交付副本位于 `.builds/issue-44-diagnostic-v2.5.0/Sexslavecraft.dll`。程序集身份、文件名和产品版本沿用发布版。构建零警告、零错误，SHA-256 为 `663caf5038a97a3c620cbcbf46cb652a642d43a8b744c3f61373518392374f17`。诊断使用既有钩子和驱动调用点，保留原派工、开始确认、清理及发光补丁行为。

每对角色每 10 秒现实时间采样一次。搜索 `[SSC Issue44]`，用 `job` 对齐返回检查与首个 `SexTick` 调用点。若现有 SSC 后置没有执行，`nativeStart` 记为 `unknown`，`playLogEntry` 单独提供同一交互条目的旁证。正常、跳过原生调用、写入后方向失配、后置缺失四组隔离探针均通过。正常组的两个 tick 均到达 `SexTick`，只输出一次首个调用标记。

复测使用 2.5.0 本体，备份并替换 `1.7/Assemblies/Sexslavecraft.dll`，完整重启后使用同一模组列表和存档。退出游戏后保留本次 `Player.log`，重点保留首次 `[SSC Issue44]` 记录、同一 Job 的首个 tick 标记及启动异常。若循环消失，记录该次启动和调教结果。

## 回传诊断与 Combat Extended 对照

原汇报方回传的 `Player-诊断补丁.log` 来自 RimWorld **1.7.5005 rev1082**。第 74146–74172、79997–79999 行记录绘制 Transpiler 异常打断 SSC 初始化。第 90480、91150、96293、101284、106348、111506 行的六个采样均为 `nativeStart=unknown`、`receiptWrite=postfix-not-seen`、`returnCheck=receipt-not-accepted`，且发起 Job 仍然有效、接收 Job 编号不变、参与者方向匹配。每次都观察到新的、与双方及动作 Def 匹配的互动记录，支持原生 `Start` 到达写入互动日志的步骤。全日志共有 387 次接收任务启动和 387 次相同的动画交互输出，未见 SSC 开始确认、`firstSexTick=reached` 或完成记录。

第 86615 行仍列出 SSC 的 `Start` Prefix 和 Postfix。条目说明 Harmony 元数据里有登记；六次实际采样都未观察到写凭据的后置逻辑执行。已确认的是启动批次被打断、日常场景未取得凭据。原汇报整合包的具体绘制改写者仍待定位。回传日志未列出标准包 `CETeam.CombatExtended`，第 29521 行的 RimPacts 兼容探测也将它列为未安装。

另一位测试者在 RimWorld **1.7.4984.1242** 下用同一存档和约 270 个模组做了 CE 单变量对照。关闭 CE 后 SSC 启动成功，绑定仪式补丁注册 9／9，日常调教完成且评分为 40.35。开启 CE 后出现同一 `DrawEquipmentAiming` 异常且调教无法完成。维护者的实测也指向 CE。

本机隔离探针使用 RimWorld **1.7.5005 rev1082** 的原始 IL、实际安装的 CE **16.7.4.0** DLL 和 SSC **2.5.0** 发布 DLL，2／2 通过。原方法有一次直接调用四参 `Graphics.DrawMesh`，SSC 能正常匹配。运行 CE 的真实 Transpiler 后，直接调用变为零次，末尾改为调用 CE 的七参绘制辅助方法。随后运行 SSC 发布版 Transpiler，重现 `Unexpected DrawEquipmentAiming draw call layout`。探针记录位于本地 `.builds/issue-44-ce-layout-probe/probe.log`。

[CE 绘制补丁源码](https://github.com/CombatExtended-Continued/CombatExtended/blob/bcdd75dc9522d526ff3bd9ffef14906c3d19f096/Source/CombatExtended/Harmony/Harmony_PawnRenderer.cs#L110-L119) · [SSC 初始化入口](../../../Sexslavecraft/ThePatches/HarmonyStartUp.cs#L21)

## 武器绘制容错测试版

四参绘制方法无法解析、替换方法无法解析或匹配锚点数量不为一时，Transpiler 逐条返回收到的指令对象，保留顺序、标签及异常块，并用 `Verse.Log.WarningOnce` 输出一次提示。单锚点的原绘制接入沿用既有实现。透传的布局继续用传入的绘制路径，SSC 矛尖叠画及共用钩子的鞭类视觉适配在该布局上跳过。

测试 DLL 基于 `v2.5.0`（`e75bca63`）叠加单个绘制源码变更，1.7 Release 构建零警告、零错误。SHA-256 为 `238d8521a1a22d75854ed5ec6bc310892c6573effeed5ad605758790fe63a117`。本地交付包位于 `.builds/issue-44-render-fallback-v2.5.0/SSC-2.5.0-Issue44-RenderFix-Test.zip`。

- **武器视觉专项 157／157。** 原有正常绘制仍通过，失配用例验证零锚点和多锚点的指令原样透传，跨两种失配仅出现一次警告。
- **隔离宿主 5／5。** 使用 RimWorld 1.7.5005 原始 IL、CE 16.7.4.0 的实际 Transpiler 及候选 DLL。真实 `ModStartup` 完成注册，CE 绘制调用保留，SSC 开始前置及后置已安装，正常场景的 SSC 开始确认出现。原生开始被跳过、方向失配和后置缺失的原有失败行为均通过。
- **实机复测。** 维护者的 CE 组合已验收，原汇报组合待反馈。

[修复入口](../../../Sexslavecraft/ThePatches/Harmony_SSCPnaSpearGlow.cs#L20) · [武器视觉专项](../../../Tests/PnaWeaponEffects/README.md)

## 2026-11-15 本机 CE 实机验收

维护者在 RimWorld **1.7.5005 rev1082** 开启 Combat Extended，确认日常调教能够完整结束并正常结算。安装目录中的 1.7 DLL 哈希与上述测试版一致。复核本次 Waylith `Logs/Player.log`，得到以下记录。

| 核对项 | 结果 | 日志行 |
| --- | --- | --- |
| CE 已加载 | 启动时输出 `Combat Extended :: initialized`，读档的启用名单包含 `CETeam.CombatExtended`。 | 2567、5865 |
| 武器绘制失配 | `[SSC PNA glow] Unsupported DrawEquipmentAiming layout` 全次运行仅 **1 条**。原有 `Unexpected DrawEquipmentAiming draw call layout` 异常和 `ModStartup` 静态初始化异常均为 **0 条**。 | 3453 |
| SSC 补丁注册 | 输出 `Harmony patches loaded successfully.`，绑定仪式补丁注册 **9／9**。 | 3497、3821 |
| 调教与结算 | 有 **2 条** `Daily training completed:`，评分分别为 **-5.80** 和 **70.00**，均为 `cooldownStarted=True`。 | 6317、137640 |

本机 CE 组合的启动、警告去重和调教结算验收通过。原汇报整合包尚待修复版实机反馈。

同日，三批启动隔离复核修正版在 RimWorld `1.7.5011 rev24` 的本机 CE 组合复测通过。日志确认 SSC 注册成功、绘制失配只告警一次，以及日常调教完成并结算。 [复核修正版 CE 实机验收](SSC-PatchAll启动注册排查与分批修复方案.md#复核修正版-ce-实机验收)

## 2026-11-15 本机 Cat Hero 开启的换 DLL 对照

两次均加载 `Autosave-1`，读档日志中的 **64 个启用模组及顺序完全一致**。Cat Hero（`LZ.CatHero`）启用，Combat Extended 未启用。两次结果按本机 Waylith 的轮转日志记录。

| DLL | 结果 | 日志行 |
| --- | --- | --- |
| 绘制容错测试版 | 失配只警告一次，SSC 补丁注册成功，同一配对正常到达 SSC 开始确认。此次日志未记录完整结算。 | `Player-prev.log` 3519、3561、5825、6734、6811 |
| `v2.5.0` 正式版 | 绘制 Transpiler 抛出 `Unexpected DrawEquipmentAiming draw call layout`，SSC 静态初始化中断。日常调教接收任务启动 **802 次**，无开始确认或完成记录。 | `Player.log` 3508–3510、5567、5595、6360 起 |

这次对照确认修复在本机启用 Cat Hero 的模组组合中阻断了启动失败和自动重派。Cat Hero 的 Transpiler 在此前隔离探针中已能单独移除 SSC 所需的绘制锚点。本机这两轮均启用 Cat Hero，原汇报环境的实际 Transpiler 执行顺序和首个改写者仍待确认。

## Samury242 的独立汇报

新汇报的 RimWorld 为 **1.7.5005 rev1082**，SSC 为 **2.5.0**。读档清单有 674 项，包含 Combat Extended 和 RIMMSqol。启动日志确认 CE 已初始化，清单中未见 Cat Hero。材料保存在本地 Issue #44 汇报目录的 `Samury242/` 子目录。

| 证据 | 核对结果 |
| --- | --- |
| `Player.log` 17758–17774、18275–18291 | `Harmony_SSCPnaSpearGlowDraw.Transpiler` 对 `DrawEquipmentAiming` 抛出 `Unexpected DrawEquipmentAiming draw call layout`，SSC 初始化批次中断。全日志无 SSC 注册成功记录。 |
| `Player.log` 30901–35801 | 同一配对启动接收任务 **155 次**，历时约 **21.6 秒**，间隔中位数 **45 毫秒**。未见开始确认或日常调教完成记录。 |
| `output_log.txt` 4163、`Player.log` 20321–20336 | `DrawEquipmentAiming` 的已登记 Transpiler 清单依次列出 CE 和 RIMMSqol。HugsLib 导出正文截断至 10,000 行，启动异常及循环细节来自完整的 `Player.log`。 |

本机 RIMMSqol 1.7 程序集中的 `PanwRenderer_DrawEquipmentAiming.Transpiler` 也会把首条四参数 `Graphics.DrawMesh` 替换成自身的 `DrawEquipment`，与 CE 都具有消耗 SSC 锚点的条件。清单记录了已登记的补丁，现场逐个 Transpiler 的 IL 变化仍未采样。对照原汇报者的 1,033 项模组清单，未发现 RIMMSqol。这次汇报与已知的初始化故障一致，待 Samury242 用原组合复测现有绘制容错 DLL。
