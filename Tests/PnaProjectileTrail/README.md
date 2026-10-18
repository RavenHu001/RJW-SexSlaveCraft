# PNA 投射物流星尾迹专项回归

运行：`dotnet run --project Tests/PnaProjectileTrail/PnaProjectileTrail.csproj -- <仓库绝对路径>`。

直接链接生产 `CompSSCPnaTrail.cs`；边界替身仅提供原版移动回调和 Fleck 输出收集，检查方向、片段衔接、三种贴图尺寸的箭尾避让、调用顺序、生命周期、地图／镜头切换、损坏输入及固定生成预算。不模拟 Bullet 命中、原生粒子曲线或 GPU 绘制。真实接口兼容由当前 1.7 Release 编译检查，最终连续性、可见度与性能需游戏实测。
