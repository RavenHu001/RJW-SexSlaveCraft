# 更新日志 / Changelog — SexSlaveCraft（TieJin 接续版 / TieJin continuation）

本项目基于原模组 7 月停更时的 **2.2.8** 版本继续维护，沿用原有版本号。更新按版本从新到旧排列，技术细节见 [开发更新记录](Docs/开发更新记录.md)。

This continuation builds on upstream **2.2.8**, the baseline when upstream development stopped in July, and retains its version numbering. Entries are listed newest first. Technical details are in the [development log (Chinese)](Docs/开发更新记录.md).

## [2.2.11] — 2026-09-13 — 人格、语言与种族分页修复合集 / Personality, localization, and race inspection tab fixes

**版本归档与候选包准备：将 2.2.10 之后的修复统一归入本版，尚未正式发布。原三组人格与特化修复已由维护者确认有效；新增语言与种族分页修复的验证单独记录。**

**Release grouping and candidate preparation: this version combines the fixes made after 2.2.10 and has not been formally released. The maintainer confirmed the original three personality and specialization fix groups in-game; validation of the added localization and race inspection tab fixes is recorded separately.**

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

原三组修复验证 / Validation of the original three fix groups：六个自动回归套件共 138 项；上述三组修复的游戏内有效性已由维护者确认，未提供逐项场景清单。Six regression suites contain 138 checks; the maintainer confirmed the original three fix groups in-game without providing an individual scenario checklist. 原候选包与构建记录 / Original candidate and build record：[2.2.11 发布验证](Docs/2.2.11发布验证.md)。

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
