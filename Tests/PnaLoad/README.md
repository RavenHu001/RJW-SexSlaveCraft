# PNA P1 负荷基础回归

日期：2026-10-14。状态：P1 已于 2026-10-14 获维护者实机有效确认，普通负荷效果强化另行验收。

宿主直接编译生产数学、共享输入、负荷组件、初始化迁移、五个 Harmony 补丁与调试动作。使用本机 **net9.0** Harmony；游戏使用的 .NET Framework Harmony 不能用于这个宿主。

```powershell
dotnet run --project Tests/PnaLoad/PnaLoad.csproj -c Release `
  -p:HarmonyAssemblyPath='C:\Dependencies\Harmony\net9.0\0Harmony.dll' -- .
```

已登记到 `Scripts/Test-All.ps1`。同严重度转换调整的专项 78/78（首版为 67/67），通过记录见[本批实现与测试说明](../../Docs/Development/Completed/PNA机制重置P1实现与测试.md)。

覆盖异常剂量、无效身体和保存阶段、精确初始剂量、同 tick 多次输入、上限截断、分段清除、有效 delta 分批和暂停、输入前结算半批、只读显示、低频稳态和首次满值场数；双向转换原实例／严重度／半批时间保留、性奴中性载体、实际链与身份／恶堕区别、零阶段有效链、无效／重复链、绑定及外部 AddHediff、旧 PNA 药物的有链保护、健康回调中绑定／移除、旧满与重复实例、半批时间保存、四语与镜像、真实 Def 属性。

引擎替身只模拟接口、调度、字段读写和回调边界。真实 Harmony 注册执行于这些目标替身上；不证明游戏中第三方补丁排序、完整能力和属性计算、Unity 界面或 Scribe 全对象恢复。克隆／凝胶、药物剂量替换、枪械事件认领与普通对象确定性成瘾属于 P2／P3，不能据本套件声称完成。
