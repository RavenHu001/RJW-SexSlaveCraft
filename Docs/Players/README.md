# 玩家阅读入口 / Player documentation

当前指南对应 **2.4.3 开发版本，未发布**；新增功能仅在 1.7，1.6 保留 2.4.1 冻结内容。公开下载仍为 [2.4.2](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.2)，不包含后续关系概览、低科技、三代九武器与恩赐剂量调整。见[本版范围](../Releases/2.4.3/2.4.3发布说明.md)。

[项目首页](../../README.md) · [文档总索引](../README.md) · [双语模组介绍](../模组介绍.md) · [版本索引](../Releases/README.md)

## 中文阅读顺序

1. [模组介绍](../模组介绍.md#中文)：先了解玩法与版本范围。
2. [快速入门](../../读我，玩法介绍.md#2-第一次游玩怎么开始)：研究、主人资格、身份和调教员设置。
3. [恶堕与锁链](../../读我，玩法介绍.md#4-恶堕与锁链) → [契约仪式](../../读我，玩法介绍.md#5-绑定仪式)：发展与阶段进阶。
4. [PNA 与泌乳](../../读我，玩法介绍.md#7-pna-与泌乳)：三代武器及其伤口／修复规则。
5. [人格与终极化](../../读我，玩法介绍.md#10-如何获得终极专精)，再按需查[完整机制](../../机制详解.md)与[常见问题](../../读我，玩法介绍.md#21-常见问题速查)。

## 按问题查找

| 问题 | 入门 | 完整规则 |
| --- | --- | --- |
| 主人资格与成长 | [资格](../../读我，玩法介绍.md#主人资格入口240) · [面板](../../读我，玩法介绍.md#主人成长与面板240) | [四阶段](../../机制详解.md#312-主人阶段与实践经验240) |
| 关系概览、头像定位与批选 | [操作](../../读我，玩法介绍.md#主从关系概览243仅-17) | [完成核对](../Development/Completed/主从关系概览完成核对.md) |
| 浓缩与低科技 | [PNA](../../读我，玩法介绍.md#7-pna-与泌乳) | [浓缩](../../机制详解.md#112-浓缩) |
| 中世纪／工业武器 | [原始](../../读我，玩法介绍.md#原始纳米催情液武器17-开发分支) · [工业](../../读我，玩法介绍.md#工业纳米催情液武器17-开发分支) | [中世纪](../../机制详解.md#116-原始纳米催情液武器与视觉反馈) · [工业](../../机制详解.md#117-工业纳米催情液武器) |
| 太空武器、无伤吸收与修复 | [操作](../../读我，玩法介绍.md#太空纳米催情液武器243仅-17) | [完整规则](../../机制详解.md#118-太空纳米催情液武器) |
| 欢愉恩赐与产量剂量 | [开启](../../读我，玩法介绍.md#欲望侵染) | [数值](../../机制详解.md#115-主人欲望侵染) |
| 凝胶外观 | [全身凝胶化](../../读我，玩法介绍.md#14-全身凝胶化) | [2.4.2 引入的外观设置](../../机制详解.md#176-凝胶外观242仅17) |
| 验证与兼容 | [主要兼容](../../读我，玩法介绍.md#19-主要兼容) | [专项边界](../Development/Verification/README.md) |

## English reading order

The guides cover **2.4.3 development, unreleased**, on 1.7, with the 1.6 runtime frozen at 2.4.1. The public download is still 2.4.2 and lacks the later additions. Start with the [2.4.3 scope](../Releases/2.4.3/2.4.3发布说明.md#english) when comparing versions.

1. Read the [mod overview](../模组介绍.md#english).
2. Follow [Getting Started](../../QUICK_START_EN.md#2-getting-started) for research, qualification, identities and trainers.
3. Read [Corruption and the Bond](../../QUICK_START_EN.md#4-corruption-and-the-bond), then [Binding Ritual](../../QUICK_START_EN.md#5-binding-ritual).
4. Read [PNA and Lactation](../../QUICK_START_EN.md#7-pna-and-lactation) for all three weapon eras; [spacer rules](../../PLAYER_GUIDE_EN.md#118-spacer-pna-weapons) explain protection and repair.
5. Continue to [Final Specializations](../../QUICK_START_EN.md#10-final-specializations), [troubleshooting](../../QUICK_START_EN.md#21-quick-troubleshooting) and the [full guide](../../PLAYER_GUIDE_EN.md).

Use [Bond Overview](../../QUICK_START_EN.md#bond-overview-243-17-only) for portrait navigation and member selection; see [Bestow Bliss](../../PLAYER_GUIDE_EN.md#115-master-desire-infestation) for output-based doses. Industrial rules are in [11.7](../../PLAYER_GUIDE_EN.md#117-industrial-pna-weapons).

## 2.4.3 升级要点 / Upgrade essentials

- **旧枪移除：** 1.7 不再加载旧简易发射器、弹丸、配方及专属研究，不提供旧定义存档迁移；1.6 内容保留。
- **武器规则：** 六把涂覆武器保留真实伤口；太空主攻击对有效链目标无伤吸收并修复，护盾／掩体仍拦截。原版品质影响伤害／穿甲，不放大药效／修复。
- **恩赐剂量：** 身体产液能力折算 3～100 点，参考产量 20 对应 8 点，普通产出者仍为 2 点。
- **继承功能：** 资格、当前身份、研究与成长仍分别管理；2.4.2 的凝胶外观继续保留，较早章节版本号不改为本版新增。

**1.7 saves referencing the removed launcher have no migration path.** Coated weapons retain wounds; spacer main attacks protect eligible bodies, while shields/cover still matter. Quality affects physical damage/penetration, not PNA or repair. Bestow Bliss now derives 3–100 load points from body output; normal producers still supply 2 points.

## 版本与资料用途 / Versions and document roles

开发指南以当前仓库内容为准，正式下载以对应标签的发布说明为准；当前 DLL／About 等版本仍待 2.4.3 发布准备时对齐，见[验证记录](../Releases/2.4.3/2.4.3版本验证.md)。已交付、专项待验收和未来规划分别登记，历史测试计数保留执行日期。

Current checkout guides describe development content; tagged release notes describe the download. Binary/About metadata alignment and a new full release gate are still required before publishing 2.4.3. Historical validation and acceptance retain their original scope.
