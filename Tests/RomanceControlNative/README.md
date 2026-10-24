# 浪漫控制第一批原生回归

2026-10-24 提交前复核：**121／121**（首次实现 114 项，本次新增 7 项）。目标为本机 RimWorld 1.7.4985 rev1269，执行隔离候选生产程序集，打印实际载入路径、SHA-256 和游戏 MVID。已登记进 `Scripts/Test-All.ps1`；正式门禁仍因既有版本元数据差异阻断，完整独立机制结果与实机范围分开记录。

[实施与验证记录](../../Docs/Development/Verification/性奴浪漫控制第一批实现与验证.md) · [专项规划](../../Docs/Design/性奴浪漫行为控制开发规划.md)

覆盖阶段阈值、五项许可、两个附加开关组合、双边受控、真实主人、旧总开关隔离、保存值与生效资格分离、安全提交、克隆默认与来源隔离、人格替换及命令授权失效、未知版本、原生 Scribe 缺字段及跨 Pawn 引用恢复。不改变关系，未接入自然互动、接受概率或 Job 执行。

本次增加空壳健康事件撤权、首次观察前旧编辑凭据、无效锁链 loadID、同主换链后的读档拒绝、缺失链身份字段、合法原链实例重建续行，以及真实关系清理回调恢复原主人的重入回归。原生测试链显式设置独立 loadID，避免无身份夹具掩盖持久凭据问题。

需要 .NET 10 SDK、当前游戏引用、RJW 引用和测试侧 net9 Harmony 2。先隔离构建生产工程，再提供 `SscAssemblyPath` 指向该候选；不通过重编覆盖正式运行 DLL 来验证。构建项目所需属性为 `GameRoot`、`RjwAssemblyPath`、`HarmonyAssemblyPath`、`SscAssemblyPath`，候选生产构建另沿现有工程传入动画和游戏 Harmony 引用。

构建后使用所选 .NET 10 执行器运行：

```powershell
dotnet Tests/RomanceControlNative/bin/Release/net10.0/RomanceControlNative.dll '<仓库绝对路径>' '<游戏绝对路径>'
```

引擎边界：使用真实 Pawn／健康／Comp 类型和生产规则；图形、完整健康状态变化、玩家配置目录及玩家阵营全局根由宿主隔离。原生 Scribe 仅对本专项的三个类型提供精确名称映射，外部 Pawn 作为已加载世界实体登记，未重建整张地图。测试 Harmony 2 通过无注解转发器调用真实生产 postfix，避免反射生产 Harmony 3 特性。初始化时预热独立 CLR 类型缓存，测试开始后的原版 Error 都作为失败。

实际 `StorePawnData`、凝胶 `CopyFrom`、克隆初始化 postfix 和原生存档均执行生产入口；完整提取／植入由 [PersonalityTraits](../PersonalityTraits/README.md) 的 1.7 变体执行生产链路，任务场景入口接线另作静态核查。完整原版／RJW 互动、地图出生、真实游戏整档及屏幕显示仍待后续批次和实机验证。
