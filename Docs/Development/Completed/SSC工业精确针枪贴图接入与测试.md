# SSC 工业精确针枪贴图接入与测试

当前状态（2026-10-19）：工业三武器的机制、正式美术与两枪声音均已分批完成，统一见[完成核对](SSC工业时代纳米催情液武器完成核对.md)。下文保留本批实施时点；其中占位图、待绘制、临时参数及测试数字不代表当前缺项。

本篇保留首次接入阶段的历史证据；现行版本已由[两枪粗描边重绘与缩放](SSC工业气枪粗描边重绘与缩放测试.md)接续，精确针枪drawSize改为1.25，该后续批次已获维护者实机正常确认并授权提交。下文保留首次接入、drawSize=1与先提交的历史记录。

日期：2026-10-19。基线：`45a910a`。状态：维护者认可轴线修正版原图，已按要求导出、接入并同步本机游戏，维护者已授权先提交，实机反馈仍待确认。

[工业规划](../../Design/SSC工业时代纳米催情液武器开发规划.md) · [资源表](../../Art/SSC纳米催情液武器资源.md)

## 选图与导出

采用内置 imagegen 绘制的纯侧视精确针枪，延续速射针枪的深蓝灰工程塑料枪身、枪托内浅暖灰气罐、侧开定位夹具与压力表，增加贴腮板、低矮瞄准镜和单针观察窗。首版机匣偏高，认可版已降低机匣并将拉柄槽、横置针弹观察窗与枪管轴线对齐。枪口朝右、水平横置，无制退器。

选定原图来源：`C:/Users/24196/.codex/generated_images/01a14d5a-bbd6-79f3-9356-75f2d8ad5113/exec-174a81c9-3756-4c0f-862d-8df766f5b136.png`，2170×725 RGBA，SHA256 `fd93bc463426bb5c92fa6156d7ae885dbe787cd96ef7e3e3ecf71e9e1c168c65`。项目保存[原图](../../Art/SSC_PnaNeedleRifle/SSC_PnaNeedleRifle_Approved.png)、[导出脚本](../../Art/SSC_PnaNeedleRifle/Export-256.py)、[参数](../../Art/SSC_PnaNeedleRifle/export.json)及[生成说明](../../Art/SSC_PnaNeedleRifle/prompt.txt)。

沿用速射针枪的确定性导出方式：alpha>1 取边界，增加两像素源图边距，裁切 (37,99,2133,655)，预乘 alpha Lanczos 等比缩为 240×64，偏移 (8,96)，居中放入 256×256 透明画布，不拉伸、不旋转、不重绘。

运行文件：[SSC_PnaNeedleRifle.png](../../../1.7/Textures/Things/Item/Equipment/SSC_PnaNeedleRifle.png)，SHA256 `ac3b11eb0fb376f2b45ff11f6becff6418285a88e909f19684a0a73a3fa73374`。

## 接入与检查

仅将 `SSC_Weapon_PnaNeedleRifle.graphicData.texPath` 从原版 BoltActionRifle 改为 `Things/Item/Equipment/SSC_PnaNeedleRifle`，同步源码 XML 镜像。保留 Graphic_Single、drawSize=1、既有握持、武器数值、投射物和尾迹配置。

资源检查通过：256×256 RGBA、alpha 范围 0～255、四角透明、有效边界 (8,96,248,160)、引用存在；对比 HEAD XML，仅精确针枪 texPath 改变。Def 镜像 21/21、布局／冻结基线 538 项检查通过。本轮无 C#、DLL 或 BUILD-INFO 改动，不重复编译或重跑机制套件。

## 同步与实机边界

本机游戏 `Version.txt` 为 `1.7.4982 rev1280`。已备份并同步至 `D:/STEAM/steamapps/common/RimWorld/Mods/RJW-SexSlaveCraft-TieJin-Modify`：替换 XML 一份、新增 PNG 一份；514 份 1.7 运行文件哈希匹配，532 份 1.6／Common 文件及 LoadFolders.xml 不变。备份、清单和检查结果位于 `.builds/pna-weapons/industrial-art/needle-rifle-256/`。

尚未记录本轮游戏加载或视觉实测；`gameRuntimeVerified=false`。请在重载资源后观察地面／信息卡、征召持枪与瞄准、左右朝向、缩放和遮挡。当前只替换资源引用，原有机制验收范围保持，本轮按维护者授权提交；不打包发布，实机反馈仍待确认。
