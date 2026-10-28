# 主人定向性吸引力原生专项

仅1.7，登记 **19项 Core**。加载完整SSC候选、实际本机RJW和游戏Harmony3，不复制生产评分或许可规则，不启动游戏、不改安装件。

实际执行原生 `AttractionUtility.Evaluate`、Preflight、`PreferenceEvaluator`、Finalize、`AppraisalResult` 双侧惰性缓存、`SexAppraiser.FindBestResults`、`CasualHookupAllowedViaSettings`、`FindPartner`、右键 `DoChecks` 与生产解释入口。样本覆盖零偏好早退出、原高合成分不膨胀、单向与目的范围、全局性别硬拒绝、原需求倍率和满预约归零、评分中改绑、解绑／空壳、既有评价缓存撤销与重新取得资格、主人一侧拒绝、仅主人候选的婚恋豁免、成年门槛和实际解释数值。

宿主仅提供必要环境：真实Pawn／训练Comp／健康列表／成年年龄和锁链，固定偏好数值作为评价输入；健康细项、社交好感和原版浪漫系数为受控外部输入，未改这些规则的实现。地图预约人数、可达性、候选扫描、随机发起判定和随机候选抽取终端被隔离；自动入口仍执行原 `FindPartner` 主体、生产候选过滤、真实Appraisal排序和原配对条件。解释使用真实翻译器和生产XML，最终窗口投递被截取，不验证字体／布局。

Finalize预约查询、Casual读取性奴意见及自动入口路径查询分别保留换链／解绑回调，核对已经取得的定向授权不能延续到成功返回。RJW首次静态预热涉及的其他Def和图形诊断在环境准备期隔离，不作为完整RJW资源加载通过的依据。

本套不证明完整地图候选构建、实际发起频率、寻路、任务安装、动画、RJW身体数据初始化、完整世界存读档或所有第三方模组组合。生产不新增保存字段，本套相应不添加无意义的存档字段快照。

从仓库根目录构建隔离候选并运行：

```powershell
$attractionRepo = (Get-Location).Path
$attractionInfo = Get-Content 1.7/BUILD-INFO.json -Raw | ConvertFrom-Json
$attractionGame = Split-Path $attractionInfo.gameAssemblyPath -Parent
$attractionSdk = Join-Path $attractionRepo '.builds/version-layout/sdk/dotnet/dotnet.exe'
$attractionCandidate = Join-Path $attractionRepo '.builds/master-attraction/candidate'
$attractionHost = Join-Path $attractionRepo '.builds/master-attraction/MasterAttractionNative'
$attractionProperties = @("-p:GameRoot=$attractionGame")
foreach ($reference in $attractionInfo.references) { $attractionProperties += "-p:$($reference.name)=$($reference.path)" }
$env:DOTNET_CLI_HOME = Join-Path $attractionRepo '.builds/dotnet-cli-home'
& $attractionSdk build Sexslavecraft/SexSlaveCraft_1.7.csproj -c Release @attractionProperties "-p:OutputPath=$attractionCandidate/"
if ($LASTEXITCODE -ne 0) { throw 'Candidate build failed' }
& $attractionSdk build Tests/MasterAttractionNative/MasterAttractionNative.csproj -c Release @attractionProperties "-p:SscAssemblyPath=$attractionCandidate/Sexslavecraft.dll" "-p:OutputPath=$attractionHost/"
if ($LASTEXITCODE -ne 0) { throw 'Native host build failed' }
& $attractionSdk "$attractionHost/MasterAttractionNative.dll" $attractionRepo $attractionGame
if ($LASTEXITCODE -ne 0) { throw 'Native cases failed' }
```

宿主输出真实加载路径、MVID与SHA256，用于确认执行的是当前隔离候选；本套结果与整仓All、实机反馈和发布门禁分别记录。

2026-10-28实际执行：游戏 `1.7.4990 rev1069`、RJW MVID `83b5bb8057c941b086031c2d1cbbf59c`；候选SHA256 `44538d60d0ba7cd35890c9429ae41bc54e720079364a89fe2af72960155d8e59`，宿主构建零警告／零错误，**19／19通过**。未执行整仓All或实机。

2026-10-28截图反馈修订：原解释回归改为实际执行RJW原Explain及全部分项／原窗口构造，核对原标题和追加SSC结果同时存在。姓名格式化为外部显示输入，颜色缓存显式初始化。修正版19／19，日志 `.builds/master-attraction/feedback-native.log`；新DLL SHA256 `9c08cb5fccc964daf868cf775c896ca2148cdfc35c435b48dfb7b443e3cbf3bf`，以上44538…保留前轮时点。
