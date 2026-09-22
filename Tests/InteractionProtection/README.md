# 任务限制与兼容生命周期回归

直接编译生产限制核心、上下文、守卫和 Harmony 补丁，当前 **112 项**。覆盖普通双人、单人、人格排泄、日常/仪式、实际主人最高许可、装备/特化、玩家命令和 AI 候选、精确清理、存档及对象池复用。

阶段 3C 新增原版 Lovin 唯一发起方向、同步接收创建、实际开始和待收尾旧档；LifeForce 能力/准备/场景及可选目标发现；事件 Job 到独立运行驱动的状态交接、等待归属、Start 后通知与去重；家具常驻任务边界。Cleanup 正常优先级探针检验 SSC 在 RJW 式成功结算前缀之前纠正结束条件。

统一执行（强制使用真实 Harmony）：

```powershell
pwsh -File Scripts/Test-All.ps1 -HarmonyAssemblyPath 'C:/Dependencies/Harmony/net9.0/0Harmony.dll'
```

额外验证可选模组未安装：

```powershell
dotnet run --project Tests/InteractionProtection -p:NoLifeForce=true -p:HarmonyAssemblyPath='C:/Dependencies/Harmony/net9.0/0Harmony.dll'
```

该模式把基因替身移入其他命名空间，让生产动态发现返回缺席；跳过四个需要实际基因类型的行为用例，其余 **108 项**仍运行真实 PatchAll。省略 Harmony 参数则运行相同生产逻辑的直接调用诊断模式。

最小模型按本机元数据重现关键调用顺序：GetCachedDriver 与 MakeDriver 是不同实例；Lovin 同步创建另一端；RJW Start 才是开始证据。它不执行 Unity、原生完整 Scribe 引用解析、真实能力消耗、心理记忆或动画。事件概率与成长由 BusTrade 套件执行生产入口，日常/仪式实际驱动由 RitualLifecycle 执行。

本机依据、覆盖范围和待实机步骤见[阶段 3C](../../Docs/Development/新限制系统阶段3C.md)。

本轮职责整理新增 8 项：可选入口缺席/签名变化与去重、匹配签名、Lovin 回调布局诊断、事件 Job 池化、取消待交接事件、原保存键以及准备对象更换的精确清理。准备/事件助手直接编译生产文件；失效模拟不等于完整实机兼容验证。
