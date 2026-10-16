# 凝胶化与 1.7 兼容回归

链接实际生产临时 Hediff、四个过程组件及属性、共享完成检查和身体染色补丁，读取 `1.7/Defs/HediffDefs/Hediffs_Gelatinization.xml` 的真实参数及组件顺序。使用 .NET 9 测试宿主，不依赖游戏或 Harmony DLL；PowerShell 状态签名检查需要 `pwsh`。

```powershell
dotnet run --project Tests/Gelatinization/Gelatinization.csproj --configuration Release -- .
```

33 项回归覆盖：从默认状态发生的双阈值竞赛、成功与失败互斥、外部移除、死亡、同角色新过程、旧调试按钮、正常扫描间隔及正常失败；覆盖内置透明 Shader、SizedApparel 渲染树角色、空节点及染色条件。最后一项调用 `Test-HarmonyState.ps1` 的 10 个状态参数断言。

状态检查通过 PowerShell AST 加载 `Verify-RestrictionCompatibility.ps1` 的实际函数，使用元数据边界对象覆盖 Prefix/Postfix/Finalizer、按特性命名的 Prefix、引用类型参数、缺失/非静态/歧义生产者及类型不匹配，不执行发布脚本或游戏代码。完整元数据验证仍需另行向该脚本提供实际 DLL 和 Cecil。

`Stubs.cs` 只替代游戏边界，保留 1.7 移除 Hediff 后仍继续遍历组件、不清空 `parent.pawn` 的契约，以及未注册 Shader 仍非空的契约。完全胶化工具的数值和结果服务使用模型；测试不执行 GPU、存读档或完整游戏生命周期。实际 1.7 DLL 构建、静态元数据检查与游戏验收分别记录。

此套件已接入 `Scripts/Test-All.ps1` 和发布门禁。`SscSourceRoot` 可指向隔离源码快照验证旧实现确实失败；不要将快照输出写回冻结目录。
