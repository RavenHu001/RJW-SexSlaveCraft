# 战斗员特化回归

在仓库根目录执行，提供本机 net9.0 版本的 Harmony：

```powershell
dotnet run --project Tests/CombatantSpecialization/CombatantSpecialization.csproj --configuration Release --property:RestoreConfigFile=Tests/NuGet.Config --property:HarmonyAssemblyPath=<0Harmony.dll绝对路径> -- .
```

已登记到 `Scripts/Test-All.ps1`，共 37 组测试。前 17 组覆盖阶段一的枚举、研究及身份入口、状态对账、方向历史、序列化边界、普通完成显示、真实 XML 的阶段属性和四语资源。

阶段二增加 20 组：两类收益公式、普通／主人上限、实际主人与指定者区别、无额外研究／身份／进度倍率、数值与完成边界、健康同步及历史恢复，以及直接死亡、屠宰／处决、持续燃烧、分摊伤害、爆炸来源、嵌套去重、重复通知、多目标、复活及异常退出。

本套件直接编译生产数据分部、健康对账、经验工具、死亡事件工具及 Harmony 补丁。公共 `AddSpecializationProgress` 已移入生产数据分部，测试不再复制其实现。游戏对象与外部资格服务使用最小边界模型；生产 Harmony 补丁实际安装到模型的 `Pawn.Kill`、`Fire.DoFireDamage`、`Explosion.AffectCell` 方法上，验证 Prefix/Postfix/Finalizer 的配合。

保存字段通过 JSON 边界模型重建；这不是 RimWorld Scribe 实机读档。种族、阵营和倒地矩阵验证事件规则不读取这些额外条件，不模拟真实游戏种族。测试不执行原版伤害计算、射弹飞行或 Unity 渲染，仍须实机检查这些链路。日常 Job 的评分传递、重入、读档认领和中断由 `RitualLifecycle` 套件验证。
