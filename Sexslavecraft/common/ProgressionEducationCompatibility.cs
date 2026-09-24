using Verse;
using Verse.AI.Group;

namespace SexSlaveCraft
{
    /// <summary>Education 可选兼容：自动调教避让实际参加课程的对象，不改变课表或教育模组行为。</summary>
    internal static class ProgressionEducationCompatibility
    {
        /// <summary>按实际课程 Lord 识别参与者，覆盖听课、授课和临时无任务阶段；手动命令保留原行为。</summary>
        public static bool ShouldDeferAutomaticTraining(Pawn target, bool playerForced = false)
        {
            // 通过运行时完整类型名识别，不引用 Education 程序集；未安装时自然不匹配。
            return !playerForced && target?.GetLord()?.LordJob?.GetType().FullName ==
                "ProgressionEducation.LordJob_AttendClass";
        }
    }
}
