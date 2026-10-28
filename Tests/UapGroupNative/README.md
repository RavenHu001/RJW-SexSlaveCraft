# UAP 手动群交原生兼容专项

仅 1.7，登记 **18 项 Core**，另有不加载 UAP 的独立 **1 项**变体。使用完整 SSC 候选、实际 RJW／UAP／游戏 Harmony 3；不启动游戏、不改安装件。

生产被测范围包括手动请求来源、实际 UAP session 构造、接收方准备方向、原许可与豁免、真实双方驱动的步骤构建、等待提交、启动返回值、精确清理、旧回调、人格及任务失效、一次性结算凭据、读档取消和既有计时观察共存。三人无床与四人床目标分别保留代表，避免人数×场地×许可全表。

独立宿主隔离 UI、地图 getter、任务调度、物理预约、解剖部位及音效／渲染服务。匹配成功由方法体用真实 out 引用写入固定有效的实际 UAP 计划对象，不能用 Prefix 修改 `__args`；失败另执行真实原 matcher 的早期拒绝路径，核对原原因不被清空。`Job.MakeDriver`、原预约方法及生产守卫、UAP `MakeNewToils`／原版 `SetupToils` 仍执行。推进边界只执行明确指定的测试动作，不执行完整 RJW Start 身体流程。测试不证明完整动画、体液、物品消耗或整场存读档表现。RJW 首次静态定义预热缺少无关 Def 的诊断不作为机制成功证据，相关实际行为须实机确认。

到齐后初始化用例执行真实 RJW `Start` 包装、RAF 后置、自动匹配及 `PawnTest_RJWInitiator`，只用边界替身隔离 RJW 身体效果和最终图形播放。先预热上层 `Start`，再安装对 RAF 后置自身的补丁，不通过重建上层包装掩盖调用问题。核对本场不做重复自动匹配、其他后置照常执行、未接管任务保留原自动匹配，以及 UAP 原命名桥接把选定动画和人物顺序送到播放入口。

从仓库根目录构建隔离候选并运行：

```powershell
$uapRepo = (Get-Location).Path
$uapInfo = Get-Content 1.7/BUILD-INFO.json -Raw | ConvertFrom-Json
$uapGame = Split-Path $uapInfo.gameAssemblyPath -Parent
$uapSdk = Join-Path $uapRepo '.builds/version-layout/sdk/dotnet/dotnet.exe'
$uapCandidate = Join-Path $uapRepo '.builds/uap-group/candidate'
$uapHost = Join-Path $uapRepo '.builds/uap-group/UapGroupNative'
$uapProperties = @("-p:GameRoot=$uapGame")
foreach ($reference in $uapInfo.references) { $uapProperties += "-p:$($reference.name)=$($reference.path)" }
$env:DOTNET_CLI_HOME = Join-Path $uapRepo '.builds/dotnet-cli-home'
& $uapSdk build Sexslavecraft/SexSlaveCraft_1.7.csproj -c Release @uapProperties "-p:OutputPath=$uapCandidate/"
if ($LASTEXITCODE -ne 0) { throw 'Candidate build failed' }
& $uapSdk build Tests/UapGroupNative/UapGroupNative.csproj -c Release @uapProperties "-p:SscAssemblyPath=$uapCandidate/Sexslavecraft.dll" "-p:OutputPath=$uapHost/"
if ($LASTEXITCODE -ne 0) { throw 'Native host build failed' }
& $uapSdk "$uapHost/UapGroupNative.dll" $uapRepo $uapGame
if ($LASTEXITCODE -ne 0) { throw 'Native cases failed' }
& $uapSdk "$uapHost/UapGroupNative.dll" $uapRepo $uapGame --no-uap
if ($LASTEXITCODE -ne 0) { throw 'UAP-absent variant failed' }
```

标准专项为18项，包含真实窗口的成功／SSC拒绝原因、UAP原匹配失败原因、到齐后初始化与动画选择、非GUI请求不被接管、池化普通任务不被冻结，以及旧回调不能操作替换会话的边界。独立变体明确改变装载环境，结果不冒充标准18项。统一入口登记的是标准套件；无需为此运行整仓 All。

[实施沿革、实机反馈与适用边界](../../Docs/Development/Completed/UAP手动群交兼容与许可豁免实现与验证.md)
