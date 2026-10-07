# 战斗员特化回归

在仓库根目录执行，提供本机 net9.0 版本的 Harmony：

```powershell
dotnet run --project Tests/CombatantSpecialization/CombatantSpecialization.csproj --configuration Release --property:RestoreConfigFile=Tests/NuGet.Config --property:HarmonyAssemblyPath=<0Harmony.dll绝对路径> -- .
```

已登记到 `Scripts/Test-All.ps1`，共 54 组测试。原阶段一的 17 组覆盖枚举、研究及身份入口、状态对账、方向历史、序列化边界、普通完成显示、真实 XML 的阶段属性和四语资源；另增加狗完成显示、猫狗终极化引导及四语资源镜像 3 组回归。

共用显示组同时回归猫狗普通路线的标签及完成显示：猫不再标记未完成，兔保留提示；猫狗使用一位小数，`0.995～0.99899` 不提前显示完成，达到 `0.999` 时显示普通培养完成。真实显示分部的终极化引导按当前方向返回，拒绝未完成、损坏进度、历史方向及任意既有宠物终极成果，不写入状态；保留战斗员原引导，四语新键唯一、非空且与源码语言镜像相同。这里只执行真实显示分部，猫狗选择与培养资格仍由宠物专项验证；界面悬停绘制仍需实机确认。

阶段二现有 22 组：共用受训公式、仪式评分映射、所有当前方向的受训资格与完成边界、普通／主人上限、实际主人与指定者区别、健康同步及历史恢复，以及战斗员直接死亡、屠宰／处决、持续燃烧、分摊伤害、爆炸来源、嵌套去重、重复通知、多目标、复活及异常退出。

阶段三增加 5 组：普通／终极互斥、终极实例保持、重复培养与经验拒绝、跨方向和留空展示、加载对账、终极属性及八种凝胶配方资源。

阶段四增加 7 组：六项倍率及实际时长配置、四语能力文本、终极与征召资格、原版条件保留、排队执行复查、技能实例与冷却保持、重复终极去重以及最终撤销。直接编译生产 Ability 子类及授予组件；Ability 基类仅记录调用与冷却哨兵，不模拟原版战斗效果、计时或 Scribe，真实存读档和武器表现仍需实机验收。

本套件直接编译生产数据分部、健康对账、经验工具、死亡事件工具及 Harmony 补丁。公共 `AddSpecializationProgress` 已移入生产数据分部，测试不再复制其实现。游戏对象与外部资格服务使用最小边界模型；生产 Harmony 补丁实际安装到模型的 `Pawn.Kill`、`Fire.DoFireDamage`、`Explosion.AffectCell` 方法上，验证 Prefix/Postfix/Finalizer 的配合。

保存字段通过 JSON 边界模型重建；这不是 RimWorld Scribe 实机读档。种族、阵营和倒地矩阵验证事件规则不读取这些额外条件，不模拟真实游戏种族。测试不执行原版伤害计算、射弹飞行或 Unity 渲染，仍须实机检查这些链路。日常 Job 的评分传递、重入、读档认领和中断由 `RitualLifecycle` 套件验证。

## 本机游戏资源检查

在独立 PowerShell 7 进程中运行，`GameRoot` 指向本机 RimWorld 安装目录：

```powershell
pwsh -NoProfile -File Scripts/Test-CombatantGameResources.ps1 -GameRoot '<RimWorld安装目录>'
```

脚本读取仓库发布 XML、四语翻译、本机 Core 定义和游戏程序集，以及仓库已编译的 DLL；改动 C# 后须先重新构建。检查能力、健康状态、研究、配方、八种凝胶和属性引用，实际构造效果、授予与消失组件，并调用原版 `AbilityComp.Initialize` 和 `GenTicks.SecondsToTicks`。缺失组件类型、翻译或原版属性时返回失败，JSON 报告包含输入哈希与失败原因，默认保存到 `.builds/combatant-stage5/game-resource-audit.json`。

本检查需要已安装的游戏，不加入无游戏依赖的 `Test-All.ps1`；它是独立的本机验收步骤，不下载或分发游戏程序集。它不运行完整游戏 XML 加载、完整技能初始化、施法、战斗或 Scribe。维护者对完整玩法及数值的验收记录见[阶段五](../../Docs/Development/战斗员特化阶段五.md)。
