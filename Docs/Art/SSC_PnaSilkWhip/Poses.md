# 太空丝鞭姿态原稿选定记录

2026-10-20。维护者已明确选定三张附件原稿并授权先提交，随后导出并接入；原稿已提交`423a883`；随后完成256px统一柄部导出和三姿态／工业残影复用接入，已同步本机待实机查看，接入改动未提交。当前运行物品态为已提交的v2，见[物品图记录](README.md)。

| 状态 | 图稿 | 当前决定 |
| --- | --- | --- |
| 关机物品态 | [Item v2](SSC_PnaSilkWhip_Item_v2_Source.png) | 已认可、接入并提交；drawSize=0.75 |
| 启动待机 | [Idle v3](SSC_PnaSilkWhip_Idle_v3_Source.png) | 已选定；原生成文件 `exec-ea492489-cd0d-4676-a899-01fae1460198.png` |
| 挥击 | [Attack v2](SSC_PnaSilkWhip_Attack_v2_Source.png) | 回选此版，已选定；原生成文件 `exec-5d89847e-5c47-47f8-a162-e8bd28a26e14.png` |
| 回落／回收 | [Recovery Approved](SSC_PnaSilkWhip_Recovery_Approved.png) | 维护者明确将上一张浅弧长束图改选为回落态；原生成文件为`exec-6462dfbc-95eb-4100-91e0-f4510d40f543.png` |

原[Idle v1文件](SSC_PnaSilkWhip_Idle_v1_Source.png)、[生成记录](Idle_v1.generation.json)和[提示词](Idle_v1.prompt.txt)保留生成时点；其待机候选角色已被否决，当前用途为回落态。新的回落副本与原图字节完全一致，选择依据及哈希见[选定记录](Recovery.selection.json)。

待机v2曾使用工业短鞭图作为弯折参考，维护者认为画风受污染而否决；[旧图](SSC_PnaSilkWhip_Idle_v2_Source.png)、[旧提示词](Idle_v2.prompt.txt)及[旧生成记录](Idle_v2.generation.json)仅保留历史，不再作为后续参考。

当前待机v3使用内置imagegen，参考图严格只有物品v2与已选回落图，分别确定握柄／描边及细丝工作端的材质画风。向右伸出、向下转弯、再折回握柄下方朝左的大回弯形态仅用文字描述。完整[提示词](Idle_v3.prompt.txt)及[生成记录](Idle_v3.generation.json)已保存。当前图稿保留高清透明RGBA，不调整运行XML、贴图或动画逻辑；后续导出需共同标定柄长与握持锚点。

挥击v1通过内置imagegen生成，参考严格使用物品v2和已选回落图，出手形态由文字描述；保留单束紫色纳米细丝与握柄设计。高清透明RGBA原稿、完整[提示词](Attack_v1.prompt.txt)和[来源记录](Attack_v1.generation.json)已保存，尚未导出或接入。

挥击v1被维护者指出相对回落态显短、弧度缺少力量；后续附图明确当前挥击所需轮廓是根部近水平向右、中后段逐渐上扬、末端明显高于握柄。线稿取代此前将鞭梢下弯的解释；挥击v2据此重绘，长度以回落态握柄比例为基准，并仅用物品／回落图提供画风。完整[提示词](Attack_v2.prompt.txt)、[原稿](SSC_PnaSilkWhip_Attack_v2_Source.png)及[来源与透明边界记录](Attack_v2.generation.json)已保存，待认可，尚未导出、接入或提交。

维护者认为挥击v2像刀、缺乏鞭子的柔感。v3仅以物品v2与已选回落图为参考，尝试削弱宽实心色块与刃状边界，并以局部曲线起伏、细丝错落和柔软端部表现柔性，仍保留根部向右、外段上扬的整体轨迹。收细幅度属于提示词目标，不记为已量测达标。完整[提示词](Attack_v3.prompt.txt)、[原稿](SSC_PnaSilkWhip_Attack_v3_Source.png)及[生成记录](Attack_v3.generation.json)已保存；当前仅供选图，尚未导出、接入或提交。

维护者进一步明确挥击重绘的参考范围：只用物品握柄和已选回落态两图，辫束状态直接参考回落态，末端明显高于握柄。当前挥击v5据此通过内置imagegen生成，不使用此前挥击试稿作为输入，不继续叠加收细或额外散开要求。[高清原稿](SSC_PnaSilkWhip_Attack_v5_Source.png)、[完整提示词](Attack_v5.prompt.txt)及[生成记录](Attack_v5.generation.json)已保存，待选定；未导出、接入、同步或提交。此前试稿及记录保留各自阶段。

维护者随后明确放弃草图提供的约束，允许模型自由处理挥击形态。当前v6仅以物品图固定握柄、以回落态固定工作端材质画风，不再要求特定弧向、上扬轨迹或末端高于柄。通过内置imagegen生成，[高清原稿](SSC_PnaSilkWhip_Attack_v6_Source.png)、[完整提示词](Attack_v6.prompt.txt)和[来源记录](Attack_v6.generation.json)已保存。此决定取代上文基于草图的约束；v6待认可，未导出、接入、同步或提交。

维护者要求尝试直接编辑回落态，唯一调整要求是鞭子末端高于握柄。v7使用内置imagegen，仅输入已选回落图作为编辑目标，以短提示词保留柄部、辫束材质、长度与细节，不另加轨迹要求。[高清原稿](SSC_PnaSilkWhip_Attack_v7_Source.png)、[完整提示词](Attack_v7.prompt.txt)和[来源记录](Attack_v7.generation.json)已保存；已选回落原图不变。v7待认可，未导出、接入、同步或提交。

维护者最终以附件明确指定上表三图，原稿均与本机生成文件逐字节一致，见[选图清单](ApprovedPoses.json)。此决定取代下文各试稿的待选状态；其提示词、来源与评审过程保留原时点。当时的选图认可独立于256px导出与游戏验收；后续三姿态及尺寸已于2026-10-20获维护者实机正常确认并授权提交。后续三姿态按共同柄长及柄心标定，特效和其他绘制数值先复用工业短鞭。

当前导出柄长约64px、柄心(50,128)，物品保持0.75，手持三姿态统一1.75，以匹配工业短鞭80px柄长、1.4手持尺寸；45°待机及6／12tick出手／回落仍复用工业实现。参见[导出参数](256/poses-export.json)、[导出脚本](Export-Poses-256.py)及[实现与测试](../../Development/Completed/SSC太空丝鞭三姿态接入与测试.md)；上文各试稿的“未接入”等状态保留生成时点。
