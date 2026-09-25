# 训导官人格恢复集成回归

运行：

```powershell
dotnet run --project Tests/TrainerIntegration/TrainerIntegration.csproj --configuration Release
```

本套件让以下生产模块在同一次恢复过程中协作：

- `SSCRestrictionLifecycle.BeginRestore` 及其真实嵌套 `RestoreScope.Dispose`。
- `Comp_Train.Specialization` 的当前方向、历史导出和整体导入。
- `SSCIdentityUtility`、`SSCBondUtility` 的身份和绑定事务。
- `TrainerSpecializationGelUtility` 的标签保存、清理、恢复。
- `TrainerSpecializationLifecycle` 和 `TrainerSpecializationUtility` 的最终状态维护及资格读取。

用例覆盖临时无绑定、最后仍失格时归档源进度、宿主高进度及终极标签清理、两种源终极记录与两种宿主资格的四种组合、零进度、空源标签、嵌套和重复释放，以及异常退出后的继续维护。嵌套用例用健康状态增删计数和最终通知计数检验只在最外层转换一次。

## 验证边界

共用 `Tests/TrainerIdentity` 的游戏接口模型；凝胶替身仅提供字段和字典容器，状态算法直接编译生产源码。恢复序列采用 `ExcretionUtility.InheritEverything` 中训导官方向对应的顺序，但没有执行整个植入方法、特质/记忆迁移或人格植入 Job。健康添加的 Harmony 后缀由用例显式调用生产通知模拟；实际 Harmony 安装和真实 Scribe 跨引用恢复仍由其他检查及实机验证覆盖。

这补充了 `PersonalityTraits`（真实完整人格数据搬运，但替换训导官维护器）与 `TrainerIdentity`（真实维护器，但没有人格标签工具和恢复作用域共同运行）之间的覆盖空隙，不将模型测试等同于游戏内全链路验证。
