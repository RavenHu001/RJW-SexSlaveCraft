# SSC 工业针弹贴图接入与测试

> 版本归属：**2.4.3，仅 1.7**。原实施日期、测试／实机反馈和构建版本保留；本版汇总见[版本说明](../../Releases/2.4.3/2.4.3发布说明.md)，现行状态优先看所属主题的完成核对。

当前状态（2026-10-19）：工业三武器的机制、正式美术与两枪声音均已分批完成，统一见[完成核对](SSC工业时代纳米催情液武器完成核对.md)。下文保留本批实施时点；其中占位图、待绘制、临时参数及测试数字不代表当前缺项。

日期：2026-10-19。基线：`eb574c2`。状态：维护者在接入并同步后确认“可以，提交”，本轮两种工业针弹贴图已获认可，按授权提交；未打包发布。

[工业规划](../../Design/Archive/SSC工业时代纳米催情液武器开发规划.md) · [资源表](../../Art/SSC纳米催情液武器资源.md) · [速射针枪验收](../Completed/SSC工业速射针枪贴图接入与测试.md)

## 资源与导出

两种针弹采用同系列结构：银灰针尖、紫色含液圆管、封闭深灰尾座和小型硬质尾翼；针尖朝上、尾翼朝下，使用明确的深色外轮廓。精确针的载药段较长，针尖及尾翼略加长。尾翼只作美术与设定表达，没有新增飞行稳定模拟、装填或独立针弹物品。

原图均由内置 imagegen 生成、1254×1254 RGBA，来自本机生成目录 `C:/Users/24196/.codex/generated_images/01a14d5a-bbd6-79f3-9356-75f2d8ad5113/`：速射针为 `exec-129877bb-f107-4f23-8a86-4f3b689e9f31.png`，精确针为 `exec-3c800c4a-cedd-4808-b5eb-d22280fe6728.png`。项目内保存[速射针原图](../../Art/SSC_PnaNeedles/SSC_PnaRepeaterNeedle_Approved.png)、[精确针原图](../../Art/SSC_PnaNeedles/SSC_PnaRifleNeedle_Approved.png)、[导出脚本](../../Art/SSC_PnaNeedles/Export-128.py)和[参数与哈希](../../Art/SSC_PnaNeedles/export.json)。

alpha>1 取有效边界，保留两像素源图边距，以精确针裁切高度 1139 为基准，共用倍率 112/1139，使用预乘 alpha Lanczos 等比缩放，不拉伸、不旋转。共同倍率保留两种规格的长度与宽度差异。

| 用途 | 运行图 | 实际图形 | 128px画布偏移 | drawSize |
| --- | --- | --- | --- | --- |
| 速射针 | [SSC_PnaRepeaterNeedle.png](../../../1.7/Textures/Things/Projectile/SSC_PnaRepeaterNeedle.png) | 24×106 | (52,11) | (0.4,0.4) |
| 精确针 | [SSC_PnaRifleNeedle.png](../../../1.7/Textures/Things/Projectile/SSC_PnaRifleNeedle.png) | 26×112 | (51,8) | (0.4,0.4) |

速射针运行 PNG SHA256：`49c0e3fe0953b7522571fc0584be26512688e06fe6e732141b962261b192e237`；精确针：`647aa14cc36cb71a444ac9337062ae26a57741fc3a7bdb3dc20c396b76567b88`。

## 接入与检查

仅将工业 `SSC_Projectile_PnaRepeaterNeedle` 的 texPath 从旧吹箭占位图改为 `Things/Projectile/SSC_PnaRepeaterNeedle`，精确针 `SSC_Projectile_PnaRifleNeedle` 从旧弩矢占位图改为 `Things/Projectile/SSC_PnaRifleNeedle`，同步源码 XML 镜像。旧吹箭／弩矢文件与中世纪武器引用保持。

两张图片保持 128×128 RGBA、透明度范围 0～255、四角透明。对比基线 XML，除这两处 texPath 外所有 Def 配置一致：drawSize=0.4、速度 70／90、伤害 2／4、穿甲 30%／40%、剂量、射击与既有紫色尾迹均保持。Def 镜像 21/21、布局／冻结基线 538 项及文档链接检查通过。这是资源接入，无 C#／DLL／BUILD-INFO 变更，不重新编译或重跑机制套件。

## 同步与实机边界

已同步至 `D:/STEAM/steamapps/common/RimWorld/Mods/RJW-SexSlaveCraft-TieJin-Modify`：新增两张 PNG、替换工业武器 XML 一份，原 XML 已备份；513 份 1.7 运行文件哈希匹配，532 份 1.6／Common 文件及 LoadFolders.xml 不变。清单、备份和结果位于 `.builds/pna-weapons/industrial-art/needle-projectiles-128/game-sync/`，保持 `gameRuntimeVerified=false`。

维护者已认可当前接入结果并授权提交，本轮验收关闭；未逐项报告的方向、尺寸及尾迹组合不另记为专项实测，原同步报告保持原始未实机验证字段。精确针枪本体仍使用占位图。
