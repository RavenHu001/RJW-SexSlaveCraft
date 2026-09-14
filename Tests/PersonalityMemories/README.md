# 人格记忆快照回归检查

本项目直接链接 `Sexslavecraft/common/PersonalityMemoryUtility.cs`，无需游戏进程或额外 NuGet 包。

运行命令：

```powershell
dotnet run --project Tests/PersonalityMemories/PersonalityMemories.csproj
```

覆盖内容包括心情阶段、实际社交好感的正负零及小数值、心情偏移、关联人物、年龄和倍率、原生持续时间与永久标志、戒律引用、独立复制、存档默认省略、旧凝胶兼容，以及无效条目的容错。

测试替身依据本机 RimWorld `Assembly-CSharp.dll` 的字段和方法核对结果实现关键契约：

- `Thought_MemorySocial.opinionOffset` 为 `float`，`Thought_Memory.moodOffset` 为 `int`。
- `ThoughtMaker.MakeThought(def, stage)` 先设置阶段再初始化，社交初始化从该阶段读取默认好感。
- `TryGainMemory(memory, otherPawn)` 会用参数更新普通记忆的关联人物，社交记忆必须有有效人物。
- 恢复值必须在进入引擎分组与数量上限检查之前赋值；合法范围内的原有堆叠和年龄应保持。

这些是生产工具配合引擎契约替身的回归测试，不等同于游戏内实测。替身不模拟完整的模组补丁、角色可获得性规则、引用交叉解析、思想附加事件或所有第三方记忆子类。实际构建还需引用游戏程序集检查公开 API 兼容性。

新版快照显式保存实例字段，旧格式继续使用原来的定义默认值，不能推断旧凝胶从未保存的阶段和好感。任意第三方记忆子类私有字段不属于当前快照协议。
