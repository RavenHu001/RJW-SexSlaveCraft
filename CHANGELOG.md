# 更新日志 / Changelog — SexSlaveCraft（TieJin 接续版 / TieJin continuation）

本项目基于原模组 7 月停更时的 **2.2.8** 版本继续维护，沿用原有版本号。更新按版本从新到旧排列，技术细节见 [开发更新记录](Docs/开发更新记录.md)。

This continuation builds on upstream **2.2.8**, the baseline when upstream development stopped in July, and retains its version numbering. Entries are listed newest first. Technical details are in the [development log (Chinese)](Docs/开发更新记录.md).

## [未发布 / Unreleased]

- 暂停并归档旧 RimTalk 兼容，移除运行入口和相关设置控件，等待整个模块重新设计。保留调教排班和旧设置数据。
- Suspends and archives the legacy RimTalk integration pending a complete redesign. Runtime hooks and active settings controls are removed; Training schedules and legacy preferences are retained.

## [2.2.13] — 2026-09-15 — 人格植入、手术记忆与仪式动画修复 / Implantation, surgery memory, and ritual animation fixes

维护者已分别确认以下四项修复有效，统一归入 `2.2.13`。正式安装包与 SHA-256 校验文件见 [v2.2.13 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft-TieJin-Modify/releases/tag/v2.2.13)。构建与验证记录见 [2.2.13 版本验证](Docs/2.2.13版本验证.md)，发布说明见 [2.2.13 发布说明](Docs/2.2.13发布说明.md)。

The maintainer has confirmed each of the following four fixes. They are grouped into `2.2.13`, now available from [v2.2.13 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft-TieJin-Modify/releases/tag/v2.2.13) with the installation ZIP and SHA-256 checksum. See the [validation record (Chinese)](Docs/2.2.13版本验证.md) and [bilingual release notes](Docs/2.2.13发布说明.md).

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

维护者验证后确认本轮修复有效，将以下两项修复统一归入 `2.2.12`。详细构建与验证记录见 [2.2.12 版本验证](Docs/2.2.12版本验证.md)。

The maintainer confirmed these fixes after testing. Version `2.2.12` groups the following two fixes; see the [version validation record (Chinese)](Docs/2.2.12版本验证.md) for build and validation details.

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

技术说明 / Technical details：[行为开始保护修复](Docs/行为开始保护修复.md)。保留 `2.2.11` 及更早版本修复。Includes the fixes from `2.2.11` and earlier versions.

## [2.2.11] — 2026-09-14 — 人格、语言与种族分页修复合集 / Personality, localization, and race inspection tab fixes

**正式发布：本版汇总人格、特化、语言与种族检查分页修复。维护者确认原三组修复有效，并反馈合并后目前未发现问题，同意按当前范围发布。安装包与校验文件见 [v2.2.11 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft-TieJin-Modify/releases/tag/v2.2.11)。**

**Released: this version combines personality, specialization, localization, and race inspection tab fixes. The maintainer confirmed the original three fix groups, reported no issues with the merged version, and approved this release. Download the installation ZIP and checksum from [v2.2.11 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft-TieJin-Modify/releases/tag/v2.2.11).**

### 1. 人格普通特质恢复及凝胶界面 / Personality traits and gel UI

- 植入以凝胶中的普通特质替换接收身体的普通特质，同体回填也恢复提取时快照；保留宿主基因及其授予特质，性奴特质继续按历史最高恶堕重建。
- 凝胶卡片拆分头部与固定操作区，技能和特质独立滚动，长特质名称换行；修复多特质排版及中、英、俄翻译同步问题。
- Implantation replaces ordinary traits from the saved snapshot, including same-body restoration, while retaining the host's genes and gene-granted traits. The Sex Slave trait is still rebuilt from highest-ever Corruption.
- The gel card separates its header and fixed action area, scrolls skills and traits independently, wraps long trait names, and corrects layout and localization in Chinese, English, and Russian.

详细说明 / Details：[人格普通特质迁移修复](Docs/人格普通特质迁移修复.md) · [人格凝胶界面布局调整](Docs/人格凝胶界面布局调整.md)。

### 2. 特化历史与记忆数值迁移 / Specialization history and memory values

- 凝胶保存所有方向的特化历史，植入时整体替换接收身体的进度，避免丢失源历史或混入宿主训练记录。
- 保存并恢复记忆的实际好感、阶段和原生实例字段，保留手动设置的数值，包括零和小数。
- Gels carry specialization history for every direction and replace the receiving body's history during implantation.
- Memory snapshots preserve actual opinion values, stages, and native instance fields, including custom zero and fractional values.

详细说明 / Details：[人格特化历史与记忆数值迁移修复](Docs/人格特化历史与记忆数值迁移修复.md)。

### 3. 特化完成显示与永久泌乳授予 / Specialization completion display and permanent lactation

- 修正一个方向终极化后，调教面板把其他未完成方向也显示为“已完成”的问题。
- 胸部改造达到 100% 后也会自动补授予永久泌乳，覆盖直接升满和满级旧档缺失状态的情况。
- The training tab checks completion for the selected specialization instead of treating every direction as complete when any one is finalized.
- Breast development at 100% also grants missing permanent lactation, covering direct jumps to the maximum and existing saves missing the state.

兼容说明 / Compatibility：旧凝胶继续按已有数据及默认值恢复，无法追溯补回此前未保存的历史或自定义数值；旧特质快照无法辨认基因来源。Older gels retain their saved data and compatible defaults; previously omitted history or custom values cannot be reconstructed, and legacy trait snapshots cannot identify gene sources.

原三组修复验证 / Validation of the original three fix groups：六个自动回归套件共 138 项；上述三组修复的游戏内有效性已由维护者确认，未提供逐项场景清单。Six regression suites contain 138 checks; the maintainer confirmed the original three fix groups in-game without providing an individual scenario checklist. 最终发布的七套件 146 项检查与构建记录 / Final release validation with seven suites and 146 checks：[2.2.11 发布验证](Docs/2.2.11发布验证.md)。

### 4. 语言与种族检查分页 / Localization and race inspection tabs

- 从第三方 2.2.10 CombinedFix 选择性移植语言与种族分页修复，保留本版已有人格、特化和仪式修复。
- 修正英文 14 条 DefInjected 路径，并移除已不存在的 `SSC_PSEdit_Bus` 的 3 条翻译。
- 新增繁中 54 个 XML，补齐本版新增的 10 个凝胶界面键；清理重复设置键，避免同一键存在多份不同文本。
- 种族分页注入保留既有检查分页，仅补加 SSC 分页；不再清空已解析分页或重跑 `ResolveReferences()`，避免新角色原分页丢失和 HAR 重复解析。
- Selectively ported the localization and race inspection tab fixes from the third-party 2.2.10 CombinedFix package, preserving this version's personality, specialization, and ritual fixes.
- Corrected 14 English DefInjected paths and removed three translations for the deleted `SSC_PSEdit_Bus` Def.
- Added 54 Traditional Chinese XML files and the ten gel UI keys introduced in this version; removed duplicate settings keys to avoid conflicting text definitions.
- Race tab injection retains existing inspection tabs and adds the SSC tab only when missing. It no longer clears resolved tabs or reruns `ResolveReferences()`, addressing missing tabs on new pawns and repeated HAR resolution.

本次暂缓 RimTalk 及渲染修复。新增修复的构建、静态检查与游戏实测状态见 [语言与种族分页合并记录](Docs/2.2.11语言与种族分页合并.md)；原三组修复的验证结果不代表新增兼容性场景已通过。RimTalk and rendering changes are deferred. Build, static-check, and in-game validation status for this addition is tracked in the [merge record (Chinese)](Docs/2.2.11语言与种族分页合并.md); previous validation does not establish that the new compatibility scenarios pass.

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

详细说明 / Details：[绑定仪式中断修复 / Fix details (Chinese)](Docs/绑定仪式中断修复.md) · [发布验证记录 / Release validation (Chinese)](Docs/2.2.10发布验证.md)。

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
