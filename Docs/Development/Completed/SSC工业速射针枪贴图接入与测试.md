# SSC 工业速射针枪贴图接入与测试

日期：2026-10-19。基线：`da6c47d`。状态：维护者确认最终描边平涂重绘版“这版可以了，提交”，本轮速射针枪贴图及 XML 接入已验收，按授权提交；未打包发布。

[资源表](../../Art/SSC纳米催情液武器资源.md) · [工业规划](../../Design/SSC工业时代纳米催情液武器开发规划.md) · [步骤一验收](../Completed/SSC工业武器步骤一实现与测试.md)

## 选图与导出

采用内置 imagegen 基于指定版本重新绘制的纯侧视图：深蓝灰工程塑料枪身、浅暖灰气罐收纳于枪托下部，保留上梁与托底板；侧开定位夹具以闭合侧视的铰链和扣位表达。气罐连接处保留压力表，弹匣纵向视窗展示逐层横放的紫色含液圆管；护木为分件边界与三道凹槽，枪口为简单直筒。原图阶段以堂丸重型冲锋枪作画风参考，没有复制其纹理到运行目录。首次接入后维护者反馈画风偏离原版、缺乏明显描边；当前选定图为保留结构的重新绘制版，增强连续深色外轮廓、简化内部明暗，气罐降为暖浅中灰，紫色管身减少高光，保留圆管的两档明暗。

选定图来自 `C:/Users/24196/.codex/generated_images/01a14d5a-bbd6-79f3-9356-75f2d8ad5113/exec-04728f40-17fa-45c6-9664-10cdce46130a.png`，1774×887 RGBA，SHA256 `d4147bf60395caa6d788577a39a98b9ecb2942eff53e69eb4a1c2c3e36f48096`。项目内保留[原图](../../Art/SSC_PnaNeedleRepeater/SSC_PnaNeedleRepeater_Approved.png)、[导出脚本](../../Art/SSC_PnaNeedleRepeater/Export-256.py)和[参数记录](../../Art/SSC_PnaNeedleRepeater/export.json)。本次只做确定性尺寸导出，不重绘或旋转。

按 alpha>1 取有效边界，增加两像素源图边距，裁切为 (32,154,1771,878)；预乘 alpha Lanczos 等比缩为 240×100，居中放入 256×256 透明画布，偏移 (8,78)。保留真实透明背景与横置朝右，不拉伸。

运行文件：[SSC_PnaNeedleRepeater.png](../../../1.7/Textures/Things/Item/Equipment/SSC_PnaNeedleRepeater.png)，SHA256 `20a5382518f42087e8112ac00067da7c1a9cd2541b92e138dbcebb0a15394ac4`。

## 接入与检查

`SSC_Weapon_PnaNeedleRepeater.graphicData.texPath` 从原版 MachinePistol 改为 `Things/Item/Equipment/SSC_PnaNeedleRepeater`，同步现有源码 XML 镜像。Graphic_Single、drawSize=1、既有握持配置保持；投射物暂用现有 PNA 吹箭占位图。

资源检查通过：256×256 RGBA、透明度范围 0～255、四角透明；对比基线 XML，仅速射针枪 texPath 改变，其他 Def／数值完全一致。Def 镜像 21/21、布局／冻结基线 538 项及文档链接检查通过。本轮没有 C#／DLL／BUILD-INFO 变更，不重复编译或重跑机制套件。

## 同步与实机边界

首次接入已同步至 `D:/STEAM/steamapps/common/RimWorld/Mods/RJW-SexSlaveCraft-TieJin-Modify`：新增 PNG 一份、替换 XML 一份，原 XML 已备份；511 份 1.7 运行文件哈希匹配，532 份 1.6／Common 文件及 LoadFolders.xml 不变。同步清单、备份和结果位于 `.builds/pna-weapons/industrial-art/needle-repeater-256/game-sync/`，保留 `gameRuntimeVerified=false`。

描边重绘版再次导出后只替换运行 PNG，XML、drawSize=1、C#、DLL 和 BUILD-INFO 均不变。重新执行 256px 透明资源／引用检查、21 份 Def 镜像和 538 项布局／冻结检查通过；旧原图、导出参数和运行图保存在 `.builds/pna-weapons/industrial-art/needle-repeater-outlined-256/before/`。已备份并覆盖游戏 PNG 一份，511 份运行文件哈希匹配，532 份冻结文件及 LoadFolders.xml 不变；最新清单、备份和结果位于 `.builds/pna-weapons/industrial-art/needle-repeater-outlined-256/game-sync/`。维护者已确认本次重绘版可以，授权提交；保留原始同步报告的未实机验证字段，不改写阶段记录。

维护者已确认最终描边平涂版可以并授权提交，本批约定范围验收关闭；未逐项报告的地面／信息卡、握持及朝向组合不另记为专项实测。精确针枪与两种工业射针正式贴图仍待开发。
