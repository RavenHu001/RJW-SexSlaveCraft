# 更新日志 / Changelog — SexSlaveCraft（TieJin 接续版 / TieJin continuation）

本项目基于原模组 7 月停更时的 **2.2.8** 版本继续维护，沿用原有版本号。更新按版本从新到旧排列，技术细节见 [开发更新记录](Docs/Development/开发更新记录.md)。

This continuation builds on upstream **2.2.8**, the baseline when upstream development stopped in July, and retains its version numbering. Entries are listed newest first. Technical details are in the [development log (Chinese)](Docs/Development/开发更新记录.md).

## [2.3.9] — 2026-10-10 — 统一受训身份与训导官反馈 / Unified receiver identity and Trainer Officer feedback

本版归并 `v2.3.8`（`fd60efa`）之后至 `7564173` 的全部 **12 项开发提交**：统一受训身份门槛与旧档恢复、训导官职责心情与主人评价、训导官独立弱化记忆套、训导官文案基调重写与去性别化，以及训导官对实际主人的反向信任。2.3.8 发布结果归档与两次 PR 合并不重复计为开发增量。版本元数据与运行 DLL 已同步为 **2.3.9／2.3.9.0**；构建、自动回归与实机验收范围见[2.3.9 版本验证](Docs/Releases/2.3.9/2.3.9版本验证.md)，玩家内容见[中英发布说明](Docs/Releases/2.3.9/2.3.9发布说明.md)。

This version consolidates all **12 development commits** after released `v2.3.8` (`fd60efa`) through `7564173`: unified receiver identity, Trainer Officer duty mood and owner appraisal, the Officer's separate weakened memory set, the rewritten gender-neutral Officer text, and the Officer's reverse trust in the actual Master. The previous release-results archive and two PR merges are excluded from that count. Version metadata and the runtime DLL are updated to **2.3.9 / 2.3.9.0**. See the linked bilingual notes and validation record for build, regression and in-game acceptance scope.

- **统一受训身份 / Unified receiver identity:** 日常自动与手动调教、绑定仪式，以及新增调教员指派统一要求 SSC 性奴身份；原版奴隶身份、关闭行为限制和强制命令不能豁免。候选、实际准入、执行及结算复查身份，同时保留各入口已有条件。Automatic and forced Training, Binding Rituals and new trainer assignments require SSC Sex Slave identity. Vanilla slave status, disabled restrictions and forced commands do not waive it. Candidate, admission, execution and outcome checks preserve each entry's existing conditions.
- **旧档与回调 / Historical saves and callbacks:** 不自动改写身份或删除成长；不合资格的旧任务停止。未设身份但保留有效历史锁链者可经面板确认恢复身份，保留原主人、配置及成长；迟到回调不得发放收益或清理新任务占用。Identity and growth are not rewritten on load; ineligible old tasks stop. Unset pawns with valid historical Chains can explicitly restore the role while retaining ownership, settings and progress. Late callbacks cannot grant rewards or clear a newer task's preparation.
- **训导官职责心情与反向信任 / Officer duty mood and reverse trust:** 有效任职的训导官完成一次日常调教或整场仪式后获得“履行训导职责”+3 心情一天，重复完成只刷新一份记忆；实际主人对自己绑定且有效任职的训导官有 +5“受委任的训导官”评价。同一任职条件还让训导官对这位主人获得 +5 反向好感与 +3 常驻心情。两条都是**条件性想法而不是记忆**：不设时长与容量、不写记忆列表、不进人格凝胶；关闭任职、切向、退阶、解绑或终极禁用后立即消失，恢复后重新生效，反复开关不累积。临时倒地、停工或旅行不影响；社交显示遵循原版约 100 tick 查询缓存。An actively appointed Officer gains a one-day +3 duty mood after daily Training or a whole Ritual, refreshed rather than stacked. The actual Master holds a conditional +5 appraisal, and the Officer holds +5 reverse opinion plus a +3 steady mood toward that Master. Both are conditional thoughts rather than memories: no duration, no capacity, no memory list, no personality gel. Disabling appointment, switching direction, regressing, unbinding or suspending the final effect removes them immediately and restoring conditions brings them back without accumulation. Temporary downing, work suspension or travel does not remove them, and social display follows vanilla's roughly 100-tick query cache.
- **训导官弱化记忆套 / Officer weakened memory set:** 训导官作为执行者发放受训者记忆时使用独立弱化套，系数 K=0.5，心情 **-5／+1／+4／+8**、社交 **-10／+3／+8**，时长沿用原套；档位仍由未弱化的评分或等级决定，弱化只改强度。主人原套的 Def、文案、数值、实例类与启动修正完全未改，两套家族独立分组、独立计时、互不覆盖；单次结算只发一套。仪式沿用原套且不发社交记忆，首版的 +3／+5 不乘 K。When an Officer is the executor, receiver memories use a separate weakened set with K=0.5: mood **-5/+1/+4/+8** and social **-10/+3/+8**, keeping the original durations. Stage selection still uses the unweakened score or level; only strength is reduced. The owner's set is untouched, the two families group and time independently, and one settlement grants exactly one set. Rituals keep the original set and grant no social memory, and the first-version +3/+5 values are not scaled.
- **弱值取整口径 / Weak value rounding:** 弱值四舍五入到整数，非零基准保底绝对值 1；实现使用显式 `MidpointRounding.AwayFromZero`，因此基准 +5 得 **+3**、+9 得 **+5**、-5 得 **-3**，不再依赖宿主运行时的取偶舍入。回退与舍入口径一致，半值边界已被回归逐项钉住。Weak values round to whole numbers with a minimum non-zero magnitude of 1, using explicit `MidpointRounding.AwayFromZero`: base +5 becomes **+3**, +9 becomes **+5** and -5 becomes **-3**, independent of the host runtime's banker's rounding. Opinion offsets follow the same rule and half-boundary cases are pinned by regression.
- **训导官文案重写 / Rewritten Officer text:** 训导官相关心情、评价、弱化记忆与同床文案整体重写，与主人原套同一基调——露骨但不描写具体动作，主线是“受训者是主人的所有物，训导官只是主人授权的支配工具”；动作词统一用“调教”。**全文去除性别指向**：中文不再使用“他／她”，英文不再使用整词 he／him／his／she／her，俄文改用现在时、无人称句与关系从句。同时补齐简体中文缺失的 `Thought_SharedBedWithTrainer.xml`，统一繁体与俄文的角色名。Officer mood, appraisal, weakened memories and shared-bed text was rewritten to match the owner's register — explicit without describing specific acts, framed as the receiver being the Master's property and the Officer merely the instrument the Master authorizes. The action verb is consistently “train”. **All gendered wording was removed**: no Chinese 他/她, no English he/him/his/she/her, and Russian uses present, impersonal or relative-clause forms. Simplified Chinese also gained the previously missing `Thought_SharedBedWithTrainer.xml`, with Traditional Chinese and Russian role names unified.
- **提醒 / Release note:** 2.3.8 已发布的 `SSC_TrainerOfficer_DutyFulfilled`、`SSC_TrainerOfficer_Appraisal` 与 `SSC_SharedBedWithTrainer` 文案在本版被重写，方向跨度较大；若旧版记忆或界面文字看上去不同，属于本版刻意修改而不是翻译错误。Three strings already shipped in 2.3.8 (`SSC_TrainerOfficer_DutyFulfilled`, `SSC_TrainerOfficer_Appraisal`, `SSC_SharedBedWithTrainer`) were rewritten in this version; different wording from an older install is intentional rather than a translation error.

历史条目中的“训导官职责反馈”原文如下，保留供对照。The original development-branch entry is retained below for reference.

- **训导官职责反馈 / Trainer Officer feedback:** 有效任职的训导官完成日常调教或整场仪式后获得“履行训导职责”心情 +3，持续一天；重复完成刷新一份记忆，不叠加，普通培养已满及有效终极跨方向同样适用。实际主人对自己有效任职的训导官获得“受委任的训导官”好感 +5，随绑定、资格和个人开关生效或消失；临时停工、倒地或离图不取消该评价。见[实现记录](Docs/Development/训导官职责反馈实现与测试记录.md)，维护者已确认本项修改有效并授权提交。Active Trainer Officers gain +3 mood for one day after completing daily Training or a whole Ritual. Further completions refresh a single memory; ordinary completion and active final Officers training another direction remain eligible. Their actual Master has a conditional +5 opinion while the Officer remains bound and actively appointed. Temporary work suspension, downing or travel does not remove this appraisal. The maintainer confirmed the changes work in game and approved committing them.

- **训导官职责反馈 / Trainer Officer feedback:** 有效任职的训导官完成日常调教或整场仪式后获得“履行训导职责”心情 +3，持续一天；重复完成刷新一份记忆，不叠加，普通培养已满及有效终极跨方向同样适用。实际主人对自己有效任职的训导官获得“受委任的训导官”好感 +5，随绑定、资格和个人开关生效或消失；临时停工、倒地或离图不取消该评价。见[实现记录](Docs/Development/训导官职责反馈实现与测试记录.md)，维护者已确认本项修改有效并授权提交。Active Trainer Officers gain +3 mood for one day after completing daily Training or a whole Ritual. Further completions refresh a single memory; ordinary completion and active final Officers training another direction remain eligible. Their actual Master has a conditional +5 opinion while the Officer remains bound and actively appointed. Temporary work suspension, downing or travel does not remove this appraisal. The maintainer confirmed the changes work in game and approved committing them.

## [2.3.8] — 2026-10-08 — 绑定仪式助兴修复与选人提示 / Binding Ritual spectator fixes and role hints

本版归并 `v2.3.7`（`71b5c88`）之后至 `2dcf68e` 的全部 8 项开发提交，不重复计入 2.3.7 发布归档。已发布为最新正式版，当次 24 套、1363/1363 项门禁通过。详见[中英发布说明](Docs/Releases/2.3.8/2.3.8发布说明.md)及[版本验证](Docs/Releases/2.3.8/2.3.8版本验证.md)。安装 ZIP 与 SHA-256 校验文件见 [v2.3.8 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.8)。

This version consolidates all 8 development commits after `v2.3.7` (`71b5c88`) through `2dcf68e`, excluding the previous release archive. Published as the latest stable release; the current gate passed 24 suites and 1363/1363 cases. See the linked bilingual notes and validation record. Download the ZIP and SHA-256 checksum from [v2.3.8 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.8).

- **观众助兴 / Spectator bonus:** 准备窗口和结算都排除主持者与仪式性奴，结算按本场实际经过时间计算，实际观看至少半场才提供加成。零观众为 +0%，四名合格观众为 +12.5%，十人及以上封顶 +20%；结果信件始终显示观众分项。维护者已确认修复有效。The preview and outcome exclude both protagonists. Only spectators attending at least half the actual elapsed ritual contribute: none gives +0%, four give +12.5%, and ten or more cap at +20%. Result letters always show the spectator factor. The maintainer confirmed the fix works in game.
- **角色图标 / Role icons:** 用金色鞭子标记主持候选、紫色项圈标记 SSC 性奴候选，双资格图标在头像左上横向排列，减少遮挡。鞭子表示主持及调教资格，不等于 SSC 主人身份或所有权；候选图标不代表已通过全部条件。新布局已获维护者实机确认。A gold whip marks host candidates and a purple collar marks SSC Sex Slave candidates. Dual icons share one row above the portrait. The whip indicates hosting/trainer eligibility rather than Master identity or ownership; candidates still require complete checks. The maintainer confirmed the new layout in game.
- **选人反馈 / Selection feedback:** 保留已选勾选和不可选斜线，悬停、拖拽显示允许状态或具体拒绝原因；配对提示遵守指定调教员、行为许可和原版角色锁定等现有限制。沿用点击、右键、拖拽、换人和交换流程，观众分配不受候选图标影响。Checks and slashes distinguish assigned and unavailable roles. Hover and drag feedback shows eligibility or the actual refusal reason, including assignment, permission and native role-lock restrictions. Existing selection, replacement, swap and spectator flows are retained.
- **简明说明与范围 / Concise text and scope:** 四语图例、候选名称和状态文案精简，界面中的“目标”统一为“性奴”。紫色候选图标只授予 SSC 性奴；实际选角与开始沿用既有后台准入条件，统一受调教门槛仍属未来规划。Four-language legends, candidate names and status text are shortened, with “target” renamed to “sex slave” in selection hints. Only SSC Sex Slaves receive the collar candidate icon; backend role admission and startup retain their existing rules. Unifying Training admission remains future work.

实现及历史验证见[观众助兴修复](Docs/Development/绑定仪式观众助兴修复.md)和[角色候选提示](Docs/Development/绑定仪式角色候选提示.md)。Implementation and historical validation are recorded in the linked development documents.

## [2.3.7] — 2026-10-06 — 猫狗特化与宠物互动 / Cat and Dog specializations and pet interactions

本版归并 `v2.3.6`（`754c3ce`）之后至 `a24562e` 的全部 17 项开发提交，版本元数据与运行 DLL 同步为 2.3.7。安装 ZIP 与 SHA-256 校验文件见 [v2.3.7 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.7)。 详见[中英发布说明](Docs/Releases/2.3.7/2.3.7发布说明.md)及[版本验证](Docs/Releases/2.3.7/2.3.7版本验证.md)。

This version consolidates all 17 development commits after `v2.3.6` (`754c3ce`) through `a24562e`, with version metadata and the runtime DLL updated to 2.3.7. Download the installation ZIP and SHA-256 checksum from [v2.3.7 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.7). See the linked bilingual notes and validation record. These additions are not included in the historical 2.3.6 package.

- 开放猫普通培养，接入成功亲昵 1 个百分点特色经验；统一猫狗兔互斥、配方消耗前保护及跨方向保留的终极效果。Cat training and its 1-point affection reward are available, with pet exclusivity, pre-consumption recipe checks and retained final effects.
- 终极猫获得同地图选人、同格两秒安抚的“安抚共鸣”，恢复当前精神状态或给予半天鼓舞；终极狗获得“定向驯导”，直接驯服野生动物或完整完成己方动物一个有效勾选训练项目。两项技能各自采用一天冷却，接入目标提示、读条及特效。Final Cats gain Soothing Resonance for mental-state recovery or half-day encouragement; final Dogs gain Directed Taming for direct taming or full completion of one eligible checked training item. Both use map-wide selection, two-second interaction on the target cell and their own one-day cooldowns, with targeting feedback and effects.
- 修整狗普通训练成长，确认真实动物训练互动后结算；普通亲昵增加接近、两秒停留、双方朝向、读条及爱心，保护双方重要任务和新命令。Dog work progress is settled after an actual training interaction. Ordinary affection adds visible approach and two-second interaction while protecting work and new commands.
- 成功亲昵后，成年猫狗在双方 SSC/RJW 自愿资格允许时，以一次 20% 判定向实际主人发起后续；不中、拒绝或失败保留亲昵结果。此后续已实现并提交，实机验收待完成。After successful affection, eligible adult Cats/Dogs make one 20% roll for a consensual follow-up with their actual bound master. A miss, refusal or failure preserves affection rewards. This follow-up is implemented and committed, with in-game acceptance pending.
- 猫狗培养进度统一一位小数和普通完成提示，完成后悬停显示四语人格凝胶终极化步骤；研究、玩家指南及规划状态同步当前实现。兔入口仍禁用。Cat/Dog progress uses one decimal place, distinguishes ordinary completion from finalization and offers localized gel finalization tooltips. Research, player guides and plans reflect 2.3.7; Rabbit selection remains disabled.

实现与验证范围见[专项规划](Docs/Design/猫狗特化与统一培养经验初步规划.md)及[显示与说明收尾记录](Docs/Development/猫狗特化显示与说明收尾.md)。Implementation and validation scope are recorded in the linked plan and development record.

## [2.3.6] — 2026-10-02 — 统一特化基础培养经验 / Unified specialization Training progress

本版将 2.3.5 之后已实现的统一特化基础培养经验正式归入 2.3.6。安装 ZIP 与 SHA-256 校验文件见 [v2.3.6 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.6)；详见[中英发布说明](Docs/Releases/2.3.6/2.3.6发布说明.md)和[版本验证](Docs/Releases/2.3.6/2.3.6版本验证.md)。

Version 2.3.6 releases the shared base specialization Training progress implemented after 2.3.5. Download the installation ZIP and SHA-256 checksum from [v2.3.6 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.6); see the [bilingual notes](Docs/Releases/2.3.6/2.3.6发布说明.md) and [validation record](Docs/Releases/2.3.6/2.3.6版本验证.md).

- **统一受训成长 / Shared Training progress:** 当前正在培养的性奴特化在完整日常调教或绑定仪式成功结算后获得一次基础经验，沿用战斗员评分公式：`min(score / 2000, 0.04)`，由实际绑定主人施教时再乘 1.5。仪式评分为 `quality × 75`；中断、重复回调、普通培养完成或已有该方向终极成果时不再发放。猫、兔的选择入口仍未开放，自我调教不提供这项特化经验。The current Sex Slave specialization gains one base progress reward after completed daily Training or a successful full Binding Ritual. It uses the Combatant formula, `min(score / 2000, 0.04)`, multiplied by 1.5 when the actual bound master trains the pawn. Rituals use `quality × 75` as the score. Interrupted or duplicate outcomes and completed or finalized specializations grant no further base progress. Cat and Rabbit selection remain disabled; self-training does not grant this progress.
- **既有奖励与验证 / Existing rewards and validation:** 训导官原固定受训奖励和公交车旧日常受训增量已由公共基础经验替换；训导官施教、战斗员直接击杀及其他特色来源保留。实现提交为 `36e9483`，Release 构建和 20 套自动回归 968/968 通过，维护者已确认游戏内有效。猫狗新技能、宠物互斥和亲昵扩展仍在规划中；详见[专项规划](Docs/Design/猫狗特化与统一培养经验初步规划.md)。The former fixed Training Officer reward and Public Use daily Training increment are replaced by shared base progress; Training Officer teaching, Combatant direct kills and other distinct sources remain. Commit `36e9483` passed a Release build and all 20 automated suites (968/968), and the maintainer confirmed it works in game. New Cat/Dog abilities, pet exclusivity and affection changes remain planned; see the [design plan](Docs/Design/猫狗特化与统一培养经验初步规划.md).

## [2.3.5] — 2026-09-28 — 自我调教与调教任务交接修复 / Self-training and Training handoff fixes

本版归并 `v2.3.4`（`9c2aadf`）之后至 `abf60d1` 的全部分支更新，并同步版本元数据及文档。2.3.5 已正式发布，安装包与 SHA-256 校验文件见 [v2.3.5 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.5)。详见[中英发布说明](Docs/Releases/2.3.5/2.3.5发布说明.md)和[版本验证](Docs/Releases/2.3.5/2.3.5版本验证.md)。

This version includes all branch changes after `v2.3.4` (`9c2aadf`) through `abf60d1`, plus version metadata and documentation updates. Version 2.3.5 is released; download the installation ZIP and SHA-256 checksum from [v2.3.5 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.5). See the [bilingual notes](Docs/Releases/2.3.5/2.3.5发布说明.md) and [validation record](Docs/Releases/2.3.5/2.3.5版本验证.md).

### 2026-09-28 — 自我调教 / Self-training

- **独立许可与任务 / Separate permission and job:** 持链 SSC 性奴默认允许自我调教，可在限制面板独立关闭；选中并右键本人可手动选择可执行部位，自动行为只从 RJW 已产生的自慰候选分流。四阶段分流概率为 30%／55%／80%／50%，首版仍受禁止自慰基因阻断。Chain-bearing SSC Sex Slaves may self-train by default, with a separate restriction toggle, a self-targeted manual menu and diversion from RJW automatic masturbation candidates. The four stage chances are 30% / 55% / 80% / 50%; anti-masturbation genes still block this first version.
- **独立收益与反馈 / Separate reward and feedback:** 自我调教按场景开始时的恶堕和正向好感评分，给予受锁链阶段约束的恶堕收益及一天专用记忆；两种许可组合有对应状态心情。左上角消息采用日常调教式“角色 | 评分 | 恶堕”格式。Self-training scores Corruption and positive opinion at scene start, grants Chain-capped Corruption and a dedicated one-day memory, and adds permission-state moods. Its result message shows `pawn | score | Corruption` in the ordinary Training style.
- **限制文本 / Restriction text:** 补齐繁中及俄文行为限制面板的全部条目，包含自我调教许可；同步源码项目语言镜像并在发布前检查一致性。Completes Traditional Chinese and Russian restriction-panel text, including self-training permission, and checks the source-project language mirror before packaging.
- **回归与发布验证 / Regression and release validation:** 修正自我调教测试汇总格式，避免用例通过却阻断发布；新增 36 项生产任务驱动检查，自我调教套件共 50 项，覆盖中断、存读档、动画回调、阶段上限及一次性结算。完整回归 20 套、966/966 通过；动画视觉仍需实机确认。Fixes the test summary format that blocked packaging despite passing tests, and adds 36 production-driver checks for 50 self-training checks in total. Coverage includes interruption, save/load, animation callbacks, stage caps and one-time rewards. All 20 suites passed, with 966/966 checks; visual animation still requires in-game confirmation.

### 2026-09-28 — 调教任务交接修复 / Training handoff fix

- **调教启动中断 / Interrupted Training startup:** 修复调教员与目标合到同一格后立即转去吃饭、加工或其他工作，导致接收任务启动失败的问题。位置同步现在保留双方当前任务，由接收任务启动步骤统一完成交接；保留玩家取消、征召等正常中断。Fixes Training failing to start when the trainer switches to eating, crafting or another job immediately after moving onto the target's cell. Position synchronization now preserves both current jobs until the receiver handoff, while normal cancellation and drafting remain available.
- **旧回调与配对保护 / Stale callbacks and pairing:** 修复原任务结束或 Job 对象被复用后，旧调教回调误释放新工作的预约、结束新工作或清理新占用的问题。接收任务按实际参与者配对，失效交接只回收本次孤立接收器；共享此流程的绑定仪式、人格排泄及家具接收器同步补充保护。Prevents callbacks from ended or pooled jobs from releasing a replacement job's reservations, ending that job or clearing its training state. Receiver handoffs verify the actual pair and clean up only their own orphan receiver. The shared Binding Ritual, personality-excretion and furniture-receiver paths receive the same safeguards.
- **验证 / Validation:** 对应提交 `0d61aa7`，新增 13 项交接回归；生命周期 92/92、无 UAP 变体 83/83、任务限制 142/142、仪式进度 29/29 通过，真实游戏程序集编译通过。已同步本机，维护者实机复测确认有效。Commit `0d61aa7` adds 13 handoff regression cases. Validation passed 92/92 lifecycle cases, 83/83 without UAP, 142/142 interaction-protection cases and 29/29 ritual-progression cases, plus compilation against the installed game assemblies. The fix was installed locally and confirmed effective in-game by the maintainer.

## [2.3.4] — 2026-09-27 — 战斗员特化、显示整理与任务修复 / Combatant specialization, status display and job fixes

本版汇总 `v2.3.3`（`3f62e41`）之后至 `3260182` 的全部已实现更新：战斗员特化五阶段、测试按钮、专属图标、特化显示整理及人格排泄任务修复，并收录中英模组介绍和配套开发记录。2.3.4 正式发布，安装包与 SHA-256 校验文件见 [v2.3.4 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.4)；详见[中英发布说明](Docs/Releases/2.3.4/2.3.4发布说明.md)与[版本验证](Docs/Releases/2.3.4/2.3.4版本验证.md)。

Version 2.3.4 includes all implemented changes after `v2.3.3` (`3f62e41`) through `3260182`: the complete Combatant specialization, testing command, dedicated icon, specialization display updates and personality-excretion fixes, plus the bilingual mod overview and development records. Version 2.3.4 is released; download the ZIP and SHA-256 checksum from [v2.3.4](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.4). See the [release notes](Docs/Releases/2.3.4/2.3.4发布说明.md) and [validation record](Docs/Releases/2.3.4/2.3.4版本验证.md).

- **人格排泄任务保持 / Personality excretion continuity:** 修复被执行者在人格排泄途中转去搬运、进食、睡眠或娱乐，导致任务中断的问题。手动和自动发起采用相同保护，保留玩家取消及任务失效时的退出。Prevents the recipient from interrupting personality excretion to haul, eat, sleep or seek recreation. The same protection applies to manually and automatically initiated jobs, while explicit cancellation and invalid-job cleanup remain available.
- **人格排泄执行者冲突 / Competing excretion initiators:** 修复两名执行者争抢同一目标的问题。先开始者保持执行，后来者会被拒绝，原任务及进度不会因争抢重启；取消后允许其他执行者重新接手，并建立正确的新配对。两项修复均已同步本机并获维护者实机有效确认，对应提交 `928b5d2`；详见[实现与验证记录](Docs/Development/人格排泄接收任务抢占修复.md)。Prevents two initiators from competing for the same target: the first continues, later contenders are rejected without restarting the scene or its progress, and cancellation allows another initiator to establish a new pair. Both fixes were installed locally and confirmed effective in-game by the maintainer (commit `928b5d2`).
- **培养入口 / Eligibility:** 完成战斗员研究后，SSC 身份为性奴的角色可选择培养；无需绑定主人或达到特定锁链阶段。主人及未设定身份不显示特化区。Combatant requires its research and SSC Sex Slave identity, without a master bond or Chain-stage requirement. The specialization section is hidden for Masters and unset identities.
- **成长来源 / Progression:** 正常完成日常调教按评分增长，单次上限 4 个百分点；实际绑定主人施教为基础收益的 1.5 倍，上限 6 个百分点。直接击杀按目标实际体型增长，单次最多 3 个百分点；排除调教中断、绑定仪式、屠宰和间接死亡，并防止重复结算。Completed daily Training grants score-based progress capped at 4 percentage points; the actual bound master grants 1.5 times the base reward, capped at 6. Direct kills grant body-size-based progress capped at 3 points. Interrupted Training, Binding Rituals, slaughter, indirect deaths and duplicate payouts are excluded.
- **阶段收益 / Passive bonuses:** 20% 和 50% 分别解锁基础与熟练效果，提高射击和近战命中评分，缩短瞄准及近战冷却，降低精神崩溃临界值；熟练阶段承伤 ×0.90，终极阶段 ×0.80。切换方向保留独立进度，各阶段不叠加。Basic and proficient bonuses begin at 20% and 50%, improving accuracy ratings, aim and melee recovery, and mental stability. Incoming damage is ×0.90 at proficiency and ×0.80 after finalization. Switching paths preserves independent progress; stages do not stack.
- **凝胶终极化 / Gel finalization:** 普通培养完成后，抽取人格凝胶，在雕刻台终极化并重新植入；兼容八种凝胶基底，消耗前复查资格。终极成果随人格保存，跨方向、解绑及退阶后保留；抽取或替换人格清理旧身体对应状态及技能。After ordinary completion, extract the personality gel, finalize it at a sculpting table and implant it. All eight gel bases are supported, with eligibility rechecked before consumption. Final achievements follow the personality and survive path changes, unbinding and Chain regression; extraction/replacement clears the old body's associated states and ability.
- **战斗超频 / Combat Overdrive:** 终极战斗员仅在征召时可手动对自身发动，持续 1 游戏小时，冷却 4 游戏小时。近战伤害 ×1.30、近战冷却 ×0.75、远程冷却 ×0.80、承伤 ×0.75、移动速度 ×1.25、疼痛 ×0.50；解除征召不提前结束已生效强化。修复效果组件初始化红字及技能发动后未施加强化的问题。Final combatants can activate a self-buff while drafted, lasting 1 in-game hour with a 4-hour cooldown. Multipliers are ×1.30 melee damage, ×0.75 melee cooldown, ×0.80 ranged cooldown, ×0.75 incoming damage, ×1.25 movement speed and ×0.50 pain. Undrafting does not end an active buff. Fixed effect-component initialization errors and missing buff application.
- **存档与人格 / Saves and transfer:** 正常存读档保留技能冷却及强化剩余时间；人格迁移按共用流程重新授予技能，不迁移旧冷却或临时强化。Normal saves retain cooldown and remaining buff duration. Personality transfer grants the ability through the shared flow without transferring the old cooldown or active buff.
- **界面与美术 / UI and art:** 新增平面化女性剪影、皮革圆环项圈与紫色神经纹路组成的专属技能图标。战斗员状态采用淡紫色，训导官采用青绿色、禁用状态采用灰绿色；删除终极公交车与奶牛重复显示的阶段名称及四语对应翻译。Adds a dedicated flat female-silhouette icon with a ringed leather collar and violet neural accents. Combatant labels use lavender; Training Officers use teal and muted gray-green when disabled. Removes duplicated final Public Use/Cow stage labels in all four languages.
- **文本与测试工具 / Text and testing tools:** 四语说明统一纳米机械液改造与主动活化设定，技能概述不重复健康页自动属性列表，补充普通完成后的终极化提示。开发模式及模组调试按钮同时开启时，可直接拉满当前特化普通培养进度；不会自动终极化。Four-language descriptions consistently explain nanofluid modification and activation, with concise ability summaries and finalization guidance. With developer mode and mod debug gizmos enabled, a command completes the current ordinary specialization without finalizing it.
- **文档与维护 / Documentation and maintenance:** 新增中英模组介绍、战斗员分阶段实施与完成核查记录，更新玩家指南和规划；自动回归由 18 套扩为 19 套，增加战斗员资源核验工具。Adds a bilingual mod overview, Combatant implementation and completion records, updated player guides and plans; expands automated regression from 18 to 19 suites and adds a Combatant resource audit.
- **验证 / Validation:** 人格排泄修复新增 32 项回归，最新完整检查 19 套、886/886 项通过，其中战斗员专项 49/49；272 个 XML、四语、图标引用与本机原版组件检查通过。战斗员完整玩法和数值验收沿用此前确认；人格排泄两轮修复另获本机实机确认。The excretion fixes add 32 regression cases. The latest full check passed all 19 suites and 886 cases, including 49 Combatant cases; 272 XML files, localization, icon references and local game-component checks also passed. Combatant gameplay and balance acceptance follows the earlier confirmation; both excretion fixes were separately confirmed in-game.

## [2.3.3] — 2026-09-26 — RJW 泄欲对象资格与特化科技说明 / RJW Comfort designation and specialization descriptions

本版包含最新两次玩家可见改动：`229e220` 的 RJW 泄欲对象资格与 `df785fb` 的特化科技说明。两次提交之前还有 `2ffdeb0`，仅更新未来开发规划，不作为已实现功能。安装包及校验文件见 [v2.3.3 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.3)，详细内容见 [2.3.3 发布说明](Docs/Releases/2.3.3/2.3.3发布说明.md)。

This release covers the two latest player-visible changes: RJW Comfort designation in `229e220` and specialization research descriptions in `df785fb`. The preceding `2ffdeb0` changes a future plan only. Download the ZIP and checksum from [v2.3.3](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.3); see the [bilingual release notes](Docs/Releases/2.3.3/2.3.3发布说明.md).

- **RJW 面板 / RJW panel：** SSC 小人到达性奴阶段（锁链第 2 阶段及以上）后，可按 RJW 原有资格、权限和身体条件在其面板中被指派为泄欲对象；见习性奴不会仅凭 SSC 身份获得该资格。保留 RJW 原有的受虐狂、囚犯和奴隶路径，不联动 SSC 个体限制。维护者已在本机游戏中确认补丁有效；其他模组组合尚未实测。At SSC Sex Slave stage (Chain stage 2 or higher), a pawn can be designated as a Comfort pawn in the RJW panel, subject to RJW's existing settings, permissions and physical checks. Trainees gain no eligibility from SSC status alone. RJW's existing Masochist, prisoner and slave paths remain, without linking SSC's individual restrictions. The maintainer confirmed the patch in game; combinations with other mods have not been tested.
- **特化科技说明 / Specialization research descriptions：** 在奶牛、公交车、训导官和宠物狗的科技介绍中补充实际功能、培养门槛及经验来源；猫、兔和战斗员明确标注未完成或未实装，以及目前没有可用的经验来源。移除人格编辑科技名称中过时的“目前只有公交车可用”，并同步简中、繁中、英文和俄文文案。Adds implemented effects, requirements and experience sources to Cow, Public Use, Training Officer and Pet Dog research descriptions. Cat, Rabbit and Combatant descriptions identify unavailable paths and experience sources. Removes the outdated “Public Use only” research label and updates Simplified Chinese, Traditional Chinese, English and Russian text. Gameplay behavior is unchanged.

## [2.3.2] — 2026-09-25 — 训导官特化与调教稳定性 / Training Officers and Training reliability

归纳 2.3.1 发布后截至 `05170b1` 的全部已实现更新，包含最新属性加成与四语文案。版本元数据与安装包按 2.3.2 统一；发布入口：[v2.3.2](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.2)。 详见 [2.3.2 中英更新说明](Docs/Releases/2.3.2/2.3.2发布说明.md)与[整理及验证状态](Docs/Releases/2.3.2/2.3.2版本验证.md)。

Covers implemented changes since released 2.3.1 through `05170b1`, including the latest bonuses and localization. Runtime metadata and the installation package use 2.3.2. Release: [v2.3.2](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.2). See the [bilingual notes](Docs/Releases/2.3.2/2.3.2发布说明.md) and [validation status](Docs/Releases/2.3.2/2.3.2版本验证.md).

- **训导官与任职 / Training Officers:** 新增代主人调教其他性奴的职能特化。SSC 性奴须为自由殖民者、已绑定实际主人且锁链达到第 3 阶段；当前普通进度达到 20% 或拥有有效终极状态，才可手动开启调教员。主人固定资格保持。Adds a specialization for performing Training on the owner's behalf. An SSC Sex Slave must be a bonded free colonist at Chain stage 3 or higher, with 20% current progress or an active final state, to opt into trainer duty. Masters retain their fixed eligibility.
- **培养与终极化 / Progression and finalization:** 新增成本 1200 的研究及工作量 3500 的雕刻台人格凝胶配方。成功接受调教、施教、主动双人行为分别增加 5、2.5、1 个百分点；主动行为包括规则允许的强制行为。普通完成不会自动终极化，重复与中断结算不发放经验。Adds research costing 1,200 and a personality-gel finalization recipe requiring 3,500 work. Completed Training received, Training performed and initiated pair activity grant 5, 2.5 and 1 percentage points respectively, including permitted forced initiation. Full ordinary progress requires manual finalization; duplicate or interrupted completion does not grant rewards.
- **属性加成 / Attribute bonuses:** 20%／50%／有效终极依次提供崩溃临界值 −3／−6／−10 个百分点、压制能力 +10／+20／+30 个百分点、教化能力 ×1.05／×1.10／×1.15；阶段不叠加，后两项要求 Ideology。At 20%, 50% and active finalization, grants mental break threshold offsets of −3/−6/−10 percentage points, suppression power offsets of +10/+20/+30 points, and conversion power factors of ×1.05/×1.10/×1.15. Stages do not stack; the latter two stats require Ideology.
- **资格生命周期与限制 / Eligibility and permissions:** 普通失格保存进度后退出培养，终极失格保留禁用记录，条件恢复后重新生效。选中有效训导官方向即提供两项主动行为强制允许，无须等待 20% 或开启任职；沿用全局开关、装备优先和对方许可。人格凝胶传承进度与完成记录，恢复结束后按接收身体条件维护。Losing eligibility archives ordinary progress or disables a retained final record. The two initiation overrides apply from selecting an eligible direction, independent of the trainer toggle and 20% threshold, while respecting global switches, equipment and recipient rules. Gel transfer retains source progress/final records and evaluates the recipient after restoration.
- **升级提示 / Upgrade note:** **旧档性奴调教员开关一次性关闭**；绑定、指派与进度保留，取得新资格后须手动重新开启，重复读档不重置新选择。**Old Sex Slave trainer toggles reset once.** Bonds, assignments and progress remain; obtain eligibility and enable duty again. Later loads preserve new choices.
- **仪式选人 / Ritual selection:** 两个执行槽改为手动选人，浏览候选使用轻量检查，实际选入和开始时验证。减少重复 RJW 查询，失败恢复分配与品质预览；修复延迟点击、拖拽、空角色及头像红字，保留合法替换和原版观众分配。Uses manual performer selection, lightweight browsing and validation on selection/start. Reduces repeated RJW checks, restores assignments/previews on rejection, and fixes delayed-click, drag, null-participant and portrait errors while preserving valid replacements and native spectators.
- **连续任务与预约 / Consecutive jobs and reservations:** 修复第二目标工作显示调教却停在原地的问题，统一 Touch 接近、清理遗留 UAP 位置锁并对停止寻路有限重试；仅释放当前 Job 持有的预约，消除重复释放红字。Fixes trainers stuck at the previous target through Touch approach, stale UAP lock cleanup and bounded path recovery. Releases only reservations owned by the initiating job, preventing repeated-release errors.
- **Education 兼容 / Education compatibility:** 自动调教避让活动课程中的学生和教师，途中或执行中入课也会退出，下课后恢复候选。兼容可选，手动强制命令及教育自身规则保持原行为。Automatic Training yields to active students and teachers, including class entry during travel/execution, and resumes eligibility after class. Education remains optional; manual forced orders and education-side rules retain their behavior.
- **界面与凝胶保护 / UI and gel safeguards:** 修复终极化后“未选择”的状态认领和显示回退，保留终极效果与历史；消耗前拒绝异常凝胶并保留账单次数；统一完成容差，优化长译文布局，补齐简中、繁中、英文、俄文研究、配方、阶段和状态提示。Preserves and correctly displays Unset after finalization without removing benefits/history; rejects invalid gels before consumption or bill counting; unifies completion tolerance, fits long text and completes four-language research, recipe, stage and status text.
- **维护与验证 / Maintenance and validation:** 减少同次资格查询，复用现有低频和状态事件；统一回归由 16 套扩为 18 套，补充仪式选人、人格恢复、真实任务交接和界面回归。历史验证和实机确认按批次记录；两处旧数量/文案断言已适配，2.3.2 Release 重建与发布预检 18 套 782/782 全部通过。Reduces repeated queries and extends the existing runner from 16 to 18 suites. Historical and in-game results remain recorded by development batch. Two outdated count/text assertions were updated; the 2.3.2 Release build and all 18 preflight suites passed (782/782).

训导官专属心情与社交反馈等未来构想未计入已实现内容。Dedicated Training Officer mood/social feedback and other future plans are not included as implemented features.

## [2.3.1] — 2026-09-22 — 新限制系统与个体行为配置 / Unified restrictions and individual behavior rules

**正式发布。** 安装包及校验文件见 [v2.3.1 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.1)。 汇总本轮已完成并经维护者确认的新限制系统、任务接入、旧档迁移、修复与界面调整。详细内容见 [2.3.1 中英双语更新说明](Docs/Releases/2.3.1/2.3.1发布说明.md)，实机反馈见 [开发完成与验收记录](Docs/Development/新限制系统开发完成与实机验收.md)。

**Released.** ZIP and checksum: [v2.3.1 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.1). Summarizes the implemented and maintainer-confirmed restriction system, job integration, save migration, fixes and UI changes. See the [bilingual 2.3.1 notes](Docs/Releases/2.3.1/2.3.1发布说明.md) and [development acceptance record (Chinese)](Docs/Development/新限制系统开发完成与实机验收.md).

- **个体规则 / Individual rules：** 新增主动、被动、调教三类共六项许可，分别管理自慰、自愿发起、强制发起、接受非主人自愿行为、接受非主人强制行为及接受非主人调教。自愿发起可选“仅限主人／不限对象”。Adds six per-pawn permissions covering solo activity, consensual/forced initiation, consensual/forced reception from non-owners, and non-owner Training. Consensual initiation supports Owner only or Anyone.
- **绑定门槛与主人许可 / Bond scope and owner permission：** 限制仅在实际绑定主人后生效；单独设置性奴身份不会启用。实际主人向自己的绑定对象发起时，在统一入口内直接允许，个人、装备和特化限制不能否决。身体、研究、可达性等任务条件仍保留。Restrictions activate only after an actual owner bond. The actual owner initiating with their own bound pawn is immediately allowed by the unified policy; individual, equipment and specialization rules cannot veto it. Normal physical, research and reachability requirements still apply.
- **装备与特化 / Equipment and specializations：** 装备保护统一为条目强制值，按“装备 > 已启用特化强制 > 个人保存值”解析。现有防护服强制禁止接受非主人强制行为；公交车保留两项被动许可的默认及强制允许。强制结果不覆盖个人选择，定义由 XML 提供。Equipment now supplies rule overrides, resolved above enabled specialization overrides and saved choices. Existing protective apparel denies forced reception from non-owners; Public Use retains its two reception defaults and forced allowances. XML defines these values, and runtime overrides do not overwrite personal choices.
- **任务与兼容 / Jobs and compatibility：** 普通双人、单人、人格排泄、日常调教、仪式、交易及宠物事件、独立原版 Lovin、已支持的 LifeForce 和家具双人/调教桥接均接入统一许可；玩家命令与 AI 使用相同规则。Unifies permission checks for ordinary pair and solo jobs, personality-excretion jobs, daily Training, rituals, trade and pet events, native Lovin, supported LifeForce paths and existing furniture interaction/Training bridges. Player orders and AI use the same rules.
- **调教员指派 / Trainer assignment：** 选择合格非主人调教员时同步开启调教许可；普通互动继续检查自身条目，强制禁止调教仍会阻止指派。自动工作仍交给指定者，主人手动发起及主持不会被其他指派排除。Selecting an eligible non-owner trainer also grants Training permission. Ordinary interactions still require their own permissions, and forced Training denial prevents assignment. Automatic work remains assigned; the owner retains manual and ritual access.
- **保存与迁移 / Saves and migration：** 新配置随角色保存，旧选择一次性迁移；解绑重绑、人格恢复及克隆按各自生命周期保留或初始化配置。默认模板编辑不追溯覆盖已有角色，另提供全存档批量应用。Per-pawn configurations persist with saves and migrate legacy choices once. Unbinding/rebinding, personality restoration and cloning preserve or initialize data as appropriate. Editing defaults leaves existing configurations intact; an explicit save-wide apply action is available.
- **界面 / UI：** ITab 在右侧展开规则栏，入口位于调教设置紧邻上方；不适用时置灰。采用原生绿勾/红叉，自愿发起下属对象单选并记住关闭前的选择；强制条目显示生效值且不可修改。默认模板收纳到子窗口，批量覆盖提供橙色警示及确认，测试入口位于限制设置区块末尾。Adds a right-side ITab panel with an entry above Training settings, disabled when inactive. Native checks/crosses and remembered target options replace value buttons; forced rules show effective values and are locked. Defaults use a separate window with a highlighted overwrite confirmation, and the test entry sits at the end of restriction settings.
- **稳定性与清理 / Reliability and cleanup：** 拒绝准备中的任务时只清理本次等待和占用；已开始场景正常收尾，下一阶段和新参与者重新检查。修复开始状态读档、事件通知时机及重复结算风险、死亡主人重新指派和恢复期间解绑清理；移除旧独立保护判定、白名单及例外控件，旧字段仅供迁移。Rejected preparation cleans up only its own waiting and participation state. Started scenes finish normally; later phases and newcomers are checked again. Improves saved start-state recovery, event notification timing, prevention of false completion, dead-owner assignment and unbinding cleanup. Removes legacy parallel policies, bypasses and exception controls, retaining legacy data only for migration.
- **维护 / Maintenance：** 拆分配置构建、指派授权、准备清理和事件通知职责，减少规则查询中的临时分配；补充中文代码注释、简中/英文限制文本、兼容签名诊断及回归覆盖。Separates configuration building, assignment authorization, preparation cleanup and event notifications; reduces query allocations and adds Chinese code comments, Simplified Chinese/English restriction text, compatibility diagnostics and regression coverage.

开发阶段的全量回归为 **16 套、645/645**；缺少 LifeForce 的额外变体 **108/108**。最新界面批次 Release 构建零警告、零错误，限制核心 **124/124**；这些是不同批次的已有结果，不累计为一次发布测试。维护者已确认基础功能、最新界面以及旧档升级后存读档、任务中途存读档、准备与已开始场景中的规则变化和常用兼容组合均无问题。

Development validation passed **645/645 across 16 suites**, plus **108/108** in the optional LifeForce-absent variant. The latest UI batch built without warnings/errors and passed **124/124** restriction-core cases. These are separate existing runs, not one combined release test. The maintainer confirmed gameplay, UI, upgraded-save reloads, mid-job saves, rule changes before/after scene start and commonly used compatibility combinations.

2.3.1 已同步运行版本元数据并重新构建，发布预检全量 **645/645** 通过，详见 [版本验证](Docs/Releases/2.3.1/2.3.1版本验证.md)。The runtime version was updated and rebuilt; release preflight passed **645/645** cases.

科技赋予限制条件与更可见、情境化的拒绝反馈仍属于未来规划，不计入本版已实现内容。Research-based activation and more visible, contextual rejection feedback remain future work.

### 2.3.1 已有工具补记 / Existing tooling notes for 2.3.1

- 统一验证入口 `Scripts/Test-All.ps1` 在 2.3.1 中已存在，当时纳入 16 套回归，提供日志、JSON 汇总、依赖检查和失败阻止打包。原未归版条目在此补记，2.3.2 的增量为套件及覆盖扩展。The unified validation runner already existed in 2.3.1 with 16 suites, logs, JSON summaries, dependency checks and packaging failure gates; 2.3.2 extends its coverage. See [发布与打包](Docs/Maintenance/发布与打包.md).
- 辅助“诊断与兼容”面板及四语设置滚动页同样已在 2.3.1 中存在，用于查看和复制版本、依赖及部分兼容注册信息；尚未确认其具体排查收益，不列作 2.3.2 新增功能。The auxiliary diagnostics panel and localized scrolling settings were already present in 2.3.1. They display/copy version, dependency and selected compatibility data, with no specific diagnostic benefit established; they are not new 2.3.2 features. See [开发记录](Docs/Development/简版诊断面板.md).

## [2.3.0] — 2026-09-16 — 同床、调教员身份与分配界面 / Shared beds, trainer roles and assignment UI

将维护者已分阶段确认有效的同床优化、调教员身份、床位标签及相关修复统一归入 2.3.0，包含 2.2.15 及更早版本修复。正式安装包与 SHA-256 校验文件见 [v2.3.0 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.0)。详见 [中英双语发布说明](Docs/Releases/2.3.0/2.3.0发布说明.md) 与 [版本验证](Docs/Releases/2.3.0/2.3.0版本验证.md)。

Groups the maintainer-confirmed shared-bed, trainer-role and bed-assignment changes, including fixes from 2.2.15 and earlier versions. The installation ZIP and SHA-256 checksum are available from [v2.3.0 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.0). See the [bilingual release notes](Docs/Releases/2.3.0/2.3.0发布说明.md) and [validation record (Chinese)](Docs/Releases/2.3.0/2.3.0版本验证.md).

- **同床 / Shared beds：** SSC 性奴恶堕高于 0.1% 时，可与主人及有效指定调教员获得额外许可，不再影响全局恋爱判断。医疗/死眠优先，原版环境检查保留；原版奴隶性奴须先有床伴才能加入空殖民者床。SSC Sex Slaves above 0.1% Corruption may share with both their Master and active Assigned Trainer without changing romance checks. Medical rest, deathrest and native checks retain priority; vanilla slaves require their partner to be assigned first.
- **心情 / Mood：** 保留六档数值，改为实际共同睡眠结束后的一天、不叠加记忆，修复起床结算及定义不匹配红字。Preserves six mood values as one-day, non-stacking memories after actual shared sleep, fixing wake-up and mismatched-definition errors.
- **界面 / UI：** 原版与 Mint 显示三个独立颜色身份标签，位于姓名右侧；空床显示全部身份，分配后按直接关系筛选，悬停说明许可状态及恶堕门槛。Vanilla and Mint show separate role badges to the right of the name, all roles on empty beds, direct relationships on occupied beds, and permission/threshold details on hover.
- **调教员 / Trainers：** 主人固定开启、未选择固定关闭、性奴可选；指派及普通调教入口统一检查。旧档保留已指定性奴资格，停用对象保留指派并标注，修复指定菜单及滚动视图红字。Masters are always trainers, Unset pawns never are, and Sex Slaves can opt in. Assignment and ordinary Training share the check; migration retains existing assigned Sex Slave trainers, inactive assignments remain visible, and menu/scroll-view errors are fixed.
- **绑定与仪式 / Bonds and rituals：** 锁定已绑定角色的身份切换，防止锁链及成长进度丢失；仪式正确解释指定调教员不匹配。Bound identity changes are blocked to preserve Chain progress, and ritual messages correctly explain trainer mismatches.

- **翻译贡献 / Translation contribution：** 从开发者 **Baphomet** 基于 **2.2.15** 的维护版本中吸收 38 条翻译：繁中动态“调教余韵”8 条、五种终极人格塑形配方繁中 15 条及英文 15 条，补齐相应语言显示。感谢 Baphomet 的翻译维护贡献。Incorporates 38 translations from developer **Baphomet**'s maintenance version based on **2.2.15**: 8 Traditional Chinese entries for dynamic training mood memories, plus 15 Traditional Chinese and 15 English entries for the five final personality-shaping recipes. Thanks to Baphomet for these localization contributions.

四语文本及中英文指南同步；旧 RimTalk 和未完成猫/兔入口的暂停状态保留。版本归并与正式发布构建均通过 Release 编译及 390 项回归；Baphomet 翻译通过 XML、重复键、定义引用及占位符静态检查，尚未进行游戏内显示验证。Four-language UI text and Chinese/English guides are updated; legacy RimTalk and unfinished Cat/Rabbit choices remain disabled. The Release build and 390 regression cases passed during both version consolidation and release verification. The Baphomet translation import passed static XML, duplicate-key, definition-reference and placeholder checks; in-game display verification remains pending.

## [2.2.15] — 2026-09-16 — 公交车交易修复与未完成特化入口禁用 / Public Use trade fix and unfinished specialization selection

本版汇总维护者已在游戏内确认有效的公交车交易修复，以及宠物猫、宠物兔入口禁用，包含 `2.2.14` 及更早版本修复。详见 [2.2.15 发布说明](Docs/Releases/2.2.15/2.2.15发布说明.md) 与 [版本验证记录](Docs/Releases/2.2.15/2.2.15版本验证.md)。正式安装包与 SHA-256 校验文件见 [v2.2.15 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.2.15)。

This version groups the maintainer-confirmed Public Use trade fix and disabled Pet Cat / Pet Rabbit selection, including fixes from `2.2.14` and earlier versions. See the [bilingual release notes](Docs/Releases/2.2.15/2.2.15发布说明.md) and [validation record (Chinese)](Docs/Releases/2.2.15/2.2.15版本验证.md). The installation ZIP and SHA-256 checksum are available from [v2.2.15 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.2.15).

### 修复 / Fix

- 修复公交车本人完成商队交易后互动未启动：使用实际 RJW 任务定义，纠正自愿分支对普通女性的能力误判，并检查实际启动结果；失败时清理本次等待或排队任务，避免成功提示误报。原有概率、交易成长和轨道贸易船排除规则保留。
- Fixes interactions failing after a Public Use pawn personally completes a caravan trade. Uses the actual RJW job definitions, corrects the consensual eligibility check for ordinary female pawns, verifies that jobs start, and cleans up event-owned waiting or queued jobs after failure. Existing probabilities, trade growth, and orbital-trade exclusion are retained.

### 界面 / Interface

- 宠物猫、宠物兔选择项置灰并标注“未完成”；旧档中的对应当前方向及终极状态摘要也显示该标记，兔子生育模式改为只读展示。保留既有存档数据和底层实现。
- Pet Cat and Pet Rabbit choices are disabled and marked Incomplete. Existing specialization and final-state summaries show the same marker, and saved rabbit birth modes are read-only. Existing save data and underlying implementations are retained.

打包脚本同步使用已改名的 `README.md`，并包含新增的公交车交易回归套件。The packaging script uses the renamed `README.md` and includes the new BusTrade regression suite.

## [2.2.14] — 2026-09-15 — 当前已发现 bug 修复完成版本 / Known-bug fixes complete

按维护者要求，将 `2.2.14` 标记为“当前已发现 bug 修复完成版本”：截至本版，已发现且确认需要修复的问题已处理完成，作为本阶段修复收尾版本。维护者已确认永久泌乳修复有效；本版包含 `2.2.13` 及更早版本修复。正式安装包与 SHA-256 校验文件见 [v2.2.14 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft-TieJin-Modify/releases/tag/v2.2.14)，详见 [2.2.14 发布说明](Docs/Releases/2.2.14/2.2.14发布说明.md) 与 [版本验证记录](Docs/Releases/2.2.14/2.2.14版本验证.md)。

At the maintainer's request, `2.2.14` marks completion of the current known-bug fixes: issues identified and confirmed as requiring fixes have been addressed as of this release. The maintainer has confirmed the permanent lactation fix. Includes fixes from `2.2.13` and earlier versions. The installation ZIP and SHA-256 checksum are available from [v2.2.14 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft-TieJin-Modify/releases/tag/v2.2.14). See the [bilingual release notes](Docs/Releases/2.2.14/2.2.14发布说明.md) and [validation record (Chinese)](Docs/Releases/2.2.14/2.2.14版本验证.md).

此标记限定于当前已确认的问题范围，不表示未来不会发现新问题。自慰拦截与现有同床行为按维护者决定暂不视为 bug；旧 RimTalk 兼容通过隔离暂停处理，整体重置仍属未来开发。

This milestone covers the currently confirmed issues and does not rule out future discoveries. Masturbation blocking and current shared-bed behavior are not classified as bugs by the maintainer for now. Legacy RimTalk integration has been suspended and isolated; a complete reset remains future work.

### 修复 / Fix

- 修复永久泌乳分批结算漏算时间：累计实际 tick 后统一生产及扣除营养，基础速度不再随组件调用间隔变化；保存未结算进度，满容量及关闭泌乳时不积攒生产时间。
- Fixes lost time in permanent lactation batches. Milk production and nutrition costs now use accumulated ticks, independent of component update intervals. Pending progress is saved, and time is discarded while full or disabled.
- 食物充足、从空开始且无额外产量或挤奶时，默认约 6 个游戏小时充满；接近满容量时只为实际存入的奶量扣营养。保留旧存档现有奶量，不追补历史漏算产量；HumanCattle 接管的生产流程不受本修复影响。
- With sufficient nutrition, an empty reservoir fills in about six in-game hours under default settings, without extra production or milking. Nutrition costs are capped to the amount actually stored. Existing milk is retained without backfilling past losses; HumanCattle-controlled production is unaffected.

### 兼容状态 / Compatibility status

- 暂停并归档旧 RimTalk 兼容，移除运行入口和相关设置控件，等待整个模块重新设计。保留调教排班和旧设置数据。
- Suspends and archives the legacy RimTalk integration pending a complete redesign. Runtime hooks and active settings controls are removed; Training schedules and legacy preferences are retained.

未来的 RimTalk 兼容重置、性奴主动自慰与主动和他人性爱的保护拆分、主奴同床优化调整和可视化，记录在 [开发规划](Docs/Design/未来内容开发规划.md)，不属于本版已实现功能。

The RimTalk integration reset, separate protections for slave-initiated masturbation and sex with others, and shared-bed improvements and visualization remain [future development plans (Chinese)](Docs/Design/未来内容开发规划.md).

## [2.2.13] — 2026-09-15 — 人格植入、手术记忆与仪式动画修复 / Implantation, surgery memory, and ritual animation fixes

维护者已分别确认以下四项修复有效，统一归入 `2.2.13`。正式安装包与 SHA-256 校验文件见 [v2.2.13 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft-TieJin-Modify/releases/tag/v2.2.13)。构建与验证记录见 [2.2.13 版本验证](Docs/Releases/2.2.13/2.2.13版本验证.md)，发布说明见 [2.2.13 发布说明](Docs/Releases/2.2.13/2.2.13发布说明.md)。

The maintainer has confirmed each of the following four fixes. They are grouped into `2.2.13`, now available from [v2.2.13 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft-TieJin-Modify/releases/tag/v2.2.13) with the installation ZIP and SHA-256 checksum. See the [validation record (Chinese)](Docs/Releases/2.2.13/2.2.13版本验证.md) and [bilingual release notes](Docs/Releases/2.2.13/2.2.13发布说明.md).

### 1. 人格植入接触检查 / Contact checks during Personality Gel implantation

- 植入的 600 tick 操作期间让目标等待并持续检查接触，保留原有姿势和睡眠；目标走远或无法接触时中断。
- 最终写入人格和消耗凝胶前，再检查双方同图、接触及目标仍存活且为空壳，避免隔空完成。
- Keeps the target waiting during the 600-tick procedure while preserving posture and sleep. Losing contact interrupts implantation.
- Rechecks contact, shared map, and a living Hollow before transferring personality data or consuming the gel, preventing completion at a distance.

### 2. 性别重置术后记忆 / Memory after gender reassignment surgery

- 删除同一术后记忆中重复的定义名称，使 XML、代码与译文使用同一个名称，恢复正常记忆添加。
- 缺失定义时保留诊断并跳过添加，避免手术收尾空引用；保留原有 15 天、基础心情 −10 及一层堆叠限制。
- Removes the duplicate memory definition name and aligns XML, code, and translations so the memory can be added normally.
- Missing definitions retain their diagnostic and safely skip memory addition. The existing 15-day duration, −10 base mood effect, and one-stack limit remain.

### 3. 仪式 UAP 位置锁兼容 / UAP position lock compatibility during rituals

- 在新阶段开始前及已启动阶段收尾时释放双方旧位置锁，避免 UAP 的旧任务检查误停新阶段动画。
- 迟到的旧阶段回调不释放新任务的位置锁；未安装 UAP 时安全跳过。
- Releases participant position locks before a new stage starts and when a started stage finishes, preventing stale UAP job checks from stopping the new animation.
- Late callbacks from an old stage preserve the new job's locks. UAP remains optional.

### 4. 仪式备用动画查找 / Ritual fallback animation lookup

- 从动画框架对应的定义库读取候选，修复有可用动画却因读取 `DefDatabase<Def>` 而找不到的问题。
- 沿用框架的适用性检查、角色顺序和原有备用选择方式。确实没有匹配资源时仍提示并保留原有仪式计时。
- Reads candidates from the animation framework's own definition database, fixing fallback lookup failing despite available animations.
- Retains framework compatibility checks, participant order, and existing fallback selection. Truly missing matches still produce the existing warning and retain ritual timing.

兼容说明 / Compatibility：不补发过去漏掉的手术记忆，也不恢复旧失败存档中已被清空的动画队列；动画仍取决于已安装的框架及适用资源。Does not retroactively grant missed surgery memories or reconstruct animation queues already cleared in old failure saves. Animation playback still depends on installed frameworks and compatible assets.

本版包含 `2.2.12` 及更早版本修复。Includes fixes from `2.2.12` and earlier versions.

## [2.2.12] — 2026-09-14 — 恶堕衰减翻译与行为保护修复 / Corruption decay localization and interaction protection fixes

维护者验证后确认本轮修复有效，将以下两项修复统一归入 `2.2.12`。详细构建与验证记录见 [2.2.12 版本验证](Docs/Releases/2.2.12/2.2.12版本验证.md)。

The maintainer confirmed these fixes after testing. Version `2.2.12` groups the following two fixes; see the [version validation record (Chinese)](Docs/Releases/2.2.12/2.2.12版本验证.md) for build and validation details.

**正式发布 / Released：安装包与 SHA-256 校验文件见 [v2.2.12 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft-TieJin-Modify/releases/tag/v2.2.12)。Download the installation ZIP and checksum from the release page.**

### 1. 恶堕衰减设置运行时翻译 / Runtime translations for Corruption decay settings

- 在实际加载的根目录 `Languages` 中补齐简中、繁中、英文和俄文的四个设置键，修复开关、每日衰减量及说明显示原始键名的问题。
- 本项仅补齐界面文本，不改变衰减公式、默认值或设置保存方式。
- Adds all four setting keys to the runtime `Languages` folders for Simplified Chinese, Traditional Chinese, English, and Russian, fixing raw keys shown for the toggle, daily decay amount, and descriptions.
- This change only supplies UI text; decay calculations, defaults, and saved settings remain the same.

### 2. 行为开始阶段的主从保护 / Owner protection when interactions start

- 修复开始阶段保护补丁未注册的问题，阻止非主人通过加入已经开始的行为绕过限制；拒绝加入者时保留原双方的现有任务。
- 首次发起提前到接收任务创建前校验，避免创建接收任务后才拒绝产生的预约黄字。玩家强制指派同样需要通过保护检查。
- 保留合法主人、他人训练许可及设置例外，补充未开始场景的收尾保护；SSC 训练和仪式在 `Start()` 被拒绝后不再继续开始通知或推进状态。
- Registers the missing start guard and prevents non-owners from bypassing protection by joining an existing interaction, while preserving the original participants' jobs.
- Checks new attempts before creating a receiver job, avoiding the reservation warning caused by rejecting that receiver too late. Player-forced jobs are also checked.
- Preserves owner access, permitted training, and setting exceptions; guards cleanup for rejected scenes and stops SSC training and ritual code from continuing start notifications or state changes after a rejected `Start()`.

验证 / Validation：八个回归套件共 173 项；新增 27 项保护用例另以真实 Harmony 注册运行。维护者确认修复有效，未提供按语言、场景或模组组合逐项列出的实测结果。Eight regression suites contain 173 checks, with the 27 new protection cases additionally run through real Harmony patch registration. The maintainer confirmed the fixes work without providing individual results for each language, scenario, or mod combination.

技术说明 / Technical details：[行为开始保护修复](Docs/Development/行为开始保护修复.md)。保留 `2.2.11` 及更早版本修复。Includes the fixes from `2.2.11` and earlier versions.

## [2.2.11] — 2026-09-14 — 人格、语言与种族分页修复合集 / Personality, localization, and race inspection tab fixes

**正式发布：本版汇总人格、特化、语言与种族检查分页修复。维护者确认原三组修复有效，并反馈合并后目前未发现问题，同意按当前范围发布。安装包与校验文件见 [v2.2.11 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft-TieJin-Modify/releases/tag/v2.2.11)。**

**Released: this version combines personality, specialization, localization, and race inspection tab fixes. The maintainer confirmed the original three fix groups, reported no issues with the merged version, and approved this release. Download the installation ZIP and checksum from [v2.2.11 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft-TieJin-Modify/releases/tag/v2.2.11).**

### 1. 人格普通特质恢复及凝胶界面 / Personality traits and gel UI

- 植入以凝胶中的普通特质替换接收身体的普通特质，同体回填也恢复提取时快照；保留宿主基因及其授予特质，性奴特质继续按历史最高恶堕重建。
- 凝胶卡片拆分头部与固定操作区，技能和特质独立滚动，长特质名称换行；修复多特质排版及中、英、俄翻译同步问题。
- Implantation replaces ordinary traits from the saved snapshot, including same-body restoration, while retaining the host's genes and gene-granted traits. The Sex Slave trait is still rebuilt from highest-ever Corruption.
- The gel card separates its header and fixed action area, scrolls skills and traits independently, wraps long trait names, and corrects layout and localization in Chinese, English, and Russian.

详细说明 / Details：[人格普通特质迁移修复](Docs/Development/人格普通特质迁移修复.md) · [人格凝胶界面布局调整](Docs/Development/人格凝胶界面布局调整.md)。

### 2. 特化历史与记忆数值迁移 / Specialization history and memory values

- 凝胶保存所有方向的特化历史，植入时整体替换接收身体的进度，避免丢失源历史或混入宿主训练记录。
- 保存并恢复记忆的实际好感、阶段和原生实例字段，保留手动设置的数值，包括零和小数。
- Gels carry specialization history for every direction and replace the receiving body's history during implantation.
- Memory snapshots preserve actual opinion values, stages, and native instance fields, including custom zero and fractional values.

详细说明 / Details：[人格特化历史与记忆数值迁移修复](Docs/Development/人格特化历史与记忆数值迁移修复.md)。

### 3. 特化完成显示与永久泌乳授予 / Specialization completion display and permanent lactation

- 修正一个方向终极化后，调教面板把其他未完成方向也显示为“已完成”的问题。
- 胸部改造达到 100% 后也会自动补授予永久泌乳，覆盖直接升满和满级旧档缺失状态的情况。
- The training tab checks completion for the selected specialization instead of treating every direction as complete when any one is finalized.
- Breast development at 100% also grants missing permanent lactation, covering direct jumps to the maximum and existing saves missing the state.

兼容说明 / Compatibility：旧凝胶继续按已有数据及默认值恢复，无法追溯补回此前未保存的历史或自定义数值；旧特质快照无法辨认基因来源。Older gels retain their saved data and compatible defaults; previously omitted history or custom values cannot be reconstructed, and legacy trait snapshots cannot identify gene sources.

原三组修复验证 / Validation of the original three fix groups：六个自动回归套件共 138 项；上述三组修复的游戏内有效性已由维护者确认，未提供逐项场景清单。Six regression suites contain 138 checks; the maintainer confirmed the original three fix groups in-game without providing an individual scenario checklist. 最终发布的七套件 146 项检查与构建记录 / Final release validation with seven suites and 146 checks：[2.2.11 发布验证](Docs/Releases/2.2.11/2.2.11发布验证.md)。

### 4. 语言与种族检查分页 / Localization and race inspection tabs

- 从第三方 2.2.10 CombinedFix 选择性移植语言与种族分页修复，保留本版已有人格、特化和仪式修复。
- 修正英文 14 条 DefInjected 路径，并移除已不存在的 `SSC_PSEdit_Bus` 的 3 条翻译。
- 新增繁中 54 个 XML，补齐本版新增的 10 个凝胶界面键；清理重复设置键，避免同一键存在多份不同文本。
- 种族分页注入保留既有检查分页，仅补加 SSC 分页；不再清空已解析分页或重跑 `ResolveReferences()`，避免新角色原分页丢失和 HAR 重复解析。
- Selectively ported the localization and race inspection tab fixes from the third-party 2.2.10 CombinedFix package, preserving this version's personality, specialization, and ritual fixes.
- Corrected 14 English DefInjected paths and removed three translations for the deleted `SSC_PSEdit_Bus` Def.
- Added 54 Traditional Chinese XML files and the ten gel UI keys introduced in this version; removed duplicate settings keys to avoid conflicting text definitions.
- Race tab injection retains existing inspection tabs and adds the SSC tab only when missing. It no longer clears resolved tabs or reruns `ResolveReferences()`, addressing missing tabs on new pawns and repeated HAR resolution.

本次暂缓 RimTalk 及渲染修复。新增修复的构建、静态检查与游戏实测状态见 [语言与种族分页合并记录](Docs/Development/2.2.11语言与种族分页合并.md)；原三组修复的验证结果不代表新增兼容性场景已通过。RimTalk and rendering changes are deferred. Build, static-check, and in-game validation status for this addition is tracked in the [merge record (Chinese)](Docs/Development/2.2.11语言与种族分页合并.md); previous validation does not establish that the new compatibility scenarios pass.

## [2.2.10] — 2026-09-13 — 绑定仪式中断热修复 / Binding Ritual interruption hotfix

**待发布：此日期为准备日期。维护者已于 2026-09-13 确认游戏内验证完成、修复有效，候选安装包尚未正式发布。**

**Pending release: this is the preparation date. On 2026-09-13, the maintainer confirmed that in-game validation was complete and the fix was effective. The candidate package has not been published as a release.**

### 中文

- 修复绑定仪式取消或关键参与者退出后，目标一直无法接受日常调教的问题。
- 新仪式从第一阶段开始；同一仪式的阶段切换和读档会保留进度。
- 自动清理旧存档中已失效仪式的残留占用，保留指定调教师、成长进度和原有日常冷却。
- 每场仪式单独判断完成资格，避免重复结算和同时举行的仪式互相干扰。
- 本次仅交付仪式生命周期热修复，保留 2.2.9 的特化与锁链阶段修复。

### English

- Fixed targets remaining unavailable for daily training after a Binding Ritual was cancelled or a required participant left.
- New rituals start at the first phase; phase changes and save/load within the same active ritual preserve its progress.
- Automatically clears stale locks from ended rituals in older saves while preserving the assigned trainer, progression, and existing daily training cooldown.
- Tracks completion separately for each ritual to prevent duplicate outcomes and interference between simultaneous rituals.
- This hotfix focuses on the ritual lifecycle and retains the specialization and Chain progression fixes from 2.2.9.

验证 / Validation：生命周期测试 31/31、阶段进度测试 29/29 通过；游戏内验证由维护者确认通过。Lifecycle tests: 31/31; progression tests: 29/29. In-game validation was confirmed successful by the maintainer.

详细说明 / Details：[绑定仪式中断修复 / Fix details (Chinese)](Docs/Development/绑定仪式中断修复.md) · [发布验证记录 / Release validation (Chinese)](Docs/Releases/2.2.10/2.2.10发布验证.md)。

## [2.2.9] — 2026-09-12 — 特化进度与仪式阶段修复 / Specialization and ritual progression fixes

本次接续更新合并性奴特化系统修复与仪式阶段修复，适用于 RimWorld 1.6。

This continuation update combines specialization and ritual progression fixes for RimWorld 1.6.

### 中文：性奴特化修复

- 切换特化方向时，各方向的进度独立保存，切回后恢复，不再因切换而清零。
- 已完成的终极化状态会保留，切换方向或选择“无”不会再将其删除。
- 修复奶牛特化切换后储奶仓充能丢失的问题。
- 修复公交车特化中，性交、交易与调教获得的进度互相覆盖的问题。
- 修复人格凝胶注入后，已有特化状态与调教面板不同步、无法继续训练的问题；旧存档中可识别的特化状态也会自动同步。

### English: Specialization fixes

- Saves progress separately for each specialization and restores it when switching back, instead of resetting it on selection changes.
- Preserves completed final specialization states when switching directions or selecting “None.”
- Fixed stored milk charge being lost when switching away from Cow Specialization.
- Fixed Public Use Specialization progress from sex, trading, and training overwriting one another.
- Fixed specialization states becoming out of sync with the Training tab after Personality Gel implantation and blocking further training. Recognizable states in older saves are also synchronized automatically.

### 中文：仪式阶段修复

- 修复仪式结束后，恶堕率不足导致高阶段性奴锁链直接清零的问题。
- 仪式新增的锁链进度现在受当前恶堕率限制；重复仪式不会降低已有锁链进度。
- 自然衰减导致退阶时，按实际阶段退一级，避免高阶段异常跳回初始状态。
- 修复装备维持底线尚未生效就触发退阶的问题；每日基础衰减设为 0 时，不再触发自然退阶。

### English: Ritual progression fixes

- Fixed advanced Sex Slave Chain stages resetting to zero after a ritual when Corruption was below the required threshold.
- Caps new Chain progress from rituals at current Corruption; repeating a ritual no longer reduces existing Chain progress.
- Natural regression now drops one actual stage at a time instead of unexpectedly returning an advanced stage to its initial state.
- Applies equipment maintenance floors before checking for regression. Setting base daily decay to zero no longer triggers natural regression.

### 中文：界面调整

- 已终极化的特化方向会显示“已终极化”，并禁止重复选择。
- 宠物猫、宠物兔特化标注为“未完成”，以说明当前实装状态。

### English: Interface changes

- Completed final specializations are labelled as finalized and cannot be selected again.
- Pet Cat and Pet Rabbit specializations are labelled “Unfinished” to reflect their implementation status.

## [2.2.8] — 上游基线 / Upstream baseline

原模组 7 月停更时的版本，本项目以此为基础继续开发。

The upstream version when development stopped in July; this project continues from that baseline.
