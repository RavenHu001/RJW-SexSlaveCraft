# 旧实现与历史调查工具

这里保留已退役、暂停或仅供追溯的代码、定义和调查脚本；不属于当前运行目录，也不因目录存在而重新启用。当前独立工具从[Tools](../Tools/README.md)进入，设计档案和开发记录在Docs中分类保存。

| 目录 | 用途与当前边界 |
| --- | --- |
| [PnaSimpleLauncher](PnaSimpleLauncher/README.md) | 1.7旧简易PNA发射器及相关定义／测试留存；2.4.3已退役，无旧定义存档迁移，1.6冻结内容保持。 |
| [PnaMeleeQuality](PnaMeleeQuality/README.md) | 已由恢复原版品质决定取代的旧固定伤害补丁，不参与当前编译。 |
| [RimTalk](RimTalk/README.md) | 旧兼容代码留存；当前对接暂停，不作为可用模块或默认待办。 |
| [RenderingInvestigation](RenderingInvestigation/README.md) | 使用旧Managed程序集路径的反射调查脚本，从仓库根目录移入，仅作历史调查依据。 |

仓库结构与归属规则见[维护约定](../Docs/Maintenance/文档维护约定.md#仓库文件归属)。新增可复用开发工具放Tools，构建／测试／打包入口放Scripts，临时输出放`.builds`；不把归档内容纳入游戏加载或发布包。
