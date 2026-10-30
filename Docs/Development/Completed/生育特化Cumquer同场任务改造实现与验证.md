# 生育特化Cumquer同场任务改造实现与验证

日期：2026-10-30。分支：`codex/fertility-specialization`。按维护者要求先提交确认规划`e59e43e2`，再实施并复核同场改造。实现已提交`8e470c79`并同步本机，相关33／33；正式测试DLL、候选、宿主及安装副本一致。维护者已确认本次实机测试没有问题，同场改造验收关闭，记录归Completed。下方首次实施的31项及未部署状态保留原执行时点。

前四步及第五步旧主体的认可保留原范围，见[第四步完成记录](../Completed/生育特化第四步婴儿生成与出生处理实现与验证.md#第四步完成与实机验收关闭)和[第五步旧版本记录](../Completed/生育特化第五步对他人技能实现与验证.md#第五步及技能修正完成与实机验收)。新方案依据[现行规划6.5节](../../Design/生育向性奴特化阶段规划与设计分析.md#65-cumquer同场任务结构与完成后结算方案)独立验证；自克隆未开发。

## 实机验收关闭

2026-10-30，维护者反馈“经测试没有问题”，确认当前已同步版本的Cumquer同场任务改造可用，本批验收关闭。交付范围包括同场实际行为正常完成后才提交施法者受孕、成功起算7天冷却、中断不受孕不扣冷却，以及局部自然受孕隔离、原请求／人格流转和确切任务清理；单独读条及独立后续性交已撤除。

认可对应实现提交`8e470c79`及同步记录提交`e78cfbae`；已部署DLL SHA-256为`3ec05beb3dcba1092d4bb314262fd7585212e2d2b57f5af472bc7a8f4ebdf92c`。前轮相关33／33、零警告／零错误、18份同步／1308份运行文件一致／532份冻结只读，保留各自执行时点，本轮未重新执行这些检查。

本次反馈作为维护者对已测范围的整体认可，不扩写为每种第三方组合、每项世界存读档或每种动作均单独实测通过。前四步及第五步旧主体验收保持；自克隆仍为最后单列的未开发分支，不包含在本次完成范围内。

本轮只整理完成与验收、归档记录并更新规划／入口后提交；不改源码或运行资源，不重编、不重跑机制测试、不再次同步或发布。构建时的gameRuntimeVerified=false保留原构建来源，不追写构建记录冒充本轮重建。

## 实际实现与接入

执行顺序为：**全图选定对象 → 实际走近 → 建立本场发起／接收任务 → RJW Start及SexTick → 计时正常完成 → ProcessSex → 提交施法者受孕 → 成功才保留7天冷却。**

| 入口 | 本轮实现及边界 |
| --- | --- |
| [Ability及Verb](../../../Sexslavecraft/Comps/Ability_FertilityConception.cs) | 沿原目标命令登记请求；不再启动单独CastVerb预热。Activate只接受当前同场驱动正常完成链的提交，不允许直接效果调用跳过性交。原PreActivate延后到建孕成功，失败不产生临时冷却；成功冷却仍由原Ability保存 |
| [同场驱动](../../../Sexslavecraft/JobDriver/JobDriver_FertilityConception.cs) | 发起者继承JobDriver_SexBaseInitiator；BeginScene选取自愿动作，复查原许可、人格及目标工作后安装专属接收Job，双方停步并调用原Start。TickScene执行SexTick，只有实际观察到ticks_left耗尽才推进正常完成。CompleteScene先认领一次，再ProcessSex及提交技能；FinishAction只清理 |
| 专属接收者 | 同文件的JobDriver_FertilityReceiver继承RJW接收基类，自建等待Toil并核对确切发起任务；不复用只接受原生JobDef白名单的Loved／Quickie接收驱动，也不因施法者征召而提前结束 |
| [原请求及建孕](../../../Sexslavecraft/common/FertilityAbilityUtility.cs) | 固定并复查双方原Comp／人格token、施法者原子宫、Ability、Job实例及loadID；动作选择、许可及ProcessSex回调之后仍复查。公共StartVanillaPregnancy继续显式指定施法者为geneticMother，核对实际妊娠和双亲；落地之后抛异常仍保留成功冷却 |
| [局部受孕拦截](../../../Sexslavecraft/common/FertilitySceneUtility.cs) | 在PregnancyHelper.impregnate前置拦截本场确切SexProps，包含受方和旧场迟到回调；弱引用记录不形成永久存储。不阻止同一对角色后来新建的场外SexProps，不拦截技能直接调用的建孕入口，不清外部妊娠 |
| 中断及清理 | 走近失败、原许可拒绝、动作无法选择、Start未真正执行、人格／任务更换或未正常结束均不提交技能受孕、不扣成功冷却。只有正常完成结算才调用可能追加结锁工作的原End；取消仅释放确切受方和外观，外观异常也要释放。每个回调后复查归属，不StopAll、不清后来工作或绑定 |
| 保存及人格流转 | 原请求及完成凭据仅存在当前进程；未完成场景读档后取消，双方不为旧命令重新Capture。已有妊娠及冷却沿各自保存；Ability Scribe及凝胶绝对冷却截止时间机制保留 |
| 资源及旧入口 | 撤除FertilityFollowupUtility及旧联动执行路径；旧JobDef／JobDriver类型仅保留拒绝预约、立即取消的存档兼容入口，防止旧curDriver深存类型丢失，不再排入或执行。新增SSC_Job_FertilityReceiver，保留原技能与发起Job稳定ID。warmupTime归零，关闭技能读条及目标施法眩晕；四语技能／工作／研究说明和现有源镜像同步更新 |

对象继续为同图非敌对成年类人，不新增性别、可育性、同主人或绑定门槛；场景不能开始便取消，不回退为静止读条建孕。目标已有不可中断、强制完成、玩家强制或排队工作、正在其它性交、倒地、精神失常或不能清醒时不强行替换工作。普通自然性交的自动邀约性欲／吸引力预检不作为本手动技能额外门槛，现行SSC互动许可保留。

## 本机取证

采用本机游戏1.7.4990 rev1069及当前依赖，不套用1.6源码。游戏DLL SHA-256：`4e61d57205944c113631234bc55136a1a7c1679667b26008bee5425bcffa1463`；RJW：`bf2534c7c433e90aa64588d8acea71c1d1d8ab17bc415c0a169ac22a11ae0703`；动画框架：`c86737fbe3840f206f2fe6cefdad3863560df1e3ec584b01198db019689b5c74`。Cumquer的目标DLL及结构取证保留于[规划6.5节](../../Design/生育向性奴特化阶段规划与设计分析.md#65-cumquer同场任务结构与完成后结算方案)。

核对了RJW Start、End、SexTick、Orgasm、ProcessSex、接收驱动及原生JobDriver清理：Start须先有真实接收驱动；预选SexProps须补设原Start选取分支中的反向动作标志；Orgasm会调用普通impregnate，因此只把技能效果后移不足以兑现“中断不受孕”。原生Loved接收者有JobDef白名单，不能直接套用。实际Start是否执行由现行SSC守卫的开始凭据确认，不能拿SexProps非空当作已经开始。原版Pawn_JobTracker.ExposeData深存curDriver，故旧联动类型仅保留取消兼容入口；已有妊娠及冷却不重交或退款。

## 整体复核与本轮验证

2026-10-30按维护者要求复核正常完成／中断、冷却、自然受孕隔离、人格流转及清理。补齐代码块和函数级说明，并修正五处边界：

1. 原版Ability.PreActivate在效果之前启动冷却。本技能改为在实际建孕成功后才调用原预激活，失败回调期间也无临时冷却，不让其被人格采集看作成功冷却；实际落地后异常仍按成功处理。
2. RJW原End可能追加结锁Job。中断不再调用它；只有本场正常计时结束并完成ProcessSex后的结算尾段调用，其他退出只回收本场受方及外观。
3. 受方驱动已标记ended、任务跟踪器尚未替换的短暂状态也按中断处理，不能继续SexTick或提交技能。
4. 外观回收抛异常时，通过finally继续释放确切受方；回调后再核对其Job实例／loadID，保留后来工作。
5. Start之后替换的SexProps及读档后首次观察的数据也登记弱表，清理前补登记双方当前引用，确保原驱动离开后迟到回调仍被隔离；同一对角色的新场外数据继续可自然受孕。

另外对照Cumquer的OnCell与JobDef，关闭旧接触读条遗留的collideWithPawns=true。原版PawnBlockingPathAt会据此启用友方碰撞，与本场走入目标所在格的路径目标不一致；只改本技能Job配置，同格接近的实际地图表现仍待实机，不新增共用寻路框架。

对照实际游戏／RJW的PreActivate、Start、SexTick、Orgasm、End、原版清理和保存入口；月经DLL亦只读核对，SHA-256为`bf0da4ff826de269b545095e4ff5f9e120a29667a3728e89cfa83f572de90964`、MVID为`bbc3d3c9b4704ca485953b20c6127535`。其普通周期采样在impregnate的bool前缀中，低于本场前置优先级；后缀只处理特殊卵孕交接，不据此声明全部第三方组合通过。没有改月经DLL或扩大孕期管理。

| 本轮实际执行 | 结果与边界 |
| --- | --- |
| 修订前候选对照 | 旧候选397e595d执行加强后的检查为15／20，五处边界复现。日志before.log；这是宿主边界复现，不冒充实机报告 |
| 修订后技能分区 | --fertility-skill **20／20**，沿用原18项并新增Core2（已结束受方、外观异常释放）；原完成／取消及自然受孕用例加强，不增加重复组合 |
| 修订后出生分区 | --birth **13／13**；第四步原消费者及孕期人格排泄保护保持。合计**33／33**，未执行完整宿主／整仓All |
| 构建及受测产物 | 隔离候选、宿主及正式测试构建零警告／零错误；正式DLL、候选及宿主副本SHA-256均为`3ec05beb3dcba1092d4bb314262fd7585212e2d2b57f5af472bc7a8f4ebdf92c`，因此不为相同产物重复运行相同分区 |
| 资源及差异 | 复用轻量Def／语言镜像、XML／Keyed检查，以及文档本地链接／锚点和git diff --check；1.6／Common冻结未改 |

本轮构建基于e59e43e2及待提交源码；正式BUILD-INFO如实记录dirty来源、实际依赖和gameRuntimeVerified=false，不伪造为实机通过。证据位于`.builds/fertility-skill/same-scene-review/`的before-build.log、before.log、build.log、test-build.log、fertility-skill.log、birth.log及formal-build.log。

原请求仍固定双方人格；换人格或原Job失效不能重捕获，已成功冷却继续沿原Ability／凝胶绝对截止时间保存。人格采集、复制、Scribe和完整植入的相关检查包含于20项，没有新增或调整绑定处理。地图／路径、随机动作选择、完整Start／SexTick／ProcessSex外围、任务交接及图形的宿主隔离边界沿下文；实际动画、世界存读档及双方站位仍待实机。

## 本机测试同步

实现提交`8e470c7905946e196dff3dbab5b743dc66c4de8c`后，于2026-10-30 21:41:24 UTC同步至`D:/STEAM/steamapps/common/RimWorld/Mods/RJW-SexSlaveCraft-TieJin-Modify`。安装身份核对为rjw.sexslavecraft.TieJinVer，复制前游戏未运行。

更新**18份1.7差异**：DLL、PDB、BUILD-INFO、3份Def和12份四语DefInjected；核对**1308份运行文件一致**，其中**532份1.6／Common冻结文件只读核对、未写入**。未删除安装独有文档或其他文件。正式DLL、受测副本及安装DLL的SHA-256均为`3ec05beb3dcba1092d4bb314262fd7585212e2d2b57f5af472bc7a8f4ebdf92c`。

备份位于`.builds/fertility-skill/same-scene-review/installed-backup-20261030-214101/`，含还原清单；同步范围和实际回执分别为同目录下的sync-plan.json及sync-result.json，后者记录实际实现提交。本文及入口的同步状态随后另作纯文档提交，不重编或重跑同一产物。没有发布，gameRuntimeVerified仍为false；待维护者重启游戏验证新同场流程，自克隆未做。

## 首次实施的最小验证

以下保留整体复核前的首次候选与31项结果；当前受测产物及33项结果以上节为准，不累计成64项。

生产候选与原生宿主构建均为**零警告、零错误**。候选位置：`.builds/fertility-skill/same-scene/candidate/Sexslavecraft.dll`；SHA-256：`397e595dc9d01492365127e86c3b9fa0c4dcb1484844aa6aff00977a0d418586`，与宿主目录受测DLL一致。正式`1.7/Assemblies`及BUILD-INFO未更新。

| 实际执行 | 结果 | 说明 |
| --- | --- | --- |
| 原生宿主`--fertility-skill` | 18／18 | 用新同场回归替换已撤销的独立预热／后续队列检查，较上一版16项净增Core2。正常完成、重复／重入、取消、许可／Start拒绝、回调后换人格／工作、确切SexProps隔离、失败不扣、实际落地后异常、原生Ability／凝胶Scribe、旧载荷默认及旧联动兼容入口只取消 |
| 原生宿主`--birth` | 13／13 | 第四步生成与出生相关既有检查，本次不改算法或重新声明第三方组合兼容 |
| Def／语言镜像 | 通过 | 38份Def、600份语言镜像一致；四语相关XML可解析，7个技能Keyed键及占位符共4份／28条对齐 |
| 差异及文档 | 通过 | git diff --check；变更文档本地链接／锚点检查。1.6／Common、正式DLL及BUILD-INFO无改动 |

本轮机制实际合计**31／31**，不是完整原生宿主或整仓All。没有按历史基线推算新的整仓通过总数。日志在`.builds/fertility-skill/same-scene/`的`build.log`、`test-build.log`、`fertility-skill.log`及`birth.log`；早期17项首轮输出不与末轮18项累计。

宿主执行生产驱动的BeginScene／TickScene／CompleteScene／CleanupScene、原Ability激活／冷却、实际公共建孕和双亲Hediff，以及生产自然受孕前置逻辑。地图／路径／接触、RJW随机动作选择、Start开始前提、场景计时／ProcessSex外围、任务跟踪器交接及图形清理由夹具隔离；不是完整RJW动作或动画的执行证据。读档回归验证原Ability与凝胶Scribe、未登记任务拒绝恢复；完整世界中双方场景保存／取消须实机，不能把它表述为完整场景恢复已通过。测试细节见[技能回归](../../../Tests/UterusDevelopmentNative/FertilityAbilityTests.cs)及[保存回归](../../../Tests/UterusDevelopmentNative/FertilityAbilityReviewTests.cs)。

## 历史实机核对范围

以下为部署时的核对范围，现已获维护者整体认可；未逐项报告的组合不补记独立通过，不再以本清单重开本批验收。

- 远处目标可选，走近后双方进入实际RJW性行为；无独立读条，不在完成后另排第二场性交。同性或不育对象能解析合适动作；有匹配动画及无匹配动画的原生表现据实确认。
- 正常完成仅建立一次施法者妊娠，之后冷却7天；走近／开始／进行中取消均无本场提前受孕或成功冷却，不能用早期高潮绕过完成要求。
- 目标或施法者换工作／人格、进行中存读档时安全取消，不清后来工作或绑定；已成功的妊娠与冷却存读档／人格流转保持。
- 抽验原生成入口的胎儿调控及后代印记，已预生成婴儿仍不追补；前四步无关验收不重开，自克隆继续后置。

首次实施时未同步游戏、未发布；本轮已按授权提交并同步测试。本轮已获实机认可，记录归Completed；此前待实机文字保留原时点。
