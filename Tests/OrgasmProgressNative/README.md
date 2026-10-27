# 原始高潮推进采样专项

仅 1.7，使用实际游戏／RJW／动画／UAP DLL、游戏 Harmony 3 和生产采样源码，目标 net10.0。当前 **57 项核心用例**；测试不启动游戏、不改运行 Def、不修改／同步游戏安装件。

依赖当前 `1.7/BUILD-INFO.json` 中的真实引用及本机 UAP `Mods/RJW_动画_UAP终极动画包/Assemblies/UAP_Animations.dll`。独立宿主复用 `Tests/CultureNative/UnityCoreShim` 类型转发，仅用于标准 CLR 的旧 Unity 名称解析；不随 mod 打包。

从仓库根目录执行（便携 SDK 须已存在）：

```powershell
$repo = (Get-Location).Path
$out = Join-Path $repo '.builds/orgasm-progress-batch1/native/'
$info = Get-Content -Encoding UTF8 '1.7/BUILD-INFO.json' -Raw | ConvertFrom-Json
$game = Split-Path -Parent $info.gameAssemblyPath
$props = @("-p:GameRoot=$game", "-p:OutputPath=$out")
foreach ($reference in $info.references) { $props += "-p:$($reference.name)=$($reference.path)" }
& '.builds/version-layout/sdk/dotnet/dotnet.exe' build Tests/OrgasmProgressNative/OrgasmProgressNative.csproj -c Release @props
if ($LASTEXITCODE -ne 0) { throw 'Native build failed' }
& '.builds/version-layout/sdk/dotnet/dotnet.exe' (Join-Path $out 'OrgasmProgressNative.dll') $repo $game
if ($LASTEXITCODE -ne 0) { throw 'Native tests failed' }
```

已注册到 `Test-All.ps1`，按当前测试范围规则属于 Core，使用游戏 Harmony 引用，不依赖另一份 net9 测试 Harmony。专项成功不能替代 All 发布门禁。

验证真实五处写回及实际调用，区分原始量 0／1／非 1、宽整数与明确阈值来源；覆盖同 tick 多操作、同驱动重入、样本共享、周期／任务／SexProps 变化、旧回调与重建记录隔离、加载期、原异常保留、IL 原子回退与分支绕过拒绝、Prefix 方法 detour、有效排序和依赖破环、补丁集合变化、诊断重放与稳态分配。无地图服务在宿主内明确隔离，不测试完整音效／体液等副作用，也不验证尚未实现的敏感度或保存余数。

[实现与验证边界](../../Docs/Development/Verification/高潮推进采样第一批实现与验证.md) · [完整规划](../../Docs/Design/RJW个人高潮敏感度调节开发规划.md)
