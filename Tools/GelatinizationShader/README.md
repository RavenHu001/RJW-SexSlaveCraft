# 1.7 凝胶 Shader 原生编译验证

```powershell
pwsh -File Tools/GelatinizationShader/Run.ps1 -GameRoot 'D:\STEAM\steamapps\common\RimWorld'
```

需要 .NET 10 SDK 和目标 1.7 的完整游戏安装。`-DotnetPath` 可指定 SDK；默认先使用仓库内的便携 SDK，再查 PATH。依赖从给定游戏目录读取，不下载、不复制游戏 DLL。报告默认放在 `.builds/gel-visual/shader/`，可用 `-ReportDirectory` 改写。

工具按实际 `ShaderProgramDefinition` 校验字段/枚举，检查顶点与片元接口及材质布局，再由游戏自带 `RuntimeShaderCompiler` 编译普通/INSTANCED 的顶点和片元四个阶段，核验 SPIR-V 头并保存输出。故意错误的 GLSL 必须被编译器拒绝。JSON 记录游戏构建、游戏程序集哈希、Waylith MVID、资源哈希与各阶段结果。

这是显式依赖实际游戏的构建验证工具，单独运行；`Tests/Gelatinization` 的 44 项模型与像素函数回归仍由统一 net9 门禁执行。没有运行 GPU 绘制、场景深度/透明排序或游戏存读档。工具不启动游戏、不部署、不写入游戏目录、不生成 ZIP；最终观感需要维护者进游戏确认。

当前材质仅处理身体和现有 SizedApparel 入口：将黑线抬为深紫色，不改变贴图轮廓宽度；保留明暗次序，少量提亮高光。维护者已确认黑边改善良好；首次试色偏淡，恢复原紫色后改用深紫色 RGB `(0.46, 0.20, 0.68)`，配色已获确认；不透明度经 `0.9` 对比后调回 `0.85`，维护者已确认最终效果，见[视觉验收记录](../../Docs/Development/Completed/1.7全身凝胶化身体视觉调整与验收.md)。头部和 1.6 冻结资源均不参与本次修改。原生定义或注册项缺失会警告并降级为内置透明染色，降级效果不包含黑边柔化。
