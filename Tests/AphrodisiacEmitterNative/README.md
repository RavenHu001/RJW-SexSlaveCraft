# 发烟器原生专项

执行真实1.7生产组件、原版PowerTrader／PowerNet、Hediff与Scribe。宿主读取本机RJW药效XML；外部阶段／上限变更用代表性Def调整检验，不能把已知文化四档表当作永久预期。当前14项Core覆盖包括XML组件解析、实例耗电、直接开关及滑条提交、缺电分配、范围／房间、共享药效、Tick去重、回调中失效及保存。提交前复核新增2项PostMake回调边界，并扩展原用例检查停机同Tick不再调度烟雾；旧候选3项失败，修订后14／14通过。

运行参数为 `repositoryRoot gameRoot`。项目支持 `-p:SscAssemblyPath=<隔离候选DLL>`；省略时沿其他原生宿主惯例引用生产工程。构建需 `GameRoot`、测试 `HarmonyAssemblyPath` 和生产依赖引用；只读定位沿仓库的BUILD-INFO，不复制或修改游戏程序集。

该套件已登记到[统一runner](../../Scripts/Test-All.ps1)与[TestProfiles](../TestProfiles.json)，Core执行全部机制，Special没有专项用例。单套All结果不能冒充整仓All。

边界：图形／声音、房间构建与整场健康通知下游隔离。房间用真实Room对象映射，电网与Hediff算法不替换；GUI验证执行真实回调与滑条setter，不模拟鼠标绘制。完整世界存档、实际房间拓扑、文化等组合、显示与性能需实机。

本次结果与部署状态见[实现与验证记录](../../Docs/Development/Verification/单格电力催情发烟器实现与验证.md)。
