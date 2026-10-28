# PNA 投射物流星尾迹专项回归

2026-10-28：[本轮精简](../../Docs/Development/Completed/测试199项与独立文案452项精简实现与验证.md)29→25，本次Special25／25。删普通尺寸及重复非法数值，保留上限、有限性、正数、传送、地图及销毁等边界。未跑整仓All，下方结果保留历史时点。

2026-10-19：本专项已登记到 `scripts/Test-All.ps1`，工业步骤一再次执行，结果见[本批记录](../../Docs/Development/Completed/SSC工业武器步骤一实现与测试.md)。

当前首批 **30/30** 已通过，三类弹丸尾迹与箭尾避让已获维护者实机认可，见[实现与验收](../../Docs/Development/Completed/SSC投射物紫色拖尾实现与测试.md)。此数为该批实际执行结果，本次文档整理未重跑；未量化实机 FPS。

运行：`dotnet run --project Tests/PnaProjectileTrail/PnaProjectileTrail.csproj -- <仓库绝对路径>`。

直接链接生产 `CompSSCPnaTrail.cs`；边界替身仅提供原版移动回调和 Fleck 输出收集，检查方向、片段衔接、三种贴图尺寸的箭尾避让、调用顺序、生命周期、地图／镜头切换、损坏输入及固定生成预算。不模拟 Bullet 命中、原生粒子曲线或 GPU 绘制。真实接口兼容由当前 1.7 Release 编译检查，最终连续性、可见度与性能需游戏实测。
