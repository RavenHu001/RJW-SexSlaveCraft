# 旧版装备渲染反射调查

2026-10-27将根目录[sexslave_analyze.csx](sexslave_analyze.csx)移入本目录，保留脚本原始字节。

脚本通过反射列举装备渲染节点及worker成员，硬编码旧`RimWorldWin64_Data/Managed/Assembly-CSharp.dll`路径。它是历史调查资料，不代表当前1.7程序集的API或运行验证，也不是本次待执行脚本。

后续调查须重新确认目标游戏构建和实际DLL，使用现行开发取证流程；本次仅调整位置，不运行脚本、不改其历史路径、不构建或同步游戏。

[归档索引](../README.md) · [现行工具](../../Tools/README.md) · [开发资料](../../Docs/Development/README.md)
