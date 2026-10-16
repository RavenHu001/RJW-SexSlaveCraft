# PNA 文案定稿 一 药效与成瘾

> 当前完成状态（2026-10-16）：定稿、四语 XML、后续调试旧称及心情标签已落地并获分批实机确认，原 2.4.0.5 开发成果归入 **2.4.1（未发布）**。下文保留交接与编写历史，不作为待执行任务；见[完成核对](../Development/Completed/PNA重置与玩法补全完成核对.md)及[本版说明](../Releases/2.4.1/2.4.1发布说明.md)。

配套[交接文档](PNA文案重写交接.md)。本稿覆盖 `HediffOfBuff.xml` 与 `HediffOfAddiction.xml` 的全部玩家可见文本，含十档药效思绪与戒断思绪。

写作约束：不给面板数值任何名词、不写身体归属声明、不写消退算法与天数、不用属性名、正文一律第一人称或世界内因果。

## 1 术语约定

| 概念 | 用法 |
| --- | --- |
| 物品 | 紫色纳米催情液／浓缩紫色纳米催情液（正式名）；纳米催情液／浓缩纳米催情液（简称） |
| 液体中的机械 | 紫色微械 |
| 机械组成的液体 | 紫色纳米机械液 |
| 技术缩写 | PNA（仅调试与技术信息） |
| 禁用 | 负荷、输入、剂量、每份、纳米流体、纳米液体、纳米机械液体、超级药物、身体归属声明 |

## 2 阶段命名

两条线共用一根“外源纳米催情液的作用强度”轴，方向相反：普通线是控制力被逐步夺走，性奴线是身体被逐步点亮。档位索引与严重度阈值不变，只替换 label。

两个状态名成对命名，都点明是纳米催情液造成的，后缀定正负，一看便知是同一种药在两种身体上的结果：**纳米催情液侵蚀**（普通人，负面）与**纳米催情液活化**（性奴，正面）。后续新增的 PNA 状态沿用同一构词法：`纳米催情液成瘾`、`纳米催情液分泌`。

| 档 | 索引 | 普通药效 `Hediff_PNA_Debuff` | 性奴活化 `SSC_Hediff_PnaRootedLoad` |
| --- | --- | --- | --- |
| 状态名 | — | 纳米催情液侵蚀 | 纳米催情液活化 |
| 1 | 0 | 初起 | 初醒 |
| 2 | 1 | 渐深 | 活络 |
| 3 | 2 | 失控 | 充盈 |
| 4 | 3 | 沉溺 | 激昂 |
| 5 | 4 | 强制高潮 | 过载 |

英文对照（供英文层使用）：

| 档 | 普通药效 | 性奴活化 |
| --- | --- | --- |
| 状态名 | Nano-Aphrodisiac Corrosion | Nano-Aphrodisiac Activation |
| 1 | First Stir | First Light |
| 2 | Rising | Quickened |
| 3 | Unmoored | Filling |
| 4 | Undone | Surging |
| 5 | Blackout | Overload |

## 3 普通药效

`Hediff_PNA_Debuff`

- label：**纳米催情液侵蚀**
- description：
  > 紫色纳米催情液正在刺激全身的神经。情欲比平时更难压住，痛觉变得迟钝；随着作用加深，动作和注意力开始不受控制，眼前与耳边出现并不存在的东西。作用到极点时，身体会不由自主地连续高潮，直到意识中断。

## 4 性奴活化

`SSC_Hediff_PnaRootedLoad`

- label：**纳米催情液活化**
- description：
  > 外源纳米催情液把已经与紫色微械共存的身体进一步点燃。人会变得更清醒、更协调、更有力，痛觉明显退去，情欲与精力同时上涌。活化越强，退去也越快。

## 5 成瘾与戒断

`PNA_Addiction`

- label：**纳米催情液成瘾**
- description：
  > 神经系统已经习惯纳米催情液带来的刺激，于是持续向身体索要它。供给一旦中断，就会出现空虚、烦躁与说不清的身体不适，情欲反而更难平息。再次取得可以暂时压下渴求，但依赖会随之加深；长期不再接触才能恢复。

`PNA_Withdrawal_Thought`（第 0 档无 label，与源文件一致）

- 第 1 档 label：**戒断**
- 第 1 档 description：
  > 身体一直在索要它。这种空缺让我烦躁又难受，什么心思都提不起来。

## 6 药效思绪

普通药效 `PNA_Debuff_Thought`，全部第一人称。

| 档 | label | description |
| --- | --- | --- |
| 1 | 催情侵蚀：微热 | 一股暖意从体内泛起来，我有点想要，但还能按住。 |
| 2 | 催情侵蚀：情动 | 那股热度散到全身，我总在想着这件事，很难专心。 |
| 3 | 催情侵蚀：迷醉 | 眼前的东西开始发虚，耳边有声音在响，我说不清那是真是假。 |
| 4 | 催情侵蚀：沉溺 | 欲望压过了我。我不管自己在做什么，也管不住自己。 |
| 5 | 催情侵蚀：极乐 | 我几乎撑不住了，脑子里只剩下快感，别的什么都没有。 |

## 7 活化思绪

性奴活化 `SSC_PnaRootedLoad_Thought`，全部第一人称。

| 档 | label | description |
| --- | --- | --- |
| 1 | 催情活化：清明 | 身体像被点亮了一样，我的感觉一下子清楚了。 |
| 2 | 催情活化：轻快 | 手脚轻快，动作跟得上念头，身体比平时听话。 |
| 3 | 催情活化：充沛 | 力量和欲望一起涨上来，我既兴奋又清醒。 |
| 4 | 催情活化：亢奋 | 身体在发烫，我比任何时候都有力，也停不下来了。 |
| 5 | 催情活化：酣畅 | 我快被这股力量撑满了，痛快得只想一直这样下去。 |

## 8 翻译键替换清单

以下按语言层逐处替换。键名、占位符与阶段索引一律不动。

| 语言目录 | 文件 | 替换内容 |
| --- | --- | --- |
| 四语 | `DefInjected/HediffDef/HediffOfBuff.xml` | `Hediff_PNA_Debuff` 的 label 与 description、`SSC_Hediff_PnaRootedLoad` 的 label 与 description、两条线各五档 label |
| 四语 | `DefInjected/ThoughtDef/HediffOfBuff.xml` | `PNA_Debuff_Thought` 五档与 `SSC_PnaRootedLoad_Thought` 五档的 label 与 description |
| 四语 | `DefInjected/HediffDef/HediffOfAddiction.xml` | `PNA_Addiction` 的 label 与 description、第 1 档 label |
| 繁中／英文／俄文 | `DefInjected/ThoughtDef/HediffOfAddiction.xml` | 第 1 档 label 与 description |

`Hediff_PNA_Consciousness_Record`：源文件中的描述连同编写者口气整句重写为简短准确的技术说明，并需在简中语言层**新增**该词条（简中现缺）；该条属隐藏记录，不属于本次叙事重写目标。

## 9 需整句删除的身体归属声明

以下位置的“该状态仅属于当前身体，不随人格迁移，也不由克隆体继承”一类句子整体删除，不在替换之列：

- `HediffOfBuff.xml`：`Hediff_PNA_Combat_Buff`、`Hediff_PNA_Combat_Buff_Plus`、`Hediff_PNA_Debuff`、`SSC_Hediff_PnaRootedLoad`、`Hediff_PNA_Consciousness_Record`
- `HediffOfAddiction.xml`：`PNA_Addiction`
- 对应语言层与镜像副本同步删除

以上属于战斗 Buff 与隐藏记录的两条按范围约束只做删句，不改写其余措辞。


## 10 心情标签来源区分（2026-10-15）

维护者在四项后续修正验收后调整心情命名：普通线统一使用“催情侵蚀”，性奴线统一使用“催情活化”，后缀采用上表的主观感受，避免与健康阶段同名或误读为负面心情。本次只修改十个思绪 label，源 Def、四语与镜像同步；description、心情数值和 Hediff 阶段名称保持。纯 XML 静态检查，不重编或运行代码测试；维护者已确认游戏内心情标签显示正常，批准更新文档并提交。原定稿旧标签保留在 Git 历史中。
