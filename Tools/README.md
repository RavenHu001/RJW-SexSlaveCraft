# 独立开发工具

这里保存有明确用途和使用说明的受控开发工具；构建／回归／打包的统一入口保留在Scripts，各机制套件保留在Tests。本地报告与临时调查输出放`.builds`。

| 工具 | 用途与验证边界 |
| --- | --- |
| [GelatinizationShader](GelatinizationShader/README.md) | 1.7凝胶Shader原生编译及可选FA契约核查，显式读取目标游戏程序集；工具说明保留原阶段证据，使用前须核对当前游戏与输入。不能用历史结果代替本次执行或GPU／游戏实机验收。 |

旧的根目录反射调查脚本归入[Archive/RenderingInvestigation](../Archive/RenderingInvestigation/README.md)，保留历史输入和字节；不作为当前1.7工具入口。

[维护入口](../Docs/Maintenance/README.md) · [测试说明](../Tests/README.md) · [目录与归属规则](../Docs/Maintenance/文档维护约定.md#仓库文件归属)
