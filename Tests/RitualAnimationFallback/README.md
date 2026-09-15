# 仪式备用动画查找回归

直接链接生产 `RitualTrainingUtility.cs`，验证备用启动能读取动画框架自己的
`GroupAnimationDef` 数据库，避免误把 `DefDatabase<Def>` 当成全部定义的合集。

在仓库根目录使用 .NET SDK 9 运行，无需安装游戏或下载 NuGet 包：

```powershell
dotnet run --project Tests/RitualAnimationFallback/RitualAnimationFallback.csproj
dotnet run --project Tests/RitualAnimationFallback/RitualAnimationFallback.csproj -p:EnableAnimationTestStub=false
```

第一条运行 6 项检查：派生库候选筛选及启动、角色顺序和时长传递、零优先级候选、
当前定义重新读取、空库、全部不匹配、接收任务缺失等场景。第二条在完全不编译
动画框架类型的宿主中运行 1 项检查，验证框架仍是可选依赖。

修复前第一组 2/6 通过，失败项直接复现：即使派生定义库存在可用动画，生产入口
也不调用候选匹配；修复后 6/6 通过，无框架构建 1/1 通过。

游戏适配层保留每个泛型类型独立存储定义的行为，动态类型查询按本机游戏
`GenDefDatabase.GetAllDefsInDatabaseForDef(Type)` 的实际实现读取泛型数据库。
框架适配层只返回预设匹配结果并记录启动参数，不复制真实角色条件匹配算法，
也不执行 Unity 渲染。随机选择固定为首项，以验证无效候选被生产代码排除。
测试中 RJW 的实际类型名 `xxx` 会触发现代编译器的 CS8981 命名提示。

本套件不能证明某个动画包资源完整或某个体位必定有动画。游戏内需重启后，
在有框架且存在适用动画的环境中测试仪式备用播放；资源确实缺失或不匹配时，
原有警告和计时行为保持不变。本修复不增加动画包依赖，不修改动画资源、
UAP 兼容逻辑或框架原有的匹配结果。
