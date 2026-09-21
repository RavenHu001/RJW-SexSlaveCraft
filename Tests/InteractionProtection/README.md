# 普通任务限制回归

直接编译生产新限制核心、上下文、守卫及 Harmony 适配器，保留后续批次旧路径测试。当前 62 项：玩家命令和 AI 候选、派生预约、行走复查、双向许可、主人最高许可、装备/特化、单人、人格排泄、共享接收用途、拒绝清理、存读档及对象池复用。

统一执行：

```powershell
pwsh -File Scripts/Test-All.ps1 -HarmonyAssemblyPath 'C:\Dependencies\Harmony\net9.0\0Harmony.dll'
```

统一脚本强制使用真实 Harmony `PatchAll`；也可不传 `HarmonyAssemblyPath` 单独构建此项目，使用替身中的直接补丁调用进行诊断。两种模式共用生产代码和场景断言。

最小对象模型模拟步骤、预约、任务队列和序列化边界，不加载 Unity，也不代替真实引用恢复、实际工作调度及模组组合测试。本机依赖元数据检查和实机步骤见[阶段 3A 开发记录](../../Docs/Development/新限制系统阶段3A.md)。
