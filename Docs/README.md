# 文档总索引

当前 1.7 分支的 SSC 武器首批已交付：先读[完成核对](Development/Completed/SSC武器拓展首批完成核对.md)，玩家规则见[武器机制](../机制详解.md#116-原始纳米催情液武器与视觉反馈)，图像与声音见[资源表](Art/SSC纳米催情液武器资源.md)。开发分支成果与既有 2.4.2 下载分开登记，未重新发布安装包。

[工业时代 PNA 三武器](Design/SSC工业时代纳米催情液武器开发规划.md)步骤一已实现，使用占位图：短鞭与两把气动针枪、品质伤害适配和远程防身调整已获实机认可，见[实现与验收](Development/Completed/SSC工业武器步骤一实现与测试.md)。正式贴图、声音选型及发布不在本批范围。

[项目首页](../README.md) · [玩家阅读入口](Players/README.md) · [当前规划](Design/README.md) · [开发资料](Development/README.md)

整理日期：2026-10-18。当前版本为 **2.4.2**，统一纳入v2.4.1发布后的打包规则、双版本重布局、1.7迁移与凝胶外观/UI开发，见[本版说明](Releases/2.4.2/2.4.2发布说明.md)和[版本登记边界](Releases/2.4.2/2.4.2版本验证.md)。正式下载为 **2.4.2**；原2.4.0.5及截至旧版发布的PNA、恩赐和绑定维护保留原归属。

欢愉恩赐首版四批均已完成并获实机确认，范围和后续归属见[完成核对](Development/Completed/主人欲望侵染完成核对.md)；操作与数值从[玩家入口](Players/README.md)进入。

## 按用途选择入口

| 用途 | 入口 | 推荐阅读顺序 |
| --- | --- | --- |
| 了解功能、准备发布页面 | [中英双语模组介绍](模组介绍.md) | English在前，中文在后；说明当前正式版及版本目录 |
| 安装和游玩 | [玩家阅读入口](Players/README.md) | 模组介绍 → 快速入门 → 按需查机制与常见问题 |
| 查看版本交付 | [发布与验证索引](Releases/README.md) | 当前版发布说明 → 验证；跨版本查[更新日志](../CHANGELOG.md) |
| 继续设计新功能 | [当前规划索引](Design/README.md) | 总体状态 → 当前专项 → 相关历史设计 |
| 接手 PNA 游戏文案 | [PNA 文案重写交接](Design/PNA文案重写交接.md) | 历史设定与文件范围 → 两份定稿 → 已完成的 XML 与标签验收 |
| 查已完成实现与修复 | [已交付开发记录](Development/Completed/README.md) | 主题完成记录 → 对应阶段与修复 |
| 查尚未关闭的实机事项 | [待验收事项](Development/Verification/README.md) | 已知缺口 → 关联实现 → 测试步骤 |
| 理解旧代码与设计取舍 | [代码与机制复盘](Development/Reviews/README.md) | 先看基线，再看复盘与后续实现 |
| 追溯交接和提示词 | [历史交接资料](Development/Reference/README.md) | 用于定位与追溯，现行规则以实现和玩家指南为准 |
| 编译、测试、打包或维护文档 | [开发维护入口](Maintenance/README.md) | 环境与命令 → 测试说明 → 发布检查 |
| 查图标与贴图来源 | [美术资源索引](Art/README.md) | 当前选定资源 → 说明 → 历史尝试 |

## 三条阅读路线

- **新玩家**：[模组介绍](模组介绍.md) → [快速入门](../读我，玩法介绍.md) → [机制详解](../机制详解.md)。英文路线见 [Player documentation](Players/README.md#english-reading-order)。
- **接手开发**：[当前版本说明](Releases/2.4.2/2.4.2发布说明.md) → [待验收事项](Development/Verification/README.md) → [对应已交付记录](Development/Completed/README.md) → [当前规划](Design/README.md)。
- **准备发布**：[发布与打包](Maintenance/发布与打包.md) → [测试说明](../Tests/README.md) → [版本文档](Releases/README.md)。

## 目录与边界

```text
Docs/
  模组介绍.md                 说明正式版及版本目录的双语简介
  Players/                    玩家入口与阅读顺序
  Design/                     当前专项方案与总体规划
    Archive/                  已交付范围的历史设计
  Development/
    开发更新记录.md           按日期追溯的统一日志
    Completed/                已交付功能的实现与修复记录
    Verification/             已实现但仍待实机确认的事项
    Reviews/                  代码与机制复盘
    Reference/                历史交接清单与提示词
  Releases/<版本>/            发布说明、验证记录与公告
  Maintenance/                构建发布与文档维护流程
  Art/                        美术说明、提示词及备份
```

根目录的中英文玩家指南保留固定路径。`Tests/` 的说明跟随测试套件；[旧 RimTalk](../Archive/RimTalk/README.md)与[着色器源码](../Sexslavecraft/ShaderSources/README.md)跟随对应资源目录。

## 如何理解文档状态

- **当前玩法**以当前版玩家指南和发布说明为入口。
- **当前规划**描述尚未完成的工作；讨论值、候选能力和未实现方案不作为现行功能。
- **已交付记录与设计归档**保留原日期、基线和验证范围，其中的后续想法不自动转为已完成。
- **已实现、待验收**单独登记；已发布不等于所有实机场景与兼容组合都已确认。
- **开发日志与历史交接**用于追溯；较早的“未发布”“待确认”按当时语境阅读。

## 文档维护约定

新增、归档、移动、版本同步和链接检查规则见[文档维护约定](Maintenance/文档维护约定.md)。打包脚本按白名单包含玩家与本版发布文档，不递归复制整个 `Docs/`；指向未随包开发资料、源码和测试的链接在包副本中转换为仓库链接。
