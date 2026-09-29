# 任务限制与兼容生命周期回归

直接编译生产限制核心、上下文、守卫和 Harmony 补丁，当前 **143 项**。覆盖普通双人、单人、人格排泄、日常/仪式、实际主人最高许可、装备/特化、玩家命令和 AI 候选、精确清理、存档及对象池复用。

阶段 3C 新增原版 Lovin 唯一发起方向、同步接收创建、实际开始和待收尾旧档；LifeForce 能力/准备/场景及可选目标发现；事件 Job 到独立运行驱动的状态交接、等待归属、Start 后通知与去重；家具常驻任务边界。Cleanup 正常优先级探针检验 SSC 在 RJW 式成功结算前缀之前纠正结束条件。

训导官阶段 3 新增 8 项：直接通过生产守卫和真实 Harmony `ProcessSex` 后缀验证普通双人及允许的强迫行为、开始与倒计时边界、跳过原结算、任务外 `SexProps`、训练专用任务排除和领取标记读档去重。实际成长资格由 TrainerIdentity 套件链接生产经验工具验证。

自我调教阶段 2 增加专用驱动用途回归：普通自慰关闭时仍可使用独立许可，开始凭据经存读档保持，替换 A 目标后不能继承凭据，关闭自我调教许可会拒绝新任务。真实 RJW 步骤、动画和恶堕需求须在游戏内验证。

人格排泄接收保护新增 22 项：手动／自动发起的搬运、进食、睡眠和娱乐抢占；接收准备时机、连续重选、队列与对象池、正常收尾和取消、配对失效、普通训练隔离、兼容设备参与者识别，以及不依赖身份或持久化标记。两个原版自动入口的关键调度顺序由最小模型重现，真实 Harmony 安装并执行生产替换判断后缀；不执行完整人格提取和游戏内需求系统，详见[修复记录](../../Docs/Development/人格排泄接收任务抢占修复.md)。

统一执行（强制使用真实 Harmony）：

```powershell
pwsh -File Scripts/Test-All.ps1 -HarmonyAssemblyPath 'C:/Dependencies/Harmony/net9.0/0Harmony.dll'
```

额外验证可选模组未安装：

```powershell
dotnet run --project Tests/InteractionProtection -p:NoLifeForce=true -p:HarmonyAssemblyPath='C:/Dependencies/Harmony/net9.0/0Harmony.dll'
```

该模式把基因替身移入其他命名空间，让生产动态发现返回缺席；跳过四个需要实际基因类型的行为用例，其余 **138 项**仍运行真实 PatchAll。省略 Harmony 参数则运行相同生产逻辑的直接调用诊断模式。

最小模型按本机元数据重现关键调用顺序：GetCachedDriver 与 MakeDriver 是不同实例；Lovin 同步创建另一端；RJW Start 才是开始证据。它不执行 Unity、原生完整 Scribe 引用解析、真实能力消耗、心理记忆或动画。事件概率与成长由 BusTrade 套件执行生产入口，日常/仪式实际驱动由 RitualLifecycle 执行。

本机依据、覆盖范围和待实机步骤见[阶段 3C](../../Docs/Development/新限制系统阶段3C.md)。

本轮职责整理新增 8 项：可选入口缺席/签名变化与去重、匹配签名、Lovin 回调布局诊断、事件 Job 池化、取消待交接事件、原保存键以及准备对象更换的精确清理。准备/事件助手直接编译生产文件；失效模拟不等于完整实机兼容验证。
