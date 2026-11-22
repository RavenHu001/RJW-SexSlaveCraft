# 第 7 项奴性爆发完成索引

初版归入 2.5.2 开发里程碑，后续无主分支等修订归入 2.5.3，均已随 **SSC 2.5.3 正式版**交付。先读[版本说明](../../../Releases/2.5.3/2.5.3发布说明.md)，再按下表查分批证据；正式构建与包审计见[发布归档](../../../Releases/2.5.3/2.5.3版本验证.md#ssc-253-release)。

2026-11-18，1.7 机制完成分批实机验收，随 PR #47 合入 `main`（`21fa009e`）。现行准入、计时、邀约和结算以已交付设计的摘要为入口。 [现行规则](../../../Design/Archive/第7项奴性爆发开发方案.md#ssc-slave-breakdown-current-rules) · [总体规划第 7 项](../../../Design/未来内容开发规划.md#7-奴性爆发)

2026-11-21，默认开启、持续满值触发、无主调教分支及原版计时随第 6 项合入。 [第 6 项实施与验收](../无主性奴/README.md) · [原版计时实机验证](../../../Design/Archive/第7项奴性爆发开发方案.md#ssc-slave-breakdown-native-timers)

## 实施与验收

| 批次 | 记录 |
| --- | --- |
| 早期开场、邀约和调教交接 | [开场与邀约实机验收](第7项奴性爆发开场与邀约实机验收.md)保留当时两类邀约的验收范围，当前游荡期只保留求用邀约。 |
| 独立触发与健康页计时 | [独立触发与 Hediff 计时实机验收](第7项奴性爆发独立触发与Hediff计时实机验收.md)记录延时读档、中断、身份限制及退出时任务保留。 |
| 有链发作与升阶 | [有链调教升阶实机验收](第7项奴性爆发有链调教升阶实机验收.md)记录主人附近游荡、主人请求与升阶反馈。 |
| 最终邀约派发与冷却 | [立即派发和一小时对象冷却](../../../Design/Archive/第7项奴性爆发开发方案.md#ssc-slave-breakdown-invited-use-cooldown-acceptance)记录最终同步版的实机反馈。 |
| 合入后测试精简 | `d57cea4f` 将第 7 项原生用例从 46 项精简为 44 项，印记与发作分区 86／86 通过。 [验证范围](../../../Design/Archive/第7项奴性爆发开发方案.md#ssc-slave-breakdown-test-pruning) |
| 后续 12 条四语定稿 | `2032a924` 落实四语文案并对齐英文默认 Def。18 份 1.7 XML 已安装同步，逐份 SHA-256 核对通过。 [新增文案定稿](../../Reference/History/第7项奴性爆发新增简中文案交接清单.md) |

## 文件归属

| 资料 | 入口 |
| --- | --- |
| 现行规则与历次设计 | [已交付开发方案](../../../Design/Archive/第7项奴性爆发开发方案.md) |
| 中文定稿和交接 | [初版交接](../../Reference/History/第7项奴性爆发简中文案交接清单.md) · [新增条目交接](../../Reference/History/第7项奴性爆发新增简中文案交接清单.md) · [1.7 运行资源](../../../../1.7/Languages/) |
| 触发和发作生命周期 | [准入及风险警报](../../../../Sexslavecraft/common/SSCSlaveBreakdown.cs) · [满值触发](../../../../Sexslavecraft/common/SSCSlaveBreakdownGameComponent.cs) · [精神状态](../../../../Sexslavecraft/common/MentalState_SSCSlaveBreakdown.cs) · [Hediff 计时](../../../../Sexslavecraft/Hediff/Hediff_SSCSlaveBreakdown.cs) |
| 行为、邀约和调教 | [行为树任务](../../../../Sexslavecraft/JobGiver/JobGiver_SSCSlaveBreakdown.cs) · [邀约派发](../../../../Sexslavecraft/common/MentalState_SSCSlaveBreakdown.Invitation.cs) · [调教结算](../../../../Sexslavecraft/common/MentalState_SSCSlaveBreakdown.Training.cs) |
| 1.7 运行配置与镜像 | [行为树补丁](../../../../1.7/Patches/SSC_SlaveBreakdownBehavior.xml) · [运行 Def](../../../../1.7/Defs/) · [源码 Def 镜像](../../../../Sexslavecraft/Defs/) |
| 原生回归 | [第 7 项用例](../../../../Tests/UterusDevelopmentNative/SlaveBreakdownTests.cs) · [宿主说明](../../../../Tests/UterusDevelopmentNative/README.md) |

## 保留事项

- 简中、繁中、英语和俄语的游戏内逐语显示仍在[待验收专项](../../Verification/README.md#其他专项)。
- [调教员换人格后旧信的读档边界](../../../Design/Archive/第7项奴性爆发开发方案.md#ssc-slave-breakdown-deferred-trainer-save-edge)已复现并暂缓处理。早期同格等待反馈及其调查结果保留在[最终邀约验收](../../../Design/Archive/第7项奴性爆发开发方案.md#ssc-slave-breakdown-invited-use-cooldown-acceptance)。
