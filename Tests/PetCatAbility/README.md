# 猫终极单一技能回归

在仓库根目录运行：

```powershell
dotnet restore Tests/PetCatAbility/PetCatAbility.csproj --configfile Tests/NuGet.Config
dotnet run --project Tests/PetCatAbility/PetCatAbility.csproj --configuration Release --no-restore -- .
```

套件不依赖NuGet包，已经注册到 `Scripts/Test-All.ps1`。直接链接真实 `Ability_PetCatComfort`、`CompAbilityEffect_PetCatComfort`、`Verb_PetCatComfort`、`JobDriver_PetCatComfort`、`PetCatAbilityUtility`、`HediffComp_GiveAbility`，以及宠物公共规则、标签工具和特化组件/健康对账，不复制技能资格、状态分支、接近重试或人格冷却迁移逻辑。

48个用例组验证：

- XML单一效果、60000 tick冷却、地图选择、自定义同格接近任务、原版2秒预热、暂停与进度条、征召与未征召入口；原版Heart社交图案及预热Effecter，连续OnTarget喷心、30 tick间隔、无初始延迟、数量上限与抬高位置。鼓舞30000 tick计时、工作速度倍率1.10、依附Hediff的8点即时心情，四语及源码语言镜像。
- 普通完成不授予，猫终极有效严重度授予；跨方向、留空、SSC身份、绑定及研究不影响终极成果。死亡、销毁、倒地、未生成、精神状态或空壳不能施放。
- 同图玩家阵营其他人形目标，使用原版意识容器 `capacities.CanBeAwake`；无意识或缺意识容器拒绝，普通睡眠和有意识倒地保留。地图选择要求 `OnCell` 可达，距离和当前视线不限制；生效与原版 `CanHitTargetFrom` 预热检查必须同格。正常分支需要心情，精神状态分支无心情仍可恢复；所有当前精神状态和来源均通过恢复入口，不删除紧张性昏迷疾病状态。
- 预热开始与生效之间正常/精神状态互换、精神实例替换、恢复回调产生新状态，实际只执行一个分支；生产组件 `Valid`/`CanApplyOn` 与能力执行前保护共同拒绝失格目标，不进入原版预扣冷却。
- 恢复与鼓舞共用同一能力冷却，效果不因原版已先扣冷却而失效；多猫刷新原激励实例的计时，清理重复鼓舞而不叠加。损坏定义在执行前拒绝，实际新状态缺计时组件时不留下永久鼓舞。
- 直接效果入口重新检查资格；不改变培养进度、各方向历史、亲昵冷却或其他技能。真实授予/移除组件只维护一份能力，跨方向同步及移除重复终极保留实例与冷却，最后终极移除后撤销。
- 冷却捕获为绝对截止tick；恢复立即授予并扣除凝胶存放时间，旧字段和过期时间就绪；无终极恢复清孤儿技能。抽取辅助清源猫标签、猫方向/历史与能力，保留其他方向、成果及技能。
- 真实任务只生成接近、到达后跳转复查、停止移动、一次原版 `CastVerb`；不调用基类可能结束扣冷却的Toil。远距行走不暂停、唤醒、启动冷却或标记施放；到达后才进入120 tick原版边界预热、暂停与唤醒，完成才结算。
- 移动目标保持角色引用；到达通知后目标移位返回接近。行走中及读条中失去资格、目标意识、同格位置或当前能力归属时取消，不结算；只清本人当前任务的本人Verb预热，不清其他任务、其他Verb或目标其他来源眩晕。成功后冷却已启动、无心情精神目标已恢复，`FinishedBusy`仍正常成功收尾。原版开始失败不主动结束任务；没有全身busy时，以及引擎独立中断或重置预热后，任务自然空收尾且不结算效果或冷却。
- 接近阶段停滞每60 tick有限补寻路，最多10次；正常移动清计数且无总行程时限，读条不补寻路或重复暂停。真实 `ExposeData` 使用 `sscCatComfortApproachIdleRetries` 并缺字段默认零，单独字段装载不启动路径/预热。
- 升级读档时 `PostLoadInit` 为原版新建的猫Verb补绑现有Ability；其他加载阶段不补绑，原能力实例、剩余冷却、预热及stance保持不变。
- 鼠标悬停使用地图选取资格显示原版目标圈和效果预览；合法远处目标显示技能光标与带目标名字的提示，非法目标不显示圈或提示并使用禁用光标。UI刷新不启动预热、不唤醒或暂停目标、不结算效果、经验和冷却，同格执行保护继续生效。
- 经原版 `Verb_CastAbilityTouch` 的动态调用入口，`IsApplicableTo` 同时执行地图资格与效果组件限制，保留显式请求的拒绝消息而在悬停时静默。目标在同一次悬停期间失去及恢复意识，目标圈、光标和名字提示随当前资格刷新；远处反馈始终不能直接开始预热或激活。

引擎宿主只提供可观察边界：原版Ability激活按已核验顺序先启动冷却再分发效果，MentalState记录恢复调用并允许恢复回调，Hediff容器执行真实能力授予组件的移除回调。`CastBoundaryStubs`模型化原版Toil调用顺序、120 tick预热、动态调用实际Verb的From检查、成功开始后有限眩晕/唤醒和结束通知；`CastVerb`忽略开始返回值，`FinishedBusy`按非全身busy完成。本宿主只建模预热stance这一种全身busy，不覆盖原版其它stance或完整任务调度。测试显式通知同格到达，路径容器仅保存目标引用及调用次数，不能据此证明真实寻路、目标寻路追踪、墙体绕行或游戏眩晕副作用。

重试存档用字典检查真实 `ExposeData` 的字段接线及旧默认值，Ability升级测试以替换Verb模拟原版重绑边界；两者都不等于完整真实Scribe/任务和stance存读档。`VisualBoundaryStubs` 按本机原版 Touch IL 建模高亮的 `IsValid`/`IsApplicableTo` 判断、光标的 `ValidateTarget` 判断与附加标签分发，不复制宠物筛选算法。`GenDraw.DrawTargetHighlight` 只记录目标引用，具体 Thing 中心、绘制高度、旋转及 `TargetHighlighter` 交给游戏实测。翻译替身保留名字参数，四语占位符另由XML检查。宿主不运行GPU、原版思绪/属性计算或Effecter爱心生成、持续、取消和读档调度。

完整人格快照、加工 `CopyFrom`、凝胶字段读写和提取/植入链路由 `PersonalityTraits` 执行真实生产入口另行验证。辅助函数边界测试不等于完整真实Scribe。鼠标圈/提示的实际画面，爱心仅在预热期间持续、取消后停止新增及读档不中途重启，实际精神状态恢复副作用、心情/工作倍率、存读档及实机迁移仍需游戏验收。
