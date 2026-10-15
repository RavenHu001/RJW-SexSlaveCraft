# PNA 文案重写交接

日期：2026-10-15。核查基线：`codex/pna-reset`，`cf47f75`。本文交接文件范围、修改方向和设定参考，不包含具体文案稿，也不授权改变现有机制或数值。

[当前规划索引](README.md) · [PNA 机制规划](PNA机制重置开发规划.md) · [现行数值与范围](PNA玩法效果与性交补给补全分析.md)

## 1 任务与术语

由文案负责人重写 PNA 的游戏内名称、描述、阶段、思绪与提示，使其符合模组的科幻情色、微械改造与主从共生背景。当前文字混入了更新调度、数值分流、人格转存等开发说明，部分物品和外语文案还保留早期占位内容。

已确定的称呼：

| 用途 | 称呼 |
| --- | --- |
| 普通物品正式名称 | 紫色纳米催情液 |
| 正文简称 | 纳米催情液 |
| 浓缩品正式名称 | 浓缩紫色纳米催情液 |
| 浓缩品简称 | 浓缩纳米催情液 |
| 液体中的纳米机械 | 可简称微械 |

PNA 缩写可在适合的短名称或技术信息中保留。不要再把“纳米液／纳米流体／纳米液体”轮流当作正文简称。除上述称呼外，本文不拟定新的阶段名、状态名或描述原文。

## 2 给文案作者的模组设定参考

本节讲世界内的物质、身体和人物关系，不是开发完成清单，也不要求作者把数值、阈值和代码术语写进游戏说明。

| 设定主题 | 写作参考 |
| --- | --- |
| 纳米催情液是什么 | 含有活性纳米机械的紫色液体。其强催情效果来自微械对身体与神经系统的刺激，属于模组身体改造设定的一部分。不要自行确定微械的制造者、历史起源或完整生物原理。 |
| 普通人的反应 | 欲望与性刺激带来的感受随体内浓度增强，痛觉逐渐迟钝；动作、注意力和知觉受到干扰。较强作用下出现幻听幻视，极高浓度能引发自发、不受控的连续高潮，最终昏厥。主线是刺激逐渐压倒身体的控制能力。 |
| 性奴为何不同 | 与锁链相应的身体改造使微械已在体内扎根。外来的纳米催情液进一步活化神经系统与体内微械，使身体更协调、轻快、清醒，情欲与活力共同增强，痛觉受到抑制。主线是被改造身体对外源微械的有益响应。 |
| 强弱与消退 | 浓缩品带来更强的刺激。体内积聚越多，作用越强；强烈活化也更快平息。药效可以逐渐消退，不是每次摄入都形成永久身体改造。 |
| 成瘾与戒断 | 神经系统长期适应这种刺激后，会持续渴求纳米催情液。停供时出现空虚、烦躁和身体不适，情欲反而更难平息，性方面也更加脆弱。再次补充可以缓解戒断，同时加深成瘾；长期摆脱供给可以恢复。 |
| 分泌与伴侣摄入 | 经过身体开发的产出者能够分泌纳米催情液。性交过程中，其分泌物也会使伴侣少量摄入。不能把这种供给写成只有某一种姿势、特定生殖器接触或插入行为才可能发生。 |
| 成瘾者的来源偏好 | 成瘾者偏好能供给纳米催情液的对象，是因为渴望其分泌物。这不是所有人都能感到的美貌加成，也不意味着对某位主人产生忠诚或专属依恋。 |
| 主人的反应 | 主人免疫这类药效与成瘾影响。免疫本身不等于天然具备分泌或侵染能力，也不等于免疫所有武器物理伤害；不要自行补出尚未确定的免疫原理。 |
| 肉体与人格 | PNA 药效残留、成瘾造成的神经适应与分泌能力依附于当前肉体，不随人格搬迁。该原则针对这些 PNA 身体状态，不能扩写成所有微械特化成果都与人格无关。 |

普通人的语气重点是刺激、迷离与失控；性奴的重点是活化、协调与力量感；成瘾的重点是身体习惯之后难以摆脱的渴求。三者不应被写成相同的泛化“愉悦增益”。

## 3 文风与信息分工

| 文本位置 | 改写方向 |
| --- | --- |
| 物品和健康状态描述 | 交代是什么、如何影响身体、主要后果及风险。用世界内因果解释，减少属性名罗列和实现术语。 |
| 五档阶段名 | 简短、递进，沿同一条身体表现或作用强度轴命名；避免剂量、活性、永久病变和病程词混杂。 |
| 思绪标题与描述 | 以角色当下的身体感受和情绪为主，采用第一人称体验。普通高档突出迷离，性奴高档突出清醒与活力，戒断突出缺失与渴求。 |
| 附加悬停 | 保留当前百分比和简短状态提示，不再复述整段健康说明。 |
| 成瘾通知、命中飘字 | 简短、容易识别；通知点明变化，飘字不承担长篇设定。 |
| 性吸引解释 | 点明观察者渴求对方的纳米催情液，同时保留原有数值解释字段。 |
| 配方、研究与工作提示 | 使用正式名称，说明材料处理、技术用途或正在进行的工作，不写占位名，不虚构解锁内容。 |
| 调试菜单与反馈 | 继续使用准确的技术表达，清除过期 P1／P2 阶段称呼即可，不需要沉浸化。 |

文风参照，不属于本次整套重写目标：

- [欲望共生](../../Defs/HediffDefs/SSC_MasterProgression.xml)：短句与微械回应的设定表达。
- [战斗员与战斗超频](../../Defs/HediffDefs/SSC_HediffDefs_CombatantSpecialization.xml)：微械、神经、身体表现之间的因果。
- [锁链说明与思绪](../../Defs/HediffDefs/HediffOfSexSlave.xml)：客观说明与主观体验的区别。
- [自我调教反馈](../../Languages/ChineseSimplified/DefInjected/ThoughtDef/SSC_SelfTrainingFeedback.xml)：角色视角的思绪写法。

人格流转、克隆、冻结、来源排除及详细数值放在玩家指南或机制资料中，不必在每个健康描述里重复。关键使用风险仍应让玩家读得明白。

## 4 中文基础文案文件清单

共 10 个源 Def 文件。文件中混有其他系统，修改范围以“涉及条目”列为准，不应整文件重写。

| 文件 | 涉及条目 | 为什么要改、方向是什么 |
| --- | --- | --- |
| [Defs/HediffDefs/HediffOfBuff.xml](../../Defs/HediffDefs/HediffOfBuff.xml) | `Hediff_PNA_Debuff`、`SSC_Hediff_PnaRootedLoad`、`PNA_Debuff_Thought`、`SSC_PnaRootedLoad_Thought` | 两种药效名称、说明、各五档阶段，以及十档心情。开发说明和属性清单较多，按普通催情／性奴活化分别建立递进与体验。 |
| [Defs/HediffDefs/HediffOfAddiction.xml](../../Defs/HediffDefs/HediffOfAddiction.xml) | `PNA_Addiction`、`PNA_Withdrawal_Thought` | 重写成瘾、戒断说明与思绪，突出神经适应和停供反应，减少输入、负荷及迁移术语。 |
| [Defs/HediffDefs/HediffOfTrain.xml](../../Defs/HediffDefs/HediffOfTrain.xml) | `SSC_Purple_HyperLactation` | 名称像生产状态，描述混入身体继承说明；应解释分泌能力及性交中的少量摄入，不让部位名称误导供给范围。 |
| [Defs/NeedDefs/NeedDefs.xml](../../Defs/NeedDefs/NeedDefs.xml) | `Need_PNA` | 统一需求名称与称呼，以身体渴求解释需求；读者应能理解条越满代表越得到满足，而非越渴求。 |
| [Defs/ChemicalDefs/ChemicalDef.xml](../../Defs/ChemicalDefs/ChemicalDef.xml) | `PNA_Chemical` | 化学品名称与药液、成瘾和需求术语对齐，避免暴露实现名称。 |
| [Defs/ThingsDefs/Nanofluid.xml](../../Defs/ThingsDefs/Nanofluid.xml) | `Purple_Aphrodisiac_Nanofluid`、`Purple_Aphrodisiac_Nanofluid_Plus` | 解释物质特性、普通／浓缩差别及风险；删除旧强化盔甲等缺乏对应内容的用途承诺。 |
| [Defs/ThingsDefs/SSC_Gun.xml](../../Defs/ThingsDefs/SSC_Gun.xml) | `Gun_PNA_Low`、`Bullet_PNA_Low` | 从分支判断说明改为武器工作方式和用途；准确表达普通对象的危险与有效锁链对象的活化、治疗。 |
| [Defs/DemageDefs/DemageDef_Gun.xml](../../Defs/DemageDefs/DemageDef_Gun.xml) | `Damage_PNA_Damage_LowTier` | 统一低阶弹丸伤害名称，避免直接展示 Low-tier PNA Damage 一类实现名称。目录拼写沿用现状。 |
| [Defs/RecipeDefs/RecipeDef_PNA_PLUS.xml](../../Defs/RecipeDefs/RecipeDef_PNA_PLUS.xml) | `SSC_Make_SuperDrug`、`SSC_Make_SuperDrug_forth` | 两份配方的 label、description、jobString 均需核对；说明浓缩工作，统一单份／四份命名，纠正外语占位和误译。 |
| [Defs/ResearchDefs/ResearchProjectDef.xml](../../Defs/ResearchDefs/ResearchProjectDef.xml) | `SSC_RES_PNASimpleApp`、`SSC_RES_PNASimpleLauncher`、`SSC_RES_PNAConcentration` | 研究描述简陋或缺失，应补技术背景和真实用途；不把研究节点名称等同于已经实际锁定某个配方。 |

## 5 现有翻译文件与镜像

根语言目录为 [Languages](../../Languages)。下表路径位于四个语言目录内，文件链接以英文为入口；“无”表示简中当前回退源 Def，不是漏列，也不要求为对称而新增文件。

| 语言目录内路径 | ChineseSimplified | ChineseTraditional | English | Russian | 对应内容 |
| --- | --- | --- | --- | --- | --- |
| [DefInjected/HediffDef/HediffOfBuff.xml](../../Languages/English/DefInjected/HediffDef/HediffOfBuff.xml) | 有 | 有 | 有 | 有 | 两种药效、阶段名称 |
| [DefInjected/ThoughtDef/HediffOfBuff.xml](../../Languages/English/DefInjected/ThoughtDef/HediffOfBuff.xml) | 有 | 有 | 有 | 有 | 十档药效心情 |
| [DefInjected/HediffDef/HediffOfAddiction.xml](../../Languages/English/DefInjected/HediffDef/HediffOfAddiction.xml) | 有 | 有 | 有 | 有 | 成瘾与戒断 |
| [DefInjected/ThoughtDef/HediffOfAddiction.xml](../../Languages/English/DefInjected/ThoughtDef/HediffOfAddiction.xml) | 无 | 有 | 有 | 有 | 戒断心情 |
| [DefInjected/HediffDef/HediffOfTrain.xml](../../Languages/English/DefInjected/HediffDef/HediffOfTrain.xml) | 无 | 有 | 有 | 有 | 分泌状态 |
| [DefInjected/NeedDef/NeedDefs.xml](../../Languages/English/DefInjected/NeedDef/NeedDefs.xml) | 无 | 有 | 有 | 有 | 渴求需求 |
| [DefInjected/ChemicalDef/ChemicalDef.xml](../../Languages/English/DefInjected/ChemicalDef/ChemicalDef.xml) | 无 | 有 | 有 | 有 | 化学品名称 |
| [DefInjected/ThingDef/Nanofluid.xml](../../Languages/English/DefInjected/ThingDef/Nanofluid.xml) | 无 | 有 | 有 | 有 | 两种药液 |
| [DefInjected/ThingDef/SSC_Gun.xml](../../Languages/English/DefInjected/ThingDef/SSC_Gun.xml) | 有 | 有 | 有 | 有 | 发射器与弹丸 |
| [DefInjected/DamageDef/DemageDef_Gun.xml](../../Languages/English/DefInjected/DamageDef/DemageDef_Gun.xml) | 无 | 有 | 有 | 有 | 伤害名称 |
| [DefInjected/RecipeDef/RecipeDef_PNA_PLUS.xml](../../Languages/English/DefInjected/RecipeDef/RecipeDef_PNA_PLUS.xml) | 无 | 有 | 有 | 有 | 两份浓缩配方与工作提示 |
| [DefInjected/ResearchProjectDef/ResearchProjectDef.xml](../../Languages/English/DefInjected/ResearchProjectDef/ResearchProjectDef.xml) | 无 | 有 | 有 | 有 | 三项 PNA 研究 |
| [Keyed/SSC_Pna.xml](../../Languages/English/Keyed/SSC_Pna.xml) | 有 | 有 | 有 | 有 | 悬停、通知、飘字、吸引解释与调试反馈 |

按当前文件结构，简中 5 个、繁中／英文／俄文各 13 个，共 44 个根语言文件；[Sexslavecraft/Languages](../../Sexslavecraft/Languages) 下有同路径的 44 个镜像文件。源 Def 中文、简中已有覆盖和另外三语须一起对齐，不能只改其中一层。

已发现的外语问题包括浓缩品仍使用 Plus，浓缩配方使用 Make Super Drug，以及把四份写成 Forth／第四个。具体改写与翻译由文案负责人决定，但不得为修正玩家文字而重命名历史 DefName。

## 6 短提示、共享文本与调试文件

### 6.1 四语 Keyed/SSC_Pna.xml

| 键 | 用途 | 修改方向与参数约束 |
| --- | --- | --- |
| `SSC_Pna_LoadTooltip` | 普通药效附加悬停 | 简短状态提示，避免复述长描述；`{0}` 为已格式化的负荷百分数，保留百分号，不再换算。 |
| `SSC_Pna_RootedLoadTooltip` | 性奴活化附加悬停 | 表现活化与简短状态，同样保留 `{0}` 百分数。 |
| `SSC_Pna_NewDependence` | 首次成瘾通知 | 点明人物发生的变化；`{0}` 为角色简称，键名继续保留旧命名。 |
| `SSC_Pna_WeaponHealed` | 治疗飘字 | 维持短词，不承担长篇设定。 |
| `SSC_Pna_WeaponActivated` | 活化飘字 | 与性奴活化术语一致，保持短。 |
| `SSC_Pna_AttractionExplanation` | RJW 吸引解释 | 体现成瘾者渴求对方供给；`{0}` 是本项乘数，`{1}` 是本项运算后 factor，不是概率或总吸引值。 |
| `SSC_Pna_SelectPawn`、`SSC_Pna_InvalidTarget`、`SSC_Pna_Rooted`、`SSC_Pna_DebugResult` | 仅调试动作反馈 | 使用清楚准确的技术语言，清除 P1／P2 旧称；Debug 来源不加深已有成瘾，不能将调试反馈当作正常摄入规则。 |

### 6.2 其他单列文件

| 文件范围 | 处理内容 |
| --- | --- |
| 四语 [Keyed/SSC_Keys.xml](../../Languages/ChineseSimplified/Keyed/SSC_Keys.xml) 及对应镜像 | 仅检查 `SSC_Produce_Progress`、`SSC_Produce_ProgressStalled` 两键，共 4 个根文件和 4 个镜像。它们也被其他产出逻辑复用，不宜改为 PNA 专属叙述。 |
| [Sexslavecraft/common/DebugActions_SSCPna.cs](../../Sexslavecraft/common/DebugActions_SSCPna.cs) | 五个开发菜单名称写在 C# 属性字符串中，仍有 P1／P2。若一并整理，只改名称字符串；这部分需要负责落地的开发者重编，单改翻译 XML 无法覆盖。 |

## 7 不应扩大的范围与落地约束

- 本次是文案任务，保留全部 DefName、翻译键、阶段索引、占位符、数值、组件与逻辑。
- `PNA_Addiction` 普通第 0 档继续无 label。原版括号百分比是恢复进度，不要为了“补全两档名称”而增加标签或改写其含义。
- 普通高浓度的高潮昏厥是既定设定；不要进一步承诺独立周期高潮事件、动画、真正更容易触发 RJW 高潮或专属精神状态。
- 性奴强化与实际有效锁链对应，不应泛化成只选性奴身份就一定产生；普通药效与供给者偏好也不意味着忠于某位主人。
- 主人侵染属于另一个专项，不在药液、成瘾或分泌说明中写成所有主人已经拥有的能力；不为兔分身增加当前性交供给承诺。
- `HediffOfBuff.xml` 内旧两种战斗 Buff 和隐藏抗性记录、`SSC_Gun.xml` 内注释禁用的高阶武器，保留兼容／历史用途即可，不作为当前玩法宣传。隐藏记录若清理措辞，保持简短准确。
- 锁链、主人共生、战斗员特化、人格排泄和其他手术不因提到 PNA 就整套重写；只对直接相关的称呼或事实冲突做必要核对。
- [玩家指南](../../机制详解.md)、[英文指南](../../PLAYER_GUIDE_EN.md)及历史验收文档继续承担规则与追溯职责，本交接不要求将其改成设定叙事，也不回写历史测试、版本或 DLL 记录。

后续落地人员应检查 XML 格式、翻译键与占位符、源 Def／语言覆盖的一致性，并通过[语言镜像脚本](../../Scripts/Sync-LanguagesMirror.ps1)检查或同步镜像。文案作者无需改动核心 C#；除了明确单列的调试菜单名称，其余正常玩家文本均可在 XML／语言资源中处理。

## 8 交接产物

文案负责人提供覆盖上述条目的中文定稿及相应语言交付；具体名称、阶段、描述、思绪与短提示由其编写。落地人员依文件清单替换文字并保持资源结构，检查效果和术语一致性。本文自身不含可直接替换的游戏文案，也不表示任何文案已经重写或验收。
