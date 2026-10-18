# SSC 近战短矛贴图与载体调整

日期：2026-10-18。基线：已获实机确认的第二步 XML 提交 `2fe21a9`。

维护者提供了一张他人绘制的近战武器原图，最终明确只去除白底、不改变矛的结构；原刺棍／刺杖的显示名称改为短矛。本批只接入这一件近战武器的透明底贴图和文案；两件远程武器的贴图及“纳米催情液效力”显示未实现。

**状态：维护者已确认 512×512 版本的握持角度和缩放正常；待命竖直握持试验已撤回。维护者确认 256×256 版本效果可以并决定保留，短矛贴图及改名本轮验收关闭。** 先前第二步 XML 的实机确认保留为原批次结果。本轮提交短矛贴图、改名及相关文档，不发布安装包。

[首批规划](../../Design/SSC武器拓展首批开发规划.md) · [原 XML 实现与验收](SSC武器拓展步骤二XML实现与测试.md)

## 1. 资源与外观

先通过内置 `image_gen` 去除白底，保留紫色药液、金属刃、绑绳、木杆和尾端圆头。后续维护者要求 512×512、矛体沿对角线；本轮直接对已使用的透明 PNG 做确定性旋转、等比缩放及透明留边，矛体结构不再重绘。背景去除阶段的生成式输出不宣称与原图逐像素一致。

资源为 [SSC_PnaShortSpear.png](../../../1.7/Textures/Things/Item/Equipment/SSC_PnaShortSpear.png)，当前采用版本为 256×256、RGBA（此前为 512×512），Alpha 范围 0～255；背景含真实全透明像素。输入为本次上传的 `codex-clipboard-544ac7da-a8f8-49e0-8e32-7a4bccf6d675.png`，原图不作为运行资产打包；本次记录不虚构原绘者署名。

武器 `graphicData.texPath` 改为 `Things/Item/Equipment/SSC_PnaShortSpear`，保留 `Graphic_Single`。移除原临时图的全局紫色 tint，防止木杆、绑绳和金属边缘一并染紫；当前显示参数 `drawSize=1`、`equippedAngleOffset=45`。图内矛体为左下至右上的 45° 对角线，方形画布保持等比显示。维护者已确认当前 256×256 版本效果可以；未逐项反馈的地面／图标等场景不补记为专项验证通过。

## 2. 载体、名称与兼容

当前显示名为“纳米催情液短矛”，英文为 `aphrodisiac nanofluid short spear`；简中、繁中、英文、俄文的标签、设定描述、矛尖／矛柄及死亡文案已同步到语言镜像。保留原伤害显示名“纳米催情液刺击”。不会恢复已删除的“制造时浸覆、无需补液”描述。

`SSC_Weapon_PnaSpikeStaff`、`SSC_Damage_PnaStaffStab`、专用 ToolCapacityDef 和 ManeuverDef 名称保留，因此既有武器和配方引用仍有效，不新增武器副本或存档迁移。伤害 5、穿甲 12%、冷却 1.8 秒、命中评分＋2、主刺击选择权重 2、PNA 基数 0.15、制作成本及普通柄击参数均不变。生产 C#／DLL 保持贴图阶段之前的版本；已撤回的待命姿势试验另见第 6 节。1.6／Common 与版本号不变。

## 3. 验证边界

- PnaLoad 470/470：原有物理／药效与 XML 接线回归通过，测试名称同步为短矛；没有为改名和贴图新增机制用例。
- 语言镜像 367 份一致，版本布局与冻结基线 538 项通过。
- 实际 1.7 XML 继承／API 检查 52/52、289 个字段通过，当前方形贴图的显示参数已纳入检查。
- PNG 检查采用只读方式确认尺寸、RGBA 与透明通道；工作区资产与确定性处理产物逐字节相同。
- 贴图阶段不重复全量 36 套回归；第二步 3284/3284 为历史接入结果。当时无新生产代码，未重编译生产 DLL；后续待命补丁曾编译验证，但已撤回，另见第 6 节。原生 XML 继承／API 检查另存 `.builds/pna-short-spear/`，不覆盖原 XML 批次证据。

## 4. 生成提示

2026-10-18 按维护者要求同步本机 `D:/STEAM/steamapps/common/RimWorld/Mods/RJW-SexSlaveCraft-TieJin-Modify`。覆盖前备份 10 个 XML／语言文件，新增 1 张短矛贴图，共复制 11 个文件；同步后 462 个 1.7 文件与工作区一致，1.6／Common 的 532 个文件保持原哈希。备份和清单位于 `.builds/pna-short-spear/game-sync/backup-20261018-184416-e641a96a/`，结果为同目录上一级的 `result.json`。DLL、版本及加载目录均未改动；本次未标记新贴图实机通过。

模式：内置 `image_gen`，编辑本次附件，`transparent_background=true`。最终提交给图像工具的提示如下，保留供后续调整使用：

```text
Use case: background-extraction. Image 1 is the exact edit target. Remove ONLY the plain white background and make it genuinely transparent. This is a faithful cutout, NOT a redraw, redesign, or reinterpretation. Preserve the original weapon EXACTLY: same broad leaf-shaped purple-and-silver spearhead, same purple drips and highlights, same dark wrapping, same length and thickness of wooden shaft, same LARGE ROUND WOODEN KNOB at the bottom, same black outlines, same colors, same angle, same proportions, same silhouette. Do not lengthen the shaft, slim the blade, simplify details, straighten or rotate the weapon, or remove the bottom knob. Preserve the original tall portrait composition and close framing of the source. The silver and white highlights INSIDE the spearhead must remain opaque and unchanged; remove only the connected background outside the black outline. Output the complete original weapon on true transparent RGBA background, no white border, no checkerboard baked into the image, no ground shadow, no extra object. Again: same original drawing, only background removed.
```

## 5. 512×512 对角线与握持修正

维护者实机反馈纵向贴图的握持角度及缩放有问题，要求改为 512×512、矛体沿对角线。当前以已同步的透明 PNG 为输入，使用 .NET System.Drawing 做旋转、等比缩放、居中和透明留边，不重新生成矛的结构。输出为精确 512×512 RGBA，主轴角度约 -45°（图像坐标），有效图形范围约 `(25,25)` 至 `(487,487)`，四角透明。

核对实际 1.7.4981 的 `PawnRenderUtility.DrawEquipmentAiming` 与 `MeshMakerPlanes.NewPlaneMesh`：贴图的向右上对角线需要正 45° 的装备偏移，先前的负值方向错误。现设 `drawSize=1`、`equippedAngleOffset=45`；基于实际 UV／旋转公式核对东南西北及水平翻转的角度模型，矛尖与目标方向一致。模型通过不代表已完成新的实际持握确认。

处理脚本、输入备份、结果和角度模型在 `.builds/pna-short-spear/layout-512/`；不覆盖先前同步备份。游戏内需重新载入资源后观察实际尺寸、朝向和手部位置。其余贴图、效力显示、战斗数值及存档 Def ID 均不变。

本轮 PnaLoad 470/470、实际 1.7 原生 XML／API 52/52 通过。修正后已同步回同一本机 SSC 目录，备份后替换贴图与武器 XML 两个文件；462 个 1.7 文件一致，532 个冻结文件未变。备份为 `.builds/pna-short-spear/layout-512/game-sync/backup-20261018-185503-4a607311/`，重启游戏后复测新显示，尚不登记为外观实机通过。

## 6. 待命竖直握持试验已撤回

维护者确认当前 512×512 贴图的握持角度与缩放正常后，曾尝试通过独立 Harmony 补丁让短矛在待命携带时矛尖朝上。该方案原生参数检查 25/25、PnaLoad 470/470、1.7 编译通过，但维护者随后实机反馈效果不佳，要求回滚。

现已移除专用绘制补丁、源码登记和原生探针，使用试验前备份恢复 DLL、调试符号与 BUILD-INFO.json，恢复原版待命绘制。保留已确认正常的 512×512 对角线贴图、drawSize=1、equippedAngleOffset=45 及全部既有短矛改名／数值调整。原生检查通过只代表参数和入口符合预期，不代表画面效果获认可。

试验前备份位于 `.builds/pna-short-spear/idle/game-sync/backup-20261018-190238-25b102e5/`；本次回滚按该备份哈希核对原文件，不重新编译或修改冻结的 1.6／Common。同步回本机后需完全退出并重启游戏，方可卸载内存中的旧绘制补丁。

已同步恢复本机三个文件；462 个 1.7 文件与工作区一致，532 个冻结文件未变，布局检查 538 项通过。回滚前备份为 `.builds/pna-short-spear/idle-rollback/game-sync/backup-20261018-190512-c0e1979b/`。本轮未提交。

## 7. 256×256 版本确认保留

按维护者要求，将已确认的 512×512 PNG 整幅等比缩小到 256×256；使用 System.Drawing 高质量双三次重采样，不重绘、裁切或改变矛的结构与对角线朝向，透明留边也按相同比例缩小。已确认 256×256、32 位 RGBA、Alpha 范围 0～255，并查看最终图片。游戏内 drawSize=1、equippedAngleOffset=45 和原版待命姿势均保留，像素尺寸变化用于比较细节呈现，不主动缩小游戏内武器尺寸。

原 512×512 文件备份和处理脚本保留在 `.builds/pna-short-spear/layout-256/`；本次只更新一张 PNG，不改 XML／C#／DLL，不重编译或重复机制回归。已备份并同步本机一张贴图，462 个 1.7 文件一致，532 个冻结文件未变。同步前备份为 `.builds/pna-short-spear/layout-256/game-sync/backup-20261018-190723-b25d6e56/`。维护者随后反馈“256这版效果可以”，决定保留该版本并提交；据此关闭本轮短矛外观验收。两件远程武器贴图和效力显示继续暂缓。

本次提交前仅整理当前状态与实机反馈，并将武器 XML 注释中的分辨率同步为 256×256；此前测试结果沿用原验证记录，不将文档收尾记为新一轮机制测试。待命竖直补丁及其探针均已撤回，生产 C#、DLL 和构建记录与本轮之前一致。
