> **废案（2026-10-22）**：维护者判定本稿与测试稿-2 无本质区别、不采用，仅作留档；不得据此回填 XML、翻译或重新提交。当前有效的文化文案以维护者选定的新稿为准。

# 主奴秩序中文文案测试稿 3（1.7）

更新日期：2026-10-22　编写模型：deepseek-flash（DeepSeek Harness）　范围：仅 1.7。对应[文案交接清单](../文化-主奴秩序文案维护清单.md)与[戒律与职位设计](../../../Design/Archive/文化-主奴秩序戒律与职位设计.md)。

本稿只写简体中文正文。Def 结构、数值、权重、索引、`include`、`priority`、规则左侧语法一律不动；落地时把表中值同时写入 Def 内联文本与 `1.7/Languages/ChineseSimplified/DefInjected/`，再同步 `Sexslavecraft/Languages` 镜像。英文、繁体、俄文与其余三语镜像不在本稿范围。

主题：欲望、堕落、沉溺、支配与被支配。写法沿用本模组既有文风——直白、带身体感、第一人称或世界内因果，不写属性名、不写“心情 +X”（数值只出现在戒律预览的阶段说明里）。露骨可以，但不做器官清单式堆砌，重心放在被握住、被驯、沉下去的那一下。

---

## 0 清单解析与写作口径

### 0.1 范围与数量

| 清单条目 | 运行 Def | 需编纂内容 | 数量 |
| --- | --- | --- | --- |
| 模因 | [SSC_Meme_MasterSlaveOrder.xml](../../../../1.7/Defs/MemeDefs/SSC_Meme_MasterSlaveOrder.xml) | 模因描述 1、命名包 3×4、组合词 3、称谓／仪式候选 9、背景段落 17 | 42 |
| 五类议题戒律 | [PreceptDefs/SSC_Precepts_SexSlave.xml](../../../../1.7/Defs/PreceptDefs/SSC_Precepts_SexSlave.xml)、[Trainer](../../../../1.7/Defs/PreceptDefs/SSC_Precepts_Trainer.xml)、[Master](../../../../1.7/Defs/PreceptDefs/SSC_Precepts_Master.xml)、[Counts](../../../../1.7/Defs/PreceptDefs/SSC_Precepts_Counts.xml) | 戒律描述 22、阶段效果说明 19 | 41 |
| 心情／社交 | [ThoughtDefs/SSC_CultureThoughts_SexSlave.xml](../../../../1.7/Defs/ThoughtDefs/SSC_CultureThoughts_SexSlave.xml)、[Trainer](../../../../1.7/Defs/ThoughtDefs/SSC_CultureThoughts_Trainer.xml)、[Master](../../../../1.7/Defs/ThoughtDefs/SSC_CultureThoughts_Master.xml)、[Counts](../../../../1.7/Defs/ThoughtDefs/SSC_CultureThoughts_Counts.xml) | 53 个阶段的名称 53＋描述 53 | 106 |
| 议题名称 | 四个 PreceptDef 内的 `IssueDef` | 仅校对，名称已定 | 0（校对 5） |
| 连接规则 | [SSC_CultureNamers.xml](../../../../1.7/Defs/RulePackDefs/SSC_CultureNamers.xml) | 无文学文本，仅校对左侧与优先级 | 0（校对 2） |
| 合计 | | | **189** |

189 个中文值＝模因 42＋戒律 41＋心情 106。议题名称与连接规则不在其中，只做同步校对。

### 0.2 键值形态（落地必读）

本文化的中文是**源文**：Def 内联中文与 DefInjected 中文逐字相同。

1. 普通字段：Def 内联写 `<label>`／`<description>`；DefInjected 键为 `DefName.label`／`DefName.description`，值为同一段中文。
2. 心情阶段：Def 内联写 `<stages><li><label>/<description>`；DefInjected 键为 `DefName.stages.N.label`／`.description`。**阶段号是机制契约，不得增删或调序。**
3. 规则行：`meme.generalRules.rulesStrings.N` 与 `meme.descriptionMaker.rules.rulesStrings.N` 的**值是整行规则**（左侧语法＋`->`＋正文），不是纯正文。落地时左侧逐字节照抄 Def 现值，只替换箭头右侧；DefInjected 键不变、值换成同样整行。
4. 戒律预览：`PreceptName.comps.N.thoughtStageDescriptions.M` 的值是**一段带数值的预览说明**，替换后 Def 内联与 DefInjected 都要改。

三种形态的样例：

```xml
<!-- 1) 普通字段（Def 内联 / DefInjected 同值） -->
<description>欲望不是要被压住的东西，是要被理顺的东西。</description>

<!-- 2) 心情阶段（阶段号不动） -->
<li>
  <label>沦为性奴</label>
  <description>我竟成了这副样子……</description>
  <baseMoodEffect>-18</baseMoodEffect>
</li>

<!-- 3) 规则行（DefInjected 中用 &gt; 转义箭头） -->
<li>memeAdjective->主奴</li>
<li>creation(tag=meme_SSC_MasterSlaveOrder)->[deity0_name]用第一根锁链量过每一具身体……</li>
```

写入 XML 时 `->` 在 DefInjected 里必须写成 `-&gt;`，在 Def 内联里写 `->`。正文中若出现 `&`、`<`、`>` 需转义；本稿的文案值已避开这三个字符。

### 0.3 术语

| 概念 | 用法 |
| --- | --- |
| 性奴 | 本模组身份，不用“奴隶”“奴仆” |
| 主人 | 持有主人资格者，不写“奴隶主” |
| 训导官 | 代主人调教其他性奴的性奴；“任职”用作状态词 |
| 调教 | 统一动作词，不用“训练” |
| 锁链／链子 | 绑定关系的具象；可作“拴”“扣上”“握” |
| 恶堕 | 数值名，正文写“沉下去”“堕落”，不直接写数值 |
| 场景词 | 鞭、项圈、链印、跪、膝、喉、腰、后颈、喘、湿、发烫、发软、被用、灌满、失神 |

对角色统一用“它”，与模组既有文本一致。

### 0.4 叙事模板变量白名单

17 段背景只能用下列变量；已逐项对照本机 1.7.4985 rev1269 的原版 `descriptionMaker` 规则，均为原版实际使用过的符号。写中文时**变量两侧不留空格**，否则替换后会多出空格。

| 变量 | 含义 | 本稿用在哪段 |
| --- | --- | --- |
| `[deity0_name]` | 神祇名 | creation、episode、intro、conflict、victory |
| `[deity0_possessive]` | 神祇的（所有格） | intro |
| `[founderName]` | 创始人名 | founderJoin、victory、setup、story |
| `[place_name]` | 定居地名 | intro、founderJoin、setup |
| `[place_job]` | 职业 | founderJoin |
| `[foeLeader]` | 敌对领袖 | conflict、victory |
| `[foeLeader_possessive]` | 敌对领袖的（所有格） | conflict |
| `[foeSoldiers]` | 敌方士兵（复数） | victory |
| `[memberNamePlural]` | 本文化信徒（复数） | conflict、founderJoin、victory |
| `[inTheEnd]` | 终局引语 | archistProphecy |
| `[itIsSaid]` | 据说／有传 | animistProphecy |

原版框架与变量要配套：神祇类变量只出现在 Theist 两框架，创始人变量只出现在 Ideological 两框架，`[inTheEnd]`／`[itIsSaid]` 只出现在 Archist／Animist。变量解析失败会整段丢弃，因此不做跨框架混用。

### 0.5 旧稿取舍

此前有两份中文草稿（现文件名：`文化-主奴秩序文案测试稿-1-1.md`、`文化-主奴秩序文案测试稿-2.md`），命名包与称谓池不一致：

- 测试稿-1-1：主奴道／锁链教／沉沦道；执鞭者／主君／大主人；首席性奴／引戒者／调教祭司；锁链契约／献身礼／初驯之仪。
- 测试稿-2：锁链教／纵欲圣教／臣服教义；大主人／执鞭者／主上；欲望祭司／沉沦引导者／驯心者；锁链契约／臣服礼／沉沦圣礼。

本稿采用并收敛第二套（更贴合欲望／沉沦主题），把“臣服教义”改为“沉沦道”，使三套命名包分别落在**锁链（秩序）—纵欲（欲望）—沉沦（堕落）**三条线上，与主题三项一一对应。

道德导师称谓另按[戒律与职位设计](../../../Design/Archive/文化-主奴秩序戒律与职位设计.md)中 2026-10-22 的维护者决定处理：放弃独立职位，「首席性奴」降为道德导师称谓池三选之一，与另两条并列随机。因此本稿的道德导师池是**首席性奴／欲望祭司／沉沦引导者**，旧稿的“驯心者”不再保留。称谓只影响显示，不改任职条件。

旧稿保留作历史参考；若采用本稿，以本稿的取值落地。

---

## 1 模因 `SSC_Meme_MasterSlaveOrder`

### 1.1 名称与描述

| 键 | 中文 |
| --- | --- |
| `SSC_Meme_MasterSlaveOrder.label` | 主奴秩序 |
| `SSC_Meme_MasterSlaveOrder.description` | 欲望不是要被压住的东西，是要被理顺的东西。有人生来该握住那根链子，有人生来就该跪在链子的另一头。把身子交出去、被驯到只会发抖的时候，人才终于尝到自己本来是什么味道。 |

### 1.2 命名包 `symbolPacks`

DefInjected 键：`SSC_Meme_MasterSlaveOrder.symbolPacks.<包号>.<字段>`。

| 包 | `ideoName` | `theme` | `adjective` | `member` | 取向 |
| --- | --- | --- | --- | --- | --- |
| 0 | 锁链教 | 锁链 | 执链 | 链徒 | 秩序：链子一扣，人就安分 |
| 1 | 纵欲圣教 | 纵欲 | 溺欲 | 欲众 | 欲望：把渴求供成教义 |
| 2 | 沉沦道 | 沉沦 | 堕落 | 沉沦者 | 堕落：越陷越深才算到家 |

### 1.3 通用规则 `generalRules`

DefInjected 键：`SSC_Meme_MasterSlaveOrder.generalRules.rulesStrings.<索引>`，值为整行规则。

| 索引 | 规则左侧（逐字保留） | 中文值 | 说明 |
| --- | --- | --- | --- |
| 0 | `memeAdjective` | 主奴 | 文化形容词 |
| 1 | `memeCreed` | 锁链 | 信条 |
| 2 | `memeHyphenPrefix` | 驯 | 连字前缀，单字以免出现生硬的“驯-” |
| 3 | `r_leaderTitle(priority=10)->[SSC_LeaderPreset]` | **保持原样** | 入口规则，不是文案 |
| 4 | `SSC_LeaderPreset` | 大主人 | 领袖称谓 1 |
| 5 | `SSC_LeaderPreset` | 执鞭者 | 领袖称谓 2 |
| 6 | `SSC_LeaderPreset` | 主上 | 领袖称谓 3 |
| 7 | `SSC_MoralistPreset` | 首席性奴 | 道德导师称谓 1（按维护者决定收回此名，非独立职位） |
| 8 | `SSC_MoralistPreset` | 欲望祭司 | 道德导师称谓 2 |
| 9 | `SSC_MoralistPreset` | 沉沦引导者 | 道德导师称谓 3 |
| 10 | `SSC_BindingRitualPreset` | 锁链契约 | 契约仪式名 1 |
| 11 | `SSC_BindingRitualPreset` | 臣服礼 | 契约仪式名 2 |
| 12 | `SSC_BindingRitualPreset` | 沉沦圣礼 | 契约仪式名 3 |

落地后第 4～12 行的整行分别为 `SSC_LeaderPreset->大主人`、`SSC_MoralistPreset->欲望祭司`、`SSC_BindingRitualPreset->锁链契约` 等；索引 3 那一行原样保留，不要替换成中文。

### 1.4 背景段落 `descriptionMaker`（17 段）

DefInjected 键：`SSC_Meme_MasterSlaveOrder.descriptionMaker.rules.rulesStrings.<索引>`，值为整行规则。

| 索引 | 段落规则（左侧逐字保留） | 中文段落 |
| --- | --- | --- |
| 0 | `creation(tag=meme_SSC_MasterSlaveOrder)` | `[deity0_name]`用第一根锁链量过每一具身体，教它们跪、教它们侍奉，也教它们在被人握住后颈的那一刻，第一次尝到安心。 |
| 1 | `episode(uses=1,tag=meme_SSC_MasterSlaveOrder)` | 最早的人不肯跪下，`[deity0_name]`便让他们在夜里互相渴求到发抖，直到他们自己开口，求一根链子把这份烧灼圈住。 |
| 2 | `intro(tag=meme_SSC_MasterSlaveOrder)` | `[place_name]`最初的定居者为一件事吵了整季：谁该跪，谁该握绳。`[deity0_name]`没有裁决，只把`[deity0_possessive]`手按在争吵者颈后，让他们的膝盖自己软下去。 |
| 3 | `conflict(tag=meme_SSC_MasterSlaveOrder)` | 有个叫`[foeLeader]`的人来布道，说身体只属于它自己。`[deity0_name]`便把`[foeLeader_possessive]`追随者交到`[memberNamePlural]`手里；三天后，那些人连一个“不”字都说不完整了。 |
| 4 | `founderJoin(tag=meme_SSC_MasterSlaveOrder)` | 一名叫`[founderName]`的`[place_job]`跪到创始人脚边，把自己的喉咙、腰和膝一并奉上，起身时，已是`[place_name]`最早的`[memberNamePlural]`之一。 |
| 5 | `victory(tag=meme_SSC_MasterSlaveOrder)` | `[founderName]`赢下那场争执。`[foeLeader]`的`[foeSoldiers]`被牵进院子，锁链扣上时没有一个人反抗——它们终于明白，被驯服比自由更让人腿软。 |
| 6 | `setup(tag=meme_SSC_MasterSlaveOrder)` | `[founderName]`在`[place_name]`立起第一排屋子，也立下第一条规矩：在这块地方，每一具身体都该找得到握住它的那只手。 |
| 7 | `story(uses=1,tag=meme_SSC_MasterSlaveOrder)` | 有个殖民者死活不肯戴项圈。`[founderName]`没有强迫，只让人每晚在它门外调教别的性奴。第七夜，它自己爬进去，把脖子凑到了锁链下面。 |
| 8 | `lessonIntro(tag=meme_SSC_MasterSlaveOrder)` | 我见过一处被`[memberNamePlural]`管得极好的殖民地，床边跪着一名性奴，膝头红肿，喘息却安稳。看着它，我才明白 |
| 9 | `lesson(tag=meme_SSC_MasterSlaveOrder)` | 身体不会因为自由而安宁，只会因为被握住而安静下来 |
| 10 | `lessonReinforcement(tag=meme_SSC_MasterSlaveOrder)` | 后来我也戴上了项圈。被人牵着走、被人按着用，反倒是我这辈子睡得最沉的几夜。 |
| 11 | `archistBasis(tag=meme_SSC_MasterSlaveOrder)` | 超凡机械算得尽万物，却算不出一具身体跪下去时，腰为什么会自己塌。 |
| 12 | `archistFact(tag=meme_SSC_MasterSlaveOrder)` | 主人是比超凡机械更古老的造物主：机械支配物质，主人支配那点让物质发烫的欲望。 |
| 13 | `archistProphecy(tag=meme_SSC_MasterSlaveOrder)` | `[inTheEnd]`，超凡机械会从人类的锁链里学会最后一样东西——服从，并跪向那只教会它支配的手。 |
| 14 | `animistBasis(tag=meme_SSC_MasterSlaveOrder)` | 肉体的精魂从不渴望自由。它们只想找到一只可以贴上去的手，一个能把自己按住、钉住、灌满的位置。 |
| 15 | `animistFact(tag=meme_SSC_MasterSlaveOrder)` | 性奴每跪下一次，精魂就安静一分；主人每握住一条链子，便是在替这世界把散开的欲望重新系紧。 |
| 16 | `animistProphecy(tag=meme_SSC_MasterSlaveOrder)` | `[itIsSaid]`，当最后一具不肯跪的身体也软下去，所有精魂会聚成一根链子，把整个世界拴在同一只手上。 |

第 8、9 段是一句话的两半：`lessonIntro` 结尾不加标点，直接接 `lesson`；`lesson` 结尾也不加句号，由 `lessonReinforcement` 另起一句。不要给这两段补句号，否则拼出来会断开。

---

## 2 议题名称（校对，不改）

| DefInjected 键 | 现值 | 结论 |
| --- | --- | --- |
| `SSC_Issue_SexSlave.label` | 性奴 | 保留 |
| `SSC_Issue_Trainer.label` | 训导官 | 保留 |
| `SSC_Issue_Master.label` | 主人 | 保留 |
| `SSC_Issue_MasterCount.label` | 主人数量 | 保留 |
| `SSC_Issue_SlaveCount.label` | 性奴数量 | 保留 |

五个名称与戒律档位的“尊重／尊敬／尊奉”分工一致：性奴议题用“尊重”，训导官议题用“尊敬”，主人议题用“尊奉”，三档不混用。

---

## 3 戒律描述（22 条）

`defName` 列对应 DefInjected 的 `defName.label` 与 `defName.description`；名称列为既有值，只校对不改。

### 3.1 性奴议题 `SSC_Issue_SexSlave`

| defName | 名称（保留） | description |
| --- | --- | --- |
| `SSC_Precept_SexSlave_Hostile` | 敌视 | 一具好好的身体，被链子拴着，被按在身下调到只会张腿发抖，还要被人称作福气。这种事不该存在，该连根拔掉。 |
| `SSC_Precept_SexSlave_Revulsion` | 厌恶 | 跪在别人脚边还一脸受用，被用到失神、被灌到打颤，还主动把腰送上去。光是听见那种声音，我就反胃。 |
| `SSC_Precept_SexSlave_Indifference` | 漠视 | 谁院里拴着谁、夜里怎么用，与我无关。只要那点响声别翻过我的墙。 |
| `SSC_Precept_SexSlave_Respect` | 尊重 | 性奴也是人。跪着、被用着，是它和主人之间的事；该给它的体面，一分不能少。 |
| `SSC_Precept_SexSlave_Sacred` | 神圣 | 性奴不是被毁掉的人，是被选中的人。身子被一寸寸打开、灌满，驯到只会发抖，跪下去那一刻，它比谁都干净。被用得越深，越接近它生来该有的样子。 |
| `SSC_Precept_SexSlave_Envy` | 艳羡 | 被那样驯着，未必是坏事。看它跪着，膝头发软、腰自己往下塌，谁心里没生过一丝羡慕。那大概才是身体最想过的日子。 |

### 3.2 训导官议题 `SSC_Issue_Trainer`

| defName | 名称（保留） | description |
| --- | --- | --- |
| `SSC_Precept_Trainer_Hostile` | 敌视 | 让一条性奴去管教另一条性奴，是对次序的亵渎。鞭子只该握在主人手里；奴举起鞭子，只会把整座院子弄脏。 |
| `SSC_Precept_Trainer_Revulsion` | 厌恶 | 脖子上还留着别人的链印，转身就去掰开下一条的腿。这种东西也配谈调教？ |
| `SSC_Precept_Trainer_Indifference` | 漠视 | 训导官是谁、怎么调教，与我无关。别把那根鞭子的响声甩进我院里就行。 |
| `SSC_Precept_Trainer_Reverence` | 尊敬 | 训导官替主人分担调教之劳。它懂得怎么让一具身体跪稳，怎么让叫声自己漏出来——这份手艺是练出来的，该敬。 |
| `SSC_Precept_Trainer_SacredOffice` | 圣职 | 被主人亲手挑出来执鞭的性奴，担的是一份圣职。它把自己受过的驯服原样交下去：怎么跪、怎么湿、怎么把身子交出去。被它调教，是仅次于被主人亲自调教的荣幸。 |

### 3.3 主人议题 `SSC_Issue_Master`

| defName | 名称（保留） | description |
| --- | --- | --- |
| `SSC_Precept_Master_Hostile` | 敌视 | 自称主人的人，都是趴在别人身体上的贼。锁链不该握在任何一只手里。 |
| `SSC_Precept_Master_Revulsion` | 厌恶 | 攥着根鞭子就以为自己是个角色。把鞭子抽走，它什么都不是。 |
| `SSC_Precept_Master_Indifference` | 漠视 | 主人不主人，与我无关。别拿那套规矩来管我。 |
| `SSC_Precept_Master_Deference` | 尊奉 | 主人该受敬重。它名下的身子，只该听它的话——那是它一鞭一鞭挣来的位置。 |
| `SSC_Precept_Master_HolyMaster` | 圣主 | 主人不是管事的，是这块地方供着的那一位。它一句话重过一切；为它跪下、为它张开、把身子交出去，不是屈辱，是荣耀。 |

### 3.4 数量议题 `SSC_Issue_MasterCount` / `SSC_Issue_SlaveCount`

| defName | 名称（保留） | description |
| --- | --- | --- |
| `SSC_Precept_MasterCount_One` | 一主为限 | 一个殖民地，一位主人就够了。第二个人敢自称主人，就是僭越——院子里只该有一种鞭子声。 |
| `SSC_Precept_MasterCount_Three` | 三主以内 | 最多三位主人，各养各的奴，各管各的院。再多，鞭子迟早抽到自己人身上。 |
| `SSC_Precept_MasterCount_Unlimited` | 主不厌多 | 只要养得起，主人越多越好。谁有本事把一具身体驯到肯跪，谁就有资格握链子。 |
| `SSC_Precept_SlaveCount_One` | 一奴为限 | 一名主人只该有一个性奴。一具身子，只经得起一双手反复调教。 |
| `SSC_Precept_SlaveCount_Three` | 三奴以内 | 一名主人最多三个性奴。够用，也不乱规矩——轮得到，也抱得过来。 |
| `SSC_Precept_SlaveCount_Unlimited` | 奴不厌多 | 只要养得起，性奴越多越好。主人身后跪得越长，越显得它有本事。 |

---

## 4 戒律预览的阶段效果说明（19 条）

这些是 `thoughtStageDescriptions`，只在戒律预览里出现，**必须带数值**；语气克制，可以带一句画面，但不能改数值。

| 键 | 预览文案 |
| --- | --- |
| `SSC_Precept_SexSlave_Sacred.comps.1.thoughtStageDescriptions.0` | 殖民地每有一名性奴：1/2/3/4/5+ 人分别为 +1/+3/+6/+10/+15，五人封顶。身子跪得越多，这地方越像该有的样子。 |
| `SSC_Precept_SexSlave_Envy.comps.0.thoughtStageDescriptions.0` | 本人是性奴：+10。被握住、被用着，心里反而踏实。 |
| `SSC_Precept_SexSlave_Envy.comps.0.thoughtStageDescriptions.1` | 既非性奴也非主人：-6。看着别人跪得安稳，自己却没人来握。 |
| `SSC_Precept_Master_Deference.comps.3.thoughtStageDescriptions.0` | 领袖是主人：+3。有人替这块地方握着链子。 |
| `SSC_Precept_Master_Deference.comps.3.thoughtStageDescriptions.1` | 领袖既非主人也非性奴：-3。 |
| `SSC_Precept_Master_Deference.comps.3.thoughtStageDescriptions.2` | 领袖是性奴：-6。次序从最上面就乱了。 |
| `SSC_Precept_Master_HolyMaster.comps.3.thoughtStageDescriptions.0` | 领袖是主人：+6。 |
| `SSC_Precept_Master_HolyMaster.comps.3.thoughtStageDescriptions.1` | 领袖既非主人也非性奴：-6。 |
| `SSC_Precept_Master_HolyMaster.comps.3.thoughtStageDescriptions.2` | 领袖是性奴：-12。圣位蒙尘。 |
| `SSC_Precept_MasterCount_One.comps.0.thoughtStageDescriptions.0` | 主人总数超出上限（一主为限）：每多一名 -6，最多 -27。 |
| `SSC_Precept_MasterCount_Three.comps.0.thoughtStageDescriptions.0` | 主人总数超出上限（三主以内）：每多一名 -6，最多 -27。 |
| `SSC_Precept_SlaveCount_One.comps.0.thoughtStageDescriptions.0` | 本人名下性奴超出上限（一奴为限）：每多一名 -3，最多 -18。 |
| `SSC_Precept_SlaveCount_Three.comps.0.thoughtStageDescriptions.0` | 本人名下性奴超出上限（三奴以内）：每多一名 -3，最多 -18。 |
| `SSC_Precept_SlaveCount_One.comps.1.thoughtStageDescriptions.0` | 目标主人蓄奴超出上限（一奴为限），超额 1 名：-5。 |
| `SSC_Precept_SlaveCount_One.comps.1.thoughtStageDescriptions.1` | 目标主人蓄奴超出上限（一奴为限），超额 2 名：-15。 |
| `SSC_Precept_SlaveCount_One.comps.1.thoughtStageDescriptions.2` | 目标主人蓄奴超出上限（一奴为限），超额 3 名及以上：-30。 |
| `SSC_Precept_SlaveCount_Three.comps.1.thoughtStageDescriptions.0` | 目标主人蓄奴超出上限（三奴以内），超额 1 名：-5。 |
| `SSC_Precept_SlaveCount_Three.comps.1.thoughtStageDescriptions.1` | 目标主人蓄奴超出上限（三奴以内），超额 2 名：-15。 |
| `SSC_Precept_SlaveCount_Three.comps.1.thoughtStageDescriptions.2` | 目标主人蓄奴超出上限（三奴以内），超额 3 名及以上：-30。 |

---

## 5 心情与社交阶段（106 条）

阶段号、`defName`、数值均来自现有 Def，本表只给名称与描述。名称列写入 `.label`，描述列写入 `.description`。

### 5.1 性奴 `SSC_CultureThoughts_SexSlave.xml`（13 阶段）

| 阶段键 | 名称 | 数值 | 描述 |
| --- | --- | --- | --- |
| `SSC_Culture_SexSlave_Hostile_Self.stages.0` | 沦为性奴 | -18 | 我竟成了这副样子。脖子被链子磨出印子，腿间还留着被用过的黏腻，连自己是谁都快想不起来。这具身子，怎么洗都洗不干净。 |
| `SSC_Culture_SexSlave_Revulsion_Self.stages.0` | 厌弃此身 | -6 | 落到这一步，连我自己都嫌。那些手、那些喘息还黏在身上，一闭眼就回来，可这身子偏偏还记着。 |
| `SSC_Culture_SexSlave_Sacred_Self.stages.0` | 蒙选之身 | +10 | 我是被选中的。跪着比站着更让我安心；被按着驯得越深，越觉得自己干净。 |
| `SSC_Culture_SexSlave_Envy_Self.stages.0` | 得偿所愿 | +10 | 我终于成了性奴。身子被人握在手里、被用到发软，心口反倒烫得踏实。 |
| `SSC_Culture_SexSlave_Envy_Self.stages.1` | 求而不得 | -6 | 看着那些跪着的人，我心里空落落的。被那样按着用、驯到只会发抖，我竟有点想要。 |
| `SSC_Culture_SexSlave_Hostile_Environment.stages.0` | 邻有性奴 | -3 | 附近就拴着那种东西。光知道它们在，我后颈就发紧。 |
| `SSC_Culture_SexSlave_Revulsion_Environment.stages.0` | 奴在身侧 | -2 | 院里就有性奴。那股甜腻的气味飘过来，我连饭都咽不下。 |
| `SSC_Culture_SexSlave_Sacred_Environment.stages.0` | 众奴环身 | +1 起 | 性奴越多，这地方越像该有的样子。被驯服的喘息一声接一声，是最好听的祷告。 |
| `SSC_Culture_SexSlave_Revulsion_Social.stages.0` | 嫌恶性奴 | 好感 -5 | 它跪在那里，一脸被用熟了的受用。我看了就恶心。 |
| `SSC_Culture_SexSlave_Respect_Social.stages.0` | 敬重性奴 | 好感 +5 | 它跪得那么稳，膝头压出的印子都是规矩。这份位置它担得起，该给的体面一分不能少。 |
| `SSC_Culture_SexSlave_Sacred_Social.stages.0` | 敬奉性奴 | 好感 +15 | 它是被选中的人。被驯得越深，身子越干净——站在它面前，我竟也想低头。 |
| `SSC_Culture_SexSlave_Envy_Social.stages.0` | 艳羡性奴 | 好感 +15 | 看它被驯成那样，膝头发软、腰自己塌下去，我分不清是羡慕还是想要。 |
| `SSC_Culture_SexSlave_Hostile_Social.stages.0` | 敌视性奴 | 好感 -15 | 被人拴着还一脸讨好的东西，我一眼都不想多看。 |

`Sacred_Environment` 基础值 +1，按人数曲线放大到 5 人时 +15，故数值列写“+1 起”。

### 5.2 训导官 `SSC_CultureThoughts_Trainer.xml`（8 阶段）

| 阶段键 | 名称 | 数值 | 描述 |
| --- | --- | --- | --- |
| `SSC_Culture_Trainer_Hostile_Self.stages.0` | 忝居训导 | -8 | 我竟去管教别的性奴。自己都只是条被拴着的奴，有什么脸拿鞭子。 |
| `SSC_Culture_Trainer_Hostile_Social.stages.0` | 鄙夷训导官 | 好感 -20 | 一条奴也配执鞭？它掰开同类双腿的样子，我只觉得荒唐又恶心。 |
| `SSC_Culture_Trainer_Revulsion_Self.stages.0` | 厌任此职 | -4 | 被主人派去调教别的性奴，这差事做得我浑身不自在。手上沾的全是别人的味道。 |
| `SSC_Culture_Trainer_Revulsion_Social.stages.0` | 厌恶训导官 | 好感 -10 | 脖子上还留着别人的链印，转身就去摆弄下一条。看着就恶心。 |
| `SSC_Culture_Trainer_Reverence_Self.stages.0` | 受托执鞭 | +3 | 主人把调教别的性奴的差事交给了我。这份被信任的滋味，竟还不错。 |
| `SSC_Culture_Trainer_Reverence_Social.stages.0` | 敬重训导官 | 好感 +5 | 它替主人分担调教之劳。那双手练过多少具身子，才练出这份稳——该敬。 |
| `SSC_Culture_Trainer_SacredOffice_Self.stages.0` | 执鞭圣职 | +5 | 我担的是一份圣职。把自己受过的驯服原样交下去——怎么跪、怎么湿、怎么叫——这是主人的恩许。 |
| `SSC_Culture_Trainer_SacredOffice_Social.stages.0` | 敬奉训导官 | 好感 +10 | 它是主人亲手挑出来执鞭的。被它调教过的性奴，眼神都会变——值得我敬一分。 |

### 5.3 主人及领袖 `SSC_CultureThoughts_Master.xml`（22 阶段）

| 阶段键 | 名称 | 数值 | 描述 |
| --- | --- | --- | --- |
| `SSC_Culture_Master_Hostile_Self.stages.0` | 忝为主人 | -12 | 我成了自己最看不起的那种人：攥着别人的身子，还管这叫支配。 |
| `SSC_Culture_Master_Hostile_ActualMaster.stages.0` | 屈居人下 | -8 | 我竟也有个主人，任它摆布。这副听话的身子，我自己都瞧不起。 |
| `SSC_Culture_Master_Hostile_Social.stages.0` | 敌视主人 | 好感 -20 | 它把别人驯成玩物还洋洋得意。这种人，我一眼都不想认。 |
| `SSC_Culture_Master_Hostile_Leader.stages.0` | 领袖握链 | -6 | 连领袖都握着链子。这个殖民地，怕是从根上就烂了。 |
| `SSC_Culture_Master_Revulsion_Self.stages.0` | 厌执此链 | -6 | 握着锁链的手，总让我觉得自己脏。可这差事，我还得接着做。 |
| `SSC_Culture_Master_Revulsion_ActualMaster.stages.0` | 受制于人 | -4 | 被主人使唤来使唤去，我心里只剩一层腻烦。 |
| `SSC_Culture_Master_Revulsion_Social.stages.0` | 厌恶主人 | 好感 -10 | 攥根鞭子就以为自己了不起。离了那根鞭子，它算什么。 |
| `SSC_Culture_Master_Revulsion_Leader.stages.0` | 领袖亦主 | -3 | 领袖竟也去当主人。这个位置，看着都腻味。 |
| `SSC_Culture_Master_Deference_Self.stages.0` | 身为主人 | +4 | 被敬着、被依着，这位置是我一鞭一鞭挣来的。跪在我脚下的每一具身体，都是我的。 |
| `SSC_Culture_Master_Deference_ActualMaster.stages.0` | 归于主人 | +3 | 有主人在，心就定了。它的话一落，我的腰自己就软了。 |
| `SSC_Culture_Master_Deference_Social.stages.0` | 敬重主人 | 好感 +5 | 它担着主人的位置。光看它握链子的那只手，就知道它懂得怎么让一具身子软下去。 |
| `SSC_Culture_Master_Deference_Leader.stages.0` | 主人领袖 | +3 | 主人带着我们，这地方才算有主心骨。 |
| `SSC_Culture_Master_Deference_Leader.stages.1` | 领袖非主 | -3 | 领袖连主人都不是，凭什么坐在那个位置上。 |
| `SSC_Culture_Master_Deference_Leader.stages.2` | 性奴领袖 | -6 | 让一条性奴爬到领袖的位置上，这地方是彻底没规矩了。 |
| `SSC_Culture_Master_Deference_SexSlaveLeaderSocial.stages.0` | 性奴窃位 | 好感 -10 | 一条被拴着的奴，也配以领袖自居？我看不下去。 |
| `SSC_Culture_Master_HolyMaster_Self.stages.0` | 身为圣主 | +10 | 这块地方供着我。每一具跪下的身体，都是我应得的敬奉。 |
| `SSC_Culture_Master_HolyMaster_ActualMaster.stages.0` | 侍奉圣主 | +5 | 能跪在主人脚下，是我的福分。它一开口，我连呼吸都跟着它的节奏走。 |
| `SSC_Culture_Master_HolyMaster_Social.stages.0` | 敬畏圣主 | 好感 +15 | 主人就是这块地方的神。对它低头的时候，我连膝头都在发烫。 |
| `SSC_Culture_Master_HolyMaster_Leader.stages.0` | 圣主在位 | +6 | 圣主领着众人，天经地义。谁不低头，谁就不配留下。 |
| `SSC_Culture_Master_HolyMaster_Leader.stages.1` | 僭居领袖 | -6 | 一个连主人都不是的人，也配被称作领袖？ |
| `SSC_Culture_Master_HolyMaster_Leader.stages.2` | 亵渎圣位 | -12 | 一条性奴竟敢坐在领袖的位置上。这是对主人最大的亵渎。 |
| `SSC_Culture_Master_HolyMaster_SexSlaveLeaderSocial.stages.0` | 圣位蒙尘 | 好感 -20 | 让性奴窃据领袖之位，是对主奴次序最恶毒的嘲弄。 |

### 5.4 数量 `SSC_CultureThoughts_Counts.xml`（10 阶段）

| 阶段键 | 名称 | 数值 | 描述 |
| --- | --- | --- | --- |
| `SSC_Culture_MasterCount_One_Self.stages.0` | 主人逾限 | -3 起 | 这地方的主人太多了。多出来的每一个，都是对次序的冒犯。 |
| `SSC_Culture_MasterCount_Three_Self.stages.0` | 主人逾三 | -3 起 | 主人已经不止三位。院子里人太多，迟早要出乱子。 |
| `SSC_Culture_SlaveCount_One_Self.stages.0` | 性奴逾限 | -3 起 | 我名下的性奴超出了该有的数目。每一具多出来的身子，都在分我的心。 |
| `SSC_Culture_SlaveCount_Three_Self.stages.0` | 奴数逾三 | -3 起 | 我的性奴不止三个了。这么多身子，规矩迟早要乱。 |
| `SSC_Culture_SlaveCount_One_Social.stages.0` | 蓄奴逾限 | 好感 -5 | 它名下的性奴超出了规矩。这种贪多，我看不惯。 |
| `SSC_Culture_SlaveCount_One_Social.stages.1` | 蓄奴无度 | 好感 -15 | 它的性奴越养越多，一点规矩都不讲。 |
| `SSC_Culture_SlaveCount_One_Social.stages.2` | 蓄奴成灾 | 好感 -30 | 它把这地方当成了自己的肉笼。这么滥收性奴，实在过分。 |
| `SSC_Culture_SlaveCount_Three_Social.stages.0` | 多蓄一奴 | 好感 -5 | 它名下的性奴已经超出三位的上限了。多出来的那一个，看着就碍眼。 |
| `SSC_Culture_SlaveCount_Three_Social.stages.1` | 贪多无度 | 好感 -15 | 它的性奴越养越多。腰上拴着那么多链子，也不怕乱了套。 |
| `SSC_Culture_SlaveCount_Three_Social.stages.2` | 奴满为患 | 好感 -30 | 它把半个殖民地都拴在了自己床上。这样滥收性奴，实在过分。 |

数量四条自身心情的基础值都是 -3，按超额累进放大（最多 -27／-18，见第 4 节预览），故写“-3 起”。

---

## 6 连接规则校对（不改）

| 键 | 现值 | 结论 |
| --- | --- | --- |
| `SSC_NamerRoleMoralist.rulePack.rulesStrings.0` | `r_roleName(priority=10)->[SSC_MoralistPreset]` | 保留；左侧、`priority`、方括号子规则引用不得改 |
| `SSC_NamerRitualBinding.rulePack.rulesStrings.0` | `r_ritualName(priority=10)->[SSC_BindingRitualPreset]` | 保留；同上 |

这两行没有文学文本。名称全部来自模因 `generalRules` 的第 7～12 行，因此第 1.3 节落地即完成名称编纂；`include` 与 `Def` 引用不动。

---

## 7 落地与自检

### 7.1 写入位置

| 内容 | Def 内联 | DefInjected（中文） |
| --- | --- | --- |
| 第 1 节 | [MemeDefs/SSC_Meme_MasterSlaveOrder.xml](../../../../1.7/Defs/MemeDefs/SSC_Meme_MasterSlaveOrder.xml) | [MemeDef/SSC_Culture.xml](../../../../1.7/Languages/ChineseSimplified/DefInjected/MemeDef/SSC_Culture.xml) |
| 第 3、4 节 | [SSC_Precepts_SexSlave.xml](../../../../1.7/Defs/PreceptDefs/SSC_Precepts_SexSlave.xml) 等四个文件 | [PreceptDef/SSC_Culture.xml](../../../../1.7/Languages/ChineseSimplified/DefInjected/PreceptDef/SSC_Culture.xml) |
| 第 5 节 | [SSC_CultureThoughts_SexSlave.xml](../../../../1.7/Defs/ThoughtDefs/SSC_CultureThoughts_SexSlave.xml) 等四个文件 | [ThoughtDef/SSC_Culture.xml](../../../../1.7/Languages/ChineseSimplified/DefInjected/ThoughtDef/SSC_Culture.xml) |

DefInjected 写入后，同步 `Sexslavecraft/Languages/ChineseSimplified/` 下同名镜像；清单第二节列出的镜像路径逐字对应，不要只改运行目录。

### 7.2 不得改动

- `IssueDef` 五个名称、`RulePackDef` 两行、模因 `generalRules.rulesStrings.3`（领袖入口行）。
- 所有阶段号、`defName`、`baseMoodEffect`／`baseOpinionOffset`、`condition`、`countLimit`、曲线点。
- 规则行左侧（含 `uses=1`、`tag=`、`priority=`）、索引顺序、`symbolPacks` 的包序。
- 已确认的 `include`、`requiredMemes`／`associatedMemes`／`conflictingMemes`、权重。

### 7.3 落地后校验

1. 全文检索 `TODO`、`占位`、`SSCDESCTODO`、`SSCLEADERTODO`、`SSCNAMETODO`、`SSCCREEDTODO`、`SSCPREFIXTODO`、`SSCMORALTODO`、`SSCRITUALTODO`、`SSCADJTODO`、`SSCTHEMETODO`、`SSCMEMBERTODO`，均为零。
2. 模因 42 个待编写值（44 键中 `label` 与领袖入口行保持原样）、戒律 41 个待编写值（22 描述＋19 预览）、心情 106 个键全部有值；键名与 Def 一一对应，无新增或缺失。
3. `rulesStrings` 整行左侧与 Def 现值逐字节一致；DefInjected 中箭头为 `-&gt;`。
4. 17 段背景只含 0.4 节白名单变量，变量两侧无空格；`lessonIntro`／`lesson` 结尾无句号。
5. 名称列无标点、无数字，长度为 4～5 字，同一议题内不重复。
6. 中文引号用“”，数值用 `+3`／`-6` 形式，文案值不出现 `&`、`<`、`>`。

### 7.4 后续批次

本稿只覆盖简体中文。繁体按现有术语表转换（性奴、主人、訓導官、調教、鎖鏈、惡墮），英文与俄文另批翻译；四语待编写值与本稿一一对应（模因 42、戒律 41、心情 106），翻译时不得改动键、阶段号与规则左侧。
