# PNA阶段维持原生专项

目标为1.7，引用实际完整生产候选和游戏Harmony。19项Core：原有地图12项覆盖实际XML补丁与驱动解析、Def阶段边界、排程优先级、忙碌／强制／队列守卫、缺药回退、原上下文撤权、有效锁链选择、运行驱动交接、对象池与设置世代、消费收尾、原生Scribe、旧档及Hediff复制；新增旅队7项覆盖真实Harmony与120tick节拍、自有／共享驮畜库存、无效栈和嵌套查询、原政策先行与独立性、失格／成员转换、原上下文复查及重入／异常释放。

不启动游戏。地图寻路查询、普通工作候选、库存整图通知、任务取消的地图清理、地图最终摄入副作用和图形服务在宿主边界隔离；阶段判断、锁链设置、任务构建、原生工作优先级、父类服用Toil构建、实际驱动序列化与跨引用恢复使用生产代码。Scribe测试将既有角色／锁链作为外部世界注册，不能替代完整游戏存档及地图实测。地图最终消费边界只写入代表性负荷验证收尾；既有PNA摄入／衰减由PnaLoad关联回归覆盖。

旅队新增用例执行真实生产补丁、原版个人政策检查、容器查询、`IngestDrug`及`Thing.Ingested`，使用实际原液XML的PNA Doer验证负荷、每次扣一份及最后一份的销毁／容器清理／缓存刷新。夜休时区与整场Stat／地图预约服务隔离；身体记录预置为低于目标且高于移除线，不冒充完整世界存档、无记录健康初始化、所有药物组件或实机通过。实施与实机验收记录见[旅队记录](../../Docs/Development/Completed/PNA阶段维持/旅队PNA阶段维持实现与验证.md)。

```powershell
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.builds/dotnet-cli-home'
$repo = (Get-Location).Path
$info = Get-Content '1.7/BUILD-INFO.json' -Raw | ConvertFrom-Json
$game = Split-Path -Parent $info.gameAssemblyPath
$candidate = Join-Path $repo '.builds/pna-caravan/candidate/Sexslavecraft.dll'
$out = Join-Path $repo '.builds/pna-caravan/host/'
$props = @("-p:GameRoot=$game", "-p:SscAssemblyPath=$candidate", "-p:OutputPath=$out")
foreach ($reference in $info.references) { $props += "-p:$($reference.name)=$($reference.path)" }
& '.builds/version-layout/sdk/dotnet/dotnet.exe' build Tests/PnaMaintenanceNative/PnaMaintenanceNative.csproj -c Release @props
if ($LASTEXITCODE -ne 0) { throw 'Native build failed' }
& '.builds/version-layout/sdk/dotnet/dotnet.exe' (Join-Path $out 'PnaMaintenanceNative.dll') $repo $game
if ($LASTEXITCODE -ne 0) { throw 'Native tests failed' }
```

先构建指定候选；不传`SscAssemblyPath`时会沿ProjectReference构建生产工程。已登记到统一runner及Core清单，专项通过不代替整仓All或游戏验收。
