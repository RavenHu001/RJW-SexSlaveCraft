# 更新日志 / Changelog — SexSlaveCraft（TieJin 接续版 / TieJin continuation）

本项目基于原模组 7 月停更时的 **2.2.8** 版本继续维护，沿用原有版本号。更新按版本从新到旧排列，技术细节见 [开发更新记录](Docs/开发更新记录.md)。

This continuation builds on upstream **2.2.8**, the baseline when upstream development stopped in July, and retains its version numbering. Entries are listed newest first. Technical details are in the [development log (Chinese)](Docs/开发更新记录.md).

## [未发布 / Unreleased] — 人格特化历史与记忆数值 / Personality specialization history and memory values

- 人格凝胶现在保存所有方向的特化历史，植入时整体替换接收身体的进度，避免丢失源历史或混入宿主训练记录。
- 保存并恢复记忆的实际好感、阶段和原生实例字段，保留手动设置的数值，包括零和小数。
- 旧凝胶继续按已保存数据恢复；此前未保存的历史和自定义数值无法追溯补回。
- Personality gels now carry specialization history for every direction and replace the receiving body's history during implantation.
- Memory snapshots preserve actual opinion values, stages, and native instance fields, including custom zero and fractional values.
- Older gels retain compatible defaults; data never stored in an older snapshot cannot be recovered retroactively.

**状态：修复完成。自动验证通过，维护者已确认实际游玩修复有效，并同意提交及上传。版本保持 `2.2.10`。**

**Status: fixed. Automated checks passed, and the maintainer confirmed the fixes work in-game and approved committing and pushing them. Version remains `2.2.10`.**

详细说明 / Details：[人格特化历史与记忆数值迁移修复](Docs/人格特化历史与记忆数值迁移修复.md)。

## [未发布 / Unreleased] — 人格凝胶界面调整 / Personality gel UI layout

- 人格卡片采用独立头部、双栏内容及固定底部操作区，解决顶部文字重叠；技能和特质分别滚动，多特质不会挤出隶属信息或分配按钮。
- 特质标题显示数量，长名称自动换行；补全姓名提示并明确锁链严重度，支持中、英、俄翻译。
- The personality card now has a separate header, independently scrolling skill and trait lists, and a fixed action area. Long trait lists keep bond information and assignment controls visible.
- Trait names wrap, the heading shows their count, and full-name tooltips and a labeled chain severity improve readability in Chinese, English, and Russian.

**状态：修复完成。维护者已确认当前界面及翻译实际游玩无问题，本轮人格凝胶相关修复结束。版本号保持 `2.2.10`。**

**Status: fixed. The maintainer has confirmed that the current UI and translations work correctly in-game, completing this round of personality gel fixes. The version remains `2.2.10`.**

详细说明 / Details：[人格凝胶界面布局调整](Docs/人格凝胶界面布局调整.md)。

## [未发布 / Unreleased] — 人格特质迁移修复 / Personality trait transfer fix

### 中文

- 人格植入现在以凝胶中的普通特质完整替换接收身体的普通特质；植回原身体也恢复提取时的快照。
- 新凝胶只保存非基因来源特质，包括暂时受到基因抑制的普通特质。接收身体的基因及其授予特质保留，性奴特质仍由历史最高恶堕重建。
- 旧凝胶继续按已保存的条目恢复人格特质；旧格式无法辨认原身体的基因来源。缺失特质快照时拒绝植入并保留凝胶，合法空快照则清除接收身体的普通特质。

### English

- Implantation now replaces the receiving body's ordinary traits with the gel's saved snapshot, including when returning a personality to its original body.
- New gels save only traits without a gene source, including temporarily gene-suppressed ordinary traits. The receiving body's genes and their traits remain intact; the Sex Slave trait is still rebuilt from highest-ever Corruption.
- Older gels restore their saved entries as personality traits because the legacy format cannot identify the original gene sources. Missing trait snapshots reject implantation and preserve the gel; valid empty snapshots clear the receiving body's ordinary traits.

**状态：修复完成。普通特质恢复此前已确认有效；后续凝胶界面与翻译问题现已处理，并经维护者确认无问题，因此将本轮状态由“部分修复”更新为“修复完成”。版本号保持 `2.2.10`。**

**Status: fixed. Trait restoration was previously confirmed effective. The follow-up gel UI and translation issues have now been addressed and confirmed by the maintainer, so this round is upgraded from partially fixed to fixed. The version remains `2.2.10`.**

详细说明 / Details：[人格普通特质迁移修复 / Fix details (Chinese)](Docs/人格普通特质迁移修复.md)。

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
