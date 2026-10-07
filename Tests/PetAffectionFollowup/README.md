# PetAffectionFollowup

运行：

```powershell
dotnet run --project Tests/PetAffectionFollowup/PetAffectionFollowup.csproj -c Release
```

本套件直接链接 `PetAffectionFollowupUtility`、`JobDriver_PetAffection`、`JobDriver_PetAffectionFollowup` 及其专属 Start/End 补丁。56 个场景覆盖固定主人配对、双向资格、20% 单次判定、完整亲昵后交接、重入及任务池复用、防止抢占新玩家命令、精确等待/接收清理、原空闲队列保留、读档去重与四语资源。

游戏/RJW 宿主只提供已核验公共接口的可配置返回与原版创建的任务输出，不在测试中复制 `ForceWait`、寻路、RJW 成年/身体/冷却/欲求/吸引力算法或统一许可策略。Start/End 边界显式调用真实生产 Prefix/Postfix；不把它计为实际 Harmony 安装验证。

生产亲昵经验和宠物成果资格由 `PetSpecialization` 101 个场景验证。统一许可、事件来源、准备归属、Start 后一次提示及其存档由 `InteractionProtection` 156 个场景验证，后者同时可运行真实 net9.0 Harmony 模式。完整游戏的原版/RJW 接近、地点选择、动画和结算仍需实机确认。
