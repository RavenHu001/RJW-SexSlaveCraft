# PNA 武器攻击与受药视觉专项

直接编译生产 `SSCPnaWeaponEffectsUtility`、`Verb_MeleeAttackSSCPna`、`Mote_SSCPnaStab` 及纯数学工具，使用 .NET 9 宿主隔离原版攻击入口和 Fleck／Mote 输出。运行：

```powershell
dotnet run --project Tests/PnaWeaponEffects/PnaWeaponEffects.csproj -- <仓库绝对路径>
```

36 项覆盖攻击成功／刺空、忙碌／离图、原返回值与异常传播、目标销毁后的坐标快照、各方向与非法坐标、刺芒前伸及固定尾部、后期收细、Mote 饱和与销毁边界、心形尺寸／位置／上浮、显示门槛、独立调用及 XML 接线。边界不模拟原版命中概率、GPU、着色器或真实地图生命周期；实际颜色、观感和性能须由真实 1.7 游戏验证。

药效是否成功、满负荷有效输入、锁链活化、主人免疫、多部位传播及嵌套去重由 `Tests/PnaLoad/WeaponEffectTests.cs` 直接执行生产伤害与药效代码验证；该宿主只替换显示函数为请求记录器，不复制施药判断。
