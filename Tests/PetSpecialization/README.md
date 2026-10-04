# 宠物互斥与培养入口回归

在仓库根目录使用 .NET 9 SDK：

```powershell
dotnet restore Tests/PetSpecialization/PetSpecialization.csproj --ignore-failed-sources --source Tests/PetSpecialization
dotnet run --project Tests/PetSpecialization/PetSpecialization.csproj --configuration Release --no-restore
```

套件无包依赖，直接链接生产 `PetSpecializationRules`、`PetSpecializationUtility`、特化组件及健康对账、共享培养入口和共享评分公式，不复制宠物资格规则。

29 个用例组覆盖：

- 猫／狗／兔的全部终极组合与切换、培养矩阵；奶牛及其他方向不受宠物组限制。
- 终极冲突的拒绝原子性，包括未规范化的 NaN／无穷／越界当前进度、历史字典、普通及终极 Hediff、留空标记、繁殖模式、身体奶量和通知副作用。
- 普通方向历史往返、同种终极内部认领、明确留空与旧档健康认领。
- 猫兔玩家入口禁用与合法恢复、旧档培养分离；基础研究和宠物研究仅约束玩家选择。
- 玩家提交时的资格复查，及旧菜单引用的训练组件已被替换时安全拒绝。
- 共享、特色和底层进度入口阻止任意宠物终极继续培养；冲突不创建普通显示标记；非当前历史不授予资格。
- 完成容差、剩余空间、非有限收益、显示严重度不成为经验、外部普通标记导入和实际绑定主人倍率。

引擎替身只提供定义表、Hediff 容器、Pawn 与未修改的其他特化边界。亲昵宿主、人格标签容器仅用于让完整生产宠物工具编译，本套件不验证亲昵任务、终极化配方、人格植入、真实 Scribe 或游戏 UI。上述后续范围需要各自的集成与游戏内验收。
