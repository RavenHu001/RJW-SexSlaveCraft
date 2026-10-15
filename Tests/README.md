# 测试说明索引

[文档总索引](../Docs/README.md) · [发布与打包](../Docs/Maintenance/发布与打包.md) · [开发记录](../Docs/Development/README.md)

当前仓库以 **2.4.0.5 开发里程碑**为基线，`Scripts/Test-All.ps1` 登记 **30 套**。PNA 基础新增 `PnaLoad`，第五步新增 `ForcedSexProps`；该里程碑最近 PNA 专项为 **288/288**，该修正未重复全量；当时的版本登记只检查文档及标记，不运行代码测试。原始记录与完整验证边界见[2.4.0.5 版本验证](../Docs/Releases/2.4.0.5/2.4.0.5版本验证.md)。历史正式 2.4.0 为 28 套，2.3.9 为 25 套；当次结果见[2.4.0 版本验证](../Docs/Releases/2.4.0/2.4.0版本验证.md)，不回写历史计数。

当前另有 2.4.0.5 基线后的未发布[欲望侵染第一批](../Docs/Development/Completed/欲望侵染第一批通用开关框架实现与测试.md)：通用状态框架与首个能力已获维护者实机确认，实际性交收益未接通。本批最终统一回归 **30/30 套、2176/2176 项通过**，PnaLoad 为 **297/297**；在该记录独立登记，不覆盖上面的历史 PNA 288/288。

## 统一入口

从仓库根目录执行，替换本机 net9.0 Harmony 路径；需要 PowerShell 7、.NET SDK 9（或更新 SDK）及 .NET 9 运行时：

```powershell
pwsh -File Scripts/Test-All.ps1 -HarmonyAssemblyPath 'C:\Dependencies\Harmony\net9.0\0Harmony.dll'
```

报告位于 `.builds/validation/`。测试桩与自动回归不能替代游戏内流程、动画及真实存档往返验收。

## 主人资格与成长（2.4.0）

交付内容见[2.4.0 发布说明](../Docs/Releases/2.4.0/2.4.0发布说明.md)。下列分批结果保留原日期与专项范围，不代替本版完整门禁；3B 随机自然投放与平衡仍待专项实机确认。

- [MasterQualificationItem](MasterQualificationItem/README.md)：主人资格道具与研究接入回归。
- [MasterEntry](MasterEntry/README.md)：主人资格初次获取任务回归与实机测试。
- [主人阶段第一批测试说明](../Docs/Development/Completed/主人阶段第一批测试说明.md)：成长回归已纳入 TrainerIdentity、PersonalityTraits 和 RitualLifecycle，未新增独立机制套件。
- [主人阶段第二批测试说明](../Docs/Development/Completed/主人阶段第二批测试说明.md)：实践经验联通真实日常 Job、整场仪式 Apply 和成长工具；441/441 专项用例通过，2026-10-13 维护者确认实机测试有效。

- [MasterPanelUI](MasterPanelUI/README.md)：第三批真实主人面板、阶段区间、四语布局及原缰绳开关回归；2026-10-13 维护者确认面板和开关符合预期，实机步骤见[第三批测试说明](../Docs/Development/Completed/主人阶段第三批测试说明.md)。
- [主人阶段第四批测试说明](../Docs/Development/Completed/主人阶段第四批测试说明.md)：现有五套专项 535/535，覆盖命名、提示去重、人格恢复静默和四语悬停字段；2026-10-13 维护者反馈实机测试未发现问题，已提交为 `708e8be`。
- 后续五项加成已在 `84a0079` 获实机确认；`66b9a13` 的悬停精简由 MasterPanelUI／TrainerIdentity 共 243/243 验证，已随本轮隐藏版本同步，复测步骤见同一第四批测试说明。
- 退出身份隐藏功能已于 2026-10-13 获维护者实机确认并提交为 `018752d`，返回主人恢复原阶段和经验；相关五套专项 536/536。

## 身份、训导官与自我调教

- [PnaLoad](PnaLoad/README.md)：2.4.0.5 的 PNA 负荷、成瘾、来源、信息栏与旧档回归；P3 直接执行 SSC 新生／成熟复制及身体归属过滤，详见[P3 测试记录](../Docs/Development/Verification/PNA机制重置P3实现与测试.md)。

- [TrainerIdentity](TrainerIdentity/README.md)：调教员身份回归。
- [TrainerIntegration](TrainerIntegration/README.md)：训导官人格恢复集成回归。
- [TrainerTrainingMemories](TrainerTrainingMemories/TrainerTrainingMemories.csproj)：弱化记忆套生产源码宿主；覆盖与验收见[实现与测试记录](../Docs/Development/Completed/训导官弱化记忆套实现与测试记录.md)，尚无独立 README。
- [SelfTrainingManual](SelfTrainingManual/README.md)：自我调教手动指派与任务生命周期验证。

## 特化与宠物

- [CombatantSpecialization](CombatantSpecialization/README.md)：战斗员特化回归。
- [PetSpecialization](PetSpecialization/README.md)：宠物互斥、培养与共同效果回归。
- [PetCatAbility](PetCatAbility/README.md)：猫终极单一技能回归。
- [PetDogAbility](PetDogAbility/README.md)：狗终极驯服与己方训练技能回归。
- [PetAffectionFollowup](PetAffectionFollowup/README.md)：PetAffectionFollowup。

## 人格与身体状态

- [PersonalityTraits](PersonalityTraits/README.md)：人格特质与迁移链路回归测试。
- [PersonalityInsertion](PersonalityInsertion/README.md)：人格植入接触与中断回归测试。
- [PersonalityMemories](PersonalityMemories/README.md)：人格记忆快照回归检查。
- [PersonalitySpecializations](PersonalitySpecializations/README.md)：人格特化进度回归。
- [PersonalityCardLayout](PersonalityCardLayout/README.md)：人格凝胶界面布局检查。
- [GenderChangeMemory](GenderChangeMemory/README.md)：性别重置手术记忆回归测试。
- [PermanentLactation](PermanentLactation/README.md)：永久泌乳累计计时回归。

## 限制、行为保护与同床

- [RestrictionCore](RestrictionCore/README.md)：新限制系统核心与生命周期回归。
- [InteractionProtection](InteractionProtection/README.md)：任务限制与兼容生命周期、PNA 完成分发回归。
- [ForcedSexProps](ForcedSexProps/README.md)：强制动作解析、旧部位清理、方向及 Owner 核对回归。
- [SharedBed](SharedBed/README.md)：主奴同床回归。
- [BusTrade](BusTrade/README.md)：交易与宠物事件限制回归。

## 绑定仪式与种族

- [RitualProgression](RitualProgression/README.md)：Ritual progression regression tests。
- [RitualLifecycle](RitualLifecycle/README.md)：Binding Ritual lifecycle regression tests。
- [RitualSelection](RitualSelection/README.md)：仪式选人性能与提交边界回归。
- [RitualAnimationFallback](RitualAnimationFallback/README.md)：仪式备用动画查找回归。
- [RaceInjection](RaceInjection/README.md)：种族分页注入回归。

## 编排与打包流程回归

- [ValidationRunner](ValidationRunner/README.md)：验证统一脚本的状态汇总、依赖预检与打包阻断；独立流程检查，不计入上述 28 套机制回归。
