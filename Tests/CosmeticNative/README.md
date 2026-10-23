# 纳米催情液医美原生专项

2026-10-23 本轮开发完成，当前专项 56／56，维护者已分批确认功能、最终 Mote 紫雾与预设名称；尚未发布。当前状态先读[完成核对](../../Docs/Development/Completed/纳米催情液医美应用完成核对.md)，下文继续说明测试边界，未将未覆盖的场景补记为实机通过。

面向本机 RimWorld **1.7.4985 rev1269**（MVID `d368493b52bd4740bc7507c1b0386895`）。测试通过 ProjectReference 构建当前生产工程，使用实际游戏类型和生产 XML；不启动游戏，不修改用户配置或安装副本。

统一入口已登记 `CosmeticNative`：

```powershell
pwsh -File Scripts/Test-All.ps1 -GameVersion 1.7 -HarmonyAssemblyPath '<net9.0/0Harmony.dll>'
```

单独构建 `CosmeticNative.csproj` 时传入 `GameRoot`、`HarmonyAssemblyPath` 和 `BUILD-INFO.json` 中的三个生产依赖路径；使用 .NET 10 SDK。构建后执行：

```powershell
dotnet Tests/CosmeticNative/bin/Release/net10.0/CosmeticNative.dll '<repositoryRoot>' '<gameRoot>'
```

56 项覆盖原版 XML 继承／交叉引用、Beauty 四步升级与保守兼容、原版 TraitSet 增删、手术／仪式身份、医术门槛、共用反应三阶段和四种自然恢复、原版权重概率、失效固定失败、原版取料 Job／单地面堆缺额预检／背包分堆消费／返回站位／主持者放下物资／原版剩余小时标签、缺料后的原版取消防重、开场原版拒绝透传／无候选与缺料区别／指定主持者不回退、DLC 门控及四语键／参数，以及配对站位与紫雾。

无界面宿主隔离了图形、通知、完整地图寻路／禁止／可预留判定、库存地图通知及落地放置和 Lord 移除的地图／信号投递；上述判定的生产调用保留为原版 API，测试只注入边界结果。材料执行真实原版取料 JobGiver 生成任务、背包／地面列表、足量判断、`ThingOwner.Take`／`SplitOff`／`Destroy`；落地放置隔离，但原版 Multi／DropThing 的角色范围和数量仍执行；取消执行原版 `ApplyOutcome` 的 `ended`／`cancelled` 及 `Lord.Cleanup`，通过实际 `Transition.Execute`／`GotoToil` 与测试用职责探针验证 postAction 清理后不再分配职责。技能禁用例通过真实 `SkillDef.IsDisabled` 验证，不替换 `TotallyDisabled`。

这不覆盖游戏内清单刷新、真实医疗麻醉／政策／扣药、完整仪式 AI 站位、地图预留与搬运竞争、整场存读档。实机清单见[实施与验收](../../Docs/Development/Completed/纳米催情液医美实现与测试.md)。

开场专项额外隔离原版全局活动／行星预检、地图主持者候选列表和仪式目标可达性；保留生产开场分支、角色资格、材料报告及原版背包数量处理。原版拒绝透传用显式拒绝结果验证，不将这个局部宿主当作完整原版开场环境。

站位／视效新增 6 项：四向祭坛选点、格子默认朝南／障碍镜像／空间不足／地图边界、两种角色迭代次序下原版三阶段位置缓存和职责／观众区域、原版 Idle 朝向、正式阶段限定的烟雾 Def 继承与原版角色绑定组件，以及真实组件 Tick 的延迟、间隔、生成坐标和颜色。地图验证器、工作队列清理和最终图形投递作为边界隔离；实际执行 Notify_AddedToLord、PawnPositionForStage、UpdateAllDuties 和 RitualVisualEffect／SpawnOnRole。角色夹具使用唯一 Thing ID，避免原版人物相等判断将不同角色误认为观众。图形投递被截取，不验证画面浓度、实际整场 AI 或存读档。

当前烟雾改用原版仪式 Mote。生命周期用例通过实际 Transition.Execute／GotoToil／Init 进入正式阶段，再执行 MoteMaker／ThingMaker、Mote.DoTick／DrawNowAt，验证受塑者坐标、紫色、可见 Alpha、尺度和自然淡出销毁。配置用例核对原版 RitualSmoke 着色器、贴图及七项参数。新增动画用例执行 Graphic_MoteWithAgeSecs，在设备边界截取动画年龄与强制逐次颜色属性。旧 Fleck／DrawBatch 用例已替换，不把历史结果计入当前覆盖。

Mote 宿主隔离 ThingDef 图形默认值构造、完整地图出生／销毁、镜头更新频率、UI、剪影和最终图形设备；XML 继承、角色组件、Mote 创建与生命周期、动画参数传递仍执行真实原版方法。上述检查不证明真实 GPU 出图或完整模组环境下的可见性，也不据此断言原 Fleck 渲染存在何种引擎缺陷。

实机存档确认 effectWorkersCurrentStage 为空后另加 2 项：模拟正式阶段已提交但运行实例缺失，执行生产行为回调补挂并由原版组件发雾，重复检查不重建或重置计数；覆盖准备阶段、当前职责与阶段索引尚未一致、结束职责时不得补挂。正常原版转换用例同时验证已存在实例不会重复。独立环境中的原版转换正常，测试仅重现已确认的缺失状态，不宣称查明完整模组环境中哪个入口造成缺失。
