# 测试说明索引

主从关系概览第二批新增 [BondOverviewUI](BondOverviewUI/README.md)，当前 **78/78**，直接链接生产会话、导航、卡片与窗口；原 [BondRelations](BondRelations/README.md) **110/110** 保持。本批全量 **35/35 套、2953/2953 项及 2/2 资源检查通过**，本机同步与实机边界见[第二批记录](../Docs/Development/Verification/主从关系概览第二批界面与导航实现与测试.md)，以下第一批数字保留为历史阶段。

主从关系概览第一批及性能优化扩展现有 [BondRelations](BondRelations/README.md)，当前专项 110/110，本批全量 34/34 套、2875/2875 项及 2/2 资源检查通过，未增加套件数。新增遍历计数、候选覆盖、缓冲释放与同机基准对比，实际 1.7 编译通过，见[当前性能记录](../Docs/Development/Verification/主从关系概览第一批性能优化与对比.md)。初批 94 项及全量 2859 项保留为[历史结果](../Docs/Development/Verification/主从关系概览第一批只读数据实现与测试.md)，真实容器与性能仍待实机核验。

当前版本：**2.4.2**。v2.4.1发布后的双版本重布局、1.7兼容及凝胶渲染/外观/UI开发统一纳入；1.6/Common冻结仍属于2.4.1。历史日期、产物版本与测试计数保留原阶段，见[本版登记](../Docs/Releases/2.4.2/2.4.2版本验证.md)。

1.7 双版本重布局与四项兼容修复已获维护者确认“经测试没有发现问题”，见[验收记录](../Docs/Development/Completed/双版本重布局与1.7兼容修复验收.md)。34 套／2549 项、两项资源门禁、28 项实际 DLL 元数据检查和 53 项发布编排为修复阶段已执行结果；注释后专项 33/33 及 10 项状态断言复验通过。本次文档收尾沿用这些结果，不登记为重新执行全量回归。

[文档总索引](../Docs/README.md) · [发布与打包](../Docs/Maintenance/发布与打包.md) · [开发记录](../Docs/Development/README.md)

当前原版／FA自动适配新增41项眼部与四组合检查，凝胶套件205/205；原版眼睛保留原色及Alpha，FA保留当前分层动画，维护者确认原版眼部／转向及FA眨眼／遮挡正常，见[适配验收](../Docs/Development/Completed/1.7原版与FA凝胶渲染自动适配与验收.md)。

当前缓存性能优化新增22项回归及稳定调用分配/复制检查，保留配色、光照、动画和遮挡，维护者实机测试未发现问题，见[性能验收](../Docs/Development/Completed/1.7凝胶化渲染性能优化与验收.md)。

光照后统一另补23项状态／开关和头部组回归，维护者确认配色一致性改善、眨眼和遮挡正常，见[本轮验收](../Docs/Development/Completed/1.7凝胶化光照阶段统一与验收.md)。

本批[个体凝胶外观](../Docs/Development/Completed/1.7个体凝胶外观设置与验收.md)增加53项配置／草稿／原版控件边界／多人缓存回归，凝胶套件当时260项；PnaLoad增加三项真实克隆协调检查。维护者已确认颜色／不透明度、恢复默认、五官动画和遮挡正常，先前自动适配的205项验收保留历史范围。

当前 1.7 开发入口登记 **34 套**，[凝胶化与 1.7 兼容回归](Gelatinization/README.md)已扩展到 300 项机制、材质选择、像素函数及头部分层用例，并内嵌 10 项 Harmony 状态参数检查。[原生 Shader 编译工具](../Tools/GelatinizationShader/README.md)独立使用实际游戏的 .NET 10／Waylith 编译器验证普通和实例绘制，不计入 net9 模型套件数。1.6 继续只检查冻结成品，不执行新源码回归；最终深紫色＋0.85 不透明度已获维护者实机确认，见[视觉验收记录](../Docs/Development/Completed/1.7全身凝胶化身体视觉调整与验收.md)。早期全量 34 套／2560 项与最终参数专项 44/44 的执行阶段分别保留。新头部分层、FA 遮罩与动画保留已有 54 项模型回归，实机遮挡反馈后又补充 21 项深度／SA 工作线程回归；维护者确认头部遮挡和眨眼正常，见[验收记录](../Docs/Development/Completed/1.7头部凝胶化与深度线程修复验收.md)。

正式 **2.4.1** 发布时登记 33 套，原 2.4.0.5 开发里程碑及后续开发统一纳入，另有 Def／Languages 独立资源门禁；发布准备检查发现 TrainerIntegration 夹具依赖旧默认值，改为先取得主人资格后专项 10/10；随后从干净提交重跑正式门禁，**33/33 套、2516/2516 项**及两项资源检查通过，结果见版本验证。此前 33 套／2516 项和 35/35 编排夹具保留为历史结果。当次正式门禁及历史结果的区别见[2.4.1 版本验证](../Docs/Releases/2.4.1/2.4.1版本验证.md)。历史 2.4.0 的 28 套及 2.3.9 的 25 套保留原计数，不回写。

以下保留现已归入 2.4.1 的原 2.4.0.5 基线后分批记录：[欲望侵染第一批](../Docs/Development/Completed/欲望侵染第一批通用开关框架实现与测试.md)：通用状态框架与首个能力已获维护者实机确认，实际性交收益在当时未接通。本批最终统一回归 **30/30 套、2176/2176 项通过**，PnaLoad 为 **297/297**；在该记录独立登记，不覆盖原 PNA 里程碑的 288/288。

第三批新增 [TrainingExperience](TrainingExperience/README.md) 生产源码专项，并扩展真实 Harmony 完成／存档集成；维护者已确认本批符合预期，结果及验收边界见[第三批记录](../Docs/Development/Completed/欲望侵染第三批真实部位与弱开发实现与测试.md)。

第四批新增 [DesireInfestationFeedback](DesireInfestationFeedback/README.md)，链接反馈、完成分发与人格记忆生产代码，并扩展真实 Harmony 完成集成；维护者已确认本批符合预期，实际结果与验收范围见[第四批记录](../Docs/Development/Completed/欲望侵染第四批独立反馈实现与测试.md)。

四批参数、提交与实际验收汇总见[主人欲望侵染完成核对](../Docs/Development/Completed/主人欲望侵染完成核对.md)；本轮文档收口只做静态与哈希检查，机制计数保留原执行日期。

## 统一入口

从仓库根目录执行，替换本机 net9.0 Harmony 路径；需要 PowerShell 7、.NET SDK 9（或更新 SDK）及 .NET 9 运行时：

```powershell
pwsh -File Scripts/Test-All.ps1 -GameVersion 1.7 -HarmonyAssemblyPath 'C:\Dependencies\Harmony\net9.0\0Harmony.dll'
```

统一入口先运行 `Test-ModLayout.ps1`，核对 `LoadFolders.xml`、两版运行目录及冻结的 2.4.1 文件哈希，再分别执行只读 Def 和 Languages 镜像检查。默认 `GameVersion` 为 `1.7`。Def 检查遍历 `Sexslavecraft/Defs` 中现有文件，要求 `1.7/Defs` 对应源存在、SHA-256 一致且 XML 可解析；运行目录独有文件不要求新增镜像，目录缺失或镜像集合为空也会失败。Languages 检查要求 `1.7/Languages` 与 `Sexslavecraft/Languages` 的完整文件集合和哈希一致，拒绝缺失、差异、镜像独有文件、缺目录及空源集合。两个检查始终分别执行和记录；布局或资源检查失败会将机制套件标为 `NotRun` 并阻止打包，不自动同步文件。

可单独执行 `pwsh -File Scripts/Test-DefMirrors.ps1` 或 `pwsh -File Scripts/Sync-LanguagesMirror.ps1`。两者均支持 `-RepositoryRoot`、`-GameVersion`、`-RuntimeRoot` 与 `-PassThru`；语言同步仍须显式加 `-Sync`，且仅允许以 `1.7/Languages` 为源，不能同步冻结的 1.6 内容。统一报告位于 `.builds/validation/`，分别生成 `DefMirrors.log`、`LanguagesMirrors.log`，并在 `summary.json` 的 `Checks` 中按 `DefMirrors`／`LanguagesMirrors` 登记状态、文件数、原因和日志路径，合计字段为 `CheckCount`、`PassedChecks`、`FailedChecks`。布局结果另记在 `LayoutValidation`，不计入两项资源检查。

`pwsh -File Scripts/Test-All.ps1 -GameVersion 1.6` 只验证冻结文件哈希与该版 Def／Languages XML，不比较当前源码镜像、不编译或运行当前源码套件，报告 `SuiteCount = 0`。`SourceValidationScope` 明确区分这项资源检查与 1.7 的 34 套当前源码模型回归；都不能替代游戏内流程、动画及真实存档往返验收。

单套测试传入的目录参数仍为仓库根目录。`Tests/Shared/TestPaths.cs` 将源码解析到 `Sexslavecraft/`，运行资源默认解析到 `1.7/`，贴图解析到 `Common/Textures/` 并优先使用版本目录中的同名覆盖。需要独立资源夹具时可设置 `SSC_TEST_REPOSITORY_ROOT`、`SSC_TEST_RUNTIME_ROOT`、`SSC_TEST_COMMON_ROOT`；统一入口仅在本次调用期间设置这些进程环境变量，结束或失败后恢复原值。`RitualProgression` 保留显式 XML 文件参数。

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

- [PnaLoad](PnaLoad/README.md)：2.4.1 的 PNA 负荷、成瘾、来源、信息栏与旧档回归；P3 直接执行 SSC 新生／成熟复制及身体归属过滤，详见[P3 测试记录](../Docs/Development/Verification/PNA机制重置P3实现与测试.md)。

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

- [ValidationRunner](ValidationRunner/README.md)：验证统一脚本的状态汇总、依赖预检与打包阻断；独立流程检查，不计入机制回归套件数。

第二批新增普通完成快照与侵染来源专项，真实生产上下文、PNA 输入和任务适配器分别执行；当前结果与游戏边界见[第二批实现记录](../Docs/Development/Completed/欲望侵染第二批普通完成与来源择强实现与测试.md)。历史计数保留各次实际运行范围。

## 恩赐与 PNA 实际选择专项

PNA 输入与来源偏好已经接通；实际 RJW 候选选择及双向评价仍需按[专项清单](../Docs/Development/Verification/欢愉恩赐PNA来源偏好专项核验.md)核验，不以宿主通过数代替实机结果。恶堕资格放宽与来源倍率差异暂缓，见未来第 22 项。

2026-10-17，维护者确认个体颜色／不透明度、恢复默认、五官动画和遮挡正常。本次默认改为HSV H272.5°／S90%／V68%（RGB .3995／.068／.68），Alpha仍为.85，已有自定义值不覆盖；补充两项回归后凝胶套件260项。代码块注释已补齐，详见[验收记录](../Docs/Development/Completed/1.7个体凝胶外观设置与验收.md)。

2026-10-17，凝胶外观 UI 与个人渲染开关新增 41 项回归，移除已取消的全局批量刷新检查后专项为 300/300。覆盖旧全局设置的一次性迁移、个人开关保存及多人隔离、生产 ITab 外观区块、显式禁用控件、任职状态组合、窄屏/长行布局及四种隔离 FA/SA 宿主。本批已获维护者实机确认，最终入口与验证范围见[验收记录](../Docs/Development/Completed/1.7凝胶外观UI与个人开关验收.md)，不把模型回归记作实机验收。

收尾按维护者要求移除命令栏重复调色按钮，两项相关回归改为无额外指令的断言，专项仍300/300；实际引用构建和布局检查通过。入口清理不重复执行其余33套，34套／2819项保留首轮门禁阶段。最终产物与同步见上述验收记录。
