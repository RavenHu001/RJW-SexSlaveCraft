# 猫终极单一技能回归

在仓库根目录运行：

```powershell
dotnet restore Tests/PetCatAbility/PetCatAbility.csproj --configfile Tests/NuGet.Config
dotnet run --project Tests/PetCatAbility/PetCatAbility.csproj --configuration Release --no-restore -- .
```

套件不依赖NuGet包，已经注册到 `Scripts/Test-All.ps1`。直接链接真实 `Ability_PetCatComfort`、`CompAbilityEffect_PetCatComfort`、`PetCatAbilityUtility`、`HediffComp_GiveAbility`，以及宠物公共规则、标签工具和特化组件/健康对账，不复制技能资格、状态分支或人格冷却迁移逻辑。

27个用例组验证：

- XML单一效果、60000 tick冷却、6格视线、2秒准备、征召与未征召入口；鼓舞30000 tick计时、工作速度倍率1.10、依附Hediff的8点即时心情，四语及源码语言镜像。
- 普通完成不授予，猫终极有效严重度授予；跨方向、留空、SSC身份、绑定及研究不影响终极成果。死亡、销毁、倒地、未生成、精神状态或空壳不能施放。
- 同图玩家阵营其他人形目标、范围与视线的运行时复查；正常分支需要心情，精神状态分支无心情或目标倒地仍可恢复；所有当前精神状态和来源均通过恢复入口，不删除紧张性昏迷疾病状态。
- 预热开始与生效之间正常/精神状态互换、精神实例替换、恢复回调产生新状态，实际只执行一个分支；生产组件 `Valid`/`CanApplyOn` 与能力执行前保护共同拒绝失格目标，不进入原版预扣冷却。
- 恢复与鼓舞共用同一能力冷却，效果不因原版已先扣冷却而失效；多猫刷新原激励实例的计时，清理重复鼓舞而不叠加。损坏定义在执行前拒绝，实际新状态缺计时组件时不留下永久鼓舞。
- 直接效果入口重新检查资格；不改变培养进度、各方向历史、亲昵冷却或其他技能。真实授予/移除组件只维护一份能力，跨方向同步及移除重复终极保留实例与冷却，最后终极移除后撤销。
- 冷却捕获为绝对截止tick；恢复立即授予并扣除凝胶存放时间，旧字段和过期时间就绪；无终极恢复清孤儿技能。抽取辅助清源猫标签、猫方向/历史与能力，保留其他方向、成果及技能。

引擎宿主只提供可观察边界：原版Ability激活按已核验顺序先启动冷却再分发效果，MentalState记录恢复调用并允许恢复回调，Hediff容器执行真实能力授予组件的移除回调。宿主不模拟真实寻路、任务预热、逐tick倒计时、原版思绪/属性计算或Ability的Scribe。

完整人格快照、加工 `CopyFrom`、凝胶字段读写和提取/植入链路由 `PersonalityTraits` 执行真实生产入口另行验证。辅助函数边界测试不等于完整真实Scribe。技能UI、实际精神状态恢复副作用、心情/工作倍率、存读档及实机迁移仍需游戏验收。
