# 狗终极直接驯服技能回归

在仓库根目录运行：

```powershell
dotnet restore Tests/PetDogAbility/PetDogAbility.csproj --configfile Tests/NuGet.Config
dotnet run --project Tests/PetDogAbility/PetDogAbility.csproj --configuration Release --no-restore -- .
```

套件不依赖 NuGet 包，注册到 `Scripts/Test-All.ps1`。直接链接真实 `Ability_PetDogTame`、`CompAbilityEffect_PetDogTame`、`Verb_PetDogTame`、`JobDriver_PetDogTame`、`PetDogAbilityUtility`、`HediffComp_GiveAbility` 及宠物公共规则、标签工具、特化组件和健康对账，技能条件、最终复查、同格限制、接近重试、旧终极维护及人格冷却迁移均由生产源码执行。

41 个用例组覆盖：

- XML 单一效果、60000 tick 冷却、两秒原版预热与进度、地图选取和同格接近、不碰撞任务、征召及未征召入口；原版驯导图标/社交符号及预热 `SpeechLines` Effecter 的连续 OnTarget 波纹、15 tick 节奏、无初始延迟、总数上限及抬高位置。四语技能标签/描述、任务报告和 8 个拒绝/目标提示键完整，源码语言镜像相同。
- 狗终极严重度达到 0.01 才有资格，普通狗与其他宠物终极不授予。旧普通 Hediff 终极实例没有授予组件时，生产 `Maintain` 可立即授予且不替换原状态；重复维护与真实授予组件保留技能实例、Verb 和已有冷却。维护只在 Inactive/PostLoadInit 运行，失去成果、死亡、销毁或空壳后撤销。
- 玩家阵营施放者存活、已生成、未倒地、无精神状态且不是空壳；不依赖培养方向、身份、研究、绑定或征召。目标为同地图上存活、已生成、无阵营、无精神状态、意识合格并通过原版 `TameUtility.CanTame` 的动物，正规人形/机械体、其他阵营或已驯服动物拒绝；普通睡眠和仍有意识的倒地动物保留。
- 地图选择要求 `OnCell` 可达，不限制当前距离/视线；真实开始、`CanHitTargetFrom` 原版预热检查和生效必须同格。组件保留原版 Valid 限制并报告非法目标；缺少或重复驯服组件的配置在进入原版预热或激活前拒绝。
- 真实效果只进入一次 `DoRecruit(caster, target)`；原版边界立即转换玩家阵营。重复调用、两名施放者先后或同时预热同一动物不重复驯服，后完成者不扣冷却。第三方阻止阵营转换时，实际生产能力撤销本次新冷却并允许立即重试，上次成功结果不泄漏到失败施放。
- 狗技能不改变施放者/目标训练进度、方向历史、亲昵冷却或额外健康状态；最终效果不依赖原版已经扣除的 CanCast 冷却。
- 真实任务为接近、到达跳转复查、停止移动、一次原版 `CastVerb`，不调用可能在任务结束扣冷却的基类。行走期间不暂停或唤醒目标；明确同格到达后才开始 120 tick 原版预热、有限眩晕及普通睡眠唤醒。移动目标保留 Pawn 引用，到达通知后移位重新接近；最后 tick 目标已被驯服、失去意识/可驯服资格或进入精神状态会取消。
- 行走或预热中的施放者成果、目标阵营/精神/意识/位置或当前 Ability 归属变化均不结算。取消只清本人当前任务的本人 Verb，保留其他任务/Verb 和目标其他来源眩晕。成功后目标变成玩家阵营仍正常 FinishedBusy 收尾；原版开始失败和引擎预热中断/重置无 busy 时自然空收尾且无结算。
- 停滞接近每 60 tick 最多补寻路 10 次，正常移动清计数且无总行程时限；不可达停止重试，预热不重寻路或重复眩晕。真实 ExposeData 接线 `sscDogTameApproachIdleRetries`，缺字段默认零，字段加载本身不启动路径或预热。
- 冷却捕获保存绝对截止 tick，凝胶存放时间继续流逝；恢复立即授予并保留迁移实例，旧字段/过期截止就绪。无终极或空壳恢复清孤儿技能，提取清源狗普通/终极标签、狗当前方向与狗历史，保留其他方向、成果及技能。
- 合法远处目标显示原版圈、预览、正常光标及带动物名字的提示；非法目标无圈/提示并使用禁用光标。刷新 UI 不发起路径、预热、暂停、唤醒、效果或冷却，同格施放保护继续生效。
- 经原版 `Verb_CastAbilityTouch` 的动态调用入口，`IsApplicableTo` 保留地图资格、效果组件限制及消息开关。动物在悬停期间变为玩家阵营后立即禁用原版反馈，资格恢复时重新显示；远处适用性不允许直接预热或生效。

原版边界宿主只记录明确调用并提供可控制的输入：`TameUtility.CanTame` 返回测试配置的资格结果，不复制野性、Scaria 或种族算法；`DoRecruit` 记录双方参数并模拟即时阵营转换或第三方阻止转换，不复制原版命名、关系、驯服记录、消息或视觉反馈。原版 Ability 边界按已核验顺序先开始冷却，再分发实际生产效果。`CastBoundaryStubs` 建模 Toil 调用、120 tick 预热、生产 Verb 的 From 动态检查、成功开始的有限眩晕/唤醒及结束通知；只建模预热这一种全身 busy，不覆盖完整调度或其他 stance。

测试明确通知同格到达；路径容器只保存目标引用与调用次数，不能据此证明真实寻路、墙体绕行、移动动物追踪或眩晕副作用。字典 Scribe 只验证真实 ExposeData 的字段名/默认值，不等于完整游戏任务、stance 或存档序列化。`VisualBoundaryStubs` 按本机原版 Touch IL 建模高亮的 `IsValid`/`IsApplicableTo` 判断、光标的 `ValidateTarget` 判断与附加标签分发，不复制宠物筛选算法。`GenDraw.DrawTargetHighlight` 只记录目标引用，具体 Thing 中心、绘制高度、旋转及 `TargetHighlighter` 交给游戏实测；不运行 GPU、Targeter 或 Effecter 波纹生成/清理/读档调度。完整人格快照、加工 CopyFrom 与提取/植入链路由 PersonalityTraits 另行验证；旧存档终极狗自动授予、真实原版驯服反馈、取消后特效停止以及存读档需实机验收。
