# 公交车商队交易回归

直接链接生产 `Sexslavecraft/ThePatches/Harmony_BusTrade.cs`，通过完整
`Postfix(bool __result, bool actuallyTraded)` 入口检查交易后的任务分配。
无需安装游戏或下载 NuGet 包，在仓库根目录使用 .NET SDK 9 运行：

```powershell
dotnet run --project Tests/BusTrade/BusTrade.csproj
```

30 项检查覆盖：普通女性公交车在恶堕率达到 20% 后向男性商人启动
`Quickie`；只有接收能力的参与者；缺少旧 `Sex` / `Rape` 定义时使用真实
RJW 引用；强制分支由商人向公交车启动 `RandomRape`；失败及空交易、
飞船、非公交车；双方身体、地图、距离、忙碌状态和预约寻路门槛；等待
600 ticks 与参与者角色；任务拒绝、排队或被其他工作替换时的精确清理；
Shift 排队及已有短等待；成功提示时机；恶堕概率边界和普通、终极成长。

修复后 30/30 通过。将相同项目的 `SscSourceRoot` 指向从修复前 `HEAD`
提取的源码目录后，旧版本为 10/30 通过，退出码为 1。女性基准用例和
独立缺少定义用例均无法启动任务；兼容定义已存在的用例也能复现旧版
忽略任务接收结果、错误报告成功及打断忙碌工作的行为。

其他源码版本可以用以下参数复查，目录内需存在
`ThePatches/Harmony_BusTrade.cs`：

```powershell
dotnet run --project Tests/BusTrade/BusTrade.csproj -p:SscSourceRoot="C:/path/to/source/Sexslavecraft"
```

适配层只提供角色状态、固定骰点、能力结果、定义引用，并记录任务请求、
当前工作、队列、提示及成长。生产代码决定分支、概率、参与者、任务定义
和清理规则。任务接收与延迟由测试输入控制，以覆盖实际任务 API 的结果
边界；没有复刻 RJW 行为驱动或训练计分算法。少数兼容性用例注册旧定义，
用于独立检测定义查找之后的缺陷。`xxx` 为真实 RJW 类型名，会触发现代
C# 编译器的 CS8981 提示。

本套件通过直接调用生产后缀验证逻辑，Harmony 特性仅检查目标元数据；
它不启动游戏、不实际安装 Harmony 补丁，也不验证 RJW 到达、动画与结算
生命周期。维护者已于 2026-09-16 手动进行游戏内验证，确认修复有效。
后续复查可重启加载更新后的程序集，用同地图 15 格内的商队头领完成
非空交易后确认任务实际启动。
