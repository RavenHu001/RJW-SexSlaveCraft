# SSC 投射物紫色拖尾实现与测试

归档说明（2026-10-18）：本批约定范围已交付。当前实现、最终参数及分批验收边界统一见[首批完成核对](SSC武器拓展首批完成核对.md)。下文保留当时的试验参数、测试计数和同步状态，旧的“待实机／未实现／暂不提交”不代表当前待办。

日期：2026-10-18。最初按纯 XML 接入三个投射物，维护者确认效果会运行，但几乎不可见，希望改为流星状拖尾。维护者随后明确授权少量 C# 实现连续尾迹；最终版本包含箭尾位置修正，已获维护者实机确认并授权提交，本轮验收关闭。

## 当前实现

三种弹丸共用 `Projectile_SSCPnaTrail` 与 `CompSSCPnaTrail`。视觉弹丸继承原版 Bullet，完整调用一次原版 TickInterval 后，仅在弹丸仍存在时通知视觉组件；不覆盖 Launch 或 Impact，不改伤害处理器、药量或物理参数。

组件在相邻真实位置之间提交沿飞行方向旋转的细长 Fleck 片段，按距离分段并保留重叠，消除逐 tick 圆点间的空隙。当前 TickInterval 在原版 Tick 后移动，因此选择移动完成后取样，避免尾迹端点滞后一帧；不在 PostDraw 中改变粒子容器。原版 FleckSystemStatic 管理收窄、淡出和回收。

| 位置 | 改动 |
| --- | --- |
| Sexslavecraft/Comps/CompSSCPnaTrail.cs | 视觉弹丸子类、组件及共享默认配置。 |
| Sexslavecraft/SexSlaveCraft_Alpha.csproj | 把新源码加入权威编译清单，供 1.7 工程使用。 |
| 1.7/Defs/EffecterDefs/SSC_PnaProjectileTrail.xml | 当前只保留 SSC_PnaTrail FleckDef；删除首版 Effecter，避免双重喷射。 |
| 1.7/Defs/ThingsDefs/SSC_PnaPrimitiveWeapons.xml | 吹箭 SSC_Projectile_PnaDart、弩矢 SSC_Projectile_PnaBolt 使用新视觉类型与组件。 |
| 1.7/Defs/ThingsDefs/SSC_Gun.xml | Bullet_PNA_Low 使用相同入口。 |
| Tests/PnaProjectileTrail | 直接执行新生产源码的专项宿主与说明。 |

## 外观与预算

| 参数 | 当前值 |
| --- | --- |
| 纹理 | Core Things/Mote/BrightFlash，直接引用原版资源。 |
| 材质 | 普通 Mote，单层，无发光材质、灯光或持续音效。 |
| 颜色 RGBA | (0.78, 0.3, 1, 0.95)。 |
| 初始宽度 | 0.26 格，实际可见宽度还受纹理软边影响。 |
| 最大片段间隔／软边重叠 | 0.35／0.55 格；尺寸和旋转通过原生 exactScale／rotation 提交。 |
| 尾迹位置 | 沿飞行反方向平移：弹丸贴图半长 + 重叠半长 + tailGap 0.02 格；完整前缘停在箭尾之后。 |
| 渐入／停留／淡出 | 0／0.02／0.12 秒，总寿命 0.14 秒。 |
| 收窄 | CurvedScaler 仅作用 X 轴；生命周期内由初始宽度缩至约 5%，长度不缩，保持片段衔接。 |
| 生命周期 | realTime=false；不附着已生成粒子，无额外漂移或增长。 |
| 上限 | 单次通知最多 16 段；忽略超过 4 格的突跳。 |

原 AirPuff 纹理中心 Alpha 约 54/255，再乘首版 45% 后仅约 9.5%，还会继续淡出；这解释了首版非常不显眼。当前 BrightFlash 中心 Alpha 约 251/255，结合 95% 实例透明度明显提高有效不透明度。纹理名称不决定发光行为，本批继续使用普通 Mote 着色器。

正常单 tick 移动下，速度 35 的吹箭通常生成约一至两段，速度 55 的弩矢／旧弹丸通常约两段；保留时间约八至九 tick。相比首版会增加少量可见粒子，但仍有固定单次预算，无每帧全局扫描。此处仅为配置估算，未进行帧率实测。

组件只在当前地图／镜头附近生成粒子；停顿不会叠加。地图切换、重新生成／加载后重新取样，不连接旧历史；纯临时字段不进存档。弹丸销毁后不继续生成，已有片段自然淡出。

维护者已认可流星状尾迹，但反馈位置过于靠前、遮住约半支箭。原因是片段以路径中点为中心，0.55 格重叠向前额外伸出 0.275 格。当前按实际方向整体后移，并读取三种竖向投射物的 graphicData.drawSize.y 作为贴图长度：吹箭后移 0.545 格、弩矢 0.595 格、旧发射器弹丸 0.795 格。片段自身长度和重叠不变，前缘与箭尾保留 0.02 格间隙，不改变原尾迹的颜色、宽度和淡出；维护者已确认最终效果与实机测试正常。

## 环境与自动验证

- 游戏：**1.7.4982 rev1280**。Assembly-CSharp.dll SHA-256：`69cf35594dcc55d1d77cae482887e880ec2d7c043e139d54d5c59b2dd2b940d4`；MVID：`a46e6f45b6164261bb63a9471bf7402a`。
- 核对当前 Thing.DoTick、Projectile.TickInterval、FleckCreationData.exactScale、FleckMaker.GetDataStatic、FleckStatic.Setup／TimeInterval 和 CurvedScalerExt.ScaleAtTime：曲线输出以 1+curve(x) 乘指定轴，故末端使用 -0.95；angle 使用与弹道一致的平面朝向。
- 原版纹理在 Assets.zip 的 `Resources/Textures/Things/Mote/BrightFlash.psd.dds`，只读解码用于检查，未将原版资源提取到 Mod 发布目录；先前 AirPuff 同样只读检查。
- **专项 30/30**：首次采样、各方向与分段衔接、原版移动调用顺序／delta、停顿、销毁、地图与镜头切换、传送、读档重置、异常配置／坐标、固定预算、错误挂载和 XML 接线；新增三种贴图长度的前缘边界检查、非法 tailGap 及缺失图形配置降级检查。
- **PnaLoad 492/492** 沿用流星版上一轮结果；本轮仅调整视觉生成位置，物理／施药源码未改。
- **465 份 XML** 解析通过；三个弹丸去掉视觉类型和组件后，XML 语义与 HEAD `82e4e3e` 一致，原参数不变；旧 Effecter 已移除，没有双重喷射。
- 版本布局 **538** 项、Def 镜像 **16** 份、语言镜像 **379** 份通过。
- 位置调整后重新进行 1.7 Release 编译，使用 SDK **10.0.402** 和本机真实依赖，**0 警告、0 错误**；DLL 哈希为 `8ae24f0583b6906b13f976477bc9eccfcc2fea4273e51e6a5172cdf50731a279`，BUILD-INFO 保留实际来源和 `gameRuntimeVerified=false`。

专项宿主只模拟移动完成回调和 Fleck 输出，不模拟 GPU、原生曲线或命中随机。编译证明接口兼容，最终外观与开销须实机确认。检查、构建、测试与原生纹理检查记录均位于 `.builds/pna-weapons/projectile-trail/`。

## 同步与验收

首版纯 XML 于 21:50:18 UTC 同步，3 份 XML、481 个 1.7 文件一致；532 个冻结文件未变。首版已确认运作但外观不符合预期，不将其反馈当作新版验收。

新版已按逐文件哈希计划同步至 `D:/STEAM/steamapps/common/RimWorld/Mods/RJW-SexSlaveCraft-TieJin-Modify`。2026-10-18 22:02:59 UTC 完成：覆盖 6 个文件（3 份 XML、DLL、PDB、BUILD-INFO），481 个 1.7 文件哈希一致，532 个冻结文件与 LoadFolders 未变。备份与结果位于 `.builds/pna-weapons/projectile-trail/meteor-sync/`，备份目录为 `backup-20261018-220259-43aaeb98`。

实机核对情境：

1. 三种弹丸的尾迹是否清楚、连贯并呈前粗后细，箭矢本体仍可辨认。
2. 各方向、近远距离、暂停和加速状态下无偏移、断节或原地叠加；快速齐射时观察帧率。
3. 命中、落空、墙壁与护甲阻挡后没有继续喷射；拖尾仅表示飞行，不表示已施药。
4. 普通武器、近战短矛和旧发射器实际结算保持原行为。

流星尾迹已获维护者认可，本轮后移版本完成专项回归与 1.7 编译，并于 2026-10-18 18:09（America/New_York）同步本机：覆盖 DLL、PDB、BUILD-INFO 三个文件，481 个 1.7 文件一致，532 个冻结文件与 LoadFolders 未变。同步记录位于 `.builds/pna-weapons/projectile-trail/rear-offset-sync/`，备份为 `backup-20261018-220935-b1ab5f80`。

维护者确认后移版本效果可以、实机测试正常，授权提交三种投射物拖尾、位置修正、相关测试和文档，本轮验收关闭。收尾仅更新验收记录，沿用专项 30/30、PnaLoad 492/492 及零警告零错误的 1.7 编译结果，不重复构建。BUILD-INFO 保留构建当时的来源和验证标记，实机反馈记录在此，不重写历史构建元数据。
