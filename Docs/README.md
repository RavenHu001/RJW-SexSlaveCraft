# 文档总索引

[项目首页](../README.md) · [玩家阅读入口](Players/README.md) · [当前规划](Design/README.md) · [开发资料](Development/README.md)

整理日期：2026-10-14。当前正式版为 **2.4.0**；具体交付与验收范围分别见[发布说明](Releases/2.4.0/2.4.0发布说明.md)和[版本验证](Releases/2.4.0/2.4.0版本验证.md)。

## 按用途选择入口

| 用途 | 入口 | 推荐阅读顺序 |
| --- | --- | --- |
| 了解功能、准备发布页面 | [中英双语模组介绍](模组介绍.md) | English 在前，中文在后；介绍当前正式版 |
| 安装和游玩 | [玩家阅读入口](Players/README.md) | 模组介绍 → 快速入门 → 按需查机制与常见问题 |
| 查看版本交付 | [发布与验证索引](Releases/README.md) | 当前版发布说明 → 验证；跨版本查[更新日志](../CHANGELOG.md) |
| 继续设计新功能 | [当前规划索引](Design/README.md) | 总体状态 → 当前专项 → 相关历史设计 |
| 查已完成实现与修复 | [已交付开发记录](Development/Completed/README.md) | 主题完成记录 → 对应阶段与修复 |
| 查尚未关闭的实机事项 | [待验收事项](Development/Verification/README.md) | 已知缺口 → 关联实现 → 测试步骤 |
| 理解旧代码与设计取舍 | [代码与机制复盘](Development/Reviews/README.md) | 先看基线，再看复盘与后续实现 |
| 追溯交接和提示词 | [历史交接资料](Development/Reference/README.md) | 用于定位与追溯，现行规则以实现和玩家指南为准 |
| 编译、测试、打包或维护文档 | [开发维护入口](Maintenance/README.md) | 环境与命令 → 测试说明 → 发布检查 |
| 查图标与贴图来源 | [美术资源索引](Art/README.md) | 当前选定资源 → 说明 → 历史尝试 |

## 三条阅读路线

- **新玩家**：[模组介绍](模组介绍.md) → [快速入门](../读我，玩法介绍.md) → [机制详解](../机制详解.md)。英文路线见 [Player documentation](Players/README.md#english-reading-order)。
- **接手开发**：[当前版本说明](Releases/2.4.0/2.4.0发布说明.md) → [待验收事项](Development/Verification/README.md) → [对应已交付记录](Development/Completed/README.md) → [当前规划](Design/README.md)。
- **准备发布**：[发布与打包](Maintenance/发布与打包.md) → [测试说明](../Tests/README.md) → [版本文档](Releases/README.md)。

## 目录与边界

```text
Docs/
  模组介绍.md                 当前正式版的对外双语简介
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

新增、归档、移动、版本同步和链接检查规则见[文档维护约定](Maintenance/文档维护约定.md)。打包脚本会递归包含 `Docs/`；其中指向源码、测试和本地构建产物的链接需要完整仓库环境。
