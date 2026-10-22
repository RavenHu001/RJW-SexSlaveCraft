# 主奴秩序文化文案定稿（简体中文源文）

更新日期：2026-10-22。仅 1.7。本稿为 **Def XML 源文（简体中文）**；英文、繁体、俄文另批补充。

主题基调：欲望、堕落、沉溺、支配与被支配。描述需一定程度色情化，但保持本模组既有文风（直白、带身体感，不堆砌辱骂）。

适用范围与规则：

- 本稿给出 ThoughtDef 的 `label` 与 `description` 正文；PreceptDef 的 `thoughtStageDescriptions` 预览文本**直接复用对应 label**，不再单列。
- IssueDef 的 label（性奴／训导官／主人／主人数量／性奴数量）已定，无需改动，仅校对。
- `SSC_CultureNamers.xml` 无文案 TODO，仅引用 `SSC_MoralistPreset`／`SSC_BindingRitualPreset`，其候选在本稿「模因 generalRules」中。

---

## 1 模因 `SSC_Meme_MasterSlaveOrder`

### 1.1 description

> 主人与性奴之间不是压迫，而是位置。每个人都该清楚自己该跪在谁面前、又该让谁跪在自己面前。把欲望理顺了，身体与身体之间才会服帖；被驯得越深，才越接近自己本来的样子。

### 1.2 symbolPacks（三套命名包）

| 序 | ideoName | theme | adjective | member |
| --- | --- | --- | --- | --- |
| 0 | 锁链教 | 锁链 | 执链 | 链徒 |
| 1 | 纵欲圣教 | 欲望 | 纵欲 | 欲众 |
| 2 | 臣服教义 | 臣服 | 驯顺 | 驯奴 |

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
| SSCDESCTODO01 | [deity0_name] 造出第一根锁链，用它教最早的身体学会跪伏、学会侍奉，也学会在侍奉里尝到唯一的安宁。 |
| SSCDESCTODO02 | 族民曾抗拒枷锁，直到 [deity0_name] 让他们看见：只有伏在主人掌下，身体才不再发抖。 |
| SSCDESCTODO03 | [place_name] 最早的定居者争论该谁跪下、谁握绳。[deity0_name] 结束了争执，选定了 [deity0_possessive] 宠儿来执鞭。 |
| SSCDESCTODO04 | 有个 [foeLeader] 来布道，说每一具身体都属于它自己。[deity0_name] 的回应，是把它的 [foeSoldiers] 交到 [memberNamePlural] 手里，从此他们再也无法拒绝。 |
| SSCDESCTODO05 | 一名叫 [founderName] 的 [place_job] 跪在创始人面前，双手奉上自己的绳子，起身成为 [place_name] 最早的 [memberNamePlural]。 |
| SSCDESCTODO06 | [founderName] 得胜之后，[foeLeader] 的 [foeSoldiers] 看清了：锁链之下并无羞耻。他们交出身体，[memberNamePlural] 把它们收作自己人。 |
| SSCDESCTODO07 | [founderName] 在一片绿地上建起殖民地，教会它每一具身体都有位置：有人握绳，有人被握。 |
| SSCDESCTODO08 | 有一次，[founderName] 看见一名殖民者抗拒项圈。[founder_pronoun] 没有动手，只是等着，等到那具身体反过来哀求它曾拒绝的锁链。 |
| SSCDESCTODO09 | 我拜访过一处经营得井井有条的 [memberNamePlural] 殖民地。整洁的床边跪着一名性奴，喘息又安详。显而易见， |
| SSCDESCTODO10 | 只有伏在主人掌下，身体才记得自己为何而生。 |
| SSCDESCTODO11 | 倘若每具身体都找到握着它的那只手，世上便不再有争执，只有跪在石上的膝，和屏息般的臣服。 |
| SSCDESCTODO12 | 超凡机械是意志庞大的机器，但只有主人的意志，能让一具身体自己弓起身来。 |
| SSCDESCTODO13 | 主人是造物者中的造物者：他们支配身体，一如超凡机械支配物质。 |
| SSCDESCTODO14 | [inTheEnd]，超凡机械会从锁链里学会一切，并跪向那只驯服了支配本身的手。 |
| SSCDESCTODO15 | 肉体的精魂并不渴望自由；它们渴望一只可以服从的手，并把这份低语送进每一个经过的身体。 |
| SSCDESCTODO16 | 性奴的每一次跪下，都是精魂听得见的祷告；主人的每一次握住，都是回应。 |
| SSCDESCTODO17 | 当最后一具自由的身体跪下，精魂便会聚拢，把自己结成一条绳，世界终于认清了自己的位置。 |

---

## 2 戒律 description

### 2.1 性奴（SSC_Issue_SexSlave）

| defName | description |
| --- | --- |
| SSC_Precept_SexSlave_Hostile | 性奴是被毁掉的人。把一具身体驯成那副模样，还管这叫乐事，这种事该被连根铲掉。 |
| SSC_Precept_SexSlave_Revulsion | 跪着还甘之如饴、被用到分不清羞耻和快感的东西，光是看着就让人反胃。 |
| SSC_Precept_SexSlave_Indifference | 别人爱跪就跪，与我无关。只要他们的锁链别拖到我的地板上。 |
| SSC_Precept_SexSlave_Respect | 性奴也是人。跪下是它和主人之间的事，该给它的那点体面，不能省。 |
| SSC_Precept_SexSlave_Sacred | 性奴不是被毁掉的人，是被选中的人。跪下去那一刻它比谁都干净；被驯得越深，越接近它本来的样子。 |
| SSC_Precept_SexSlave_Envy | 被驯成那样，未必是坏事。看着它跪在那里，谁心里没生出一丝羡慕，那才是一具身体该去盼望的活法。 |

### 2.2 训导官（SSC_Issue_Trainer）

| defName | description |
| --- | --- |
| SSC_Precept_Trainer_Hostile | 让性奴去管教性奴，是对主奴次序的亵渎。鞭子只能握在主人手里；握在奴手里，只会把整座院子弄脏。 |
| SSC_Precept_Trainer_Revulsion | 一条奴，身上还留着别人的印子，转头就去摆弄下一个，看着就恶心。 |
| SSC_Precept_Trainer_Indifference | 训导官是谁、怎么调教，与我无关。只要别闹到我院子里来。 |
| SSC_Precept_Trainer_Reverence | 训导官替主人分担调教之劳，手上那套是练出来的。该给它的敬意，得给。 |
| SSC_Precept_Trainer_SacredOffice | 被主人挑出来执鞭的性奴，担的是一份圣职。它把自己受过的驯服，原样交到下一个性奴身上；被它调教，是仅次于被主人亲自调教的荣幸。 |

### 2.3 主人（SSC_Issue_Master）

| defName | description |
| --- | --- |
| SSC_Precept_Master_Hostile | 自称主人的人，都是窃据他人身体的小偷。锁链不该握在任何人手里。 |
| SSC_Precept_Master_Revulsion | 攥着一根鞭子，不说明你是什么人物。离了那根鞭子，你什么都不是。 |
| SSC_Precept_Master_Indifference | 主人不主人的，与我无关。别拿你那套来管我就行。 |
| SSC_Precept_Master_Deference | 主人该受敬重。它的意志在自己名下的身体上，优先于旁人，这是它挣来的位置。 |
| SSC_Precept_Master_HolyMaster | 主人不是管事的，是这块地方供着的那一位。主人一句话重过一切；为主人跪下不是屈辱，是荣耀。 |

### 2.4 数量（SSC_Issue_MasterCount / SSC_Issue_SlaveCount）

| defName | description |
| --- | --- |
| SSC_Precept_MasterCount_One | 一个殖民地，一个主人就够了。第二个人敢自称主人，就是僭越。 |
| SSC_Precept_MasterCount_Three | 最多三位主人，各养各的奴，各管各的院。再多，院子里就该打起来了。 |
| SSC_Precept_MasterCount_Unlimited | 只要养得起，主人越多越好。谁有本事把一具身体驯到跪下，谁就有资格当主人。 |
| SSC_Precept_SlaveCount_One | 一名主人只该有一个性奴，多了就是分心。 |
| SSC_Precept_SlaveCount_Three | 一名主人最多三个性奴。够用，也不至于乱了规矩。 |
| SSC_Precept_SlaveCount_Unlimited | 只要养得起，性奴越多越好。身后跪得越多，越显得主人有本事。 |

---

## 3 ThoughtDef 阶段 label / description

### 3.1 性奴心情与社交（SSC_CultureThoughts_SexSlave.xml）

| defName.stage | label | description |
| --- | --- | --- |
| SSC_Culture_SexSlave_Hostile_Self.0 | 身为性奴 | 我竟成了这副模样，被拴着、被驯着，还被人摆弄到忘了自己是谁。这身子现在脏得洗不干净了。 |
| SSC_Culture_SexSlave_Revulsion_Self.0 | 身为性奴 | 落到这步田地，连自己都嫌恶。这具被人摆布过的身子，我看一眼就反胃。 |
| SSC_Culture_SexSlave_Sacred_Self.0 | 被选中之身 | 我被选中了。跪着比站着更让我心安；被驯得越深，越觉得自己干净。 |
| SSC_Culture_SexSlave_Envy_Self.0 | 如愿以偿 | 我终于成了性奴。身体被人握在手里，心里反而踏实得发烫。 |
| SSC_Culture_SexSlave_Envy_Self.1 | 渴望被驯 | 看着那些跪着的人，我心里空落落的。被那样驯着、用着，我竟有点想要。 |
| SSC_Culture_SexSlave_Hostile_Environment.0 | 身边有性奴 | 附近就拴着那种东西。光是知道它们在那里，我就浑身不自在。 |
| SSC_Culture_SexSlave_Revulsion_Environment.0 | 身边有性奴 | 眼前有性奴在，我连饭都吃不下。这种地方，一刻都不想多待。 |
| SSC_Culture_SexSlave_Sacred_Environment.0 | 众奴环绕 | 性奴越多，这地方越像该有的样子。被驯服的喘息声，是最好听的祷告。 |
| SSC_Culture_SexSlave_Revulsion_Social.0 | 厌恶性奴 | 它跪在那里还一脸受用的样子，我看了就恶心。 |
| SSC_Culture_SexSlave_Respect_Social.0 | 敬重性奴 | 它既然担了这份位置，就该得到应得的那点体面。 |
| SSC_Culture_SexSlave_Sacred_Social.0 | 敬畏性奴 | 它是被选中的人。被驯得那么深，比谁都干净，值得我低头。 |
| SSC_Culture_SexSlave_Envy_Social.0 | 艳羡性奴 | 看着它被驯成那样，我竟说不清是羡慕还是渴望。 |
| SSC_Culture_SexSlave_Hostile_Social.0 | 敌视性奴 | 那种被人拴着还讨好的东西，我一眼都不想看。 |

### 3.2 训导官心情与社交（SSC_CultureThoughts_Trainer.xml）

| defName.stage | label | description |
| --- | --- | --- |
| SSC_Culture_Trainer_Hostile_Self.0 | 身为训导官 | 我竟去管教别的性奴。自己都只是一条被拴着的奴，还有什么脸拿鞭子。 |
| SSC_Culture_Trainer_Hostile_Social.0 | 敌视训导官 | 一条奴也配拿鞭子？它摆弄同类的样子，我只觉得荒唐又恶心。 |
| SSC_Culture_Trainer_Revulsion_Self.0 | 身为训导官 | 被主人使唤去调教别的性奴，这差事做得我浑身不自在。 |
| SSC_Culture_Trainer_Revulsion_Social.0 | 厌恶训导官 | 身上还留着别人的印子，转头就去摆弄下一个，看着就恶心。 |
| SSC_Culture_Trainer_Reverence_Self.0 | 身为训导官 | 主人把调教别的性奴的差事交给了我。这份被信任的感觉，还不错。 |
| SSC_Culture_Trainer_Reverence_Social.0 | 敬重训导官 | 它替主人分担调教之劳，手上那套是练出来的，值得敬重。 |
| SSC_Culture_Trainer_SacredOffice_Self.0 | 执鞭圣职 | 我担的是一份圣职。把自己受过的驯服，交到下一个性奴身上，这是主人的恩许。 |
| SSC_Culture_Trainer_SacredOffice_Social.0 | 敬畏训导官 | 它是主人挑出来执鞭的人，调教着众奴，值得我敬一分。 |

### 3.3 主人及领袖心情与社交（SSC_CultureThoughts_Master.xml）

| defName.stage | label | description |
| --- | --- | --- |
| SSC_Culture_Master_Hostile_Self.0 | 身为主人 | 我成了自己最看不起的那种人，攥着别人的身子，还管这叫支配。可耻。 |
| SSC_Culture_Master_Hostile_ActualMaster.0 | 受人役使 | 我竟有个主人，还任它摆布。这副听话的身子，我自己都瞧不起。 |
| SSC_Culture_Master_Hostile_Social.0 | 敌视主人 | 它把别人驯成玩物还洋洋得意。这种人，我一眼都不想认。 |
| SSC_Culture_Master_Hostile_Leader.0 | 领袖是主人 | 连领袖都成了那种东西。这个殖民地，怕是从根上烂了。 |
| SSC_Culture_Master_Revulsion_Self.0 | 身为主人 | 握着锁链的手，总让我觉得自己脏。可这差事，我还得接着做。 |
| SSC_Culture_Master_Revulsion_ActualMaster.0 | 受人役使 | 被主人使唤来使唤去，我心里只剩一层腻烦。 |
| SSC_Culture_Master_Revulsion_Social.0 | 厌恶主人 | 攥着根鞭子就以为自己了不起，离了那根鞭子，它算什么。 |
| SSC_Culture_Master_Revulsion_Leader.0 | 领袖是主人 | 领袖竟也去当主人。这个位置，看着都让人腻味。 |
| SSC_Culture_Master_Deference_Self.0 | 身为主人 | 被敬着、被依着，这位置是我一步一步挣来的。跪在我脚下的每一具身体，都是我的。 |
| SSC_Culture_Master_Deference_ActualMaster.0 | 归于主人 | 有主人在，心就定了。它的意志，就是我该听的话。 |
| SSC_Culture_Master_Deference_Social.0 | 敬重主人 | 它担着主人的位置，值得一分敬重。 |
| SSC_Culture_Master_Deference_Leader.0 | 主人领袖 | 主人带着我们，这殖民地才算有主心骨。 |
| SSC_Culture_Master_Deference_Leader.1 | 领袖非主人 | 领袖连主人都不是，凭什么坐在那个位置。 |
| SSC_Culture_Master_Deference_Leader.2 | 性奴领袖 | 让一条性奴爬到领袖的位置上，这座殖民地是彻底没规矩了。 |
| SSC_Culture_Master_HolyMaster_Self.0 | 身为圣主 | 这块地方供着我。每一具跪下的身体，都是我应得的敬奉。 |
| SSC_Culture_Master_HolyMaster_ActualMaster.0 | 侍于圣主 | 能跪在主人脚下，是我的福分。它的一句话，比我自己的心思还重。 |
| SSC_Culture_Master_HolyMaster_Social.0 | 敬畏圣主 | 主人就是这块地方的神。对它低头，是我的荣耀。 |
| SSC_Culture_Master_HolyMaster_Leader.0 | 圣主在位 | 圣主领着众人，天经地义。谁不低头，谁就不配留下。 |
| SSC_Culture_Master_HolyMaster_Leader.1 | 领袖非圣主 | 一个连主人都不是的人，也配被称作领袖？ |
| SSC_Culture_Master_HolyMaster_Leader.2 | 性奴窃位 | 一条性奴竟敢坐在领袖的位置上。这是对主人最大的亵渎。 |
| SSC_Culture_Master_Deference_SexSlaveLeaderSocial.0 | 性奴领袖 | 一条被拴着的奴，也配以领袖自居？这个位置，我看不下去了。 |
| SSC_Culture_Master_HolyMaster_SexSlaveLeaderSocial.0 | 性奴窃位 | 让性奴窃据领袖之位，是对主奴次序最恶毒的嘲弄。 |

### 3.4 数量心情与社交（SSC_CultureThoughts_Counts.xml）

| defName.stage | label | description |
| --- | --- | --- |
| SSC_Culture_MasterCount_One_Self.0 | 主人过多 | 这个殖民地的主人太多了。多出来的每一个，都是对次序的冒犯。 |
| SSC_Culture_MasterCount_Three_Self.0 | 主人过多 | 主人已经超出三位。院子里的人太多了，迟早要出事。 |
| SSC_Culture_SlaveCount_One_Self.0 | 性奴过多 | 我名下的性奴超出了该有的数目。每一具多余的身子，都在分我的心。 |
| SSC_Culture_SlaveCount_Three_Self.0 | 性奴过多 | 我的性奴不止三个了。这么多身子，乱了该有的规矩。 |
| SSC_Culture_SlaveCount_One_Social.0 | 其主蓄奴过多 | 它名下的性奴超出了规矩。这种贪多，我看不惯。 |
| SSC_Culture_SlaveCount_One_Social.1 | 其主蓄奴过多 | 它的性奴越养越多，一点规矩都没有。 |
| SSC_Culture_SlaveCount_One_Social.2 | 其主蓄奴成灾 | 它把殖民地当成了自己的肉笼。这样滥收性奴，实在过分。 |
| SSC_Culture_SlaveCount_Three_Social.0 | 其主蓄奴过多 | 它的性奴已经超过三位。这种贪多，我看不惯。 |
| SSC_Culture_SlaveCount_Three_Social.1 | 其主蓄奴过多 | 它的性奴越养越多，一点规矩都没有。 |
| SSC_Culture_SlaveCount_Three_Social.2 | 其主蓄奴成灾 | 它把殖民地当成了自己的肉笼。这样滥收性奴，实在过分。 |

---

## 4 落地说明

1. 本稿为**简体中文源文**，写入 Def XML 的 `<description>` 与 ThoughtDef 的 `<stages><li><label>/<description>`。
2. PreceptDef 的 `thoughtStageDescriptions` 预览文本 = 对应 ThoughtDef 阶段的 `label`（逐字复用，不改写）。
3. `generalRules`／`symbolPacks`／`descriptionMaker` 的键名与值照第 1 节填入；DefInjected 用相同键名，值为本稿文案。
4. 英文、繁体、俄文在源文确认后另行翻译，不随本稿一并落地。

## 5 待确认

- 模因 description 是否需要加一句玩法提示（如「信息：解锁性奴／训导官／主人戒律与首席性奴职位」），原版模因 description 通常纯设定、不含玩法说明。
- 领袖／道德导师／契约仪式候选词是否符合预期（现为：大主人／执鞭者／主上；欲望祭司／沉沦引导者／驯心者；锁链契约／臣服礼／沉沦圣礼）。
