#if SSC_RW17
using System;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private const SexSlaveSpecializationType Fox = SexSlaveSpecializationType.PetFox;

    private static void RunFoxTrainingCases()
    {
        Run("狐狸仅从研究台入账累计科研经验，小额余量可跨方向继续结算", FoxResearchProgress);
        Run("狐狸护理仅结算另一名人形患者且普通完成时清除科研余量", FoxTendingProgress);
        Run("狐狸和既有宠物终极双向锁定方向及培养", FoxTerminalConflict);
        Run("狐狸科研余量经凝胶保存、复制、植入随人格转移", FoxResearchGelRoundTrip);
    }

    private static void FoxResearchProgress()
    {
        var pawn = Body("Fox researcher");
        var comp = Training(pawn);
        comp.pawnIdentity = PawnIdentity.Slave;
        var research = new ResearchProjectDef { defName = "SSC_RES_PetFox", IsFinished = false };
        DefDatabase<ResearchProjectDef>.AllDefs.Add(research);
        try
        {
            Assert(!PetSpecializationUtility.TrySelectPetSpecialization(pawn, comp, Fox, out var failure)
                && failure == PetSpecializationFailure.ResearchRequired, "狐狸跳过研究要求");
            research.IsFinished = true;
            Assert(PetSpecializationUtility.TrySelectPetSpecialization(pawn, comp, Fox, out _), "狐狸选择失败");
        }
        finally { DefDatabase<ResearchProjectDef>.AllDefs.Remove(research); }

        comp.specializationProgress = .8f;
        var records = new Pawn_RecordsTracker { pawn = pawn };
        void Research(float points) => Patch_FoxResearchProgress.Postfix(records, RecordDefOf.ResearchPointsResearched, points);
        pawn.jobs.curDriver = new JobDriver_DoBill();
        Research(100f);
        pawn.jobs.curDriver = new JobDriver_Research();
        DebugSettings.fastResearch = true;
        try { Research(100f); }
        finally { DebugSettings.fastResearch = false; }
        Research(float.NaN);
        Assert(comp.specializationProgress == .8f && comp.foxResearchPointRemainder == 0d, "无效科研事件获得了经验");

        const float small = 1f / 4096f;
        Research(100f - small);
        Assert(Math.Abs(comp.specializationProgress - .802475f) < .0000001f, "整研究点未按约定兑换");
        comp.SetSpecialization(SexSlaveSpecializationType.Cow);
        Research(10f);
        comp.SetSpecialization(Fox);
        Research(small);
        Assert(Math.Abs(comp.specializationProgress - .8025f) < .0000001f && comp.foxResearchPointRemainder == 0d,
            "低速研究或方向切换丢失科研余量");
    }

    private static void FoxTendingProgress()
    {
        var doctor = Body("Fox medic");
        var comp = Training(doctor);
        comp.pawnIdentity = PawnIdentity.Slave;
        comp.SetSpecialization(Fox);
        var records = new Pawn_RecordsTracker { pawn = doctor };
        doctor.jobs.curDriver = new JobDriver_TendPatient();
        doctor.jobs.curJob = new Job { targetA = new LocalTargetInfo(doctor) };
        Patch_FoxTendingProgress.Postfix(records, RecordDefOf.TimesTendedOther);
        var patient = Body("Patient");
        patient.RaceProps.Humanlike = false;
        doctor.CurJob.targetA = new LocalTargetInfo(patient);
        Patch_FoxTendingProgress.Postfix(records, RecordDefOf.TimesTendedOther);
        Assert(comp.specializationProgress == 0f, "自疗或动物护理误计经验");

        patient.RaceProps.Humanlike = true;
        Patch_FoxTendingProgress.Postfix(records, RecordDefOf.TimesTendedOther);
        Assert(comp.specializationProgress == .001f, "一次对他人护理的经验不符");
        comp.specializationProgress = .9985f;
        comp.foxResearchPointRemainder = .75d;
        Patch_FoxTendingProgress.Postfix(records, RecordDefOf.TimesTendedOther);
        Assert(Math.Abs(comp.specializationProgress - .9995f) < .0000001f && comp.foxResearchPointRemainder == 0d,
            "护理达到普通完成后仍保留科研余量");
        Patch_FoxTendingProgress.Postfix(records, RecordDefOf.TimesTendedOther);
        Assert(Math.Abs(comp.specializationProgress - .9995f) < .0000001f, "普通完成后仍继续培养");
    }

    private static void FoxTerminalConflict()
    {
        var pawn = Body("Pet terminal");
        var comp = Training(pawn);
        comp.pawnIdentity = PawnIdentity.Slave;
        comp.SetSpecialization(Fox);
        pawn.health.AddHediff(PetFinal(Rabbit));
        Assert(comp.AddSpecializationProgress(.3f) == 0f && !PetSpecializationUtility.HasActivePetEffects(pawn, Fox)
            && !comp.TrySetSpecialization(Fox, out var failure) && failure == PetSpecializationFailure.ConflictingFinal,
            "终极兔未锁定狐狸培养");
        pawn.health.RemoveHediff(pawn.health.hediffSet.GetFirstHediffOfDef(PetFinal(Rabbit)));
        pawn.health.AddHediff(PetFinal(Fox));
        Assert(!comp.TrySetSpecialization(Rabbit, out failure) && failure == PetSpecializationFailure.ConflictingFinal,
            "终极狐未锁定其他宠物");
        comp.SetSpecialization(SexSlaveSpecializationType.Cow);
        Assert(PetSpecializationUtility.HasActivePetEffects(pawn, Fox), "终极狐切换组外方向后失效");
    }

    private static void FoxResearchGelRoundTrip()
    {
        var source = Body("Fox source");
        var comp = Training(source);
        comp.pawnIdentity = PawnIdentity.Slave;
        comp.SetSpecialization(Fox);
        comp.specializationProgress = .5f;
        comp.foxResearchPointRemainder = .75d;
        comp.SetSpecialization(SexSlaveSpecializationType.Cow);
        var gel = Excrete(source);
        Assert(gel.foxResearchPointRemainder == .75d && comp.foxResearchPointRemainder == 0d
            && !comp.ExportSpecializationProgress().ContainsKey("PetFox"), "提取遗漏科研余量或未清除来源");

        Scribe.mode = LoadSaveMode.Saving;
        gel.PostExposeData();
        var loaded = Gel();
        Scribe.mode = LoadSaveMode.LoadingVars;
        loaded.PostExposeData();
        Scribe.mode = LoadSaveMode.PostLoadInit;
        loaded.PostExposeData();
        Scribe.mode = LoadSaveMode.Inactive;
        var copy = Gel();
        copy.CopyFrom(loaded);
        var receiver = Body("Fox receiver");
        var host = Training(receiver);
        host.pawnIdentity = PawnIdentity.Slave;
        host.foxResearchPointRemainder = .125d;
        Assert(ExcretionUtility.InheritEverything(receiver, copy), "凝胶植入失败");
        host.SetSpecialization(Fox);
        Assert(host.specializationProgress == .5f && host.foxResearchPointRemainder == .75d, "植入后未恢复源人格的狐狸历史与余量");
        receiver.jobs.curDriver = new JobDriver_Research();
        Patch_FoxResearchProgress.Postfix(new Pawn_RecordsTracker { pawn = receiver }, RecordDefOf.ResearchPointsResearched, .25f);
        Assert(Math.Abs(host.specializationProgress - .500025f) < .0000001f && host.foxResearchPointRemainder == 0d,
            "植入后的研究未从原余量续算");
    }
}

namespace Verse
{
    public static class DebugSettings { public static bool fastResearch; }
}

namespace RimWorld
{
    public class JobDriver_Research : JobDriver { }
    public class JobDriver_TendPatient : JobDriver { }
}
#endif
