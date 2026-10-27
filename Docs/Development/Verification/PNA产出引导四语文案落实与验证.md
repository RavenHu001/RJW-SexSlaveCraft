# PNA产出引导四语文案落实与验证

日期：2026-10-26。状态：已落实、文案专项通过并完成复核，按授权提交；尚未同步游戏或进行本次实机显示验收。

## 1. 确认范围

维护者确认游戏内引导只放在物品与科技描述中，兔特化没有开放且已决定重做，兔分身不纳入引导。本次只修改两个物品和两项研究的描述，不改名称、配方、产出机制、研究条件、健康状态描述或界面。

| 对象 | 稳定ID | 新增引导 |
| --- | --- | --- |
| 普通PNA | `Purple_Aphrodisiac_Nanofluid` | 生殖器开发满100%取得分泌能力；营养、基础积累速度／批量、进度查看及产物去向 |
| 浓缩PNA | `Purple_Aphrodisiac_Nanofluid_Plus` | 先取得普通原液，再通过研究解锁的药物台／篝火／灶台工艺浓缩 |
| 部位调教研究 | `SSC_RES_BodyPartTraining` | 实际完成相应部位开发才取得分泌能力，完成研究本身不会直接开始产出 |
| PNA浓缩技术 | `SSC_RES_PNAConcentration` | 原液来源、两种加工效率与相同产物，研究只解锁浓缩，不赋予身体分泌能力 |

本次与[未来规划第46项](../../Design/未来内容开发规划.md#46-pna-产出方式的文案引导文案已落实待实机)对应。第44项增产特化、第45项生产设施仍待设计，不写入现有获取说明；也不把性交传入药效当成物品产出路线。

## 2. 机制核对与四语资源

- [生殖器开发定义](../../../1.7/Defs/HediffDefs/HediffOfTrain.xml)满阶段授予 `SSC_Purple_HyperLactation`；原液基础进度每天20%，满进度10份，从零积累约5天。
- [生产组件](../../../Sexslavecraft/Hediff/HediffComp_ProducePAN.cs)按现有饱食需求支付营养，不足以支付时暂停积累；现有健康状态及角色信息栏可查看普通产出进度。地图中产物放在角色附近，远行队中进入库存。文案不承诺新增停产诊断或收集按钮。
- [药物台配方](../../../1.7/Defs/RecipeDefs/RecipeDef_PNA_PLUS.xml)为5→1、20→4；[粗放配方](../../../1.7/Defs/RecipeDefs/RecipeDef_PNA_Rough.xml)为10→1，篝火／燃料灶／电灶使用，均要求原PNA浓缩研究，产物相同。

四语共16条描述。简中使用现有基础Def描述；繁中、英语、俄语沿原DefInjected键写入，键名为对应ID加 `.description`，无新增格式参数。

| 文件类别 | 简中来源 | 其他三语来源 |
| --- | --- | --- |
| 物品 | [Nanofluid.xml](../../../1.7/Defs/ThingsDefs/Nanofluid.xml) | 各语言 `DefInjected/ThingDef/Nanofluid.xml` |
| 研究 | [ResearchProjectDef.xml](../../../1.7/Defs/ResearchDefs/ResearchProjectDef.xml) | 各语言 `DefInjected/ResearchProjectDef/ResearchProjectDef.xml` |

运行资源共8份XML，现有语言镜像6份逐字节同步。Def镜像为部分集合，这两份基础Def没有现成源码镜像，不新增镜像文件；未创建额外简中覆盖文件。所有描述保留原有核心设定／药效说明，并增加独立操作段落；浓缩品不再仅以减压工艺描述，以兼容已有粗放路线。

## 3. 本次文案专项及复核

- **16／16条描述核查通过**：四语条目齐全，XML可解析，原名称／键／参数保留，数字顺序和内容与当前生产／配方参数对应，无兔分身、替换字符或占位回退；核对营养、进度入口、产物去向及两类加工地点的四语表达。
- 以本次修改前 `HEAD` 为比较基准，屏蔽四个授权描述节点后逐树比对：八份运行XML的其他字段、结构和属性均未变化；六份语言镜像与来源一致。
- `Scripts/Test-DefMirrors.ps1 -PassThru`：**31份镜像一致，0问题**。
- `Scripts/Sync-LanguagesMirror.ps1 -PassThru`，未使用 `-Sync`：**528份来源文件，0缺失／差异／额外文件**。
- 文档链接／锚点及 `git diff --check` 纳入提交前核查。

文案核查脚本及结果保存在 `.builds/pna-production-copy/`，属于局部文本检查，不是游戏机制回归。未构建或重编DLL，未跑PNA机制、完整回归或发布门禁，未同步游戏；本次实机显示与可读性待维护者后续确认，不沿用其他功能验收结果。
