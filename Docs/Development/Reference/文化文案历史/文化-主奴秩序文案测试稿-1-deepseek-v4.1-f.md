> **废案（2026-10-22）**：维护者判定本测试稿不合格，仅作留档，不作为实现依据，也不得据此回填 XML 或翻译。当前有效的文化文案以维护者选定的新稿为准。

# 文化文案定稿 一 模因与戒律

> 状态：**批次 1 中文定稿 + 英文对照，未落 XML**。覆盖 `SSC_Meme_MasterSlaveOrder.xml` 与四个 `SSC_Precepts_*.xml`。心情（ThoughtDef，106 处）与模因背景段落（17 段）留给批次 2；繁中、俄语与 XML 落地留给批次 3。

配套[交接清单](../文化-主奴秩序文案维护清单.md)与[设计定稿](../../../Design/Archive/文化-主奴秩序戒律与职位设计.md)。

写作约束：

- 默认第一人称或世界内因果，不写数值、不写属性名、不写"心情 +X"（数值只出现在戒律预览的阶段说明里）。
- 露骨可以，但不做器官清单式堆砌；重心在**支配与被支配**的身体反应与心理沉溺。
- 动作词统一「调教」，身份词统一「主人 / 性奴 / 训导官」，不用「奴隶」「奴仆」。

## 1 术语约定

| 概念 | 用法 |
| --- | --- |
| 性奴 | 本模组身份；不使用「奴隶」 |
| 主人 | 持有主人资格者；不写「奴隶主」 |
| 训导官 | 代主人调教其他性奴的性奴 |
| 调教 | 统一动作词；不用「训练」 |
| 锁链 | 性奴之锁链、绑定关系 |
| 恶堕 | 数值名；文案里可写「沉下去」「堕落」，不直接写数值 |
| 场景词 | 鞭、项圈、锁链、跪、驯、被用、灌满、发抖 |

## 2 模因：主奴秩序

### 2.1 描述

`SSC_Meme_MasterSlaveOrder.description`

> 支配不是暴行，是秩序；被支配不是屈辱，是归宿。锁链递到手里时，人要学着握紧；套上脖颈时，人要学着沉下去。跪得越深，越接近欲望真正的声音。

EN: Domination is not cruelty, it is order. Submission is not humiliation, it is home. When the chain is placed in your hand, learn to close your fist; when it closes on your throat, learn to sink. The deeper you kneel, the closer you come to the true voice of desire.

### 2.2 命名包

| 包 | ideoName | theme | adjective | member |
| --- | --- | --- | --- | --- |
| 0 | 主奴道 | 主奴 | 驯顺的 | 主奴信徒 |
| 1 | 锁链教 | 锁链 | 被驯的 | 戴链者 |
| 2 | 沉沦道 | 沉沦 | 堕落的 | 沉沦者 |

| 包 | ideoName | theme | adjective | member |
| --- | --- | --- | --- | --- |
| 0 | Way of Master and Slave | master–slave | submissive | chainbearer |
| 1 | Church of the Chain | chain | collared | collar-wearer |
| 2 | Way of Sinking | sinking | fallen | the sunken |

### 2.3 通用规则与预设

| 键（rulesStrings 序号） | 中文 | 英文 |
| --- | --- | --- |
| 0 memeAdjective | 驯顺 | submissive |
| 1 memeCreed | 锁链 | chain |
| 2 memeHyphenPrefix | 奴 | slave- |
| 4～6 领袖 | 执鞭者 / 主君 / 大主人 | whipbearer / master sovereign / the great master |
| 7～9 道德导师 | 首席性奴 / 引戒者 / 调教祭司 | chief sex slave / keeper of precepts / priest of training |
| 10～12 契约仪式 | 锁链契约 / 献身礼 / 初驯之仪 | chain covenant / rite of offering / rite of first breaking |

> 「首席性奴」是独立职位放弃后留下的备选名，作为道德导师称谓池的三条预设之一，与另两条并列随机：某个文化的道德导师可能叫首席性奴，也可能叫引戒者或调教祭司。称谓只影响显示，不改任职条件；要让这个称谓只能由 SSC 性奴担任，需另给 `IdeoRole_Moralist` 补 `roleRequirements`，不在本轮范围。

## 3 性奴议题（`SSC_Issue_SexSlave`）

| defName | label | 中文描述 | English |
| --- | --- | --- | --- |
| `SSC_Precept_SexSlave_Hostile` | 敌视 | 性奴是被毁掉的东西。锁链一扣上脖子，人就只剩下一个用处；把人弄成这副样子还管这叫乐事——这种事该被连根铲掉。 | A sex slave is a ruined thing. Once the chain closes on its neck, a person is nothing but something to be used. Doing that to someone and calling it joy — the whole practice should be torn out by the root. |
| `SSC_Precept_SexSlave_Revulsion` | 厌恶 | 被驯成那样还甘之如饴，光是看着就让人反胃。院墙里传出来的那些声音，我一步都不想靠近。 | Writhing happily after being broken like that — the sight alone turns my stomach. I will not take a single step toward the sounds coming from behind those walls. |
| `SSC_Precept_SexSlave_Indifference` | 漠视 | 别人院里锁着谁、怎么用，与我无关。只要别把那种声音带进我的院子。 | Who gets chained in another's yard, and how they are used, is none of my business. Just keep those sounds out of my own. |
| `SSC_Precept_SexSlave_Respect` | 尊重 | 性奴也是人。跪着是它和主人之间的事，该给的那份体面，一分都不能少。 | A sex slave is still a person. Kneeling is between it and its master; the dignity it is owed is not one bit less. |
| `SSC_Precept_SexSlave_Sacred` | 神圣 | 性奴不是被毁掉的人，是被选中的人。身子被一寸寸调开、灌满、驯到只会发抖，跪下去那一刻，它比谁都干净。被用得越深，越接近它本该有的样子。 | A sex slave is not a ruined person, but a chosen one. Flesh opened inch by inch, filled, trained until it can only tremble — in the moment it kneels, it is cleaner than anyone. The deeper it is used, the closer it comes to what it was made to be. |
| `SSC_Precept_SexSlave_Envy` | 艳羡 | 被驯成那样，未必是坏事。看着性奴跪在那里，膝盖发软、腰自己塌下去——谁心里没生出过一点羡慕？那大概才是人该有的活法。 | Being broken like that may not be a bad thing. Watch a sex slave kneel — knees going weak, back sinking on its own — and who has never felt a flicker of envy? That, perhaps, is how a person ought to live. |

## 4 训导官议题（`SSC_Issue_Trainer`）

| defName | label | 中文描述 | English |
| --- | --- | --- | --- |
| `SSC_Precept_Trainer_Hostile` | 敌视 | 让性奴去管教性奴，是对主奴次序的亵渎。鞭子只能握在主人手里；一条奴拿起鞭子，只会把整座院子弄脏。 | Setting a slave to discipline slaves is blasphemy against the order. The whip belongs in a master's hand; when a slave picks it up, it only soils the whole yard. |
| `SSC_Precept_Trainer_Revulsion` | 厌恶 | 一条奴也配拿鞭子？身上还留着别人的印子，转头就去摆弄下一个——光是想想就恶心。 | A slave, with a whip? Still marked by someone else's hand, and already fingering the next one — the thought alone is vile. |
| `SSC_Precept_Trainer_Indifference` | 漠视 | 训导官是谁、怎么调教，与我无关。只要别把鞭子声甩进我的院子。 | Who serves as training officer, and how they train, is none of my concern. Just keep the crack of that whip out of my yard. |
| `SSC_Precept_Trainer_Reverence` | 尊敬 | 训导官替主人分担调教之劳，手上那套是练出来的。它懂得怎么让人跪稳、叫顺——该给的敬意，得给。 | A training officer takes some of the master's work. What it does with its hands is practiced. It knows how to keep a body kneeling and how to make the sounds come out right — the respect is earned. |
| `SSC_Precept_Trainer_SacredOffice` | 圣职 | 被主人挑出来执鞭的性奴，担的是一份圣职。它把自己被驯服的那一套原样交下去：怎么跪、怎么叫、怎么把身子交出去。被它调教，是仅次于被主人亲自调教的荣幸。 | The slave a master lifts up to hold the whip carries a sacred office. It passes on exactly what it was taught: how to kneel, how to moan, how to give the body over. To be trained by it is an honor second only to the master's own hand. |

## 5 主人议题（`SSC_Issue_Master`）

| defName | label | 中文描述 | English |
| --- | --- | --- | --- |
| `SSC_Precept_Master_Hostile` | 敌视 | 自称主人的人，都是窃据他人身体的小偷。锁链不该握在任何人手里。 | Anyone who calls themselves a master is a thief squatting on someone else's body. No hand should hold a chain. |
| `SSC_Precept_Master_Revulsion` | 厌恶 | 攥着一根鞭子就以为自己是个角色，离了那根鞭子什么都不是。 | Clutch a whip and they think they are someone. Take it away and there is nothing left. |
| `SSC_Precept_Master_Indifference` | 漠视 | 主人不主人的，与我无关。别拿那套规矩来管我就行。 | Master or not, it is nothing to me. Just do not manage me with that set of rules. |
| `SSC_Precept_Master_Deference` | 尊奉 | 主人该受敬重。它的意志在自家性奴身上优先于旁人——这是它挣来的位置。 | A master is owed respect. Over its own sex slave, its will outranks anyone else's — it earned that place. |
| `SSC_Precept_Master_HolyMaster` | 圣主 | 主人不是管事的人，是这块地方供着的那一位。主人一句话重过一切；为主人跪下、张开腿、把身子交出去，不是屈辱，是荣耀。 | A master is not an administrator. A master is the one this place enshrines. One word from a master outweighs everything; to kneel for it, open for it, give your body over to it is not shame — it is glory. |

## 6 数量议题（`SSC_Issue_MasterCount` / `SSC_Issue_SlaveCount`）

| defName | label | 中文描述 | English |
| --- | --- | --- | --- |
| `SSC_Precept_MasterCount_One` | 一主为限 | 殖民地只容得下一位主人。第二个人敢自称主人，就是僭越；一个院里只能有一种鞭子声。 | The colony has room for exactly one master. A second one claiming the word is a usurper; a single yard should carry a single whip. |
| `SSC_Precept_MasterCount_Three` | 三主以内 | 最多三位主人，各养各的奴，各管各的院子。再多，鞭子迟早抽到自己人身上。 | Three masters at most, each keeping its own slaves, each minding its own yard. Any more, and the whip starts finding your own people. |
| `SSC_Precept_MasterCount_Unlimited` | 主不厌多 | 只要养得起，主人越多越好。谁有本事驯服人，谁就有资格握住那根链子。 | As many masters as can be fed. Whoever can break a person has earned the right to hold the chain. |
| `SSC_Precept_SlaveCount_One` | 一奴为限 | 一名主人只该有一个性奴。一个人的身子，只经得起一双手驯。 | One master, one sex slave. A body can only take being trained by one pair of hands. |
| `SSC_Precept_SlaveCount_Three` | 三奴以内 | 一名主人最多三个性奴。够用，也不至于乱了规矩——轮得到，也抱得过来。 | Three sex slaves at most for one master. Enough to use, not so many that the order frays — there is time for each, and arms enough to hold them. |
| `SSC_Precept_SlaveCount_Unlimited` | 奴不厌多 | 只要养得起，性奴越多越好。主人身后跟的奴越多，越显得它有本事。 | As many sex slaves as can be fed. The longer the line trailing behind a master, the more it proves. |

## 7 戒律预览的阶段说明

这些是`thoughtStageDescriptions`，会在戒律预览里显示数值，语气保持克制。

| 键 | 中文 | English |
| --- | --- | --- |
| `SSC_Precept_SexSlave_Sacred.comps.1.thoughtStageDescriptions.0` | 殖民地每有一名性奴，心情 +1；五人时封顶 +15。 | Each sex slave in the colony: +1 mood, up to +15 at five. |
| `SSC_Precept_SexSlave_Envy.comps.0.thoughtStageDescriptions.0` | 本人是性奴：+10。 | If you are a sex slave: +10. |
| `SSC_Precept_SexSlave_Envy.comps.0.thoughtStageDescriptions.1` | 既不是性奴也不是主人：-6。 | If you are neither a sex slave nor a master: -6. |
| `SSC_Precept_Master_Deference.comps.3.thoughtStageDescriptions.0` | 领袖是主人：+3。 | Leader is a master: +3. |
| `SSC_Precept_Master_Deference.comps.3.thoughtStageDescriptions.1` | 领袖既不是主人也不是性奴：-3。 | Leader is neither master nor sex slave: -3. |
| `SSC_Precept_Master_Deference.comps.3.thoughtStageDescriptions.2` | 领袖是性奴：-6。 | Leader is a sex slave: -6. |
| `SSC_Precept_Master_HolyMaster.comps.3.thoughtStageDescriptions.0` | 领袖是主人：+6。 | Leader is a master: +6. |
| `SSC_Precept_Master_HolyMaster.comps.3.thoughtStageDescriptions.1` | 领袖既不是主人也不是性奴：-6。 | Leader is neither master nor sex slave: -6. |
| `SSC_Precept_Master_HolyMaster.comps.3.thoughtStageDescriptions.2` | 领袖是性奴：-12。 | Leader is a sex slave: -12. |
| `SSC_Precept_MasterCount_One.comps.0.thoughtStageDescriptions.0` | 主人总数超出上限：每多一名 -6，最多 -27。 | Colony exceeds the master limit: -6 per extra master, up to -27. |
| `SSC_Precept_MasterCount_Three.comps.0.thoughtStageDescriptions.0` | 主人总数超出上限：每多一名 -6，最多 -27。 | Colony exceeds the master limit: -6 per extra master, up to -27. |
| `SSC_Precept_SlaveCount_One.comps.0.thoughtStageDescriptions.0` | 本人名下性奴超出上限：每多一名 -3，最多 -18。 | Own sex slaves exceed the limit: -3 per extra, up to -18. |
| `SSC_Precept_SlaveCount_Three.comps.0.thoughtStageDescriptions.0` | 本人名下性奴超出上限：每多一名 -3，最多 -18。 | Own sex slaves exceed the limit: -3 per extra, up to -18. |
| `SSC_Precept_SlaveCount_One.comps.1.thoughtStageDescriptions.0` | 目标主人名下性奴超出上限 1 名：-5。 | Target master exceeds the limit by 1: -5. |
| `SSC_Precept_SlaveCount_One.comps.1.thoughtStageDescriptions.1` | 目标主人名下性奴超出上限 2 名：-15。 | Target master exceeds the limit by 2: -15. |
| `SSC_Precept_SlaveCount_One.comps.1.thoughtStageDescriptions.2` | 目标主人名下性奴超出上限 3 名及以上：-30。 | Target master exceeds the limit by 3 or more: -30. |
| `SSC_Precept_SlaveCount_Three.comps.1.thoughtStageDescriptions.0` | 目标主人名下性奴超出上限 1 名：-5。 | Target master exceeds the limit by 1: -5. |
| `SSC_Precept_SlaveCount_Three.comps.1.thoughtStageDescriptions.1` | 目标主人名下性奴超出上限 2 名：-15。 | Target master exceeds the limit by 2: -15. |
| `SSC_Precept_SlaveCount_Three.comps.1.thoughtStageDescriptions.2` | 目标主人名下性奴超出上限 3 名及以上：-30。 | Target master exceeds the limit by 3 or more: -30. |

## 8 待办

1. **批次 2**：`SSC_CultureThoughts_*.xml` 的 106 处阶段名与描述（性奴 9 条、训导官 8 条、主人 15 条、数量 6 条），以及模因的 17 段 `descriptionMaker` 背景。
2. **批次 3**：落 XML——Def 内联文本、四语 DefInjected、`Sexslavecraft` 源码镜像，最后全库搜 `TODO` / `占位` 清零。
3. 繁中按现有术语表转换（訓導官、性奴、主人、鎖鏈、調教、惡墮），俄语单独过一遍。
