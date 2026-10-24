# 子宫开发第 1～2 批原生专项

2026-10-23：**100／100 通过**，包括第 1 批原有 42 项、第 2 批原有 43 项及复查修复新增 15 项。目标为 RimWorld **1.7.4985 rev1269**，游戏程序集 MVID `d368493b52bd4740bc7507c1b0386895`。不启动游戏，不改安装副本或用户配置。

通过 ProjectReference 编译当前生产工程，实际执行生产 XML 的原版加载、Hediff 创建／添加／合并／移除、原版 `StatPart_FertilityByHediffs.TransformValue`、健康缓存通知，以及 Scribe 的保存、交叉引用和 PostLoadInit。测试没有复制开发进度、生育状态或合并算法。

覆盖：无 Hediff 的旧身体、躯干挂载与初始 1% 存续、缺躯干拒绝、各未完成进度、满级默认关闭、两态切换与重复请求、不同角色状态隔离、永久泌乳自然叠加、隐藏的原版／RJW 妊娠拒绝关闭、已有妊娠不变、移除后不恢复、重复添加不相加、异常值、四种状态的真实保存读档、缺字段默认关闭、重复存档记录和无效挂载清理。

宿主只构造本批所需身体与 Def；图形资源、完整健康状态判定及能力脏通知被隔离。保留真实 Notify_HediffChanged 和 DirtyCache。RJW 妊娠登记取自真实 Humanlike／Bestial 类的关联属性，避免独立 CLR 扫描其旧版 UI 类型；实际 IsPregnant／KnownPregnancies／HasHediff 查询继续执行依赖原代码。测试没有执行 RJW 的完整启动、受孕或分娩。

第 2 批另从本机原版 `SurgeryFlesh`／`SurgeryOutcomeBase` 与生产研究、手术 XML 执行原版继承和交叉引用。实际运行生产资格报告、当前器官检查、成年判据、种族注入器、原版医疗账单及生产接单补丁。覆盖双前置、药物范围、重复启动拒绝、隐藏妊娠、医术 9／10、原版接单限制、等待期间身体变化，以及成功落地前再复查。

复查修复新增 15 项：实际调用原版配方技能判断，验证受控医护机械体固定技能 10 可接单并完成手术，技能 9、未受控、未启用医疗工作、普通角色缺技能和技能禁用仍拒绝。仅将夹具机械体的监管状态作为环境前提提供，不测试地图中的机械师监管系统。

菜单回归实际执行原版 `HealthCardUtility.DrawMedOperationsTab` 的菜单工厂及 `GenerateSurgeryOption`，不复制选项过滤或标签拼接。隔离账单绘制、字体测量并视为库存齐全，验证妊娠、身份不符、缺器官、已开发及原版 Adult 的 17 岁身体均产生带中文原因的不可点击项，且接单／结果继续拒绝；合格身体只有一个可用选项，无躯干没有伪造目标，错误部位继续拒绝。布尔接口只负责身体候选，完整资格报告不能用布尔值替代。

成功／失败通过控制质量稳定选支，`CheckSurgeryFail`、`GetOutcome` 及原版结果 `Apply` 继续执行。失败损伤只记录原版 `GiveRandomSurgeryInjuries` 请求，不执行完整伤害计算；信件、消息、事迹及地图预定清理隔离。原版直接死亡结果在概率 0 时实际调用并拒绝执行。实际执行 `CalculateIngredients` → `ConsumeIngredients` → `Bill_Medical.Notify_IterationCompleted`，验证 5 份浓缩 PNA 与 6 份混合药物在成功和失败时只消费一次、账单完成删除，且只有成功创建 1% 躯干记录。

药物质量单独执行原版 `SurgeryOutcomeComp_MedicineQuality`，其曲线来自原版结果定义，医疗效力取自当前游戏药物 XML。草药／工业药／闪耀药对应质量因子 0.88／1／1.18；三份草药加三份闪耀药为 1.03。上述是药物一项的质量乘数，不是最终成功率。

独立宿主使用可独立运行的 Harmony 2，生产游戏引用 Harmony 3；接单补丁通过不含业务逻辑的转发器挂到真实原版方法，避免读取跨版本特性，不复制生产判断。完整游戏中的自动特性扫描与其他补丁组合仍需实机确认。

这不证明完整模组环境中的健康页、研究树排版、实际寻找／搬运材料、麻醉、伤口、完整存档或开关 UI 已通过实机验收。成长、孕期缩短及共用身体管理栏仍属第 3～5 批。

已登记到 `Scripts/Test-All.ps1`。单独验证使用 .NET 10 SDK、实际游戏引用及测试侧 net9.0 Harmony；依赖路径可从 `1.7/BUILD-INFO.json` 读取：

```powershell
$repo = $PWD.Path
$info = Get-Content '1.7/BUILD-INFO.json' -Raw | ConvertFrom-Json
$sdk = '.builds/version-layout/sdk/dotnet/dotnet.exe'
$arguments = @('build', 'Tests/UterusDevelopmentNative/UterusDevelopmentNative.csproj',
    '-c', 'Release', "-p:GameRoot=$([IO.Path]::GetDirectoryName($info.gameAssemblyPath))",
    '-p:HarmonyAssemblyPath=<测试用 net9.0/0Harmony.dll 的绝对路径>')
foreach ($reference in $info.references) { $arguments += "-p:$($reference.name)=$($reference.path)" }
& $sdk @arguments
if ($LASTEXITCODE -ne 0) { throw '专项构建失败' }
& $sdk 'Tests/UterusDevelopmentNative/bin/Release/net10.0/UterusDevelopmentNative.dll' `
    $repo ([IO.Path]::GetDirectoryName($info.gameAssemblyPath))
```

隔离 Scribe 文件继续写入 `.builds/uterus-development-batch1/`；第 2 批本轮运行日志保存于 `.builds/uterus-development-batch2/native.log`，验证记录见[第 2 批](../../Docs/Development/Verification/子宫开发第2批实现与验证.md)。测试构建会重建 1.7 生产 DLL；需要更新构建来源记录时，再运行 `Scripts/Build-Mod.ps1`，不要把 DLL 哈希不一致作为通过。
