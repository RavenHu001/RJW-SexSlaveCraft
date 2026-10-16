# TrainingExperience

[测试索引](../README.md) · [第三批记录与实机清单](../../Docs/Development/Completed/欲望侵染第三批真实部位与弱开发实现与测试.md)

执行生产 `TrainingExpUtility`、新旧评分、普通部位凭据、侵染 20% 计划、完成分发和人格 token；仅引擎身体／健康／Scribe 与 RJW 已选部位数据使用边界模型。不实现 RJW 部位搜索，不把重新解析结果作为开始凭据。

```powershell
dotnet run --project Tests/TrainingExperience/TrainingExperience.csproj
```

已纳入统一 `Scripts/Test-All.ps1`，无需 Harmony 参数。重点覆盖实际部位映射、去重均分、饱和不转移、新旧评分及随机隔离、快照倍率、器官移除、保存独立深对象、回调中止、精确上限／微量新建，以及原日常／仪式式调用。真实任务适配和 Harmony 集成由 `InteractionProtection` 覆盖。

自动结果与实机验收以第三批记录为准；维护者确认“经测试都符合预期”。宿主仍不等同于完整 Unity 或真实 Scribe XML，反馈范围见记录。
