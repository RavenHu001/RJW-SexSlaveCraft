# 更新日志 / Changelog — SexSlaveCraft（TieJin 接续版 / TieJin continuation）

本项目基于原模组 7 月停更时的 **2.2.8** 版本继续维护，沿用原有版本号。更新按版本从新到旧排列，技术细节见 [开发更新记录](Docs/Development/开发更新记录.md)。

This continuation builds on upstream **2.2.8**, the baseline when upstream development stopped in July, and retains its version numbering. Entries are listed newest first. Technical details are in the [development log (Chinese)](Docs/Development/开发更新记录.md).

## [2.3.2] — 待发布 / Unreleased — 训导官特化与调教稳定性 / Training Officers and Training reliability

归纳 2.3.1 发布后截至 `05170b1` 的全部已实现更新，包含最新属性加成与四语文案。**本次仅整理文档，尚未更新运行版本元数据、打包或发布。** 详见 [2.3.2 中英更新说明](Docs/Releases/2.3.2/2.3.2发布说明.md)与[整理及验证状态](Docs/Releases/2.3.2/2.3.2版本验证.md)。

Covers implemented changes since released 2.3.1 through `05170b1`, including the latest bonuses and localization. **Documentation only: runtime version metadata, packaging and publication remain pending.** See the [bilingual notes](Docs/Releases/2.3.2/2.3.2发布说明.md) and [validation status](Docs/Releases/2.3.2/2.3.2版本验证.md).

- **训导官与任职 / Training Officers:** 新增代主人调教其他性奴的职能特化。SSC 性奴须为自由殖民者、已绑定实际主人且锁链达到第 3 阶段；当前普通进度达到 20% 或拥有有效终极状态，才可手动开启调教员。主人固定资格保持。Adds a specialization for performing Training on the owner's behalf. An SSC Sex Slave must be a bonded free colonist at Chain stage 3 or higher, with 20% current progress or an active final state, to opt into trainer duty. Masters retain their fixed eligibility.
- **培养与终极化 / Progression and finalization:** 新增成本 1200 的研究及工作量 3500 的雕刻台人格凝胶配方。成功接受调教、施教、主动双人行为分别增加 5、2.5、1 个百分点；主动行为包括规则允许的强制行为。普通完成不会自动终极化，重复与中断结算不发放经验。Adds research costing 1,200 and a personality-gel finalization recipe requiring 3,500 work. Completed Training received, Training performed and initiated pair activity grant 5, 2.5 and 1 percentage points respectively, including permitted forced initiation. Full ordinary progress requires manual finalization; duplicate or interrupted completion does not grant rewards.
- **属性加成 / Attribute bonuses:** 20%／50%／有效终极依次提供崩溃临界值 −3／−6／−10 个百分点、压制能力 +10／+20／+30 个百分点、教化能力 ×1.05／×1.10／×1.15；阶段不叠加，后两项要求 Ideology。At 20%, 50% and active finalization, grants mental break threshold offsets of −3/−6/−10 percentage points, suppression power offsets of +10/+20/+30 points, and conversion power factors of ×1.05/×1.10/×1.15. Stages do not stack; the latter two stats require Ideology.
- **资格生命周期与限制 / Eligibility and permissions:** 普通失格保存进度后退出培养，终极失格保留禁用记录，条件恢复后重新生效。选中有效训导官方向即提供两项主动行为强制允许，无须等待 20% 或开启任职；沿用全局开关、装备优先和对方许可。人格凝胶传承进度与完成记录，恢复结束后按接收身体条件维护。Losing eligibility archives ordinary progress or disables a retained final record. The two initiation overrides apply from selecting an eligible direction, independent of the trainer toggle and 20% threshold, while respecting global switches, equipment and recipient rules. Gel transfer retains source progress/final records and evaluates the recipient after restoration.
- **升级提示 / Upgrade note:** **旧档性奴调教员开关一次性关闭**；绑定、指派与进度保留，取得新资格后须手动重新开启，重复读档不重置新选择。**Old Sex Slave trainer toggles reset once.** Bonds, assignments and progress remain; obtain eligibility and enable duty again. Later loads preserve new choices.
- **仪式选人 / Ritual selection:** 两个执行槽改为手动选人，浏览候选使用轻量检查，实际选入和开始时验证。减少重复 RJW 查询，失败恢复分配与品质预览；修复延迟点击、拖拽、空角色及头像红字，保留合法替换和原版观众分配。Uses manual performer selection, lightweight browsing and validation on selection/start. Reduces repeated RJW checks, restores assignments/previews on rejection, and fixes delayed-click, drag, null-participant and portrait errors while preserving valid replacements and native spectators.
- **连续任务与预约 / Consecutive jobs and reservations:** 修复第二目标工作显示调教却停在原地的问题，统一 Touch 接近、清理遗留 UAP 位置锁并对停止寻路有限重试；仅释放当前 Job 持有的预约，消除重复释放红字。Fixes trainers stuck at the previous target through Touch approach, stale UAP lock cleanup and bounded path recovery. Releases only reservations owned by the initiating job, preventing repeated-release errors.
- **Education 兼容 / Education compatibility:** 自动调教避让活动课程中的学生和教师，途中或执行中入课也会退出，下课后恢复候选。兼容可选，手动强制命令及教育自身规则保持原行为。Automatic Training yields to active students and teachers, including class entry during travel/execution, and resumes eligibility after class. Education remains optional; manual forced orders and education-side rules retain their behavior.
- **界面与凝胶保护 / UI and gel safeguards:** 修复终极化后“未选择”的状态认领和显示回退，保留终极效果与历史；消耗前拒绝异常凝胶并保留账单次数；统一完成容差，优化长译文布局，补齐简中、繁中、英文、俄文研究、配方、阶段和状态提示。Preserves and correctly displays Unset after finalization without removing benefits/history; rejects invalid gels before consumption or bill counting; unifies completion tolerance, fits long text and completes four-language research, recipe, stage and status text.
- **维护与验证 / Maintenance and validation:** 减少同次资格查询，复用现有低频和状态事件；统一回归由 16 套扩为 18 套，补充仪式选人、人格恢复、真实任务交接和界面回归。历史全量 780/780 及后续专项通过、实机确认均按批次记录；最新身份专项为 85/87，两处旧数量/文案断言待同步，尚无本版发布预检结论。Reduces repeated queries and extends the existing runner from 16 to 18 suites. Historical 780/780 and later targeted/in-game results are recorded by development batch. The latest identity check is 85/87 due to two outdated count/text assertions; release validation is pending.

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
