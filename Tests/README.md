# 测试说明索引

[文档总索引](../Docs/README.md) · [发布与打包](../Docs/Maintenance/发布与打包.md) · [开发记录](../Docs/Development/README.md)

当前 `Scripts/Test-All.ps1` 登记 **28 套机制回归**；正式 2.3.9 发布时为 25 套，后续开发增加 `MasterQualificationItem`、`MasterEntry` 与 `MasterPanelUI`。下列专项 README 保留各自命令、覆盖范围和实机步骤，实际通过数量以当次报告为准。

## 统一入口

从仓库根目录执行，替换本机 net9.0 Harmony 路径；需要 PowerShell 7、.NET SDK 9（或更新 SDK）及 .NET 9 运行时：

```powershell
pwsh -File Scripts/Test-All.ps1 -HarmonyAssemblyPath 'C:\Dependencies\Harmony\net9.0\0Harmony.dll'
```

报告位于 `.builds/validation/`。测试桩与自动回归不能替代游戏内流程、动画及真实存档往返验收。

## 主人资格（未发布开发内容）

- [MasterQualificationItem](MasterQualificationItem/README.md)：主人资格道具与研究接入回归。
- [MasterEntry](MasterEntry/README.md)：主人资格初次获取任务回归与实机测试。
- [主人阶段第一批测试说明](../Docs/Development/主人阶段第一批测试说明.md)：成长回归已纳入 TrainerIdentity、PersonalityTraits 和 RitualLifecycle，未新增独立机制套件。
- [主人阶段第二批测试说明](../Docs/Development/主人阶段第二批测试说明.md)：实践经验联通真实日常 Job、整场仪式 Apply 和成长工具；441/441 专项用例通过，2026-10-13 维护者确认实机测试有效。

- [MasterPanelUI](MasterPanelUI/README.md)：第三批真实主人面板、阶段区间、四语布局及原缰绳开关回归；2026-10-13 维护者确认面板和开关符合预期，实机步骤见[第三批测试说明](../Docs/Development/主人阶段第三批测试说明.md)。
- [主人阶段第四批测试说明](../Docs/Development/主人阶段第四批测试说明.md)：现有五套专项 535/535，覆盖命名、提示去重、人格恢复静默和四语悬停字段；2026-10-13 维护者反馈实机测试未发现问题，已提交为 `708e8be`。
- 后续五项加成已在 `84a0079` 获实机确认；`66b9a13` 的悬停精简由 MasterPanelUI／TrainerIdentity 共 243/243 验证，已随本轮隐藏版本同步，复测步骤见同一第四批测试说明。
- 退出身份隐藏功能已于 2026-10-13 获维护者实机确认并提交为 `018752d`，返回主人恢复原阶段和经验；相关五套专项 536/536。

## 身份、训导官与自我调教

- [TrainerIdentity](TrainerIdentity/README.md)：调教员身份回归。
- [TrainerIntegration](TrainerIntegration/README.md)：训导官人格恢复集成回归。
- [TrainerTrainingMemories](TrainerTrainingMemories/TrainerTrainingMemories.csproj)：弱化记忆套生产源码宿主；覆盖与验收见[实现与测试记录](../Docs/Development/训导官弱化记忆套实现与测试记录.md)，尚无独立 README。
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
- [InteractionProtection](InteractionProtection/README.md)：任务限制与兼容生命周期回归。
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
