# 文档总索引

**2.4.4 正式版 — 2026-10-23。** [下载 v2.4.4](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.4)。1.7 运行元数据对齐 2.4.4，1.6／Common 冻结于 2.4.1；此前开发登记与分批测试保留原时点，发布结果见版本验证。

[项目首页](../README.md) · [玩家阅读入口](Players/README.md) · [当前规划](Design/README.md) · [开发资料](Development/README.md)

## 最新开发状态

2026-10-24：[子宫开发与生育控制功能已通过本轮实机验收](Development/Completed/子宫开发与生育控制完成核对.md)，尚未发布，正式文案按[填充文件清单](Development/Reference/子宫开发与生育控制文案填充文件清单.md)后续处理。

## 按用途选择入口

| 用途 | 入口 | 推荐阅读顺序 |
| --- | --- | --- |
| 了解功能、准备发布页面 | [双语模组介绍](模组介绍.md) | English 在前、中文在后；先看开发／下载版本区别 |
| 安装和游玩 | [玩家阅读入口](Players/README.md) | 简介 → 快速入门 → 按需查机制与常见问题 |
| 了解版本归属 | [发布与验证索引](Releases/README.md) | 2.4.4 范围 → 归并核对 → 验证；历史见 [CHANGELOG](../CHANGELOG.md) |
| 接手已完成功能 | [已交付开发记录](Development/Completed/README.md) | 主题完成核对 → 对应阶段 → 历史设计 |
| 查明确的验证缺口 | [待验收事项](Development/Verification/README.md) | 尚待确认范围 → 实现依据 → 实机步骤 |
| 继续设计 | [当前规划索引](Design/README.md) | 总体状态 → 未完成需求 → 关联历史；已完成方案在 [Archive](Design/Archive/README.md) |
| 追溯实现与交接 | [开发日志](Development/开发更新记录.md) · [代码复盘](Development/Reviews/README.md) · [历史参考](Development/Reference/README.md) | 按日期／基线查，旧待办保留当时语境 |
| 编译、测试、发布和文档维护 | [维护入口](Maintenance/README.md) | [发布与打包](Maintenance/发布与打包.md) → [测试说明](../Tests/README.md) → 版本检查 |
| 贴图、声音与提示词 | [美术索引](Art/README.md) | [PNA 资源表](Art/SSC纳米催情液武器资源.md) → 当前选定原稿 → 历史尝试 |
| 绘制武器资源 | [绘制与导入经验](Art/RimWorld武器贴图绘制与导入经验.md) | 构型与小尺寸辨识 → 导出 → 实机验证 |

## 三条阅读路线

- **新玩家**：[模组介绍](模组介绍.md) → [快速入门](../读我，玩法介绍.md) → [机制详解](../机制详解.md)。英文路线见 [English reading order](Players/README.md#english-reading-order)。
- **接手开发**：[2.4.4 范围](Releases/2.4.4/2.4.4发布说明.md) → [完成记录](Development/Completed/README.md) → [待验收](Development/Verification/README.md) → [未来规划](Design/README.md)。
- **准备发布**：[版本验证](Releases/2.4.4/2.4.4版本验证.md) → [构建与打包](Maintenance/发布与打包.md) → [测试索引](../Tests/README.md)。

## 目录与状态

```text
Docs/
  模组介绍.md                  当前开发版本双语简介
  Players/                     玩家入口和阅读顺序
  Design/                      当前需求与未完成设计
    README.md                  规划入口与状态索引
    未来内容开发规划.md        未完成需求及历史编号
    性能测量与定点优化首批规划.md  暂缓方案留存
    子宫开发与生育控制开发规划.md  手术启动、培养与两态收益，未实现
    性奴浪漫行为控制开发规划.md    四阶段权限与精确命令，未实现
    Archive/                   已交付范围的设计依据
  Development/
    开发更新记录.md            按日期追溯
    Completed/                 实现、修复与已关闭批次
    Verification/              当前验证缺口及历史验证资料
    Reviews/                   代码和机制复盘
    Reference/                 历史交接与提示词
  Releases/<版本>/             版本说明、归并核对、验证与历史公告
  Maintenance/                 构建发布和文档维护
  Art/                         原稿、资源表、提示词与备份
```

根目录四份玩家指南保留固定路径。已完成内容、明确待验证事项、未来提案分别维护；版本登记不等于已发布，历史测试计数不等于当前结果。工业和太空武器的设计已移入 Archive，完成状态先查各自汇总；鞭子后续声音等需求只留未来规划。

2026-10-23 当次归档后，Design 根目录保留 3 份 Markdown；PNA／恩赐的 4 份设计及分析归入 Archive，4 份文案定稿及交接归入 Development/Reference。同日后续新增[子宫开发与生育控制规划](Design/子宫开发与生育控制开发规划.md)和[浪漫行为控制规划](Design/性奴浪漫行为控制开发规划.md)，当前根目录共 5 份 Markdown。新增内容仅为后续 1.7 规划，未写运行代码，历史参数与验收范围保持原记录。

源码旁的专用说明：[旧 RimTalk 归档](../Archive/RimTalk/README.md) · [Shader 源码与工具](../Sexslavecraft/ShaderSources/README.md)。
