# 原始高潮推进采样专项

2026-10-27 第三批兼容回归 **57／57**：改为引用实际完整候选与测试友元，不再把三份采样源码复制编入宿主。当前RJW6.2.2、动画／UAP及游戏Harmony3实际桥接、有效排序与门槛通过；五处原始写回及共享样本语义保持，见[第三批记录](../../Docs/Development/Verification/高潮敏感度第三批统一推进与余数恢复实现与验证.md)。

仅 1.7，使用实际游戏／RJW／动画／UAP DLL、游戏 Harmony 3 和实际完整生产候选DLL，目标 net10.0。当前 **57 项核心用例**；测试不启动游戏、不改运行 Def、不修改／同步游戏安装件。

默认读取 `1.7/BUILD-INFO.json` 中的引用，可用 `SSC_TEST_BUILD_INFO_PATH` 显式指定隔离引用JSON；正式记录对应原产物，不因本机RJW更新而重写。依赖实际编译所用引用及本机 UAP `Mods/RJW_动画_UAP终极动画包/Assemblies/UAP_Animations.dll`。独立宿主复用 `Tests/CultureNative/UnityCoreShim` 类型转发，仅用于标准 CLR 的旧 Unity 名称解析；不随 mod 打包。

从仓库根目录执行（便携 SDK 须已存在）：

```powershell
$repo = (Get-Location).Path
$out = Join-Path $repo '.builds/orgasm-progress-batch1/native/'
$referenceInfo = if ($env:SSC_TEST_BUILD_INFO_PATH) { $env:SSC_TEST_BUILD_INFO_PATH } else { '1.7/BUILD-INFO.json' }
$info = Get-Content -Encoding UTF8 $referenceInfo -Raw | ConvertFrom-Json
$game = Split-Path -Parent $info.gameAssemblyPath
$props = @("-p:GameRoot=$game", "-p:OutputPath=$out")
foreach ($reference in $info.references) { $props += "-p:$($reference.name)=$($reference.path)" }
# 验证已有隔离候选时增加 -p:SscAssemblyPath=<候选绝对路径>；省略时ProjectReference会构建生产工程。
& '.builds/version-layout/sdk/dotnet/dotnet.exe' build Tests/OrgasmProgressNative/OrgasmProgressNative.csproj -c Release @props
if ($LASTEXITCODE -ne 0) { throw 'Native build failed' }
& '.builds/version-layout/sdk/dotnet/dotnet.exe' (Join-Path $out 'OrgasmProgressNative.dll') $repo $game
if ($LASTEXITCODE -ne 0) { throw 'Native tests failed' }
```

已注册到 `Test-All.ps1`，按当前测试范围规则属于 Core，使用游戏 Harmony 引用，不依赖另一份 net9 测试 Harmony。专项成功不能替代 All 发布门禁。

验证真实五处写回及实际调用，区分原始量 0／1／非 1、宽整数与明确阈值来源；覆盖同 tick 多操作、同驱动重入、样本共享、周期／任务／SexProps 变化、旧回调与重建记录隔离、加载期、原异常保留、IL 原子回退与分支绕过拒绝、Prefix 方法 detour、有效排序和依赖破环、补丁集合变化、诊断重放与稳态分配。无地图服务在宿主内明确隔离，不测试完整音效／体液等副作用，本套57项保持原始采样契约，敏感度与余数恢复由UterusDevelopmentNative的第三批原生专项覆盖。

[实现与验证边界](../../Docs/Development/Verification/高潮推进采样第一批实现与验证.md) · [完整规划](../../Docs/Design/RJW个人高潮敏感度调节开发规划.md)
