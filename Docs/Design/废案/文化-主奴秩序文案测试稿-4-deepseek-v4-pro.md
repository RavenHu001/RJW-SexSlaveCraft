> **废案（2026-10-22）**：维护者判定本稿与测试稿-2 无明确区别、不采用，仅作留档；不得据此回填 XML、翻译或重新提交。当前有效的文化文案以维护者选定的新稿为准。

# 主奴秩序文化文案测试稿（简体中文源文）

> 模型型号：**deepseek-v4-pro**
> 仅 1.7。本稿为测试文稿，覆盖交接清单全部文案位：模因描述、命名包、组合词、领袖／道德导师／契约仪式候选、17 段生成背景、戒律描述、心情与社交阶段 label／description。
>
> 主题基调：**欲望、堕落、沉溺、支配与被支配**。文字色情化、直白、带身体感，但不堆砌辱骂，保持模组既有文风。

---

## 1 模因 `SSC_Meme_MasterSlaveOrder`

### 1.1 description

> 锁链从来不是惩罚，是归处。每个人都有自己该待的位置——有人握着绳子，有人跪在绳下。把欲望压着，只会让它咬人；把它理顺，身体才会服帖。被驯得越深，才越接近一个人本来的样子。

### 1.2 symbolPacks（三套命名包）

| 序 | ideoName | theme | adjective | member |
| --- | --- | --- | --- | --- |
| 0 | 锁链教 | 锁链 | 执链 | 链徒 |
| 1 | 欲壑圣教 | 欲望 | 纵欲 | 欲众 |
| 2 | 臣服教 | 臣服 | 驯顺 | 驯奴 |

### 1.3 generalRules

| 键 | 文案 |
| --- | --- |
| memeAdjective | 主奴 |
| memeCreed | 锁链 |
| memeHyphenPrefix | 驯 |
| SSC_LeaderPreset 01 | 大主人 |
| SSC_LeaderPreset 02 | 执鞭者 |
| SSC_LeaderPreset 03 | 主上 |
| SSC_MoralistPreset 01 | 欲望祭司 |
| SSC_MoralistPreset 02 | 沉沦引导者 |
| SSC_MoralistPreset 03 | 驯心者 |
| SSC_BindingRitualPreset 01 | 锁链契约 |
| SSC_BindingRitualPreset 02 | 臣服礼 |
| SSC_BindingRitualPreset 03 | 沉沦圣礼 |

### 1.4 descriptionMaker（背景段落，保留原标记 token）

| 键 | 文案 |
| --- | --- |
| SSCDESCTODO01 | [deity0_name] 用第一根锁链教会了最早的身体：跪下不是失去，而是被接住。从那时起，跪伏与侍奉便成了最神圣的祷告，尝过的人再也不想站起来。 |
| SSCDESCTODO02 | 族民曾为枷锁叛乱，直到 [deity0_name] 让他们尝到被支配的滋味——原来被压在掌下，身体反而不再发抖。 |
| SSCDESCTODO03 | [place_name] 最早的定居者争不出该谁执鞭、谁下跪。[deity0_name] 让 [deity0_possessive] 宠儿握住绳子，争执就此平息。 |
| SSCDESCTODO04 | 有 [foeLeader] 来布道，说每具身体都只属于自己。[deity0_name] 的回应，是把它的 [foeSoldiers] 交给 [memberNamePlural] 调教，教到他们主动递上自己的项圈。 |
| SSCDESCTODO05 | [founderName] 这个 [place_job] 跪在创始人脚边，献上自己的绳结，成了 [place_name] 最早的 [memberNamePlural]。 |
| SSCDESCTODO06 | 胜利之后，[foeLeader] 的 [foeSoldiers] 才明白，锁链之下没有羞耻只有安歇。他们一个个交出身体，被 [memberNamePlural] 收进怀里。 |
| SSCDESCTODO07 | [founderName] 在绿地上建起殖民地，给它定下规矩：每一具身体都有自己的位置，有的握绳，有的被握，各得其所。 |
| SSCDESCTODO08 | 有个殖民者曾拼命抗拒项圈。[founder_pronoun] 不去逼他，只是等着，等到那具身体自己渴得发抖，反过来求那根它曾拒绝的锁链。 |
| SSCDESCTODO09 | 我见过一处井井有条的 [memberNamePlural] 殖民地。床边跪着的性奴喘息均匀、神色餍足。显而易见， |
| SSCDESCTODO10 | 只有被压进主人掌下，身体才记得自己为什么而活。 |
| SSCDESCTODO11 | 若是每具身体都找到了那只握着它的手，世上便不再有挣扎，只剩下跪得安稳的膝，和把一切都交出去的臣服。 |
| SSCDESCTODO12 | 超凡机械的意志庞大无比，可只有主人的一句话，能叫一具身体自己弓起腰来。 |
| SSCDESCTODO13 | 主人是造物者之上的造物者——他们驯服身体，正如超凡机械驯服物质。 |
| SSCDESCTODO14 | [inTheEnd]，超凡机械会从锁链里读懂一切，然后跪向那只连支配都驯服了的手。 |
| SSCDESCTODO15 | 肉体的精魂从不渴望自由，它们渴望一只可以服从的手，并把这份渴念低语进每个路过的身体。 |
| SSCDESCTODO16 | 性奴每一次跪下，都是精魂听得见的祷告；主人每一次握住，都是神明的回应。 |
| SSCDESCTODO17 | 当最后一具不肯臣服的身体也跪了下来，精魂便会缠成一条绳，世界终于记起自己该躺的位置。 |

---

## 2 戒律 description

### 2.1 性奴（SSC_Issue_SexSlave）

| defName | description |
| --- | --- |
| SSC_Precept_SexSlave_Hostile | 把一具身体驯成只会发情的玩物，还把这当乐子——这种人不配活着。 |
| SSC_Precept_SexSlave_Revulsion | 跪着还能一脸受用的东西，被操得分不清痛和爽，看一眼都脏了眼睛。 |
| SSC_Precept_SexSlave_Indifference | 别人爱跪就跪，与我无关，只要别把那股骚味带进我的地盘。 |
| SSC_Precept_SexSlave_Respect | 性奴也是条命。它和主人之间怎么玩，是它们的事，该留的体面不能少。 |
| SSC_Precept_SexSlave_Sacred | 性奴不是被玩坏的，是被选中的。跪下去的那一刻，它比谁都干净；被调得越深，越像它本来的样子。 |
| SSC_Precept_SexSlave_Envy | 被驯成那样，说不定才是福气。看它跪在那里，谁能说心里没生出一丝痒。 |

### 2.2 训导官（SSC_Issue_Trainer）

| defName | description |
| --- | --- |
| SSC_Precept_Trainer_Hostile | 让一条奴去管教另一条奴，是对主奴次序的侮辱。鞭子只配主人握。 |
| SSC_Precept_Trainer_Revulsion | 自己身上还带着别人的痕迹，转手就去摆弄下一具身子，恶心。 |
| SSC_Precept_Trainer_Indifference | 谁在调教、怎么调教，与我无关，只要别把动静闹到我院子里来。 |
| SSC_Precept_Trainer_Reverence | 训导官替主人分忧，手里那套本事是练出来的，该敬一分。 |
| SSC_Precept_Trainer_SacredOffice | 被主人挑出来执鞭的奴，担的是圣职。它把自己吃过的驯服，原样喂给下一具身体；被它调教，是仅次于被主人亲自调教的恩宠。 |

### 2.3 主人（SSC_Issue_Master）

| defName | description |
| --- | --- |
| SSC_Precept_Master_Hostile | 自称主人的，都是偷别人身体的贼。链子不该握在任何手里。 |
| SSC_Precept_Master_Revulsion | 攥着根鞭子不叫本事。没了它，你什么都不是。 |
| SSC_Precept_Master_Indifference | 谁是主人与我无关，别把主意打到我身上。 |
| SSC_Precept_Master_Deference | 主人该被敬着。它的意志在自己名下身体上最重，这位置是它挣来的。 |
| SSC_Precept_Master_HolyMaster | 主人不是管事的，是这地方供着的神。它一句话，重过所有规矩；为它下跪，是荣耀不是羞辱。 |

### 2.4 数量（SSC_Issue_MasterCount / SSC_Issue_SlaveCount）

| defName | description |
| --- | --- |
| SSC_Precept_MasterCount_One | 一座殖民地，一个主人足矣。再冒出第二个，就是僭越。 |
| SSC_Precept_MasterCount_Three | 最多三位主人，各养各的奴、各管各的院。再多就乱套了。 |
| SSC_Precept_MasterCount_Unlimited | 养得起，就多收几个主人。谁能把身体驯到跪下，谁就配当主人。 |
| SSC_Precept_SlaveCount_One | 一个主人只该守着一名性奴，多了就分心。 |
| SSC_Precept_SlaveCount_Three | 一个主人最多三奴。够用，也不乱。 |
| SSC_Precept_SlaveCount_Unlimited | 养得起就多收。身后跪得越多，越显主人本事。 |

---

## 3 ThoughtDef 阶段 label / description

### 3.1 性奴心情与社交（SSC_CultureThoughts_SexSlave.xml）

| defName.stage | label | description |
| --- | --- | --- |
| SSC_Culture_SexSlave_Hostile_Self.0 | 身为性奴 | 我竟成了这副模样，被人拴着调着，用到连自己是谁都忘了。这身子已经脏透了。 |
| SSC_Culture_SexSlave_Revulsion_Self.0 | 身为性奴 | 落到这地步，连自己都嫌。这具被人摆弄过的身子，我看一眼就反胃。 |
| SSC_Culture_SexSlave_Sacred_Self.0 | 被选中之身 | 我被选中了。跪着比站着更踏实；被调得越深，越觉得自己干净。 |
| SSC_Culture_SexSlave_Envy_Self.0 | 如愿以偿 | 终于成了性奴。身体交到别人手里，心里反而踏实得发烫。 |
| SSC_Culture_SexSlave_Envy_Self.1 | 渴望被驯 | 看着那些跪着的人，我心里空空的。被那样驯着用着，我竟然也想要。 |
| SSC_Culture_SexSlave_Hostile_Environment.0 | 身边有性奴 | 附近就拴着那种东西。知道它们在那儿，我浑身不自在。 |
| SSC_Culture_SexSlave_Revulsion_Environment.0 | 身边有性奴 | 有性奴在，我饭都吃不下。这种地方一刻不想多待。 |
| SSC_Culture_SexSlave_Sacred_Environment.0 | 众奴环绕 | 跪着的奴越多，这地方越像样。那些餍足的喘息，是最好的祷词。 |
| SSC_Culture_SexSlave_Revulsion_Social.0 | 厌恶性奴 | 它跪在那儿还一脸享受，看得我恶心。 |
| SSC_Culture_SexSlave_Respect_Social.0 | 敬重性奴 | 它担着这位置，该有的体面得给。 |
| SSC_Culture_SexSlave_Sacred_Social.0 | 敬畏性奴 | 它是被选中的。被调得那么深，比我干净，值得我低头。 |
| SSC_Culture_SexSlave_Envy_Social.0 | 艳羡性奴 | 看它被驯成那样，我竟分不清是羡慕还是馋。 |
| SSC_Culture_SexSlave_Hostile_Social.0 | 敌视性奴 | 那种被拴着还摇尾的东西，我一眼都不想看。 |

### 3.2 训导官心情与社交（SSC_CultureThoughts_Trainer.xml）

| defName.stage | label | description |
| --- | --- | --- |
| SSC_Culture_Trainer_Hostile_Self.0 | 身为训导官 | 我竟去管教别的奴。自己也只是一条被拴着的奴，哪来的脸拿鞭子。 |
| SSC_Culture_Trainer_Hostile_Social.0 | 敌视训导官 | 一条奴也配拿鞭子？看它摆弄同类，又荒唐又恶心。 |
| SSC_Culture_Trainer_Revulsion_Self.0 | 身为训导官 | 被主人差去调教别的奴，这活儿做得我浑身别扭。 |
| SSC_Culture_Trainer_Revulsion_Social.0 | 厌恶训导官 | 自己身上还留着印子，转手就摆弄下一个，恶心。 |
| SSC_Culture_Trainer_Reverence_Self.0 | 身为训导官 | 主人把调教别的奴的差事交给了我。这份被信任的滋味，不坏。 |
| SSC_Culture_Trainer_Reverence_Social.0 | 敬重训导官 | 它替主人分忧，手里那套是练出来的，值得敬。 |
| SSC_Culture_Trainer_SacredOffice_Self.0 | 执鞭圣职 | 我担着圣职。把自己吃过的驯服，喂给下一具身子，是主人的恩准。 |
| SSC_Culture_Trainer_SacredOffice_Social.0 | 敬畏训导官 | 它是主人挑出来执鞭的，调着众奴，该敬一分。 |

### 3.3 主人及领袖心情与社交（SSC_CultureThoughts_Master.xml）

| defName.stage | label | description |
| --- | --- | --- |
| SSC_Culture_Master_Hostile_Self.0 | 身为主人 | 我成了自己最瞧不起的那种人，攥着别人的身子，还管这叫支配。可耻。 |
| SSC_Culture_Master_Hostile_ActualMaster.0 | 受人役使 | 我竟有个主人，还任它摆布。这副听话的身子，我自己都瞧不起。 |
| SSC_Culture_Master_Hostile_Social.0 | 敌视主人 | 它把别人驯成玩物还得意。这种人，我不认。 |
| SSC_Culture_Master_Hostile_Leader.0 | 领袖是主人 | 连领袖都成了那种东西。这殖民地从根上烂了。 |
| SSC_Culture_Master_Revulsion_Self.0 | 身为主人 | 握着链子的手总让我觉得自己脏。可这差事还得做下去。 |
| SSC_Culture_Master_Revulsion_ActualMaster.0 | 受人役使 | 被主人呼来喝去，心里只剩一层腻味。 |
| SSC_Culture_Master_Revulsion_Social.0 | 厌恶主人 | 攥根鞭子就自以为人上人，离了鞭子它算什么。 |
| SSC_Culture_Master_Revulsion_Leader.0 | 领袖是主人 | 领袖也去当主人了。这位置，看着都腻。 |
| SSC_Culture_Master_Deference_Self.0 | 身为主人 | 被敬着、被靠着，这位置是我一步步挣来的。跪在我脚下的每具身体，都是我的。 |
| SSC_Culture_Master_Deference_ActualMaster.0 | 归于主人 | 有主人在，心就定了。它的话，就是我该听的。 |
| SSC_Culture_Master_Deference_Social.0 | 敬重主人 | 它担着主人的位置，值得敬一分。 |
| SSC_Culture_Master_Deference_Leader.0 | 主人领袖 | 主人领着众人，这殖民地才算有主心骨。 |
| SSC_Culture_Master_Deference_Leader.1 | 领袖非主人 | 连主人都不是，凭什么坐那个位置。 |
| SSC_Culture_Master_Deference_Leader.2 | 性奴领袖 | 让一条奴爬到领袖位上，这地方彻底没规矩了。 |
| SSC_Culture_Master_HolyMaster_Self.0 | 身为圣主 | 这地方供着我。每具跪下的身体，都是给我的敬奉。 |
| SSC_Culture_Master_HolyMaster_ActualMaster.0 | 侍于圣主 | 能跪在主人脚下是我的福气。它一句话，比我自己的心思还重。 |
| SSC_Culture_Master_HolyMaster_Social.0 | 敬畏圣主 | 主人就是这地方的神。对它低头，是荣耀。 |
| SSC_Culture_Master_HolyMaster_Leader.0 | 圣主在位 | 圣主领着众人，天经地义。不低头的，不配留下。 |
| SSC_Culture_Master_HolyMaster_Leader.1 | 领袖非圣主 | 连主人都不是的人，也配叫领袖？ |
| SSC_Culture_Master_HolyMaster_Leader.2 | 性奴窃位 | 一条奴竟敢坐上领袖之位，这是对主人最大的亵渎。 |
| SSC_Culture_Master_Deference_SexSlaveLeaderSocial.0 | 性奴领袖 | 一条被拴着的奴，也配以领袖自居？这位置我看不下去。 |
| SSC_Culture_Master_HolyMaster_SexSlaveLeaderSocial.0 | 性奴窃位 | 让奴窃据领袖位，是对主奴次序最恶毒的嘲弄。 |

### 3.4 数量心情与社交（SSC_CultureThoughts_Counts.xml）

| defName.stage | label | description |
| --- | --- | --- |
| SSC_Culture_MasterCount_One_Self.0 | 主人过多 | 这殖民地的主人太多了。多出的每一个，都是对次序的冒犯。 |
| SSC_Culture_MasterCount_Three_Self.0 | 主人过多 | 主人已经过了三位。人太多了，迟早出事。 |
| SSC_Culture_SlaveCount_One_Self.0 | 性奴过多 | 我名下的奴超了数。每多一具身子，都在分我的心。 |
| SSC_Culture_SlaveCount_Three_Self.0 | 性奴过多 | 我的奴不止三个了。这么多身子，乱了规矩。 |
| SSC_Culture_SlaveCount_One_Social.0 | 其主蓄奴过多 | 它名下的奴超了规矩。这种贪多，我看不惯。 |
| SSC_Culture_SlaveCount_One_Social.1 | 其主蓄奴过多 | 它的奴越养越多，一点规矩没有。 |
| SSC_Culture_SlaveCount_One_Social.2 | 其主蓄奴成灾 | 它把殖民地当成了自己的肉笼。这么滥收，太过分。 |
| SSC_Culture_SlaveCount_Three_Social.0 | 其主蓄奴过多 | 它的奴已经过了三位。这种贪多，看不惯。 |
| SSC_Culture_SlaveCount_Three_Social.1 | 其主蓄奴过多 | 它的奴越养越多，一点规矩没有。 |
| SSC_Culture_SlaveCount_Three_Social.2 | 其主蓄奴成灾 | 它把殖民地当成了自己的肉笼。这么滥收，太过分。 |

---

## 4 说明

1. 本稿仅简体中文源文，英文、繁体、俄文不随本稿落地。
2. PreceptDef 的 `thoughtStageDescriptions` 预览文本直接复用对应 ThoughtDef 阶段的 `label`（逐字复用，不改写），故不再单列。
3. IssueDef 的 label（性奴／训导官／主人／主人数量／性奴数量）已定，无需改动。
4. `SSC_CultureNamers.xml` 无文案 TODO，仅引用 `SSC_MoralistPreset`／`SSC_BindingRitualPreset`，候选见本稿「1.3 generalRules」。

## 5 待确认

- 模因 description 是否需要追加一句玩法提示（如解锁性奴／训导官／主人戒律）；原版模因 description 通常纯设定。
- 领袖／道德导师／契约仪式候选词是否符合预期。
