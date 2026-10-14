# PNA 负荷与 P2 渴求／来源回归

日期：2026-10-14。状态：P1 已获维护者实机有效确认；P2 已实现为开发测试批，实机验收待反馈。

宿主直接编译生产数学、共享输入、负荷／条件恢复组件、摄入 Doer、枪械 Worker、初始化迁移、五个 Harmony 补丁与调试动作。使用本机 **net9.0** Harmony；游戏使用的 .NET Framework Harmony 不能用于这个宿主。

```powershell
dotnet run --project Tests/PnaLoad/PnaLoad.csproj -c Release `
  -p:HarmonyAssemblyPath='C:\Dependencies\Harmony\net9.0\0Harmony.dll' -- .
```

已登记到 `Scripts/Test-All.ps1`。同严重度转换调整的专项 78/78（首版为 67/67），通过记录见[本批实现与测试说明](../../Docs/Development/Completed/PNA机制重置P1实现与测试.md)。

普通五档数值批仅更新本套件现有 XML 断言，该批未改生产 C# 或 DLL。后续 P2 在原结构扩展依赖与来源，不保留动态属性插值、缓存或其测试。新数值说明见[五档调整](../../Docs/Development/Verification/普通PNA五档效果调整.md)，P2 记录见[P2 实现与测试](../../Docs/Development/Verification/PNA机制重置P2实现与测试.md)。

此前最高 ×6 数值批专项 78/78、全量 29 套 1876/1876，日志见 `.builds/pna-xml-stages/validation/`。后续最高 ×4 与第三档起视听惩罚批仅做 XML 检查，更新上述既有断言供未来回归使用，本次未运行套件。

覆盖异常剂量、无效身体和保存阶段、精确初始剂量、同 tick 多次输入、上限截断、分段清除、有效 delta 分批和暂停、输入前结算半批、只读显示、低频稳态和首次满值场数；双向转换原实例／严重度／半批时间保留、性奴中性载体、实际链与身份／恶堕区别、零阶段有效链、无效／重复链、绑定及外部 AddHediff、旧 PNA 药物的有链保护、健康回调中绑定／移除、旧满与重复实例、半批时间保存、四语与镜像、真实 Def 属性。

P2 增加未满不随机成瘾、满值与截断、重复满不重置、复发与通知、回调失效、需求生成／补充、多份实际摄入和修正一次、条件恢复与有效 delta、旧速率字段、重复旧依赖与非法值、枪械传播去重／同 tick 独立命中／重入、抗性／偏转／实际链以及来源 XML 断言。人格最小身体归属适配由 PersonalityTraits 运行实际采集／植入代码核验。

引擎替身只模拟接口、调度、字段读写和回调边界。真实 Harmony 注册执行于这些目标替身上；不证明游戏中第三方补丁排序、完整能力和属性计算、Unity 界面、地图／旅队 AI、真实伤害传播或 Scribe 全对象恢复。克隆／完整空壳规则及第三方任意外部来源仍未宣称完成。最新结果以 `.builds/pna-p2/validation/summary.json` 为准，不复用上方 P1 历史通过数。
