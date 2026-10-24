# 文档总索引

**2.4.5 开发版 — 2026-10-24 登记，尚未发布。** 2.4.4 后开发已归入本版；最新正式下载仍为 [v2.4.4](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.4)。1.7 运行元数据暂保留 2.4.4，1.6／Common 冻结于 2.4.1。

[项目首页](../README.md) · [玩家阅读入口](Players/README.md) · [当前规划](Design/README.md) · [开发资料](Development/README.md)

## 最新开发状态

本轮按用途整理：Verification 保留子宫／胃部四语显示、来源偏好及亲昵后续四份待核查记录；[通用能力开关第一批](Development/Completed/通用能力开关框架第一批实现与验证.md)已获维护者实机确认未发现问题，验收关闭并归入 Completed，第二、三批未实施。已发布 PNA 批次与旧品质适配归入 Completed，P1 跳转副本合并，旧特效提示词归入 Art/Source。当前状态详见[总体规划](Design/未来内容开发规划.md)。

2026-10-24：2.4.4 后截至 `94e37bb` 的开发已统一登记为 **2.4.5 开发版（未发布）**，见[版本说明](Releases/2.4.5/2.4.5发布说明.md)、[归并核对](Releases/2.4.5/2.4.5开发归并核对.md)和[验证边界](Releases/2.4.5/2.4.5版本验证.md)。包括主人免疫／猫狗调整、连续人格排泄修复，以及[子宫开发与生育控制](Development/Completed/子宫开发与生育控制完成核对.md)及[胃部恶堕化开发](Development/Completed/胃部恶堕化开发完成核对.md)的约定功能已完成，并分别取得实机认可；两项定稿均已落实四语，尚未发布。设计及已交付分批记录已归档，部署与四语显示的剩余核查集中在[待验收索引](Development/Verification/README.md)。

## 按用途选择入口

| 用途 | 入口 | 推荐阅读顺序 |
| --- | --- | --- |
| 了解功能、准备发布页面 | [双语模组介绍](模组介绍.md) | English 在前、中文在后；先看开发／下载版本区别 |
| 安装和游玩 | [玩家阅读入口](Players/README.md) | 简介 → 快速入门 → 按需查机制与常见问题 |
| 了解版本归属 | [发布与验证索引](Releases/README.md) | 2.4.5 开发范围 → 归并核对 → 验证；2.4.4 正式范围保留；历史见 [CHANGELOG](../CHANGELOG.md) |
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
    ITab个人能力开关通用框架开发规划.md  三批适配；第一批已验收，后二批未实施
    性能测量与定点优化首批规划.md  暂缓方案留存
    性奴浪漫行为控制开发规划.md    四阶段权限与精确命令，未实现
    Archive/                   已交付设计，含子宫与胃部开发
  Development/
    开发更新记录.md            按日期追溯
    Completed/                 实现、修复与已关闭批次
    Verification/              尚未完成的实机、部署与显示核查
    Reviews/                   代码和机制复盘
    Reference/                 文案定稿、维护清单与历史交接
  Releases/<版本>/             版本说明、归并核对、验证与历史公告
  Maintenance/                 构建发布和文档维护
  Art/                         原稿、资源表、提示词与备份
```

根目录四份玩家指南保留固定路径。已完成内容、明确待验证事项、未来提案分别维护；版本登记不等于已发布，历史测试计数不等于当前结果。工业和太空武器的设计已移入 Archive，完成状态先查各自汇总；鞭子后续声音等需求只留未来规划。

2026-10-24 分类整理并补入[ITab 个人能力开关三批规划](Design/ITab个人能力开关通用框架开发规划.md)后，Design 根目录保留 5 份 Markdown：README、总体规划、能力开关专项、性能测量暂缓方案及未实现的浪漫控制规划。能力开关第一批已获维护者实机确认未发现问题，约定范围验收关闭；第二、三批未实施，总规划已纠正泌乳偏好归属。子宫与胃部的两份已交付设计移入 Archive；10 份已交付实现／反馈／历史迭代记录归入 Completed。两份四语显示待验收记录继续留在 Verification，文案定稿与文件清单留在 Reference，废案身份不变。分类不改历史验证结论或游戏安装副本。

源码旁的专用说明：[旧 RimTalk 归档](../Archive/RimTalk/README.md) · [Shader 源码与工具](../Sexslavecraft/ShaderSources/README.md)。
