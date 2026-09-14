# 人格特化进度回归

运行：

```powershell
dotnet run --project Tests/PersonalitySpecializations/PersonalitySpecializations.csproj
```

本项目不依赖外部测试包，直接编译生产文件 `Comp_Train.Specialization.cs` 中的导出、恢复、方向切换和历史读写逻辑。游戏角色、方向派生配置以及健康状态清理入口由最小宿主提供；测试没有另行实现训练进度算法。

覆盖 21 个场景：源人格多方向历史迁移、宿主当前与非当前方向污染、同体恢复、当前值覆盖陈旧缓存、双向字典隔离、旧凝胶缺失历史、零值与无方向、未知枚举和键、损坏数值、方向派生开关、基础状态清理请求、重复覆盖以及身体奶量不迁移。

此测试验证组件状态逻辑和健康状态清理的调用边界；完整的凝胶采集、复制、序列化与植入调用由 `Tests/PersonalityTraits` 验证，真实健康状态对账仍需使用游戏环境。
