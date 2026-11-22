# 文档总索引

[项目首页](../README.md) · [玩家阅读入口](Players/README.md) · [当前规划](Design/README.md) · [开发资料](Development/README.md)

## 当前正式版本

**SSC 2.5.3 于 2026-11-21 正式发布，面向 RimWorld 1.7。** 同一安装包为 RimWorld 1.6 提供 SSC 2.4.1。2.5.2 开发里程碑的实现随本包交付。

[下载 2.5.3](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.5.3) · [版本说明](Releases/2.5.3/2.5.3发布说明.md) · [正式发布验证](Releases/2.5.3/2.5.3版本验证.md#ssc-253-release) · [完成索引](Development/Completed/README.md#ssc-253-completed) · [历史版本](Releases/README.md)

## 按用途选择入口

| 用途 | 入口 | 推荐阅读顺序 |
| --- | --- | --- |
| 了解功能、维护发布流程页面 | [双语模组介绍](模组介绍.md) | English 在前、中文在后；先看当前版本与功能范围 |
| 安装和游玩 | [玩家阅读入口](Players/README.md) | 简介 → 快速入门 → 按需查机制与常见问题 |
| 了解版本归属 | [发布与验证索引](Releases/README.md) | 版本说明 → 开发归并 → 版本验证；已发布版本从验证页进入正式发布归档 |
| 接手已完成功能 | [已交付开发记录](Development/Completed/README.md) | 主题完成核对 → 对应阶段 → 历史设计 |
| 新增个人能力开关 | [现行接入规范](Development/Reference/个人能力开关接入规范.md) | [开发协作约定](../AGENTS.md) → 保存源与适用边界 → 原载体后端 → 公共查询、UI 与提交 → 生命周期验收 |
| 查明确的验证缺口 | [待验收事项](Development/Verification/README.md) | 尚待确认范围 → 实现依据 → 实机步骤 |
| 继续设计 | [当前规划索引](Design/README.md) | 总体状态 → 未完成需求 → 关联历史；已完成方案在 [Archive](Design/Archive/README.md) |
| 查叙事与文案基准 | [设定索引](设定/README.md) | 已交付机制／分支表 → 叙事设定 → 实际定稿 |
| 查开发规范与追溯交接 | [开发规范与历史参考](Development/Reference/README.md) · [开发日志](Development/开发更新记录.md) · [代码复盘](Development/Reviews/README.md) | 现行接入规范用于后续开发；历史交接按日期／基线查，旧待办保留当时语境 |
| 编译、测试、发布和文档维护 | [维护入口](Maintenance/README.md) | [发布与打包](Maintenance/发布与打包.md) → [测试说明](../Tests/README.md) → 版本检查 |
| 贴图、声音与提示词 | [美术索引](Art/README.md) | [PNA 资源表](Art/SSC纳米催情液武器资源.md) → 当前选定原稿 → 历史尝试 |
| 绘制武器资源 | [绘制与导入经验](Art/RimWorld武器贴图绘制与导入经验.md) | 构型与小尺寸辨识 → 导出 → 实机验证 |

## 三条阅读路线

- **新玩家**：[模组介绍](模组介绍.md) → [快速入门](../读我，玩法介绍.md) → [机制详解](../机制详解.md)。英文路线见 [English reading order](Players/README.md#english-reading-order)。
- **接手开发**：[开发协作约定](../AGENTS.md) → [开发规范与历史参考](Development/Reference/README.md) → [2.5.3 接手顺序](Development/README.md#ssc-253-reading-order)。实施与验收见[完成记录](Development/Completed/README.md#ssc-253-completed)，独立复测见[待验收](Development/Verification/README.md)，未完需求见[当前规划](Design/README.md#当前专项规划)。
- **维护发布流程**：[2.5.3 版本验证](Releases/2.5.3/2.5.3版本验证.md) → [构建与打包](Maintenance/发布与打包.md) → [测试索引](../Tests/README.md)。本次只整理文档时按[文档维护约定](Maintenance/文档维护约定.md#移动与链接检查)检查。

## 目录与状态

```text
Docs/
  模组介绍.md                  当前正式版本双语简介
  Players/                     玩家入口和阅读顺序
  Design/                      当前需求与未完成设计
    README.md                  规划入口与状态索引
    未来内容开发规划.md        未完成需求及历史编号
    性能测量与定点优化首批规划.md  暂缓方案留存
    浪漫控制后续社交关系与浪漫事件拓展规划.md  未开发方向
    Archive/                   已交付设计及历史版本更新摘录
  Development/
    开发更新记录.md            按日期追溯
    Completed/                 实现、修复与已关闭批次
      无主性奴/                 第 6／6.1 项完成索引、五批实现与验收
      奴性爆发/                 第 7 项分批实机验收与文件定位
      调教管理/                 窗口重做与多调教员的实施及验收
      宠物特化/                 兔家政与狐狸科研医疗的分批验收
      堕落凝胶/                 四步实现与完成核对
      催情发烟器/               太空首版、三档机制与贴图验收
      PNA阶段维持/               地图与旅队实施及验收
    Verification/              尚未完成的实机、部署与显示核查
    Reviews/                   代码和机制复盘
    Reference/                 现行规范、心情分支表与文案依据
      History/                 已使用的交接、提示词和定稿底稿
        索引沿革/              从现行入口移出的历史导航与状态摘录
  Releases/<版本>/             版本说明、归并核对、验证与历史公告
  Maintenance/                 构建发布和文档维护
  Art/                         原稿、资源表、提示词与备份
  设定/                        叙事设定与文案基准
```

根目录四份玩家指南保留固定路径。已完成内容、明确待验收事项和未来提案分别维护；开发里程碑登记、分批实机与正式发布门禁分别记录。旧版导航与分批状态见[索引沿革](Development/Reference/History/索引沿革/README.md)，当前文档归属与维护方法见[维护约定](Maintenance/文档维护约定.md)。

源码旁的专用说明：[旧 RimTalk 归档](../Archive/RimTalk/README.md) · [Shader 源码与工具](../Sexslavecraft/ShaderSources/README.md)。独立工具从 [Tools](../Tools/README.md)进入，旧实现与调查脚本从[根目录 Archive](../Archive/README.md)追溯。
