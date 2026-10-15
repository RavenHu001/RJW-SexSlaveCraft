# 玩家阅读入口 / Player documentation

[项目首页](../../README.md) · [文档总索引](../README.md) · [模组介绍 / Overview](../模组介绍.md)

正式版本 / Current release：**RimWorld 1.6 · SSC 2.4.0**。玩家指南的 PNA 章节另同步已验收、尚未发布的 `codex/pna-reset` 分支；现有 2.4.0 下载附件不包含这些调整。 / PNA guide sections additionally cover the accepted, unreleased development branch, not the existing 2.4.0 package.

## 中文阅读顺序

1. [模组介绍](../模组介绍.md#中文)：先了解培养、六种特化、人格凝胶与身体改造。
2. [快速入门](../../读我，玩法介绍.md#2-第一次游玩怎么开始)：完成研究、选择身份、指派调教员，建立第一套培养流程。
3. [恶堕与锁链](../../读我，玩法介绍.md#4-恶堕与锁链) → [契约仪式](../../读我，玩法介绍.md#5-绑定仪式)：理解日常积累与阶段进阶。
4. 按殖民地需要选择特化，再读[终极特化](../../读我，玩法介绍.md#10-如何获得终极专精)、人格转移与凝胶化。
5. 遇到问题先查[常见问题](../../读我，玩法介绍.md#21-常见问题速查)，需要数值和完整条件时再查[机制详解](../../机制详解.md)。

从旧版升级可先读[2.4.0 发布说明](../Releases/2.4.0/2.4.0发布说明.md#中文)，重点了解欲望之种、主人资格和成长面板。实际遇到问题时可直接看[2.4.0 常见问题](../../读我，玩法介绍.md#21-常见问题速查)。

## 按问题查找

| 想了解什么 | 入门说明 | 深入参考 |
| --- | --- | --- |
| 主人资格、研究与开局 | [主人资格入口](../../读我，玩法介绍.md#主人资格入口240) | [身份与调教面板](../../机制详解.md#3-调教-itab-与身份) |
| 主人成长、经验与伤害分担 | [主人成长与面板](../../读我，玩法介绍.md#主人成长与面板240) | [四阶段完整数值](../../机制详解.md#312-主人阶段与实践经验240) |
| 培养与阶段进阶 | [恶堕与锁链](../../读我，玩法介绍.md#4-恶堕与锁链) | [恶堕、衰减与锁链](../../机制详解.md#6-恶堕需求衰减与锁链) |
| 六种特化的用途 | [模组介绍](../模组介绍.md#中文) | [研究树速查](../../机制详解.md#23-研究树速查) |
| 人格凝胶与终极化 | [如何获得终极专精](../../读我，玩法介绍.md#10-如何获得终极专精) | [人格凝胶闭环](../../机制详解.md#14-终极专精与人格凝胶闭环) |
| 身体改造 | [局部凝胶化](../../读我，玩法介绍.md#13-局部凝胶化) · [全身凝胶化](../../读我，玩法介绍.md#14-全身凝胶化) | [全身改造机制](../../机制详解.md#17-全身凝胶化) |
| 设置与兼容 | [重要设置](../../读我，玩法介绍.md#18-重要设置) · [主要兼容](../../读我，玩法介绍.md#19-主要兼容) | [已知待验收事项](../Development/Verification/README.md) |

## English reading order

1. Read the [mod overview](../模组介绍.md#english) for features and colony roles.
2. Follow [Getting Started](../../QUICK_START_EN.md#2-getting-started) to set up research, identities and trainers.
3. Read [Corruption and the Bond](../../QUICK_START_EN.md#4-corruption-and-the-bond), then [Binding Ritual](../../QUICK_START_EN.md#5-binding-ritual) for stage progression.
4. Choose a specialization, then read [Final Specializations](../../QUICK_START_EN.md#10-final-specializations) before personality transfer and body modification.
5. Use [Quick Troubleshooting](../../QUICK_START_EN.md#21-quick-troubleshooting) for problems and the [Complete Player Guide](../../PLAYER_GUIDE_EN.md) for exact conditions and values.

Upgrading from an older version? Start with the [2.4.0 release notes](../Releases/2.4.0/2.4.0发布说明.md#english). Master qualification is covered in the [quick start](../../QUICK_START_EN.md#master-qualification-entry-240); see [Master growth and panel](../../QUICK_START_EN.md#master-growth-and-panel-240) for controls or [the full stage table](../../PLAYER_GUIDE_EN.md#312-master-progression-and-practice-240) for values. The [2.4.0 FAQ](../../QUICK_START_EN.md#21-quick-troubleshooting) covers missing quests, qualification and the moved damage-sharing control.

## 2.4.0 升级要点 / Upgrade essentials

- **资格、身份与研究分别管理。** 默认可直接选择主人，但不会取得永久资格；种子不自动切身份，研究蓝图也不等于完成研究。
- **成长看主人面板。** 本人完成日常调教 +1、整场契约仪式 +3；伤害分担也迁入此面板。退出主人保留经验，返回恢复原阶段。
- **人格凝胶按来源替换。** 资格和成长随来源人格迁移，不与宿主旧值取高；克隆不继承。
- **开发与发布分开。** PNA 重置和玩法五步已在开发分支完成并验收，尚未发布；欲望侵染、新增主人主动技能及可选性奴主动补给仍待开发／设计，见[当前规划](../Design/README.md)。专项存读档、旅队补给和长期平衡见[待验收](../Development/Verification/README.md)。

Qualification, current identity and research are separate. Direct Master selection grants no permanent qualification; seeds grant no instant research completion. Use the Master panel for growth and damage sharing. Gel implantation replaces records with the source personality's records; clones do not inherit them. Future features and pending in-game checks are listed separately in the linked planning and verification indexes.

## 版本与资料用途 / Versions and document roles

当前指南以 2.4.0 为基础，PNA 章节明确标注未发布分支的最新规则；正文中的较早版本号通常表示功能引入时间。查旧版本请从[发布索引](../Releases/README.md)进入对应记录。开发方案与历史复盘另收在[开发资料](../Development/README.md)和[当前规划](../Design/README.md)。

The guides use 2.4.0 as their base; PNA sections explicitly describe the accepted, unreleased branch. Earlier version numbers in individual sections usually identify when a feature was introduced. Use the [release index](../Releases/README.md) for historical versions.
