# 子宫开发第 1 批原生专项

2026-10-23：**42／42 通过**。目标为 RimWorld **1.7.4985 rev1269**，游戏程序集 MVID `d368493b52bd4740bc7507c1b0386895`。不启动游戏，不改安装副本或用户配置。

通过 ProjectReference 编译当前生产工程，实际执行生产 XML 的原版加载、Hediff 创建／添加／合并／移除、原版 `StatPart_FertilityByHediffs.TransformValue`、健康缓存通知，以及 Scribe 的保存、交叉引用和 PostLoadInit。测试没有复制开发进度、生育状态或合并算法。

覆盖：无 Hediff 的旧身体、躯干挂载与初始 1% 存续、缺躯干拒绝、各未完成进度、满级默认关闭、两态切换与重复请求、不同角色状态隔离、永久泌乳自然叠加、隐藏的原版／RJW 妊娠拒绝关闭、已有妊娠不变、移除后不恢复、重复添加不相加、异常值、四种状态的真实保存读档、缺字段默认关闭、重复存档记录和无效挂载清理。

宿主只构造本批所需身体与 Def；图形资源、完整健康状态判定及能力脏通知被隔离。保留真实 Notify_HediffChanged 和 DirtyCache。RJW 妊娠登记取自真实 Humanlike／Bestial 类的关联属性，避免独立 CLR 扫描其旧版 UI 类型；实际 IsPregnant／KnownPregnancies／HasHediff 查询继续执行依赖原代码。测试没有执行 RJW 的完整启动、受孕或分娩。

这不证明完整模组环境中的健康页显示、真实身份流转、完整游戏存档或开关 UI 已通过实机验收。本批无手术、成长、孕期缩短和共用身体管理栏；这些按后续批次验证。

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

原生日志与隔离 Scribe 文件写入 `.builds/uterus-development-batch1/`。测试构建会重建 1.7 生产 DLL；需要更新构建来源记录时，再运行 `Scripts/Build-Mod.ps1`，不要把 DLL 哈希不一致作为通过。
