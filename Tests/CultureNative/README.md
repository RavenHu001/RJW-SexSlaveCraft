# 文化步骤 1 原生专项

目标为本机 RimWorld 1.7，使用 .NET 10、真实游戏程序集和测试用 net9.0 Harmony。通过 ProjectReference 编译完整生产 1.7 工程，执行生产人口、领袖、个人状态与快照查询，不使用模拟 MapPawns、领袖资格或缓存算法。

## 覆盖

- 原生 XML 反序列化和交叉引用：1 个模因、1 个分组、5 个 Issue、22 条戒律；两类权重、挂载与排序、四语键和语言镜像。
- 原生 CanAdd／CanAddForFaction、完整 RandomizePrecepts（不是 requiredPreceptsOnly）、改革添加／移除、零权重合法旧项保留、禁止默认项的可行性负例。
- 读取本机原版模因的资格字段，用原版 XmlInheritance 合并继承、IdeoUtility.CanAdd 检查排斥，用原版 CanAddForFaction 检查 SSC 五组。当前包括 27 个普通模因配对及 340 个合法三模因组合资格检查；保留原版性别至上互斥。全面禁欲的资格配置随模因添加／移除保持。
- 真实地图来源、递归 ThingOwner／休眠舱、商队、可控异变者、任务额外势力借住者、飞行运输；名册／环境／每主绑定三种集合分别断言。
- 主／次文化实际职位及 ChosenPawnSingle、原版自由非奴隶资格、RequirementsMet、领袖三态和多有效任职者冲突；不写职位或显示顺序。
- 同代共享、按需项目与每主计数、99／100 tick、延后项目不延长寿命、周期内回退、加载／实例／DLC 边界、无读取不扫描。
- 原生人口属性入口和内部递归分别计数：两地图名册递归 4 次，加入环境首次查询后累计 6 次；50 次同代重复读取不增加递归。此为确定性工作量结果，不是 TPS 或耗时收益。
- 真实 Scribe 往返：文化保留零权重手选档位；组件不写快照字段，重载后首次有效查询重建。

## 初始化与隔离边界

本宿主不启动游戏 UI 或枚举用户模组；用户配置写入仓库 `.builds/culture-step1/userdata`。世界根入口提供实际 Map、WorldObjectsHolder 和 Faction 夹具，时钟可控；MapPawns、容器递归、QuestUtility、原版职位要求、生成／改革／资格和生产查询均运行真实代码。健康列表和任职记录直接布置条件，不声称测试了完整绑定事务／授职仪式。

图形 Def 构造、贴图、翻译、容器增添时的界面通知、日志投递及 Scribe 的外观样式子对象被隔离；组件、文化及戒律自身的保存／读取不被替换。SSC 全局 Def 注册器预热时未加载本专项以外的完整资源，忽略该启动边界日志，随后布置所需锁链和缰绳定义；实际被测阶段的错误日志均使测试失败。

原版模因组合只装载相关资格字段，保留继承和原有排斥标签，未装载它们完整的仪式／建筑／故事／美术依赖。Lovin_Prohibited 只验证资格及改革保留，不执行其历史事件组件；不能称为启用全部 DLC／模组后的完整文化生成验收。无 Ideology 项验证 XML 加载保护与生产读取短路，不代替无 DLC 实机加载。首批不含文化 thought workers，所以不覆盖心情／社交最终结算、原版反馈调度或非人化最终数值。

## 运行

已登记到 `Scripts/Test-All.ps1`，沿用 NativeGameRoot、NativeDotnetPath、HarmonyAssemblyPath；生产 RJW、动画和游戏 Harmony 引用由 `1.7/BUILD-INFO.json` 提供，缺失时构建失败，不回退旧 DLL。无需联网，也不安装、提交或发布模组。

直接构建本项目时同时传入 GameRoot、RjwAssemblyPath、AnimationsAssemblyPath、GameHarmonyAssemblyPath、HarmonyAssemblyPath；用匹配 .NET 10 运行生成 DLL，参数为仓库根目录和游戏根目录。首次使用 SDK 时将 DOTNET_CLI_HOME 指向仓库内可写目录。
