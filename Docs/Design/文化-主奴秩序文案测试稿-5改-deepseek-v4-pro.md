# 主奴秩序文化文案测试稿 v5改

> **文件版本**：v5改（基于 v5 的露骨化修订版）
> **模型版本**：deepseek-v4-pro
> **编写日期**：2026-10-22
> **适用版本**：仅 RimWorld 1.7
> **主题基调**：欲望、堕落、沉溺、支配与被支配（成人向）

---

## 修订说明

> 本稿为「5版改」：在 v5 基础上按「露骨化」风格整体改写。凡叙事性文案（模因描述、文化生成背景段落、正向／主动立场的戒律与 Thought 描述）已改写得更直白露骨；结构性条目（命名包、称谓、通用组合词、阶段效果数值、label 短标题、负向立场描述、IssueDef／RulePackDef 校对项）保持与 v5 一致，不予更动。

---

## 使用说明

1. 本文为**中文（简体）文案测试稿**，供维护者审阅后落地到 `1.7/Languages/ChineseSimplified/DefInjected/` 下的 `MemeDef`、`PreceptDef`、`ThoughtDef` 三个 `SSC_Culture.xml`（及 `Sexslavecraft/Languages` 源码镜像）。
2. 本稿**不改动任何 defName、翻译键、规则名、索引、`->`、priority、include、tag、uses**，仅替换右侧文案值。左侧与机制契约保持不变。
3. 涉及 `IssueDef`（议题名称）与 `RulePackDef`（命名连接规则）的部分为**同步校对**，无需新编文学文本，本文已注明核对结论。
4. 术语基准沿用既有约定：**性奴 / 主人 / 训导官 / 持链 / 锁链 / 项圈 / 调教 / 契约 / 欲望 / 沉溺 / 堕落 / 支配与服从**。文本一律以“性奴”直呼，不做委婉替换，且性奴默认女性（第三人称一律以“她／她们”指代）；露骨化改写中使用的直白性描写词（阳具／肉棒、淫穴／肉穴、抽送／贯穿／肏、高潮／失禁、呻吟／浪叫、撅臀／湿透等）为配合成人向主题的表述，不改变任何机制含义。
5. 本文按 [文案交接清单](文化-主奴秩序文案交接清单.md) 的条目顺序编排，每条给出对应占位标识或翻译键，便于逐项回填。

---

## 一、模因 MemeDef（`SSC_Meme_MasterSlaveOrder`）

### 1.1 模因描述

- **`SSC_Meme_MasterSlaveOrder.description`**
> 主奴秩序将人与人的羁绊简化为支配与服从：主人以锁链与契约束缚性奴的肉体，令其一丝不挂地跪伏膝前，以唇舌与身体侍奉，在鞭笞与占有的调教中逼出阵阵呻吟，直至高潮失禁。在这一文化里，欲望是圣礼，交合是祷告，项圈与锁链是最高等级的饰物，鞭痕与吻痕是荣耀的凭证——沉溺即是归途，堕落即是升华。

### 1.2 文化命名包（symbolPacks，3 组 × 4 字段）

| 组 | 字段 | 占位标识 | 中文文案 |
| --- | --- | --- | --- |
| 0 | ideoName | SSCNAMETODO01 | 锁链之约 |
| 0 | theme | SSCTHEMETODO01 | 支配 |
| 0 | adjective | SSCADJTODO01 | 锁链的 |
| 0 | member | SSCMEMBERTODO01 | 持链者 |
| 1 | ideoName | SSCNAMETODO02 | 欢愉圣契 |
| 1 | theme | SSCTHEMETODO02 | 沉溺 |
| 1 | adjective | SSCADJTODO02 | 沉溺的 |
| 1 | member | SSCMEMBERTODO02 | 沉溺者 |
| 2 | ideoName | SSCNAMETODO03 | 堕落圣所 |
| 2 | theme | SSCTHEMETODO03 | 堕落 |
| 2 | adjective | SSCADJTODO03 | 堕落的 |
| 2 | member | SSCMEMBERTODO03 | 供奉者 |

### 1.3 通用组合词（generalRules，索引 0–2）

| 占位标识 | 规则左侧 | 中文文案 |
| --- | --- | --- |
| SSCADJTODO | memeAdjective | 驯服的 |
| SSCCREEDTODO | memeCreed | 服从即极乐 |
| SSCPREFIXTODO | memeHyphenPrefix | 链- |

### 1.4 领袖称谓（`SSC_LeaderPreset`，索引 4–6）

| 占位标识 | 中文文案 |
| --- | --- |
| SSCLEADERTODO01 | 至高主人 |
| SSCLEADERTODO02 | 万奴之主 |
| SSCLEADERTODO03 | 锁链之尊 |

### 1.5 道德导师称谓（`SSC_MoralistPreset`，索引 7–9）

> 依据设计决定，“首席性奴”降级为道德导师（传道者／传教士）称谓池备选名之一，与另两条并列随机，非固定称谓。

| 占位标识 | 中文文案 |
| --- | --- |
| SSCMORALTODO01 | 首席性奴 |
| SSCMORALTODO02 | 欲望导师 |
| SSCMORALTODO03 | 欢愉传道 |

### 1.6 契约仪式名称（`SSC_BindingRitualPreset`，索引 10–12）

| 占位标识 | 中文文案 |
| --- | --- |
| SSCRITUALTODO01 | 缔约之礼 |
| SSCRITUALTODO02 | 项圈加冕 |
| SSCRITUALTODO03 | 沉沦圣仪 |

### 1.7 文化生成背景段落（descriptionMaker，六类框架 17 条）

> 此类为 `Ideo.description` 的自动生成背景／起源段落，仅持有主奴秩序时加入原版候选，不保证每次生成都命中 SSC 段落。

#### TheistMyth（神祇创世神话）

| 占位标识 | 规则 | 中文文案 |
| --- | --- | --- |
| SSCDESCTODO01 | creation | 最初，大神以锁链分隔了混沌：一端是握着缰绳、胯下昂然的主人，一端是四肢匍匐、撅臀承欢的奴。大神说，欲望是世界的轴心，交合是万物的律法——主人以阳具贯穿奴隶的淫穴，奴隶以高潮与呻吟敬拜其主，自此主奴各归其位，秩序遂成。 |
| SSCDESCTODO02 | episode | 传说不驯的众生曾妄图挣脱锁链，大神便降下第一枚项圈，套在倨傲者的颈上。倨傲者被勒得跪伏于地，颈环深深勒进血肉，大神却将她翻过身来，抵入她的淫穴，一下下贯穿到底。倨傲者在痛楚与耻辱中第一次攀上高潮，自此甘愿为奴。 |

#### TheistFounder（神祇引导创教）

| 占位标识 | 规则 | 中文文案 |
| --- | --- | --- |
| SSCDESCTODO03 | intro | 在动乱与饥荒的年代，大神向一位先知显灵，赐予他一条不锈的锁链与不竭的欲望。先知由此悟得真道：人间的安宁，系于主人以锁链束奴、以阳具浇灌众奴，众奴以淫穴与呻吟回应，各安其分。 |
| SSCDESCTODO04 | conflict | 世人讥笑先知，说人怎可自甘为奴。先知便当众褪去衣衫、跪下身来，让锁链套上自己的颈，又撅起淫穴任人贯入，称这被填满的屈从便是最大的自由；愚者畏惧，智者跪拜。 |
| SSCDESCTODO05 | founderJoin | 先知收下第一位弟子，教其以欲望为誓、以肉身为礼。弟子褪尽衣衫，献上自己的项圈与肉身，任先知破入、占满、灌满，从此誓愿一生以唇舌与淫穴侍奉先知为主。 |
| SSCDESCTODO06 | victory | 当第一个王国在锁链与欢愉之上建立，先知的教义遂成国法。主人以阳具为权杖、以高潮为赏赐，奴隶以承欢受肏为荣、以呻吟浪叫为颂，天下乃定。 |

#### IdeologicalFounder（思想创始者）

| 占位标识 | 规则 | 中文文案 |
| --- | --- | --- |
| SSCDESCTODO07 | setup | 哲人观察众生，见强者凌弱、欲望横流、交合无人节制，遂悟：与其让欲望无序流窜，不如将其纳入支配与服从的秩序，让每一处淫穴都有归属、每一根阳具都有归处。 |
| SSCDESCTODO08 | story | 哲人立下主奴之道，言称“欲望不灭，唯链可束”。他的门徒首开契约之礼，将交合与服从订立为盟约——主以阳具为誓，奴以淫穴为契，信众日众。 |

#### IdeologicalLesson（思想启示／教训）

| 占位标识 | 规则 | 中文文案 |
| --- | --- | --- |
| SSCDESCTODO09 | lessonIntro | 这一文化的信徒自幼聆听一则训诫：欲望是人与生俱来的火种，唯有将肉身交付于主人，任其抽插浇灌，方能燃烧成温暖的高潮，而非焚身的烈焰。 |
| SSCDESCTODO10 | lesson | 长者教导后辈：锁链不是枷锁，而是荣耀的凭据；项圈不是耻辱，而是被选中者的印记。俯首撅臀并非软弱，而是将淫穴献于主人、归顺于更深的贯穿与更大的欢愉。 |
| SSCDESCTODO11 | lessonReinforcement | 每有迷惘者，信徒便剥去她的衣衫、缚上锁链，引她亲历一次沉溺——被主人压在身下、一下下肏到高潮失禁的快感，远胜于孤身漂泊的空虚。迷惘者遂重拾链环，甘愿归顺。 |

#### ArchistDescription（超凡智能信仰）

| 占位标识 | 规则 | 中文文案 |
| --- | --- | --- |
| SSCDESCTODO12 | archistBasis | 古老的超凡智能观测到：当支配与服从被制度化、交合与调教被规范化，人群的欲望便趋于有序，冲突随之平息。于是它向世人降下主奴秩序的算法，为每具饥渴的肉体匹配一条锁链与一个归属。 |
| SSCDESCTODO13 | archistFact | 超凡智能的推算显示，自愿沉溺于锁链、被反复贯穿至高潮的个体，其满足与忠诚都达到峰值；主与奴的契约——主人以阳具为誓、奴隶以淫穴为印——是它所见过最稳定的社会纽带。 |
| SSCDESCTODO14 | archistProphecy | 预言称，当项圈遍布每一座城邦、锁链环环相扣、每一张床榻都响起交合的呻吟，人类将在无尽的贯穿与沉沦中抵达超凡智能所许诺的、永恒而有序的极乐。 |

#### AnimistDescription（泛灵信仰）

| 占位标识 | 规则 | 中文文案 |
| --- | --- | --- |
| SSCDESCTODO15 | animistBasis | 万物皆有灵，而欲望是其中最强的灵。信徒相信，唯有让欲望之灵被主人之手牵引、被阳具贯穿驯服，它才会安分守己，不再作乱。 |
| SSCDESCTODO16 | animistFact | 传说锁链的铁环中栖居着“服从之灵”，项圈的皮革里沉睡着“沉溺之灵”，主人胯下的阳物里则住着“征服之灵”。当锁链扣上、阳物入穴，三灵合欢，便如猛兽归栏，狂躁的心得以安宁。 |
| SSCDESCTODO17 | animistProphecy | 灵卜者言：终有一日，天地将化为一副巨大的缰绳，所有生灵各归其主——主人执链纵欲，奴隶撅臀承欢，在无尽的贯穿、高潮与欢愉中归于太初的秩序。 |

---

## 二、议题 IssueDef（同步校对）

| defName | 现 label | 校对结论 |
| --- | --- | --- |
| SSC_Issue_SexSlave | 性奴 | 保持，无需修改 |
| SSC_Issue_Trainer | 训导官 | 保持，无需修改 |
| SSC_Issue_Master | 主人 | 保持，无需修改 |
| SSC_Issue_MasterCount | 主人数量 | 保持，无需修改 |
| SSC_Issue_SlaveCount | 性奴数量 | 保持，无需修改 |

---

## 三、戒律 PreceptDef（description）

### 3.1 性奴（`SSC_Issue_SexSlave`，6 条）

- **`SSC_Precept_SexSlave_Hostile.description`**（敌视）
> 视性奴制为应当铲除的恶——人不应被锁链缚身、以肉身侍奉他人。本文化将这种支配视为最可憎的罪行，必欲除之而后快。

- **`SSC_Precept_SexSlave_Revulsion.description`**（厌恶）
> 对性奴制深感反感，见人俯首于链下便觉胃中翻涌，不愿与任何以身为奴者共处一室。

- **`SSC_Precept_SexSlave_Indifference.description`**（漠视）
> 不关心性奴制的存废，也无意为之喝彩。锁链与项圈在这些人眼中，不过是他人的私事。

- **`SSC_Precept_SexSlave_Respect.description`**（尊重）
> 承认性奴亦是人，即便颈上套着项圈，也当给予基本尊重与人格尊严。

- **`SSC_Precept_SexSlave_Sacred.description`**（神圣）
> 视性奴为被神选中的存在——她们赤身裸体、四肢匍匐，将每一寸皮肉与每一次高潮都奉献于主人，在鞭笞、插入与吞咽中淬炼出圣洁。这种彻底的驯服本身，便是对神最高的礼赞。

- **`SSC_Precept_SexSlave_Envy.description`**（艳羡）
> 艳羡那颈上戴着项圈之人：她们已寻得归宿，肉身不再寂寞，夜夜被主人压在身下、填满最深处，在交合与支配中攀上高潮。未持链的信徒暗自发痒、下身潮湿，渴望有朝一日自己也能跪伏于主人胯下，被占有、被调教、被肏到甘愿沉沦。

### 3.2 训导官（`SSC_Issue_Trainer`，5 条）

- **`SSC_Precept_Trainer_Hostile.description`**（敌视）
> 视训导官为对主奴秩序的亵渎——主人与奴之间，岂容第三人横插一手。调教之责当归主人，假手他人便是僭越。

- **`SSC_Precept_Trainer_Revulsion.description`**（厌恶）
> 对训导官的存在强烈反感，见其以鞭与链驯人，便觉此人下作不堪。

- **`SSC_Precept_Trainer_Indifference.description`**（漠视）
> 不关心训导官的存续与作为，调教之事与己无关。

- **`SSC_Precept_Trainer_Reverence.description`**（尊敬）
> 承认训导官的调教技艺，对其能将野性之人鞭笞驯服、调教成俯首承欢的合格性奴的本领，致以由衷敬意。

- **`SSC_Precept_Trainer_SacredOffice.description`**（圣职）
> 视训导官为一份神圣职务——他们以鞭为法器、以链为圣器，替主人调教出翘臀红肿、淫穴濡湿、顺从而虔诚的奴，其职如祭司般庄严。

### 3.3 主人（`SSC_Issue_Master`，5 条）

- **`SSC_Precept_Master_Hostile.description`**（敌视）
> 视主人为窃据他人身体的僭越者——凭一条锁链便攫取他人肉身与意志，这是不可容忍的暴行。

- **`SSC_Precept_Master_Revulsion.description`**（厌恶）
> 对“主人”这一身份强烈反感，见人执链而行便心生厌恶。

- **`SSC_Precept_Master_Indifference.description`**（漠视）
> 对主人身份不置可否，谁是主、谁是奴，皆与己无涉。

- **`SSC_Precept_Master_Deference.description`**（尊奉）
> 敬重主人的意志并甘愿听命——主人是欲望的主人，也是秩序的主人，跪伏于主人胯下、顺从主人的每一声命令，即顺从天命。

- **`SSC_Precept_Master_HolyMaster.description`**（圣主）
> 视主人为被供奉的神祇，予以最高尊崇。主人的每一次抽送、每一声命令皆是神意，信徒当以唇舌、乳房与淫穴献身侍奉，将高潮作为祭品奉上——能为主人泄身，是信徒至高的荣耀。

### 3.4 主人数量（`SSC_Issue_MasterCount`，3 条）

- **`SSC_Precept_MasterCount_One.description`**（一主为限）
> 殖民地至多接纳一位主人。多主并立则乱，唯有一主，欲望方能各安其位。

- **`SSC_Precept_MasterCount_Three.description`**（三主以内）
> 殖民地最多接受三位主人。主人可以多，但不可滥，秩序须在节制之中。

- **`SSC_Precept_MasterCount_Unlimited.description`**（主不厌多）
> 主人多多益善，越多主人，越多欲望得偿，越多奴仆有处可归。

### 3.5 性奴数量（`SSC_Issue_SlaveCount`，3 条）

- **`SSC_Precept_SlaveCount_One.description`**（一奴为限）
> 每位主人至多拥有一位性奴。独占其淫穴，方显恩宠专一，链环方能严丝合缝。

- **`SSC_Precept_SlaveCount_Three.description`**（三奴以内）
> 每位主人最多拥有三位性奴。齐人之福可享、三人之淫穴可贯，但不可失了调教的用心。

- **`SSC_Precept_SlaveCount_Unlimited.description`**（奴不厌多）
> 性奴多多益善，越多性奴，越多淫穴可供驰骋，主人越是荣光，欲望越是酣畅。

### 3.6 阶段效果说明（thoughtStageDescriptions，共 19 条）

> 此类为戒律预览里解释情绪／好感效果的功能说明，须与已确认数值逐格一致（参考设计文档第 3、5、6 节）。

- **`SSC_Precept_SexSlave_Sacred.comps.1.thoughtStageDescriptions.0`**
> 环境中有性奴时按人数获得心情加成：1 名 +1、2 名 +3、3 名 +6、4 名 +10、5 名及以上 +15。

- **`SSC_Precept_SexSlave_Envy.comps.0.thoughtStageDescriptions.0`**
> 本人是性奴：心情 +10。
- **`SSC_Precept_SexSlave_Envy.comps.0.thoughtStageDescriptions.1`**
> 本人既非性奴也非主人：心情 -6。

- **`SSC_Precept_Master_Deference.comps.3.thoughtStageDescriptions.0`**
> 领袖是主人（本人非主人）：心情 +3。
- **`SSC_Precept_Master_Deference.comps.3.thoughtStageDescriptions.1`**
> 领袖是普通非主人：心情 -3。
- **`SSC_Precept_Master_Deference.comps.3.thoughtStageDescriptions.2`**
> 领袖是性奴：心情 -6。

- **`SSC_Precept_Master_HolyMaster.comps.3.thoughtStageDescriptions.0`**
> 领袖是主人（本人非主人）：心情 +6。
- **`SSC_Precept_Master_HolyMaster.comps.3.thoughtStageDescriptions.1`**
> 领袖是普通非主人：心情 -6。
- **`SSC_Precept_Master_HolyMaster.comps.3.thoughtStageDescriptions.2`**
> 领袖是性奴：心情 -12。

- **`SSC_Precept_MasterCount_One.comps.0.thoughtStageDescriptions.0`**
> 主人总数超过 1 人时，按超额计罚：超额 1/2/3 名及以上，心情 -6/-15/-27。
- **`SSC_Precept_MasterCount_Three.comps.0.thoughtStageDescriptions.0`**
> 主人总数超过 3 人时，按超额计罚：超额 1/2/3 名及以上，心情 -6/-15/-27。

- **`SSC_Precept_SlaveCount_One.comps.0.thoughtStageDescriptions.0`**
> 本人名下性奴超过 1 人时，按超额计罚：超额 1/2/3 名及以上，心情 -3/-9/-18。
- **`SSC_Precept_SlaveCount_Three.comps.0.thoughtStageDescriptions.0`**
> 本人名下性奴超过 3 人时，按超额计罚：超额 1/2/3 名及以上，心情 -3/-9/-18。

- **`SSC_Precept_SlaveCount_One.comps.1.thoughtStageDescriptions.0`**
> 目标主人名下性奴超额 1 名：好感 -5。
- **`SSC_Precept_SlaveCount_One.comps.1.thoughtStageDescriptions.1`**
> 目标主人名下性奴超额 2 名：好感 -15。
- **`SSC_Precept_SlaveCount_One.comps.1.thoughtStageDescriptions.2`**
> 目标主人名下性奴超额 3 名及以上：好感 -30。

- **`SSC_Precept_SlaveCount_Three.comps.1.thoughtStageDescriptions.0`**
> 目标主人名下性奴超额 1 名：好感 -5。
- **`SSC_Precept_SlaveCount_Three.comps.1.thoughtStageDescriptions.1`**
> 目标主人名下性奴超额 2 名：好感 -15。
- **`SSC_Precept_SlaveCount_Three.comps.1.thoughtStageDescriptions.2`**
> 目标主人名下性奴超额 3 名及以上：好感 -30。

---

## 四、心情／社交 ThoughtDef（阶段 label 与 description）

> 阶段 label 为心情条目短标题，description 为条目下的完整说明句。数值以 ThoughtDef 配置为准，本文不另注。

### 4.1 性奴（12 条／13 阶段）

- **`SSC_Culture_SexSlave_Hostile_Self.stages.0`**
  - label：身为性奴
  - description：我竟沦落至此，颈上锁着耻辱的链子——这比死更令我难以忍受。

- **`SSC_Culture_SexSlave_Revulsion_Self.stages.0`**
  - label：身为性奴
  - description：这副躯体被链子束缚着，每次呼吸都令我心生厌恶。

- **`SSC_Culture_SexSlave_Sacred_Self.stages.0`**
  - label：身为性奴
  - description：我赤身跪伏在主人脚下，任他抽送、鞭笞、填满我的每一处，将高潮尽数奉献——这圣洁的驯服令我浑身发软，满心虔诚。

- **`SSC_Culture_SexSlave_Envy_Self.stages.0`**
  - label：已成为性奴
  - description：我终于如愿以偿，颈上有了项圈，下身被主人反复贯穿——这份被占有、被肏弄的沉溺，正是我梦寐以求的极乐。

- **`SSC_Culture_SexSlave_Envy_Self.stages.1`**
  - label：渴慕为奴
  - description：我既非主人也非性奴，下身瘙痒、欲望无所归依——多么希望有人将链子套上我的颈、将肉棒插进我的身体。

- **`SSC_Culture_SexSlave_Hostile_Environment.stages.0`**
  - label：周围有性奴
  - description：有人被锁链役使，我无法对此视而不见。

- **`SSC_Culture_SexSlave_Revulsion_Environment.stages.0`**
  - label：周围有性奴
  - description：附近有人俯首为奴，令我不适。

- **`SSC_Culture_SexSlave_Sacred_Environment.stages.0`**
  - label：性奴环绕
  - description：这些赤身跪伏、被调教得服服帖帖的性奴环绕四周，她们的呻吟与虔诚令此地更显圣洁。

- **`SSC_Culture_SexSlave_Revulsion_Social.stages.0`**
  - label：厌恶此性奴
  - description：此人身负锁链、以身为奴，令我厌弃。

- **`SSC_Culture_SexSlave_Respect_Social.stages.0`**
  - label：尊重此性奴
  - description：此人虽为奴，亦是人，我敬其人格。

- **`SSC_Culture_SexSlave_Sacred_Social.stages.0`**
  - label：敬奉此性奴
  - description：此人将肉身与高潮尽数献于主人，是被神选中的存在，我对其满怀敬意。

- **`SSC_Culture_SexSlave_Envy_Social.stages.0`**
  - label：艳羡此性奴
  - description：此人已寻得归宿，夜夜被主人占有、肏弄到高潮，令我下身发痒、艳羡不已。

- **`SSC_Culture_SexSlave_Hostile_Social.stages.0`**
  - label：仇视此性奴
  - description：此人甘心为奴、自甘堕落，是可耻的帮凶。

### 4.2 训导官（8 条／8 阶段）

- **`SSC_Culture_Trainer_Hostile_Self.stages.0`**
  - label：身为训导官
  - description：我竟担此亵渎之职，横在主人与奴之间，心中唯有羞耻。

- **`SSC_Culture_Trainer_Hostile_Social.stages.0`**
  - label：敌视此训导官
  - description：此人僭越主奴秩序，以调教为名亵渎神圣，我深恶之。

- **`SSC_Culture_Trainer_Revulsion_Self.stages.0`**
  - label：身为训导官
  - description：做这份差事让我浑身不自在。

- **`SSC_Culture_Trainer_Revulsion_Social.stages.0`**
  - label：厌恶此训导官
  - description：见此人执鞭驯人，我便心生反感。

- **`SSC_Culture_Trainer_Reverence_Self.stages.0`**
  - label：身为训导官
  - description：我能以鞭笞与操弄调教他人，使之成为俯首承欢的奴，这是份值得自豪的差事。

- **`SSC_Culture_Trainer_Reverence_Social.stages.0`**
  - label：尊敬此训导官
  - description：此人调教有方，能驯得再倔强的人也甘愿撅臀承欢，我敬其本领。

- **`SSC_Culture_Trainer_SacredOffice_Self.stages.0`**
  - label：司掌圣职
  - description：我以鞭为法器、以链为圣器，将野性之人抽打到翘臀红肿、下身湿透，再调教成虔诚顺从的奴——这神圣职务令我满心庄严。

- **`SSC_Culture_Trainer_SacredOffice_Social.stages.0`**
  - label：敬奉此训导官
  - description：此人以鞭与链、以阳具与技艺代主调教众奴，所司乃神圣职务，我对其满怀敬意。

### 4.3 主人（18 条／22 阶段）

- **`SSC_Culture_Master_Hostile_Self.stages.0`**
  - label：身为主人
  - description：我竟成了窃据他人身体的僭越者，这身份令我寝食难安。

- **`SSC_Culture_Master_Hostile_ActualMaster.stages.0`**
  - label：受制于主人
  - description：我的链子握在他人手中，被人当作玩物支配——这屈辱难以忍受。

- **`SSC_Culture_Master_Hostile_Social.stages.0`**
  - label：仇视此主人
  - description：此人以锁链窃据他人肉身与意志，是可憎的暴君。

- **`SSC_Culture_Master_Hostile_Leader.stages.0`**
  - label：领袖竟是主人
  - description：我们的领袖竟是个窃据者，这令我痛心。

- **`SSC_Culture_Master_Revulsion_Self.stages.0`**
  - label：身为主人
  - description：执链驭奴的身份令我心中不适。

- **`SSC_Culture_Master_Revulsion_ActualMaster.stages.0`**
  - label：受制于主人
  - description：有人握着我的链子，这让我心生厌恶。

- **`SSC_Culture_Master_Revulsion_Social.stages.0`**
  - label：厌恶此主人
  - description：此人的主人做派令我反感。

- **`SSC_Culture_Master_Revulsion_Leader.stages.0`**
  - label：领袖竟是主人
  - description：领袖竟是这般身份，令我心中不快。

- **`SSC_Culture_Master_Deference_Self.stages.0`**
  - label：身为主人
  - description：我执链驭奴、号令众人，众奴撅臀待我、任我驰骋，这主人之位令我自豪。

- **`SSC_Culture_Master_Deference_ActualMaster.stages.0`**
  - label：有主可依
  - description：我的链子握在主人手中，我的淫穴归主人所有，这份被占有的归属让我安心。

- **`SSC_Culture_Master_Deference_Social.stages.0`**
  - label：敬奉此主人
  - description：此人执掌欲望、统御奴仆，众奴拜服其胯下，我敬重其主人之威。

- **`SSC_Culture_Master_Deference_Leader.stages.0`**
  - label：领袖为主人
  - description：我们的领袖身为主人，统御有方，我由衷拥戴。

- **`SSC_Culture_Master_Deference_Leader.stages.1`**
  - label：领袖非主人
  - description：领袖竟非主人，这样的领袖如何统御欲望？

- **`SSC_Culture_Master_Deference_Leader.stages.2`**
  - label：领袖竟是性奴
  - description：我们的领袖竟是个俯首为奴之人，这实在有辱秩序。

- **`SSC_Culture_Master_HolyMaster_Self.stages.0`**
  - label：身为圣主
  - description：我乃被供奉的主人，众奴以唇舌与肉身服侍于我，任我尽情驰骋、抽送到她们失声浪叫——这神圣的身份令我如登极乐。

- **`SSC_Culture_Master_HolyMaster_ActualMaster.stages.0`**
  - label：供奉主人
  - description：我的主人是神圣的存在，能以唇舌与淫穴侍奉于他、任他灌满我的身体，我满心感恩。

- **`SSC_Culture_Master_HolyMaster_Social.stages.0`**
  - label：敬奉此圣主
  - description：此人是被供奉的主人，一言一行皆是神意，众奴以高潮为祭、以肉身为香，我予以最高尊崇。

- **`SSC_Culture_Master_HolyMaster_Leader.stages.0`**
  - label：圣主为领袖
  - description：我们的领袖乃神圣的主人，这是天降的恩典。

- **`SSC_Culture_Master_HolyMaster_Leader.stages.1`**
  - label：领袖非主人
  - description：领袖竟不是主人，这简直是对神圣秩序的嘲弄。

- **`SSC_Culture_Master_HolyMaster_Leader.stages.2`**
  - label：领袖竟是性奴
  - description：神圣的领袖之位竟被一个性奴占据，这是莫大的亵渎。

- **`SSC_Culture_Master_Deference_SexSlaveLeaderSocial.stages.0`**
  - label：鄙夷此性奴领袖
  - description：此人身为性奴却窃居领袖之位，有辱主奴秩序。

- **`SSC_Culture_Master_HolyMaster_SexSlaveLeaderSocial.stages.0`**
  - label：痛恨此性奴领袖
  - description：一个性奴竟敢僭居神圣领袖之位，这是对秩序的亵渎。

### 4.4 数量（6 条／10 阶段）

- **`SSC_Culture_MasterCount_One_Self.stages.0`**
  - label：主人过多
  - description：殖民地的主人多于一人，欲望各执其链，秩序已然混乱。

- **`SSC_Culture_MasterCount_Three_Self.stages.0`**
  - label：主人过多
  - description：殖民地的主人多于三人，众多主人各立门户，令我心生不安。

- **`SSC_Culture_SlaveCount_One_Self.stages.0`**
  - label：性奴过多
  - description：我名下的性奴多于一人，恩宠既分，淫穴与链环便难严丝合缝。

- **`SSC_Culture_SlaveCount_Three_Self.stages.0`**
  - label：性奴过多
  - description：我名下的性奴多于三人，阳具分身乏术、调教不周，令我愧疚。

- **`SSC_Culture_SlaveCount_One_Social.stages.0`**
  - label：主人蓄奴过多
  - description：此主人名下性奴不止一人，令我侧目。

- **`SSC_Culture_SlaveCount_One_Social.stages.1`**
  - label：主人蓄奴过多
  - description：此主人蓄奴无度，实属放纵。

- **`SSC_Culture_SlaveCount_One_Social.stages.2`**
  - label：主人蓄奴过多
  - description：此主人贪得无厌、蓄奴成群，令我深恶。

- **`SSC_Culture_SlaveCount_Three_Social.stages.0`**
  - label：主人蓄奴过多
  - description：此主人名下性奴多于三人，令我侧目。

- **`SSC_Culture_SlaveCount_Three_Social.stages.1`**
  - label：主人蓄奴过多
  - description：此主人蓄奴无度，实属放纵。

- **`SSC_Culture_SlaveCount_Three_Social.stages.2`**
  - label：主人蓄奴过多
  - description：此主人贪得无厌、蓄奴成群，令我深恶。

---

## 五、命名连接规则 RulePackDef（同步校对，无文学文本）

| defName | 连接规则 | 校对结论 |
| --- | --- | --- |
| SSC_NamerRoleMoralist | `r_roleName(priority=10)->[SSC_MoralistPreset]` | 保持，专属子规则引用 `SSC_MoralistPreset`（见 1.5），无模因时由原版 `NamerRoleMoralist` include 兜底 |
| SSC_NamerRitualBinding | `r_ritualName(priority=10)->[SSC_BindingRitualPreset]` | 保持，专属子规则引用 `SSC_BindingRitualPreset`（见 1.6），保留节庆命名兜底与唯一性规则 |

> 定点连接规则本身无文学文本；四语保持相同左侧、优先级与专属子规则引用即可，不做翻译替换。

---

## 六、附录：待编纂文本统计

| 类别 | 文本值数量 |
| --- | --- |
| 模因 description | 1 |
| 文化命名包（symbolPacks） | 12 |
| 通用组合词（generalRules 0–2） | 3 |
| 领袖称谓 | 3 |
| 道德导师称谓 | 3 |
| 契约仪式名称 | 3 |
| 文化生成背景段落 | 17 |
| 戒律 description | 22 |
| 戒律阶段效果说明（thoughtStageDescriptions） | 19 |
| 心情／社交阶段 label | 53 |
| 心情／社交阶段 description | 53 |
| **合计待编纂文本值（不含 IssueDef／RulePackDef 校对项）** | **189** |

> 命名扩展（24 命名 + 17 描述段落 = 41 项）与设计文档第 13.6 节一致；心情／社交共 44 条 thought、53 阶段（每阶段 label／description 各 1 项）。
>
> 本稿（v5改）相对 v5 的露骨化改写范围：模因描述 1、文化生成背景段落 17、戒律 description 9（正向／主动立场）、Thought description 18（正向／主动立场），共 45 处；其余条目与 v5 逐字一致。
