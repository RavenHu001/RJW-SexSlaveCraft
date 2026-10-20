# 简易 PNA 发射器废案

2026-10-20 按维护者授权从 **1.7** 移除。不兼容包含旧发射器、弹丸、配方或专属研究引用的存档；没有隐藏 Def、替换武器、研究补偿或存档迁移。本目录不在 LoadFolders 中，也不进入生产或测试编译及游戏同步。

现役三代九武器已经覆盖这一方向。旧枪将实体创伤、有效链无伤治疗和旧严重度剂量放在部位回调中结算，与现役涂覆／太空入口的规则不同，不再作为新武器模板。

保留内容：

- [旧伤害处理器](DamageWorker_PNA.cs)：`PNA_DWExtension`、`DamageWorker_SSCPna` 及高低阶处理器。单次调用投药认领、嵌套状态恢复、命中期间保护保持具有历史参考价值。旧治疗按部位回调随机选择自然伤势，不是现役太空即时修复规则。
- [旧公共剂量工具快照](SSCPnaWeaponDoseUtility.Legacy.cs)：保留 `LauncherCompatibility`、`FromSeverity` 和先 AddHediff 后赋严重度的历史回调顺序，便于理解旧处理器。当前生产工具仅保留归一化剂量及完整快照提交。
- [武器／弹丸配置](Defs/SSC_Gun.xml)、[伤害配置](Defs/DemageDef_Gun.xml)、[专属研究](Defs/Research_PNASimpleLauncher.xml)：保留移除前配置，高阶枪和弹丸原本已注释禁用。
- `Languages/` 保留已删除的专属四语注入文件，供历史文档引用；它们不再作为游戏翻译加载。
- [旧兼容剂量测试](Tests/WeaponDoseUtilityTests.cs)、[旧治疗反馈测试](Tests/EffectsStep3Tests.cs)：原始历史片段，不编译。其他测试可从本轮之前的 Git 版本查阅。

归档来源为本轮移除前工作区（基线 `d7f38cf`）。源码和测试依赖当时的公共工具及边界夹具，不能单独编译；归档工具与生产工具类型同名，禁止把本目录加入通配编译或打包。无需同时启用两份实现。

保留现役公共负荷、抗性 Hediff、旧强化身体状态清理、拖尾和受药特效；`Verb_MeleeAttackSSCPna` 实际用于短矛，也继续编译。1.6 及 Common 保持冻结，Common 中旧枪贴图仍供 1.6 使用。

实施、验证和安装结果见[移除记录](../../Docs/Development/Completed/SSC旧简易PNA发射器移除与归档.md)。
