# SSC 短鞭四姿态接入与测试

> 版本归属：**2.4.3，仅 1.7，开发未发布**。原实施日期、测试／实机反馈和构建版本保留；本版汇总见[版本说明](../../Releases/2.4.3/2.4.3发布说明.md)，现行状态优先看所属主题的完成核对。

日期：2026-10-19。基线提交：`58f7859`。状态：维护者在待机握持与出手触发修正后确认“效果很好，可以提交”，本轮四姿态与两项修正已实机验收，按授权提交；不将未逐项报告的方向／动作组合另记为专项实测。

[原稿与导出](../../Art/SSC短鞭多姿态原稿选定记录.md) · [工业规划](../../Design/Archive/SSC工业时代纳米催情液武器开发规划.md) · [测试专项](../../../Tests/PnaWeaponEffects/README.md)

## 资源与动作

四份运行 PNG 均为 256×256 RGBA，逐字节等同已选原稿的对齐导出；标定柄长 79.90～80.15 px，柄轴中心约 (50,128)。源图与导出脚本保留。物品／待机／抽击／回落对应源图仍为 `d7621759`／`757d1a74`／`64bd2382`／`e2027163`，修正握持与触发时不改贴图。

| 用途 | 运行文件 | 触发条件 |
| --- | --- | --- |
| 物品 | [SSC_PnaShortWhip.png](../../../1.7/Textures/Things/Item/Equipment/SSC_PnaShortWhip.png) | ThingDef 默认图：地面、库存、信息卡 |
| 待机 | [SSC_PnaShortWhip_Idle.png](../../../1.7/Textures/Things/Item/Equipment/SSC_PnaShortWhip_Idle.png) | 原版正在绘制手持武器，且没有正在播放的近战出手；柄尾向下斜 45° |
| 挥击 | [SSC_PnaShortWhip_Attack.png](../../../1.7/Textures/Things/Item/Equipment/SSC_PnaShortWhip_Attack.png) | 持鞭者一次原版近战出手完成后，经过时间 0～5 tick |
| 回落 | [SSC_PnaShortWhip_Recovery.png](../../../1.7/Textures/Things/Item/Equipment/SSC_PnaShortWhip_Recovery.png) | 经过时间 6～17 tick；第 18 tick 恢复待机 |

60 tick/s 下，挥击约 0.1 秒、回落约 0.2 秒。贴图离散切换，未增加蓄力、柔体模拟或跨格鞭击；物理冷却仍为 1.4 秒。原版不显示武器时不会强制显示。

## 待机握持与绘制

`drawSize=1`、XML 装备角度和距离不改动。绘制仍复用原版最终矩阵与 plane10／plane10Flip 网格，仅短鞭的待机姿态进一步调整方向：正常图为 -45°、镜像图为 +45°，使柄尾都朝画面下方。以共同柄轴中心 (50,128) 为旋转锚点，补偿平移，保持握柄的原世界位置、缩放与绘制高度；不绕画布中心转动。

挥击和回落继续使用原版攻击方向和矩阵；恢复待机时才再次采用固定角度。物品图不经过手持入口，不随之旋转。原稿握柄细节并非逐像素一致，本轮整体表现已获实机认可；未逐项报告的方向不另记为专项实测。

`SSCPnaWhipRenderer` 通过既有 DrawEquipmentAiming 的唯一 DrawMesh 包装入口只为短鞭选择姿态材质、绘制一次。副本材质保留原着色器与颜色，仅替换自身贴图；缓存复用，不修改共享材质，不发光、不增加粒子。其他武器继续原绘制，短矛泛光仍走原分支。

## 每次近战出手均触发

初版只从专用鞭束 Verb 启动，虽然打空也播放，但原版选择柄击或拳击时未经过该入口。本轮撤下 `Verb_MeleeAttackSSCPnaWhip` 的 TryCastShot 覆写，改为空继承原版 `Verb_MeleeAttackDamage`。保留初版 XML 与类型名：原版 VerbTracker.ExposeData 对 verbs 使用 LookMode.Deep，并按类型及 loadID 恢复，故不删除上一版已可能写入测试存档的类型身份；空类没有独立出手或伤害逻辑。

新补丁 `Harmony_SSCPnaWhipAttack` 接在原版 `Verb_MeleeAttack.TryCastShot` 的 Prefix／Postfix：

- 仅当前主手是纳米催情液短鞭时捕获武器，不要求这次动作的 EquipmentSource 是短鞭。鞭束、柄击或拳击均可启动视觉；后两者仍是原来的普通伤害，不施加 PNA。
- Prefix 对齐原版未生成／FullBodyBusy 两个提前退出条件，仅准备本次视觉状态。Postfix 不检查命中返回值，因此命中、打空、被闪避、挡甲或药效免疫都不妨碍已经发生的出手播放。原版方法、返回值与异常保持，不拦截或复制结算。
- 不直接挂 Pawn_DrawTracker.Notify_MeleeAttackOn：反编译显示该通知还被灭火／点火共用，避免持鞭灭火也播放攻击。普通受击偏移与该通知本身不启动短鞭姿态。

`SSCPnaWhipPoseUtility` 以游戏 tick 查询姿态，不按绘制帧推进，暂停保持、加速遵从游戏时间。弱键和弱引用只保留临时动作，不写存档；再次出手重启，卸装后缀立即清理，查询时拒绝失效持有者、死亡／销毁、离图／换图、不同 TickManager 或时钟回退。

不改伤害 3、穿甲 20%、PNA 18 点、动作权重 3、命中 +3、品质例外、镇压／恐怖属性、配方、科技与伤口／剂量结算；倒置爱心保持。源 XML 镜像同步维护，1.6／Common、版本与发布附件不改动。

## 测试与构建

本机为 1.7.4982 rev1280，Assembly-CSharp SHA-256 `69cf35594dcc55d1d77cae482887e880ec2d7c043e139d54d5c59b2dd2b940d4`、MVID `a46e6f45b6164261bb63a9471bf7402a`。已核对原版近战出手、绘制、卸装与通知调用者。

- 修正版 PnaWeaponEffects **119/119**：真实 Harmony 执行四处补丁；包含既有短矛／爱心回归、时间边界／状态清理、正常／镜像与四方向的待机 45°、柄心位置／缩放保持、挥击回落原矩阵、柄击／拳击与命中结果无关触发、普通武器／灭火通知放行。
- .NET 10 实际游戏 DLL 探针：绘制、卸装与共用近战出手三处生产补丁成功安装／卸载；真实 Matrix4x4 两种镜像的待机角度及柄轴锚点检查通过；不调用 GPU。
- 修正批次 6 项 XML／镜像与四图字节核对通过；两份工业 XML 的数值配置与基线一致，鞭击 Verb 类型名保留初版空继承类，四张贴图与此前导出及游戏目录均一致。尺寸／alpha／留边沿用未变资源上一批检查。
- 共用触发／角度修正完整回归 39/39 套、3524/3524 项、2/2 资源检查通过。之后为保留初版存档类型身份加入空继承类并保留原 XML，再次运行最终视觉 119/119、药效／XML 530/530 与原生探针（包含类型无覆写检查），全部通过；最终 Def 镜像 21/21、布局／冻结基线 538 项通过。各次报告独立保留，不将完整回归报告改写为兼容类加入后的运行。
- 1.7 编译零警告、零错误。修正版 DLL SHA-256 `e16078c15c03d28ba205ff7a9adcfc18e5adfb2c47aa30ba8d6a43501b749e8a`；BUILD-INFO 保留来源提交与生成时的 `gameRuntimeVerified=false`。

修正批次报告与原生探针位于 `.builds/pna-weapons/whip-poses/adjustments/`。宿主不模拟原版命中概率、GPU 或完整地图／存档生命周期；实际外观与方向以维护者实机反馈为准。

### 初版证据保留

初版专项 109/109、实际游戏绘制／卸装补丁探针、19 项资源检查及零警告零错误的构建通过，DLL 为 `c1916544cff285655d930c2a93bd54d35c474d1f70b7e60b22e880316c69f015`。完整回归首次 38 套／3483 项通过，原生品质专项因 SDK 首次运行缓存写入受限未启动；指定工作区 DOTNET_CLI_HOME 后单独重跑 31/31，通过结果合计 39 套／3514 项及 2/2 资源检查，原始失败报告与汇总保留。

初版已同步 9 份文件、核对全部 510 份 1.7 文件一致，532 份 1.6／Common 文件及 LoadFolders 不变；备份为 `.builds/pna-weapons/whip-poses/game-sync/backup-20261019-165533-84cf5ab9`。维护者对初版动画作肯定反馈，本轮只修正其指出的待机角度和触发范围，不回写初版构建／同步 JSON。

## 修正版同步与实机观察

修正版已备份并同步至 `D:/STEAM/steamapps/common/RimWorld/Mods/RJW-SexSlaveCraft-TieJin-Modify`，仅覆盖 DLL／PDB／BUILD-INFO 共 3 份，四张 PNG 和 XML 保持初版运行文件。全部 510 份 1.7 文件与工作区匹配，532 份 1.6／Common 文件及 LoadFolders 保持。备份在 `E:/Games/RimWorld/modDevelopment/RJW-SexSlaveCraft-TieJin-Modify/.builds/pna-weapons/whip-poses/adjustments/game-sync/backup-20261019-171551-634086fc`，计划与结果见修正目录下 game-sync。构建／同步 JSON 保留 `gameRuntimeVerified=false`；维护者已确认修正版“效果很好”，按授权提交，验收关闭。

此前实机观察范围包括待机柄尾向下斜 45°、柄部稳定、持鞭各次近战出手的姿态切换及已有反馈保持。维护者于 2026-10-19 确认最终效果并授权提交；本次收口只记录反馈和提交，不重新编译或回写构建／同步元数据。新增紫色弧形命中特效仍处分析阶段，不在本轮实现与验收范围。
