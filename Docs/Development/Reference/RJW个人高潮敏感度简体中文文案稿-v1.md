# RJW 个人高潮敏感度 · 简体中文文案稿 v1

> 编写日期：2026-10-27。编写者：**DeepSeek Harness（AI 辅助）**。版本：**v1**。
>
> 状态：**维护者已确认简中v1，四语已落实。** 2026-10-27按维护者指令逐字回填本稿44项，并落实繁中、英语、俄语及资源镜像；三语译文待审阅；后续已[同步本机](../Completed/第27项重编译与本机同步记录.md)、待实机，见[落实与验证](../Completed/高潮敏感度第四批四语文案落实与验证.md)。本稿保留原创作内容与来源，不改变机制、数值、字段或开发范围。
>
> 依据：[交接清单](RJW个人高潮敏感度简体中文文案交接清单.md)。本稿覆盖 **44 个字段**：18 个 Keyed、6 个 HediffDef 注入、20 个 ThoughtDef 注入（21 个标题／名称 + 23 个其他文本）。

---

## 1. 术语与命名（先定）

| 命名对象 | 采用 |
| --- | --- |
| 功能／健康名 | **性敏感改写**（K01 功能标题、H01 健康名同名） |
| 关闭与四档 | **关闭 / 轻度敏感 / 中度敏感 / 深度敏感 / 极致敏感** |
| 系数术语 | **高潮频率系数**（显示于 Hediff 详细信息，须自解释） |
| 敏感度用词 | 统一 **性敏感／敏感**，弃用“感度”；**脆弱度**沿用 RJW 既有名 |
| 十格心情 | 前缀统一 **性敏感改写：**，后缀为该格直接情绪，见第 3 节 |

四档对应：一档＝轻度敏感（`stages.0`）、二档＝中度敏感（`stages.1`）、三档＝深度敏感（`stages.2`）、四档＝极致敏感（`stages.3`）。关闭无 Hediff 阶段，仅由 Keyed `Level0` 显示。

---

## 2. 心情文案编写经验

本次写作中与维护者逐步对齐的「心情标题／描述」写作偏好，已沉淀为可复用文档：[文案经验·偏色情化-心情标题与描述](文案经验/偏色情化-心情标题与描述.md)。本稿第 5 节即按该经验成稿。

---

## 3. Keyed 逐项（18 项）

| 编号 | 键 | 建议文本 |
| --- | --- | --- |
| K01 | `SSC_Sensitivity_Title` | 性敏感改写 |
| K02 | `SSC_Sensitivity_Level0` | 关闭 |
| K03 | `SSC_Sensitivity_Level1` | 轻度敏感 |
| K04 | `SSC_Sensitivity_Level2` | 中度敏感 |
| K05 | `SSC_Sensitivity_Level3` | 深度敏感 |
| K06 | `SSC_Sensitivity_Level4` | 极致敏感 |
| K07 | `SSC_Sensitivity_Summary` | 保存：{0}；实际：{1}。高潮频率系数 ×{2}，脆弱度 {3}。 |
| K08 | `SSC_Sensitivity_Adjust` | 调整档位：{0} |
| K09 | `SSC_Sensitivity_OptionParameters` | （高潮频率系数 ×{0}，脆弱度 {1}） |
| K10 | `SSC_Sensitivity_Tip` | 这副身子有多性敏感，全凭主人决定；锁链阶段越高，能调到的档位越高。 |
| K11 | `SSC_Sensitivity_ConfigInvalid` | 敏感度配置无效，仍可关闭已有设置。 |
| K12 | `SSC_Sensitivity_BodyUnavailable` | 当前身体无法接受敏感度调节。 |
| K13 | `SSC_Sensitivity_PersonalityUnavailable` | 空壳或人格替换期间，敏感度调节暂停。 |
| K14 | `SSC_Sensitivity_NotSlave` | 当前并非 SSC 性奴身份，调节暂停。 |
| K15 | `SSC_Sensitivity_NoChain` | 缺少有效锁链，调节暂停。 |
| K16 | `SSC_Sensitivity_NotEditable` | 只能调整玩家所属、存活且可编辑的当前身体。 |
| K17 | `SSC_Sensitivity_InvalidLevel` | 档位无效。 |
| K18 | `SSC_Sensitivity_LevelLocked` | 当前锁链阶段尚未开放该档；保存选择保留。 |

K11～K18 是功能状态／异常提示，保持清晰直接，不硬塞色情。

### 3.1 占位符与拼接

| 键 | 参数含义（不可改） |
| --- | --- |
| `SSC_Sensitivity_Summary` | `{0}`保存档位名；`{1}`实际档位名；`{2}`倍率数值串；`{3}`脆弱度带符号串 |
| `SSC_Sensitivity_Adjust` | `{0}`保存档位名 |
| `SSC_Sensitivity_OptionParameters` | `{0}`待选档位倍率；`{1}`待选档位脆弱度偏移（已自带正负号） |

保留全部占位符及含义；脆弱度前不另写固定“+”；脆弱度不是百分比；除上述三项外不新增占位符。菜单后缀（K09）须能直接接在档位名之后，摘要（K07）须能接在 Hediff 通用说明之后。

---

## 4. 身体 Hediff（6 项）

| 编号 | 键 | 建议文本 |
| --- | --- | --- |
| H01 | `SSC_OrgasmSensitivity.label` | 性敏感改写 |
| H02 | `SSC_OrgasmSensitivity.description` | 被微械侵蚀过的性奴，身子有多性敏感，全凭主人决定。 |
| H03 | `SSC_OrgasmSensitivity.stages.0.label` | 轻度敏感 |
| H04 | `SSC_OrgasmSensitivity.stages.1.label` | 中度敏感 |
| H05 | `SSC_OrgasmSensitivity.stages.2.label` | 深度敏感 |
| H06 | `SSC_OrgasmSensitivity.stages.3.label` | 极致敏感 |

H02 只阐述“主人决定被微械侵蚀的性奴有多性敏感”这一核心，不描述改造过程与手段；动态倍率／档位由摘要（K07）自动追加，不在 H02 重复。

---

## 5. 持续心情 ThoughtDef（20 项）

索引是思想阶段索引，不是档位编号。`S` 为锁链阶段（1 见习性奴、2 性奴、3 屈服性奴、4 肉便器化），档位为敏感度档。

| 编号 | 键 | 建议文本 | 阶段·档位·心情 |
| --- | --- | --- | --- |
| T01 | `.stages.0.label` | 性敏感改写：害怕 | S1·轻·−3 |
| T02 | `.stages.0.description` | 小穴没来由地热了，湿意一点一点往外渗……陌生得发慌，这身子，不像是我的了…… | 抗拒 |
| T03 | `.stages.1.label` | 性敏感改写：难受 | S2·轻·−1 |
| T04 | `.stages.1.description` | 小穴又热起来了，湿意也漫上来……黏得难受，别想，别去想…… | 别扭 |
| T05 | `.stages.2.label` | 性敏感改写：烦躁 | S2·中·−2 |
| T06 | `.stages.2.description` | 热一路烧到小穴，越来越湿，怎么压都压不住……烦，好烦，别再有反应了…… | 难熬 |
| T07 | `.stages.3.label` | 性敏感改写：羞耻 | S3·轻·+1 |
| T08 | `.stages.3.description` | 小穴热起来，湿意也涌上来……丢人，可又有点，想要…… | 开始沉溺 |
| T09 | `.stages.4.label` | 性敏感改写：渴望 | S3·中·+2 |
| T10 | `.stages.4.description` | 小穴越来越湿，热一路往上顶……哈啊，还想要，更多…… | 沉溺加深 |
| T11 | `.stages.5.label` | 性敏感改写：兴奋 | S3·深·+3 |
| T12 | `.stages.5.description` | 乳尖被一碰，小穴就绞起来……去了，差点去了❤哈啊。别碰了，快，快撑不住了…… | 极易高潮 |
| T13 | `.stages.6.label` | 性敏感改写：满足 | S4·轻·+2 |
| T14 | `.stages.6.description` | 小穴发胀，湿意慢慢漫开……好舒服，就这样，也挺好…… | 接受沉溺 |
| T15 | `.stages.7.label` | 性敏感改写：贪求 | S4·中·+3 |
| T16 | `.stages.7.description` | 小穴湿透了，还嫌不够……哈啊，好舒服，再敏感一点，还要❤ | 沉溺加深 |
| T17 | `.stages.8.label` | 性敏感改写：陶醉 | S4·深·+4 |
| T18 | `.stages.8.description` | 乳尖一蹭就去了❤……小穴跟着抽搐，哈啊，好舒服，再来，再来❤ | 极易高潮 |
| T19 | `.stages.9.label` | 性敏感改写：沉溺 | S4·极·+6 |
| T20 | `.stages.9.description` | 去了❤去了❤……小穴在喷水、在抽搐，哈啊❤。停不下来了，好舒服❤……就这样，一直，唔❤ | 日常不停高潮 |

---

## 6. 落地映射

| 资源 | 运行文件 | 镜像 |
| --- | --- | --- |
| Keyed | `1.7/Languages/ChineseSimplified/Keyed/SSC_OrgasmSensitivity.xml` | `Sexslavecraft/Languages/ChineseSimplified/Keyed/SSC_OrgasmSensitivity.xml` |
| HediffDef 注入 | `1.7/Languages/ChineseSimplified/DefInjected/HediffDef/SSC_OrgasmSensitivity.xml` | `Sexslavecraft/Languages/ChineseSimplified/DefInjected/HediffDef/SSC_OrgasmSensitivity.xml` |
| ThoughtDef 注入 | `1.7/Languages/ChineseSimplified/DefInjected/ThoughtDef/SSC_OrgasmSensitivity.xml` | `Sexslavecraft/Languages/ChineseSimplified/DefInjected/ThoughtDef/SSC_OrgasmSensitivity.xml` |

三份镜像不另计创作量；落地时与源码镜像同步，不单独维护两套文本。键名、占位符、阶段索引一律不动，只替换文字。

---

## 7. 审读边界

- 本稿只覆盖交接清单的 44 项，不新增心情、Thought、面板、事件记忆、信件、研究／手术名称或独立系数 StatDef。
- 基础 Def 的 defName、XML 字段、阶段顺序、思想映射及数值均保持。
- 起稿时名称与表达作为候选供维护者审读；现已按维护者确认完成四语落实，当前状态见页首。编写者署名仅标记稿件来源，不修改Mod作者信息。
