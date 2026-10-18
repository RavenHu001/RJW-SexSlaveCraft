# SSC 近战短矛贴图与载体调整

日期：2026-10-18。基线：已获实机确认的第二步 XML 提交 `2fe21a9`。

维护者提供了一张他人绘制的近战武器原图，最终明确只去除白底、不改变矛的结构；原刺棍／刺杖的显示名称改为短矛。本批只接入这一件近战武器的透明底贴图和文案；两件远程武器的贴图及“纳米催情液效力”显示未实现。

**状态：维护者认可最新加粗横置紫色阔刃短矛及撤去额外角度偏移的效果，授权提交本轮贴图、科技／武器／配方文本及文档。当前短矛为 256×256、drawSize=1，无额外装备角度偏移。效力显示暂缓。** 先前第二步 XML 的实机确认保留为原批次结果。本轮提交短矛贴图、改名及相关文档，不发布安装包。

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

## 后续预览与淬毒工艺文案

吹箭筒批次 `4037846` 提交后，维护者提出短矛收细、横置，并进一步要求保留阔刃构型、均匀紫色浸染金属表层、缩小矛头和尾端圆球。已生成两版高清预览，最新位于 `.builds/pna-short-spear/preview-horizontal-slim-v2-hd/`。按维护者修订，不调整装备角度；这些新预览尚未替换运行贴图或同步游戏，现有对角线贴图与装备角度 45 保留。

按后续文案要求及维护者范围修订，科技与三件武器的源 Def、四语描述统一加入“沿用传统淬毒工艺，使紫色纳米催情液均匀浸润矛头金属表层”的设定；保留浅刺送液、真实创伤与可能致死的原说明。英文按武器淬毒／涂毒技法表达，不额外设定加热或金属热处理流程。科技保留低技术阶段初步探索压制敌人与奴隶的定位；短矛写矛头表层，吹箭筒和轻弩分别写细箭／弩矢金属箭头的处理，不暗示木杆、尾羽或整个发射器都靠浸透施药。阔刃设定与预览对应，名称、Def ID、配方、材质／伤害／穿甲／PNA 剂量等机制保持不变。

XML 结构对比确认研究与武器源文件仅上述四项 description 改动；四语 XML、全部 367 份语言镜像及 16 份既有 Def 镜像检查通过。不新增机制测试、不编译；本批新描述与预览均未同步游戏或提交，待后续确认。

后续按维护者反馈精简三件武器描述：工艺细节集中保留在科技说明；物品仅简述淬毒／携液设定及各自优缺点，四语与镜像同步。科技描述、所有数值、装备角度及贴图不改，本批仍未同步游戏或提交。

## 淬毒设定文本与自动配方描述同步

维护者随后授权修改制造配方描述并同步游戏。三种配方继续由 recipeMaker 生成，不添加独立 RecipeDef、不改变产物、材料、技能、工作台或成本。新增四语 `DefInjected/RecipeDef/SSC_PnaPrimitiveWeapons.xml`，覆盖 `Make_SSC_Weapon_PnaSpikeStaff`、`Make_SSC_Weapon_PnaBlowpipe`、`Make_SSC_Weapon_PnaLightCrossbow` 的 description，并同步源码语言镜像。

简中配方描述为：

- 制作短矛，并以淬毒工艺将纳米催情液浸入矛头表层。
- 制作吹箭筒及配用细箭，以纳米催情液淬染箭头。
- 制作轻弩及配用弩矢，以纳米催情液淬染箭头。

核对实际 1.7.4982（MVID a46e6f45-b616-4261-bb63-a9471bf7402a）的 CreateRecipeDefFromMaker：自动配方原描述来自 RecipeMakeDescription，不复制产品说明；本批通过生成配方的本地化键覆盖文本。配方提及配用箭具仅为设定描述，不新增独立箭具产物或耗弹机制。

12 个四语配方键与生成 ID 核对通过；371 份语言镜像一致、16 份 Def 镜像一致，PnaLoad 470/470、布局 538 项通过。文本级改动不编译或重复全量回归。

已将本轮科技／三武器／三配方文本同步本机：备份后复制 14 个运行 XML（10 个覆盖、4 个新增），471 个 1.7 文件一致，532 个冻结文件未变。备份为 `.builds/pna-weapon-text/game-sync/backup-20261018-205549-f9bec0da/`。短矛横置预览仍未接入，现有贴图和装备角度 45 不变，效力显示未开始。重启后检查研究、物品及配方信息中的描述与语言覆盖；本批文本尚未登记实机通过，暂不提交。

## 横置紫色阔刃贴图同步

维护者明确要求同步此前的最新预览。以第二版横置收细高清图为输入，确定性等比导出 256×256 RGBA，Alpha 0～255，已查看结果。矛头整体呈紫色金属浸染效果，保留阔刃构型并缩小矛头与圆形尾端。输入、脚本、结果与旧对角线 PNG 位于 `.builds/pna-short-spear/layout-horizontal-256-v2/`。

已替换运行 PNG，并仅更新 XML 中对应贴图方向的注释；按维护者要求保持 equippedAngleOffset=45、drawSize=1，不补偿新的水平源图朝向，不改任何机制或 C#／DLL。参数核对通过，本轮仅图像／注释变化，不重复机制回归或编译。

备份后同步本机 PNG 和 XML 两个文件，471 个 1.7 文件一致、532 个冻结文件未变。备份为 `.builds/pna-short-spear/layout-horizontal-256-v2/game-sync/backup-20261018-205726-c0b1a639/`。重启后比较外观及持握，尚未登记新贴图实机通过，暂不提交。前述“未接入预览”为历史阶段。

## 横置版加粗与撤去额外角度偏移

维护者实机反馈短矛仍偏细，希望接近截图右侧堂丸武器的观感，并授权尝试去除本模组的待机角度设计。核查源码及项目登记，先前 DrawCarriedWeapon 待命专用补丁早已撤回，本批没有残留专用绘制补丁；当前额外旋转来自短矛 XML 中保留的 equippedAngleOffset=45。

以第二版高清横图生成加粗第三版，增强矛杆、握持部与外轮廓，保留紫色金属阔刃、小圆形尾端、水平朝右和暖木／深灰绑带材质；导出 256×256 RGBA、Alpha 0～255，已查看最终图。此为按参考观感调整，不宣称与截图武器像素粗细严格一致。堂丸模组另外为原版长矛设 drawSize=1.3，本短矛保持显示尺寸 1，不一并放大长度。输入、导出脚本和旧图位于 `.builds/pna-short-spear/layout-horizontal-256-v3-thicker/`。

按本次最新授权撤去短矛 equippedAngleOffset=45 配置；核对原版完整继承链均未设置此字段，恢复默认零额外偏移。该字段同时参与待命与攻击绘制，不是独立待命开关；源图水平朝右，由原版绘制决定姿势。无新增 Harmony、C# 或 DLL 变化，伤害和 PNA 等参数不变。此前“保持 45”仅对应更早的要求。

PnaLoad 470/470、图片与角度继承检查通过。备份后同步本机 PNG／XML 两个文件，471 个 1.7 文件一致、532 个冻结文件未变；备份位于 `.builds/pna-short-spear/layout-horizontal-256-v3-thicker/game-sync/` 下的时间戳目录。待重启检查待命姿势、瞄准朝向与粗细，本批仍未提交。

## 横置版与淬毒文本确认提交

维护者反馈“可以，提交”，据此关闭最新加粗横置短矛、额外角度撤除及本轮淬毒文本的验收收尾。最终采用第三版横图 256×256、显示尺寸 1，不设置额外装备偏移；科技保留初步探索定位，三武器描述精简，三制造配方通过四语本地化补充淬毒工艺说明，源文本与语言镜像一致。

沿用此前 PnaLoad 470/470、布局 538 项、371 份语言镜像／16 份 Def 镜像检查及实际同步结果；收尾核对最终文件和状态，不重复机制回归或编译，无数值、C#／DLL、版本或冻结资源变化。前述各节的未同步／未提交／待实机内容保留为阶段历史。新提出的远程箭头金属质感仅另行分析，不纳入本次提交。
