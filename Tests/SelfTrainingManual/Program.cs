using System;
using System.Linq;
using RimWorld;
using rjw;
using rjw.Modules.Interactions;
using SexSlaveCraft;
using Verse;
using Verse.AI;

int count = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    count++;
}

var hand = new InteractionDef { Extension = new SexInteractionExtension { Type = xxx.rjwSextype.Masturbation } };
var breast = new InteractionDef { Extension = new SexInteractionExtension { Type = xxx.rjwSextype.Masturbation } };
var other = new InteractionDef { Extension = new SexInteractionExtension { Type = xxx.rjwSextype.Vaginal } };
SexUtility.SexInteractions.AddRange(new[] { hand, breast, other });
var pawn = new Pawn();
pawn.Available.Add(hand);
var listed = SSCSelfTrainingInteractions.Available(pawn).ToList();
Check(listed.Count == 1 && listed[0] == hand, "菜单只能列出当前可执行的单人交互");
Check(SSCSelfTrainingInteractions.TryBuild(pawn, hand, out SexProps props) &&
    props.pawn == pawn && props.partner == pawn && props.resolved.Interaction.Def == hand && !props.canBeGuilty,
    "选择必须保留本人参与者及具体交互");
Check(!SSCSelfTrainingInteractions.TryBuild(pawn, other, out _), "不能混入双人交互");
pawn.Available.Clear();
Check(!SSCSelfTrainingInteractions.TryBuild(pawn, hand, out _), "开始前身体条件失效必须拒绝选定交互");

var comp = new CompSexSlaveTraining();
var first = new Job { loadID = 31 };
var second = new Job { loadID = 32 };
comp.RegisterManualSelfTraining(first, hand);
Check(comp.TakeManualSelfTraining(second) == null, "其他任务不能取走选择");
Check(comp.TakeManualSelfTraining(first) == hand, "原任务取得选定交互");
Check(comp.TakeManualSelfTraining(first) == null, "选择只能取得一次");
comp.RegisterManualSelfTraining(first, hand);
comp.RegisterManualSelfTraining(second, breast);
Check(comp.TakeManualSelfTraining(first) == hand && comp.TakeManualSelfTraining(second) == breast,
    "连续手动命令分别保留自己的选择");

comp.RegisterManualSelfTraining(first, hand);
Scribe.mode = LoadSaveMode.Saving;
comp.ExposeForTest();
var loaded = new CompSexSlaveTraining();
Scribe.mode = LoadSaveMode.LoadingVars;
loaded.ExposeForTest();
Scribe.mode = LoadSaveMode.Inactive;
Check(loaded.TakeManualSelfTraining(second) == null && loaded.TakeManualSelfTraining(first) == hand,
    "任务开始前存读档保留 Job 编号和选定交互");
Scribe.Data.Clear();
var oldSave = new CompSexSlaveTraining();
Scribe.mode = LoadSaveMode.LoadingVars;
oldSave.ExposeForTest();
Scribe.mode = LoadSaveMode.PostLoadInit;
oldSave.ExposeForTest();
Scribe.mode = LoadSaveMode.Inactive;
oldSave.RegisterManualSelfTraining(first, hand);
Check(oldSave.TakeManualSelfTraining(first) == hand, "旧档缺少手动选择字段时恢复空容器");

comp.RegisterManualSelfTraining(first, hand);
pawn.CurJob = first;
comp.ReconcileForTest(pawn);
Check(comp.TakeManualSelfTraining(first) == hand, "当前任务未开始前不能清理选择");
comp.RegisterManualSelfTraining(second, breast);
pawn.CurJob = first;
pawn.jobs.jobQueue.Add(new QueuedJob { job = second });
comp.ReconcileForTest(pawn);
Check(comp.TakeManualSelfTraining(second) == breast, "排队任务的选择须保留");
comp.RegisterManualSelfTraining(second, breast);
pawn.jobs.jobQueue.Clear();
comp.ReconcileForTest(pawn);
Check(comp.TakeManualSelfTraining(second) == null, "取消任务后应清理过期选择");
comp.RegisterManualSelfTraining(first, hand);
comp.RegisterManualSelfTraining(second, breast);
comp.ClearManualSelfTraining(second);
Check(comp.TakeManualSelfTraining(first) == hand, "第二次指派被拒绝不能清除前一任务的选择");

DriverLifecycleTests.Run(Check);
Console.WriteLine($"{count}/{count} passed (self-training selection and production driver lifecycle).");
