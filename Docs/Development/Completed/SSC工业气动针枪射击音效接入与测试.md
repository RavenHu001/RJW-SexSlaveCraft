# 工业气动针枪射击音效接入与测试

> 版本归属：**2.4.3，仅 1.7**。原实施日期、测试／实机反馈和构建版本保留；本版汇总见[版本说明](../../Releases/2.4.3/2.4.3发布说明.md)，现行状态优先看所属主题的完成核对。

2026-10-19：维护者确认“实机测试正常，音效效果可以”，本轮两把气动针枪的纯 XML 射击音效验收通过，按授权提交配置与相关文档。以下保留接入、检查及同步证据。

## 实现范围

新增 [工业枪械 SoundDef](../../../1.7/Defs/SoundDefs/SSC_PnaIndustrialWeapons.xml)，将[两把气枪](../../../1.7/Defs/ThingsDefs/SSC_PnaIndustrialWeapons.xml)的 `verbs/li/soundCast` 从吹箭筒音效分别改为独立定义，并同步已有[武器源码镜像](../../../Sexslavecraft/Defs/ThingsDefs/SSC_PnaIndustrialWeapons.xml)。SoundDefs 目录原无源码镜像，本次沿用现有部分镜像集合，不额外建立镜像目录。

两枪共同引用 Core 的 `Misc/Hiss/HissSmall`；实际资源为游戏根目录 `Assets.zip` 中的 `Resources/Sounds/Misc/Hiss/HissSmall.ogg`，文件约 0.288 秒。仅引用原版路径，不复制音频到模组，不增加 DLC 依赖。本轮最终采用气流单层，不叠加机械辅声。

| 参数 | 速射针枪 | 精确针枪 |
| --- | --- | --- |
| SoundDef | `SSC_PnaNeedleRepeater_Shot` | `SSC_PnaNeedleRifle_Shot` |
| 音量，原版百分数单位 | 55～65 | 65～75 |
| 音调倍率 | 1.15～1.25 | 0.85～0.95 |
| 距离范围 | 10～45 | 10～45 |
| 淡入至满幅 | 0.005 秒 | 0.008 秒 |
| 满幅保持至 | 0.04 秒 | 0.08 秒 |
| 中途淡出节点 | 0.08 秒／0.4 倍 | 0.15 秒／0.5 倍 |
| 音量归零 | 0.12 秒 | 0.22 秒 |

均为 `MapOnly`、`onCamera=False`、`sustain=False`、`sustainLoop=False`；并发限制 `maxVoices=12`、`maxSimultaneous=8`。不新增枪声尾响或循环声。时间曲线从 `(0,0)` 起，用 `SoundParamSource_SourceAge` 的 `RealtimeSeconds` 映射 `SoundParamTarget_Volume`，按 `Constant` 每帧更新；包络为音量乘数，达到末节点后保持零。

当前 1.7 原版 `Verb.TryCastNextBurstShot` 在每次成功发射后播放 `soundCast`，不依赖命中。速射枪沿用三针／间隔 6 tick，正常速度下各针相隔 0.1 秒；无需录制三连发片段。第一针的包络在下一针发射时降至 0.2 倍，0.12 秒时归零。包络按真实时间运行；维护者已确认本轮整体听感，未逐项报告的帧率／倍速场景不另作专项通过声明。

这里缩短的是可闻包络，不是截断原始音频文件或提前销毁音源；静音后的样本仍按原版单发播放生命周期回收。未改变吹箭筒音效、武器取用声、三连发节奏、伤害／穿甲／PNA 剂量、贴图、缩放、尾迹或受药特效。无生产 C#、DLL、BUILD-INFO、版本及加载目录变更。

## 检查证据

- 本机游戏为 **1.7.4982 rev1280**；`Assembly-CSharp.dll` SHA-256 为 `69cf35594dcc55d1d77cae482887e880ec2d7c043e139d54d5c59b2dd2b940d4`，MVID 为 `a46e6f45-b616-4261-bb63-a9471bf7402a`。
- 使用工作区已有 .NET 10 SDK 编译隔离检查宿主，直接调用该游戏 DLL 的 `DirectXmlToObject` 加载两套 SoundDef：**25/25 项**检查通过，包括原生 `ConfigErrors`、字段／枚举、`SimpleCurve` 淡入／淡出及归零后保持静音。未调用完整内容解析、音频设备播放或启动游戏；没有重编译模组 DLL。
- 资源／范围／同步预检 **536 项通过**，其中包含 513 份未改动运行文件与游戏副本逐一比对；91 份运行 Def XML 均可解析。确认两枚新 SoundDef 唯一、原版 OGG 存在、最高音调下素材长度仍覆盖完整淡出；工业武器除两处 `soundCast` 外与提交基线一致。
- `Scripts/Test-DefMirrors.ps1`：**21/21 份**现有镜像逐字节一致；`Scripts/Test-ModLayout.ps1`：**538 项**布局／冻结与二进制来源检查通过。检查脚本与 JSON 证据保留在本机 `.builds/pna-weapons/industrial-sounds/`。

## 同步与实机试听

已备份后同步至 `D:/STEAM/steamapps/common/RimWorld/Mods/RJW-SexSlaveCraft-TieJin-Modify`，替换武器 XML 一份、新增 SoundDef XML 一份。**515 份 1.7 运行文件**哈希一致，游戏目录 **532 份 1.6／Common 冻结文件**和 `LoadFolders.xml` 未变。

同步报告与计划在 `.builds/pna-weapons/industrial-sounds/game-sync/`，备份为其下 `backup-20261019-223650-b6760477/`；报告保留 `gameRuntimeVerified=false`。

试装时的试听关注项目如下，保留为后续排查参照：

1. 正常交战缩放下，两枪是否清楚可闻，精确枪是否比速射枪低沉、稍长。
2. 速射三针是否能听出三个发射点，连续点射和多名射手时有无长嘶声、明显断声或音量堆叠。
3. 近／远镜头及加速状态下，空间衰减和节奏是否合适；暂停后有无异常残留。
4. 吹箭筒原有声音是否保持，日志是否出现本轮 SoundDef、曲线或音频资源错误。

维护者已确认本轮实机正常、音效效果可以，据此关闭本批验收并提交。原始同步报告的 `gameRuntimeVerified=false` 保留其同步阶段含义，后续实机认可记录在本页；未逐项报告的场景不补记为专项测试通过。收尾仅整理文档和提交，沿用上述检查证据，不重复编译或同步；未打包发布。
