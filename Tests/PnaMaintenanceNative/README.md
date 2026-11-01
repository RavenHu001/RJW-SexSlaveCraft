# PNA阶段维持原生专项

目标为1.7，引用实际完整生产候选和游戏Harmony。12项Core用例覆盖：实际XML补丁与驱动解析、Def阶段边界、原版排程优先级、忙碌／强制／队列守卫、缺药回退、原上下文撤权、有效锁链选择、候选到实际驱动交接、对象池与设置世代、消费收尾、运行驱动Scribe、旧档及原版Hediff复制。

不启动游戏。地图寻路查询、普通工作候选、库存整图通知、任务取消的地图清理、最终摄入副作用和图形服务在宿主边界隔离；阶段判断、锁链设置、任务构建、原生工作优先级、父类服用Toil构建、实际驱动序列化与跨引用恢复使用生产代码。Scribe测试将既有角色／锁链作为外部世界注册，不能替代完整游戏存档及地图实测。最终消费边界只写入代表性负荷以验证达标后的收尾，不冒充原生Thing.Ingested全路径；既有PNA摄入／衰减由PnaLoad关联回归覆盖。

```powershell
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.builds/dotnet-cli-home'
$repo = (Get-Location).Path
$info = Get-Content '1.7/BUILD-INFO.json' -Raw | ConvertFrom-Json
$game = Split-Path -Parent $info.gameAssemblyPath
$candidate = Join-Path $repo '.builds/pna-maintenance/candidate/Sexslavecraft.dll'
$out = Join-Path $repo '.builds/pna-maintenance/host/'
$props = @("-p:GameRoot=$game", "-p:SscAssemblyPath=$candidate", "-p:OutputPath=$out")
foreach ($reference in $info.references) { $props += "-p:$($reference.name)=$($reference.path)" }
& '.builds/version-layout/sdk/dotnet/dotnet.exe' build Tests/PnaMaintenanceNative/PnaMaintenanceNative.csproj -c Release @props
if ($LASTEXITCODE -ne 0) { throw 'Native build failed' }
& '.builds/version-layout/sdk/dotnet/dotnet.exe' (Join-Path $out 'PnaMaintenanceNative.dll') $repo $game
if ($LASTEXITCODE -ne 0) { throw 'Native tests failed' }
```

先构建指定候选；不传`SscAssemblyPath`时会沿ProjectReference构建生产工程。已登记到统一runner及Core清单，专项通过不代替整仓All或游戏验收。
