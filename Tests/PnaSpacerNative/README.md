# 太空 PNA 武器原生 1.7 专项

直接链接生产太空处理器、专用近战 Verb、两处动作选择补丁、效力解析、剂量及品质规则，并引用本机真实游戏 DLL。当前 **25/25**，已登记进 [Test-All](../../scripts/Test-All.ps1)。

真实护甲随机／取整、减伤转钝、单次判甲、衣甲磨损调用、低于 1 的纳米近战类型、正常／显式近战动作优先、不可用／普通武器放行及效力冲突均执行生产代码。属性缓存、最终健康写入与 SSC 身份／暴露仅提供边界；完整真实 PNA 状态与身份回调由 [PnaLoad](../PnaLoad/README.md) 执行。上游吸收测试运行原版 TakeDamage 配合吸收回调边界，未模拟整张地图或实际护盾装置。

运行参数与品质专项相同，需要实际 1.7 GameRoot、.NET 10 SDK 及测试用 net9.0 Harmony；不把游戏依赖 DLL 复制进模组。

```powershell
& '<.NET 10 SDK 的 dotnet.exe>' run --project Tests/PnaSpacerNative/PnaSpacerNative.csproj -c Release `
  -p:GameRoot='<本机 RimWorld 1.7 目录>' `
  -p:HarmonyAssemblyPath='<测试用 net9.0 0Harmony.dll>' -- '<仓库目录>' '<本机 RimWorld 1.7 目录>'
```

构建／源码身份、执行结果与后续游戏验证边界见[步骤一记录](../../Docs/Development/Verification/SSC太空武器步骤一实现与测试.md)。本专项使用内存隔离定义，不创建正式游戏武器 XML。
