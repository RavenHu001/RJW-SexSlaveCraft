using System.Linq;
using SexSlaveCraft;
using Verse.AI.Group;

// 仅模拟外部课程类型及成员关系，不复制兼容判断。
namespace Verse.AI.Group
{
    public class LordJob { }
    public class Lord { public LordJob LordJob; }
}
namespace ProgressionEducation
{
    public class LordJob_AttendClass : LordJob { }
}

internal static partial class Program
{
    private static void RunEducationCompatibilityTests()
    {
        Run("课程成员即使没有听课Job也不进入自动候选，手动命令仍允许，下课后恢复", () =>
        {
            var f = Setup(); var work = new WorkGiver_Training();
            Assert(work.JobOnThing(f.master, f.slave) != null);
            f.slave.lord = new Lord { LordJob = new ProgressionEducation.LordJob_AttendClass() };
            TrainingJobUtility.ValidationCalls = 0;
            Assert(!work.PotentialWorkThingsGlobal(f.master).Contains(f.slave));
            Assert(!work.HasJobOnThing(f.master, f.slave) && work.JobOnThing(f.master, f.slave) == null);
            Assert(TrainingJobUtility.ValidationCalls == 0);
            Assert(work.JobOnThing(f.master, f.slave, true) != null);
            f.slave.lord = null;
            Assert(work.PotentialWorkThingsGlobal(f.master).Contains(f.slave));
            Assert(work.JobOnThing(f.master, f.slave) != null);
        });
        Run("候选查询后才入课仍拒绝创建自动任务，其他Lord保持原行为", () =>
        {
            var f = Setup(); var work = new WorkGiver_Training();
            Assert(work.HasJobOnThing(f.master, f.slave));
            f.slave.lord = new Lord { LordJob = new ProgressionEducation.LordJob_AttendClass() };
            Assert(work.JobOnThing(f.master, f.slave) == null);
            f.slave.lord = new Lord { LordJob = new LordJob() };
            Assert(work.JobOnThing(f.master, f.slave) != null);
            Assert(!ProgressionEducationCompatibility.ShouldDeferAutomaticTraining(null));
        });
    }
}
