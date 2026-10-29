# 子宫开发第 1～5 批原生专项

2026-10-29：[第三步整体复核](../../Docs/Development/Verification/生育特化第三步孕前设置实现与验证.md#提交前整体复核与本轮验证)新增实际Draw Special1项；`--preconception`13／13（Core10／Special3），Special独立3／3。执行真实Draw及共用按钮，隔离Widgets绘制／悬停末端，验证禁用active、置灰、外层禁用和底层误报点击不打开菜单，GUI状态恢复；不把它当作真实游戏显示通过。原孕期69、生育18及印记21也通过，同正式DLL指纹一致，待提交同步及实机；未执行完整宿主或整仓All。

2026-10-29：[第三步孕前设置](../../Docs/Development/Verification/生育特化第三步孕前设置实现与验证.md)新增`PreconceptionTests` Core10及原生UI Special2，`--preconception`12／12，Special独立2／2；同候选相关`--pregnancy`69、`--fertility`18和`--imprint`21均通过。默认完整入口也纳入新12项，未执行默认完整宿主或整仓All。验证身体字段／原生Scribe、阶段、孕态／临产、旧凭据、合法人格链和实际菜单；实际婴儿生成、世界存档／完整任务及游戏显示另验。首次越界身份夹具与并行共享文件失败保留原日志，见记录；未同步游戏。

2026-10-29：[显示与文案专项已获实机确认](../../Docs/Development/Completed/生育特化第二步效果展示与名称修订.md#界面与文案完成及实机验收关闭)并归Completed。末轮`--fertility`18／18与前轮结果保留原执行时点；本次只收尾文档，不重跑、重编或再次同步。下方网格／滚动待测描述为历史范围。

2026-10-29终极名单网格修订：同一布局Special用例补验紧凑基高、多列／窄栏单元格、框底内边距及GUI换行状态恢复，数量和名称不变。本轮`--fertility`18／18通过，真实居中标题／边框与滚动待实机；见[网格记录](../../Docs/Development/Completed/生育特化第二步效果展示与名称修订.md#终极化标题栏与多列名单)，未重跑历史人格模型或整仓All。

2026-10-29终极成果列表修订：复用原布局Special用例，改名为`final specialization rows include only retained achievements and reserve their full height`，覆盖所有现有终极记录及整份列表高度，数量不变。宿主补装原方向名称及共用状态语言文件；本轮`--fertility`18／18通过，首次夹具缺名称资源的17／18及后续修复见[记录](../../Docs/Development/Completed/生育特化第二步效果展示与名称修订.md#全部终极特化成果统一显示)。图形及真实滚动待实机，未跑整仓All。

2026-10-29显示补充：新增健康属性条目与悬停Core1项，`--fertility`现18项；普通有机身体夹具补齐肉体及尸体分类，保留真实健康列表与RJW成年判断。第二步原机制验收已关闭，新增显示另待实机。

2026-10-29复核：`--fertility`现17项（Core16、布局Special1），增加原账单拒绝／产物检查、最终消耗守卫及真实状态行测高；原课堂用例保留技能增益检查，同时验证零技能经验仍累计培养时间。下方14项为原实施时点。

2026-10-29：新增[生育第二步原生专项](FertilityTests.cs)14项Core，复用当前宿主及实际候选；支持尾参`--fertility`，`--pregnancy`仅执行原子宫阶段／孕期69项，`--imprint`仍仅执行第一步21项，缺省包含全部。验证范围及宿主边界见[实施记录](../../Docs/Development/Completed/生育特化第二步实现与验证.md)。本轮只运行相关分区，不将完整宿主默认范围或整仓All记为已执行。

2026-10-29反馈修正：新增主人排泄禁用与准备状态清理回归，本轮印记专项21／21；同时验证手术准入／执行、直接提取拒绝、残留状态与身份变化清理，主人侧绑定保留。凝胶名称显示取消锁后缀；新改动待实机复试。

2026-10-29复核：印记专项增加1项Core，确认人格清理移出旧印记时仍保留宿主主人侧缰绳、受方锁链实例及指向，本轮20／20通过。错误的主动解绑预期已撤回；见[复核与同步](../../Docs/Development/Completed/三态印记第一步实现与验证.md#第一部分复核与本机测试同步)。

2026-10-29：[三态印记第一步](../../Docs/Development/Completed/三态印记第一步实现与验证.md)新增 [ImprintTests](ImprintTests.cs) Core19项，本次通过19／19。复用原生游戏、生产DLL与Scribe宿主；在原两个参数后添加 `--imprint` 仅运行本批，默认入口也纳入这19项。专项覆盖实际三态属性、DLC条件、身份锁、种子与绑定分化、停龄、凝胶与健康存读档、复制及完整植入。图形、消息与地图共振维护为边界；完整世界／实机另验，本轮未跑原全部子宫用例或整仓All。

隔离候选验证沿用 `-p:SscAssemblyPath=<候选DLL绝对路径>`；执行格式为 `dotnet <宿主DLL> <仓库根> <游戏根> --imprint`。本次生产构建和宿主实际使用的候选哈希、版本、日志及待验情境见实现记录。

2026-10-28：[本轮精简](../../Docs/Development/Completed/测试199项与独立文案452项精简实现与验证.md)582→564，本次Core538／538、Special26／26；妊娠异常旧档减6，四语重复机制减12并由轻量键／参数校验接替资源覆盖。锁链／缰绳准备移出Core用例，两个分区可独立运行；保留初次Special失败及该套补验日志。复用原生产DLL，未跑整仓All，下方数字保留历史时点。

2026-10-28 [PNA营养不足提示](../../Docs/Development/Completed/PNA营养不足提示实现与验证.md)：实施轮582／582，既有577＋新Core5，实际生产DLL验证共用状态、关闭优先、精确支付边界、无食物需求／零成本、只读及满批次结算。原生与布局合计642／642，已同步本机并获维护者实机无问题确认，验收关闭；本轮只整理文档并提交，不重跑回归，旧记录保留原时点。

2026-10-28 [个人 PNA 生产启停](../../Docs/Development/Completed/个人PNA生产启停实现与验证.md)：前轮 **577／577**（原567＋新增 [PnaProductionTests](PnaProductionTests.cs) Core10）通过，验证实际生产暂停／恢复、营养与满批结算、牛特化催熟A段及乳汁效果保留、原生Scribe／旧档、载体／人格凭据与身体选择隔离。隔离候选与正式1.7 DLL的SHA-256均为 `e2c26ed188410de91e1288493596e46a5c59964c657e6d9a686734d52237c5cd`，日志 `.builds/pna-production-toggle/native.log`及两次构建零警告／零错误保留前轮时点。维护者现已整体确认实机没有问题并关闭验收；本轮只整理文档，不重编、重跑或再次同步。宿主的食物容量／地图生成末端／世界对象边界保留，整体反馈不补记世界存档、地图／旅队或第三方独立组合全部实测通过，未执行整仓All。

2026-10-28 [界面与通用Stat](../../Docs/Development/Completed/调教界面与高潮频率属性优化实现与验证.md)：前次**567／567**，新增Core3项验证无SSC组件角色、多个属性来源一次合成、无SSC引用余数恢复；原UI用例适配同行按钮／缩进／tooltip与健康说明简化。复用实际StatWorker和Scribe，不恢复逐语文案／镜像单测；候选后续已同步并获实机认可，本次仅文档收尾，不重跑机制测试。

2026-10-27 [第27项测试精简](../../Docs/Development/Completed/第27项测试精简实现与验证.md)后本套 **564／564**（Core526／Special38）：既有其他功能453、第27项身体55、倍率余数56。删除四语文案／镜像方法及登记，缩减重复组合和同类非法输入；关键倍率、余数、实际Scribe／SetupToils、自我调教与载体替换保留，不把断言塞入单一Check凑删减数。只重编测试宿主，复用第三批候选；下方625为历史执行结果，未同步或实机。

2026-10-27 第27项第四批四语文案：本次 **625／625**。四语各44项按确认的简中v1落实，原四语断言改为读取真实本地化档位名，不再要求包含“四／IV／4”；仍验证保存四档、实际二档及0.75参数。只重编测试宿主，复用第三批候选，不重编生产DLL；[本轮记录](../../Docs/Development/Completed/高潮敏感度第四批四语文案落实与验证.md)，未同步／实机、未跑整仓All。

2026-10-27 第三批提交前复核：**625／625**，新增Core4项，另纠正3项饱和算术预期；本批共新增76（Core75／Special1）。旧候选618／625仅7项余数边界失败；修正后通过，涵盖跳过原高潮、阈值处小数保存及真实事件清理。最终候选与当前实际依赖见[复核记录](../../Docs/Development/Completed/高潮敏感度第三批统一推进与余数恢复实现与验证.md#6-提交前复核2026-10-27)，未同步或实机。

2026-10-27 第27项第三批：**621／621**，新增 [OrgasmAdvanceTests](OrgasmAdvanceTests.cs) 72项（71核心／1性能专项）。使用实际完整候选及测试友元，移除健康补丁的重复源码链接，统一游戏Harmony3；实际SetupToils成功锚点、深存引用／坏载荷、原生重设、嵌套／内部捕获／Dispose失败、首次推进前再保存及实际SSC自我调教恢复均通过。Pawn世界目录与地图副作用是宿主边界，不替代完整世界存档／实机；日志在 `.builds/orgasm-sensitivity-batch3/`，详见[本批记录](../../Docs/Development/Completed/高潮敏感度第三批统一推进与余数恢复实现与验证.md)。

2026-10-27 第27项第二批提交前复核：**549／549**。新增真实健康回调改写最终档位的回归，在旧候选上548／549，仅该项失败；修正后全通过。当前RJW6.2.2已迁至1.7目录，宿主资源从实际已加载RJW DLL版本目录读取，复核使用游戏Harmony3（旧宿主2.4.2不足以满足新RJW的2.4.3引用）。可选环境变量 `SSC_TEST_BUILD_INFO_PATH` 指向显式隔离引用JSON，默认仍取正式记录；编译属性应与该JSON的引用目录一致，不修改正式BUILD-INFO。当前日志在 `.builds/orgasm-sensitivity-review/`，详见[复核依据](../../Docs/Development/Completed/高潮敏感度第二批身体载体与界面实现与验证.md#6-提交前复核2026-10-27)，未同步或实机。下方548／旧Harmony2及1.6路径说明保留原环境时点。

2026-10-27 第27项第二批：使用本次隔离生产候选，**548／548**（当前既有453＋新增95）。新增 [OrgasmSensitivityTests](OrgasmSensitivityTests.cs) 验证实际身体载体、原生XML／StatWorker／持续思想／Scribe／出生健康复制／成熟保持、配置故障、UI菜单及回调后旧凭据保护；四语4项划为Special，思想消费时复核无通知外部变动。共用图形度量、完整健康状态机与地图仍为宿主边界；新增健康补丁源码链接到测试Harmony2，业务仍取真实生产DLL。生产隔离构建零警告／零错误，未同步或实机，详见[本批记录](../../Docs/Development/Completed/高潮敏感度第二批身体载体与界面实现与验证.md)。下方计数保留历史时点。

2026-10-27：本套All459项、Core422项、Special37项。五项存档组合32→12，旧字段/载体/克隆回归保留。旧479项保留历史范围，见[本轮记录](../../Docs/Development/Completed/测试分级与数量精简实现与验证.md)。

2026-10-24 通用能力开关第三批：**479／479**（437＋42）。新增真实五项保存源的 32 种真假 Scribe 组合、全部旧字段默认、Comp／人格／四种 owner 替换、marker 对身体／关系隔离及原健康克隆例外。健康和训练组件分别使用原生节点恢复再组合，不能据此宣称完整 Pawn 世界跨引用存档通过；加载实际本批候选并核对哈希，见[第三批记录](../../Docs/Development/Completed/通用能力开关框架第三批生命周期复核与验证.md)。

2026-10-24 通用开关框架第二批：**437／437**（原 432＋5），使用本次隔离候选 DLL；公共五项注册、只读不补建、生育身体记录／实际倍率、原妊娠拒绝和牛特化四档收益均通过。新增可选 `-p:SscAssemblyPath=<候选 DLL 绝对路径>`，此模式不经生产 ProjectReference 重建正式 DLL，仍构建 Unity 边界；启动输出实际载入路径及 SHA-256，已与候选核对。正常默认 ProjectReference 模式保留，构建参数可沿用文末命令并增加该参数，见[本批验证](../../Docs/Development/Completed/通用能力开关框架第二批实现与验证.md)。

2026-10-24 医药类别调整：两项启动手术的药物筛选均采用原版 `Medicine` 类别；局部宿主补齐类别成员及类别库阶段标记，继续由真实 `ThingFilter.ResolveReferences` 解析生产资源。原生专项仍为 432／432，通过 `BuildProjectReferences=false` 仅重编宿主，生产 DLL／BUILD-INFO 不变；日志在 `.builds/surgery-medicine-category/`，未同步游戏。

2026-10-24：**357／357 通过**，包括前两批 100 项、第 3 批 92 项、第 4 批 81 项、第 5 批 20 项、早期文案本地化收尾 12 项、健康页倍率展示 15 项、永久泌乳生育抑制中和 20 项及三态描述 17 项。目标为 RimWorld **1.7.4985 rev1269**，游戏程序集 MVID `d368493b52bd4740bc7507c1b0386895`。不启动游戏，不改安装副本或用户配置。

通过 ProjectReference 编译当前生产工程，实际执行生产 XML 的原版加载、Hediff 创建／添加／合并／移除、原版 `StatPart_FertilityByHediffs.TransformValue`、健康缓存通知，以及 Scribe 的保存、交叉引用和 PostLoadInit。测试没有复制开发进度、生育状态或合并算法。

覆盖：无 Hediff 的旧身体、躯干挂载与初始 1% 存续、缺躯干拒绝、各未完成进度、满级默认关闭、两态切换与重复请求、不同角色状态隔离、SSC 永久泌乳按满级开启态中和、其他生育抑制正常叠加、隐藏的原版／RJW 妊娠拒绝关闭、已有妊娠不变、移除后不恢复、重复添加不相加、异常值、四种状态的真实保存读档、缺字段默认关闭、重复存档记录和无效挂载清理。

宿主只构造本批所需身体与 Def；图形资源、完整健康状态判定及能力脏通知被隔离。保留真实 Notify_HediffChanged 和 DirtyCache。RJW 妊娠登记取自真实 Humanlike／Bestial 类的关联属性，避免独立 CLR 扫描其旧版 UI 类型；实际 IsPregnant／KnownPregnancies／HasHediff 查询继续执行依赖原代码。测试没有执行 RJW 的完整启动、受孕或分娩。

第 2 批另从本机原版 `SurgeryFlesh`／`SurgeryOutcomeBase` 与生产研究、手术 XML 执行原版继承和交叉引用。实际运行生产资格报告、当前器官检查、成年判据、种族注入器、原版医疗账单及生产接单补丁。覆盖双前置、药物范围、重复启动拒绝、隐藏妊娠、医术 9／10、原版接单限制、等待期间身体变化，以及成功落地前再复查。

复查修复新增 15 项：实际调用原版配方技能判断，验证受控医护机械体固定技能 10 可接单并完成手术，技能 9、未受控、未启用医疗工作、普通角色缺技能和技能禁用仍拒绝。仅将夹具机械体的监管状态作为环境前提提供，不测试地图中的机械师监管系统。

菜单回归实际执行原版 `HealthCardUtility.DrawMedOperationsTab` 的菜单工厂及 `GenerateSurgeryOption`，不复制选项过滤或标签拼接。隔离账单绘制、字体测量并视为库存齐全，验证妊娠、身份不符、缺器官、已开发及原版 Adult 的 17 岁身体均产生带中文原因的不可点击项，且接单／结果继续拒绝；合格身体只有一个可用选项，无躯干没有伪造目标，错误部位继续拒绝。布尔接口只负责身体候选，完整资格报告不能用布尔值替代。

成功／失败通过控制质量稳定选支，`CheckSurgeryFail`、`GetOutcome` 及原版结果 `Apply` 继续执行。失败损伤只记录原版 `GiveRandomSurgeryInjuries` 请求，不执行完整伤害计算；信件、消息、事迹及地图预定清理隔离。原版直接死亡结果在概率 0 时实际调用并拒绝执行。实际执行 `CalculateIngredients` → `ConsumeIngredients` → `Bill_Medical.Notify_IterationCompleted`，验证 5 份浓缩 PNA 与 6 份混合药物在成功和失败时只消费一次、账单完成删除，且只有成功创建 1% 躯干记录。

药物质量单独执行原版 `SurgeryOutcomeComp_MedicineQuality`，其曲线来自原版结果定义，医疗效力取自当前游戏药物 XML。草药／工业药／闪耀药对应质量因子 0.88／1／1.18；三份草药加三份闪耀药为 1.03。上述是药物一项的质量乘数，不是最终成功率。

独立宿主使用可独立运行的 Harmony 2，生产游戏引用 Harmony 3；接单补丁通过不含业务逻辑的转发器挂到真实原版方法，避免读取跨版本特性，不复制生产判断。完整游戏中的自动特性扫描与其他补丁组合仍需实机确认。

第 3 批新增 [GrowthTests](GrowthTests.cs)、[GrowthFlowTests](GrowthFlowTests.cs)、[GrowthSaveTests](GrowthSaveTests.cs)。实际执行 RJW 接触对象、生产快照捕获与方向复查、统一成长预算、原生开发 Hediff 写入、三个完成出口及真实 Scribe；验证单次 10 个百分点、0.5 系数、旧恶堕／锁链倍率、剩余进度、反向插入、空接触、器官／任务替换、重复／重入、旧部位独立及没有每日总上限。

日常实际枚举生产 Toil 并执行最终完成闭包，原有认领和新成长运行，旧结算分量作为边界。普通场景使用生产守卫准备、Context 认领与 Dispatch，两个开发计划共用一次隔离随机状态的评分；旧六部位仍按 0.20 分摊，子宫独立采用 0.5。既有能力资格、PNA、反馈与原 RJW 结算是外部边界，不把它们的夹具前提当作实机能力验收。Start 提供原方法真实返回的前提进入生产守卫，不执行动画或完整 RJW Start。

仪式实际执行生产 OnPhaseComplete、阶段推进、整场认领及 Apply；地图活动／角色和质量由夹具提供，memo 同步调用真实结果处理器。覆盖六阶段整场一次、最后阶段才有接触、早期接触保留、空阶段、阶段中断、旧场清理、新 Lord、结果重入与失去归属；原有 SSC 后果、特化、图形和信件隔离，新成长与认领算法未替换。

存档执行真实 RJW／SSC 派生驱动 ExposeData 和基类保存后缀、快照与新字段的 Scribe，恢复到新的驱动对象并检查交叉引用、PostLoadInit、已领取不重发、缺字段／快照不补造、重复 Start 不补造，以及仪式已完成阶段列表往返。Pawn／器官／Lord 作为外部世界登记，原生 JobDriver 地图 Toil 恢复层隔离；仪式列表调用生产 partial 的序列化入口，不声称整套角色组件恢复已验收。独立 CLR 仅把 `rjw.SexProps` 类名定位到真实依赖类型，避免整程序集的旧 IMGUI 扫描，后续原生序列化不替换。

第 4 批新增 [PregnancyTests](PregnancyTests.cs)、[PregnancyFlowTests](PregnancyFlowTests.cs)、[PregnancySaveTests](PregnancySaveTests.cs)。通过原版 StatWorker 实际计算 30%／70%／100% 阶段与两态收益，验证旧身体状态相乘、角色隔离、移除和切换。StatDef 采用本机实名及正常基础值，隔离能力／装备等其他修正。

本机四个妊娠 XML 经原生 PatchOperation、继承及字段解析，保留原有 comps，实际创建生产组件。覆盖首次真实 Add 冻结待遇、未加入不提前判定、原先不合格后来开启不补发。原版真实 TickInterval 验证正常进度 ×1.25、保留饥饿倍率、连续 Tick、ageTicks 及达到终点当次进入临产。RJW 真实 Create<T>／Initialize／GetGestationPeriod／InitializeIfNoBabies 及 StartVanillaPregnancy 验证只应用一次、重复初始化不累乘、原有时长设置与 FertilityEnhancer 保留。四种妊娠真实 Scribe 恢复进度／判定／RJW 起止时间；旧字段缺失、未判定或坏枚举均为普通待遇，不补发。

因独立宿主 Harmony 2 与生产 Harmony 3 的 CodeInstruction 类型不同，直接链接同一生产孕期补丁源码，以 Harmony 2 构建和安装；无匹配或倍率算法副本，组件与工具仍来自生产 DLL。只在该测试文件局部抑制刻意同名链接的 CS0436。游戏 Harmony 3 的自动扫描与完整补丁组合仍待实机确认。食物类别、遗传与完整婴儿生成是显式边界；正常孕期计算保留，StartLabor 只检查调用，不执行其后地图／出生流程。Scribe 不重建完整胎儿 Pawn 列表或地图；真实健康与组件序列化及 PostLoadInit 保留，提供真实空 WorldPawns 容器。所需 RJW 类型名映射至实际依赖类型，不扫描其旧版 UI 模块。

第 5 批新增 [BodyManagementTests](BodyManagementTests.cs) 的 16 项实际生产程序集验证：只读准入和提交一致，三种 SSC 身份且无绑定时仍可操作，实际身份退出／恢复、绑定／换绑／解绑、器官移除／恢复与性别变化均保留躯干记录；移除后不恢复历史。实际调用现有克隆健康复制，验证严重度继承、新副本开关默认关闭及身体隔离；执行完全凝胶化健康清理和永久状态添加，保留原开发实例／进度／开关。后者仅隔离图形刷新及成功消息翻译，绑定提供真实空 GameComponent 容器，不执行完整地图限制系统。

本地化复核新增 [BodyManagementLanguageTests](BodyManagementLanguageTests.cs) 的 4 项：四语生产 Keyed XML 内容交给实际 LoadedLanguage／Translate，验证五参数预览、生育拒绝原因及已有泌乳翻译键，不显示键名、未替换参数或字面量换行。语言加载提供局部真实容器，不扫描完整模组语言目录或执行 Def 注入。原生专项现为 293／293。

这不证明完整模组环境中的行为、健康页、研究树排版、实际取料／麻醉／伤口、完整地图存档、分娩或开关 UI 已通过实机验收。阶段强化及孕期缩短已实现并完成本专项验证，共用身体管理栏第 5 批已实现，近似字体／输入布局检查另见 [BodyManagementLayout](../BodyManagementLayout/README.md)，真实页面仍待实机验收。

第 1～2 批文案收尾新增 [UterusStartupLanguageTests](UterusStartupLanguageTests.cs) 的 12 项：四语各执行创建拒绝、真实健康页禁用菜单与医生报告、原生成功及重复拒绝消息；医术提示与既有 SSC_Cosmetic_MedicineRequired 当前译文一致，成功进度参数正确。新增键来自真实生产 XML，实际翻译器查键／格式化保留，医疗和地图边界沿用旧专项。当前日志在 `.builds/uterus-development-startup-localization/`，见[收尾记录](../../Docs/Development/Completed/子宫开发第1～2批文案本地化与验证.md)。

已登记到 `Scripts/Test-All.ps1`。单独验证使用 .NET 10 SDK、实际游戏引用及测试侧 net9.0 Harmony；依赖路径可从 `1.7/BUILD-INFO.json` 读取：

```powershell
$repo = $PWD.Path
$info = Get-Content '1.7/BUILD-INFO.json' -Raw | ConvertFrom-Json
$sdk = '.builds/version-layout/sdk/dotnet/dotnet.exe'
$arguments = @('build', 'Tests/UterusDevelopmentNative/UterusDevelopmentNative.csproj',
    '-c', 'Release', "-p:GameRoot=$([IO.Path]::GetDirectoryName($info.gameAssemblyPath))",
    '-p:HarmonyAssemblyPath=<测试用 net9.0/0Harmony.dll 的绝对路径>')
foreach ($reference in $info.references) { $arguments += "-p:$($reference.name)=$($reference.path)" }
& $sdk @arguments
if ($LASTEXITCODE -ne 0) { throw '专项构建失败' }
& $sdk 'Tests/UterusDevelopmentNative/bin/Release/net10.0/UterusDevelopmentNative.dll' `
    $repo ([IO.Path]::GetDirectoryName($info.gameAssemblyPath))
```

隔离 Scribe 文件继续写入 `.builds/uterus-development-batch1/`；第 5 批日志保存在 `.builds/uterus-development-batch5/`，本地化收尾日志保存在 `.builds/uterus-development-startup-localization/`；前五批记录均保留原时点。测试构建会重建 1.7 生产 DLL；需要更新构建来源记录时，再运行 `Scripts/Build-Mod.ps1`，不要把 DLL 哈希不一致作为通过。

身体管理栏精简后，原生四语界面检查改为当前标题、开关和拒绝原因查键；旧两态预览已移出 ITab，不再测试其界面格式。阶段属性、孕期倍率等原生业务检查继续运行，当次为 305／305，后续专项见下文。

健康页生育倍率展示专项新增 15 项，当次累计 **320／320**。覆盖子宫开发各阶段及开关、原始永久泌乳 XML 阶段倍率、关闭产奶后倍率不变、兼容中性倍率、两项并存时各自展示、其他 Hediff 不变、无 Biotech 标签回退及四语真实查键／格式化。通过无业务转发器安装生产展示 Postfix，实际执行 HediffStatsUtility 与 TipStringExtra；测试宿主使用 Harmony 2，永久泌乳只装载原始阶段并隔离依赖游戏 Harmony 3 的产奶组件初始化，不据此声称完整产奶或补丁自动安装已实机通过。

永久泌乳生育控制新增 **20 项**，累计 **340／340**。安装实际 Hediff.CurStage getter 的生产后置逻辑，计算与说明均走原生 StatPart_FertilityByHediffs；未手写替代统计。覆盖无开发／培养／关闭／开启矩阵、多角色、共享源阶段全部其他字段不变、关闭与移除、失效躯干、其他抑制及零倍率、源配置动态变化和 HumanCattle 式中和、真实 Scribe 回环与缺字段旧档、妊娠锁定。实际永久泌乳组件夹具通过反射设置已有奶量和累计计时，验证查询与生育开关均不改这些字段、组件或产奶开关；不调用依赖游戏 Harmony 3 的 CompPostMake。四语检查实际健康悬停的 ×1、解除抑制说明及关闭后恢复 ×0.05。相关布局 92／92、永久泌乳组件 23／23，三套合计 455／455。日志位于 `.builds/lactation-fertility-override/`；未执行完整游戏或 Test-All。

三态描述新增 **17 项**，累计 **357／357**。直接调用生产 Hediff.Description 和原版 GetTooltip，验证三套正文、培养档共用、模式／语言即时切换、共享 Def 与角色隔离、无效或离体实例兜底、保存读取及缺开关旧档；悬停仍保留阶段名和生育倍率。只补齐原版 ColoredText 的语言初始化上下文，不替换悬停生成或描述选择算法。与布局 92／92 合计 449／449；此前泌乳组件 23 项保留上批时点。日志：`.builds/uterus-three-state-description/`。上述为自动验证生成时点；随后提交 `d2ecb6f` 已同步，维护者确认三态描述显示正常、本批显示验收关闭。正式文案仍待定，见[验收记录](../../Docs/Development/Completed/子宫开发Hediff三态描述实现与验证.md)。

2026-10-24 v2 定稿四语：本轮原生 **362／362**，含新增 5 项 [UterusDefInjectedTests](UterusDefInjectedTests.cs)。实际执行当前游戏的 DefInjectionPackage XML 加载及注入，覆盖研究／手术／四档阶段／扩展开启阶段、四语再回简中、健康悬停与研究提示译名一致；此前占位断言改为定稿／当前语言资源。本次以 BuildProjectReferences=false 只重编宿主，生产 DLL 和 BUILD-INFO 不变；布局另跑 92／92，共 454／454。记录见[四语落实](../../Docs/Development/Completed/子宫开发v2定稿四语落实与验证.md)，未同步游戏，不冒充完整游戏语言重载或四语排版验收。

## 胃部开发增量（2026-10-24）

现有原生宿主增加 [StomachDevelopmentTests](StomachDevelopmentTests.cs) 的 70 项，合计 432／432。胃部使用独立共享 BodyDef、真实胃／口部与 RJW 分类数据，保留旧子宫样本；XML／原版属性与医疗结果、严格接触与三个生产完成出口、普通／日常／仪式 Scribe、移除恢复、克隆、凝胶化及四语原版 DefInjected 均执行真实实现。人格过滤另外由 PersonalityTraits 的 4 项胃部增量验证，总计 135／135。

胃部占位为显式 WIP，非正式文案；局部语言宿主加载胃部 Keyed，保留原版查键／格式化及注入。RJW 分类初始化中未纳入宿主的 Def 在 bootstrap 阶段隔离日志，实际口部检查及接触判定不替换。损伤、地图／动画及完整健康界面依旧属于游戏边界，不据此声称实机通过。

本轮运行日志与统一回归：`.builds/stomach-development/`；实际实现、部署与待验情境见[胃部实现记录](../../Docs/Development/Completed/胃部恶堕化实现与验证.md)。

## 胃部 v1 中文定稿与四语落实（2026-10-24）

维护者确认 v1 中文稿后，已替换每语 10 键＋9 处 DefInjected 及两份基础 Def 中文，语言镜像同步。原有四语 WIP 断言改为逐键真实 Translate、原版 DefInjectionPackage 对全部九个字段的注入、四阶段悬停正文／名称和启动进度参数核对；测试总数不变，仍为 432／432。只重编宿主（BuildProjectReferences=false），生产 DLL 和 BUILD-INFO 不变。日志 `.builds/stomach-copy-v1/`，本轮按维护者要求提交，尚未同步游戏，完整游戏中的语言选择与排版仍待实机确认。
