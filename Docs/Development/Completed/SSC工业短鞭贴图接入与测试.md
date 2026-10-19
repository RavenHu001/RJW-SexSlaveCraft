# SSC 工业短鞭贴图接入与测试

当前状态（2026-10-19）：工业三武器的机制、正式美术与两枪声音均已分批完成，统一见[完成核对](SSC工业时代纳米催情液武器完成核对.md)。下文保留本批实施时点；其中占位图、待绘制、临时参数及测试数字不代表当前缺项。

日期：2026-10-19。基线：步骤一提交 `01860bb`。**状态：维护者于 2026-10-19 确认“实机测试正常”，本批贴图与 XML 已验收，按授权提交；未打包发布。** 本轮只处理短鞭贴图及其引用，两把针枪与射针仍使用原占位资源。

[资源表](../../Art/SSC纳米催情液武器资源.md) · [工业规划](../../Design/SSC工业时代纳米催情液武器开发规划.md) · [步骤一验收](../Completed/SSC工业武器步骤一实现与测试.md)

## 选图与导出

维护者认可 imagegen 重绘的粗描边／简洁色块版本：单柄连接三股独立编织短鞭束，握柄在左、鞭束向右，鞭束收细并嵌紫色金属刃片；柄尾为可见紫色 PNA 和液面的透明储液端盖。液面仅为静态美术，不新增储液量或运行耗液机制。

认可原图来自 `C:/Users/24196/.codex/generated_images/01a14d5a-bbd6-79f3-9356-75f2d8ad5113/exec-28c3fe14-a0d2-4bf7-bf78-13b58308e48a.png`，1774×887 RGBA，SHA-256 `c2b67cca995d82a0102d8a47f6868afbbec317c7bfd3dea22a9155650b2974d8`。生成阶段使用内置 imagegen，最终修改要求为保持三股带刃鞭束构型、以干净平涂重绘并改透明储液柄尾；本轮导出不重新生成或改变构型。

以 alpha>1 确定可见边界，保留两像素源图边距，忽略近乎不可见的孤立边界像素对排版的干扰；内部原 alpha 保留。采用预乘 alpha 的 Lanczos 等比缩放为 240×74，放入真实 **256×256 RGBA 透明画布**，偏移 `(8,91)`。没有拉伸或旋转，透明度范围 0～255。

运行文件：[SSC_PnaShortWhip.png](../../../1.7/Textures/Things/Item/Equipment/SSC_PnaShortWhip.png)，SHA-256 `376fcfd5df90aee95197a82da131a93dbfa0d377e3c0c8cc0079924fc55325e9`。原图工作副本和导出参数保存在 `.builds/pna-weapons/industrial-art/short-whip-256/approved-original.png`、`export.json`；不将临时原图视为发布资源。

## 接入范围

`SSC_Weapon_PnaShortWhip.graphicData.texPath` 从原版 Club 改为 `Things/Item/Equipment/SSC_PnaShortWhip`，并维护 `Sexslavecraft/Defs` 镜像。`Graphic_Single`、drawSize=1 及现有装备角度不变，不另加紫色 tint。

伤害 3、穿甲 20%、PNA 18%、命中评分 +3、动作权重 3、镇压／恐怖属性、配方、品质适配和既有受药反馈均不改动。没有 C#／DLL／构建元数据变更，本轮不重复编译，也不将步骤一的 39/39 套结果冒充本轮新跑结果。

## 检查与同步

资源检查通过：实际 256×256 RGBA、透明度范围 0～255、四角透明、可见轮廓水平；对照提交 `01860bb` 的 XML，除短鞭 texPath 外所有 Def 配置一致。Def 镜像 21/21、布局／冻结基线 538 项、文档链接检查通过。此为资源接入，不新增测试套件或重跑机制回归。

已同步至 `D:/STEAM/steamapps/common/RimWorld/Mods/RJW-SexSlaveCraft-TieJin-Modify`：新增贴图 1 份、覆盖 XML 1 份（原 XML 已备份）；全部 507 个 1.7 文件与工作区哈希一致，532 个 1.6／Common 文件和 LoadFolders 保持不变。清单、备份及结果在 `.builds/pna-weapons/industrial-art/short-whip-256/game-sync/`，`gameRuntimeVerified=false` 为同步时的原始状态，保留不改；后续实机确认见下节。

## 实机观察

维护者于 2026-10-19 反馈“实机测试正常”，授权提交本轮短鞭贴图、XML 及相关文档。本批整体验收关闭；不将未逐项报告的地面／信息卡、朝向或握持组合补记为专项通过。两把针枪及射针正式贴图尚未开始。
