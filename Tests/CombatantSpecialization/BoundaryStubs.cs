// 仅隔离游戏对象、旧方向与翻译；战斗员、方向切换、健康对账和显示均使用生产源码。
using System;
using System.Collections.Generic;
using System.Linq;
using SexSlaveCraft;

namespace Verse
{
    public class Thing { public bool Destroyed; }
    public partial class Pawn : Thing
    {
        public string LabelShort => "pawn";
        public bool TrainerEligible = true, TrainerFinal, PetFinal;
        public bool IsColonist = true, IsPrisonerOfColony, IsSlave;
        public CompSexSlaveTraining Training;
        public Health health;
        public Pawn() { health = new Health { Owner = this }; abilities = new RimWorld.Pawn_AbilityTracker(this); }
        public T TryGetComp<T>() where T : class => Training as T;
    }
    public class HediffDef { public string defName; }
    public class Hediff
    {
        public HediffDef def;
        public Pawn pawn;
        public HediffComp_GiveAbility Grant;
        public float Severity;
        public T TryGetComp<T>() where T : class => null;
    }
    public class HediffSet
    {
        public readonly List<Hediff> hediffs = new List<Hediff>();
        public Hediff GetFirstHediffOfDef(HediffDef def) => hediffs.FirstOrDefault(h => h.def == def);
        public bool HasHediff(HediffDef def) => GetFirstHediffOfDef(def) != null;
    }
    public class Health
    {
        public Pawn Owner;
        public readonly HediffSet hediffSet = new HediffSet();
        public Hediff AddHediff(HediffDef def)
        {
            var h = new Hediff { def = def, Severity = 0.01f, pawn = Owner };
            if (def == SSCDefOf.SSC_Hediff_Combatant_Final)
                h.Grant = new HediffComp_GiveAbility { parent = h, props = new HediffCompProperties_GiveAbility { abilityDef = SSCDefOf.SSC_CombatOverdrive } };
            hediffSet.hediffs.Add(h);
            return h;
        }
        public void RemoveHediff(Hediff h)
        {
            if (hediffSet.hediffs.Remove(h)) h.Grant?.CompPostPostRemoved();
        }
    }
    public class ResearchProjectDef { public bool IsFinished; }
    public static class DefDatabase<T> where T : class
    {
        public static T GetNamedSilentFail(string name) => null;
    }
    public enum LoadSaveMode { Inactive, LoadingVars, PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; }
    public static class PercentFormatting
    {
        public static string ToStringPercent(this float value) => value.ToString("P0");
    }
    public static class Messages
    {
        public static void Message(string text, Pawn pawn, object type, bool historical) { }
    }
}

namespace SexSlaveCraft
{
    using Verse;
    public enum PawnIdentity { Unset, Slave, Master }
    public enum RabbitReproductionMode { Offspring, Clone }
    public partial class CompSexSlaveTraining
    {
        public Thing parent;
        public PawnIdentity pawnIdentity;
        public bool specializationExplicitlyUnset, trainerInvalidExitBlocksAdoption;
        public RabbitReproductionMode rabbitReproductionMode;
        public float savedCowReservoirCharge;
    }
    public static class SSCIdentityUtility
    {
        public static bool IsSexSlave(Pawn p) => p?.Training?.pawnIdentity == PawnIdentity.Slave;
        public static bool IsSupportedVanillaStatus(Pawn p) =>
            p != null && (p.IsColonist || p.IsPrisonerOfColony || p.IsSlave);
    }
    public static class SSCDefOf
    {
        public static readonly RimWorld.AbilityDef SSC_CombatOverdrive = new RimWorld.AbilityDef();
        public static HediffDef SSC_Hediff_Combatant_Final = new HediffDef { defName = "SSC_Hediff_Combatant_Final" };
        public static HediffDef SSC_Hediff_Combatant = new HediffDef { defName = "SSC_Hediff_Combatant" };
        public static readonly HediffDef SSC_Hediff_Bus = new HediffDef();
        public static readonly HediffDef SSC_Hediff_Bus_Final = new HediffDef();
        public static readonly HediffDef SSC_Hediff_Cow = new HediffDef();
        public static readonly HediffDef SSC_Hediff_Cow_Final = new HediffDef();
        public static readonly HediffDef SSC_Hediff_TrainerOfficer = new HediffDef();
        public static readonly ResearchProjectDef SSC_BasicTraining = new ResearchProjectDef();
        public static ResearchProjectDef SSC_RES_Combatant = new ResearchProjectDef();
    }
    public class HediffComp_CowMilkReservoir { public float CurrentCharge; }
    public static class TrainerSpecializationLifecycle
    {
        public static void Notify(Pawn p) { }
        public static void Maintain(Pawn p) { }
    }
    public static class TrainerSpecializationUtility
    {
        public static bool HasFinalRecord(Pawn p) => p?.TrainerFinal == true;
        public static bool MeetsContinuousConditions(Pawn p, out object failure)
        {
            failure = null;
            return p?.TrainerEligible == true;
        }
    }
    public static class TrainerSpecializationProgressUtility
    {
        public static float TryGainProgress(Pawn p, float amount)
        {
            float before = p.Training.specializationProgress;
            return p.Training.AddSpecializationProgress(amount) - before;
        }
    }
    public static class BusSpecializationUtility
    {
        public static bool HasFinalBusState(Pawn p) => p.health.hediffSet.HasHediff(SSCDefOf.SSC_Hediff_Bus_Final);
        public static bool HasFinalCowState(Pawn p) => p.health.hediffSet.HasHediff(SSCDefOf.SSC_Hediff_Cow_Final);
        public static void EnsureBusHediffFromSpecialization(Pawn p) { }
        public static void EnsureCowHediffFromSpecialization(Pawn p) { }
    }
    public static class PetSpecializationUtility
    {
        public static bool HasFinalPetState(Pawn p, SexSlaveSpecializationType t) => p?.PetFinal == true;
        public static bool CanTrainPetSpecialization(Pawn p, SexSlaveSpecializationType t) =>
            PetSpecializationRules.CanTrain(t, p?.Training?.specializationType ?? SexSlaveSpecializationType.None,
                p?.PetFinal == true, p?.PetFinal == true, p?.PetFinal == true);
        public static void EnsurePetHediffFromSpecialization(Pawn p) { }
        // 本套件只验证战斗员生命周期；宠物状态清理由宠物专项运行真实源码验证。
        public static void RemoveInactiveOrdinaryPetStates(Pawn p) { }
        public static void SyncPetStates(Pawn p) { }
        public static bool TryGainPetProgress(Pawn p, SexSlaveSpecializationType t, float amount)
        {
            if (p?.Training?.specializationType != t) return false;
            p.Training.AddSpecializationProgress(amount);
            return true;
        }
        public static HediffDef GetBaseHediffDef(SexSlaveSpecializationType t) => null;
        public static string GetSpecializationLabel(SexSlaveSpecializationType t) => t.ToString();
    }
    public static class Strings
    {
        public static string Message_BusSpecializationUnlocked(string name) => name;
        public static string Message_CowSpecializationUnlocked(string name) => name;
        public const string ITab_SpecializationCombatantDisabledIdentity = "sex slave identity required";
        public const string ITab_SpecializationCombatantDisabledResearch = "research required";
        public const string ITab_SpecializationNone = "none";
        public const string ITab_SpecializationBus = "bus";
        public const string ITab_SpecializationCow = "cow";
        public const string ITab_SpecializationTrainerOfficer = "trainer";
        public const string ITab_SpecializationCombatant = "combatant";
        public const string ITab_SpecializationFinalizedSuffix = "finalized";
        public const string ITab_SpecializationUnfinishedSuffix = "unfinished";
        public const string ITab_SpecializationComplete = "complete";
        public const string ITab_SpecializationOrdinaryComplete = "ordinary complete";
    }
    public partial class ITab_SexSlaveTraining
    {
        public static string Label(Pawn p) => GetSpecializationLabel(p, p.Training);
        public static string Progress(Pawn p) => GetSpecializationProgressText(p, p.Training);
        public static bool SpecializationVisible(Pawn p) => CanShowSpecializationSection(p?.Training);
        public static bool LegacyOptions(Pawn p) => CanShowLegacySpecializationOptions(p.Training);
    }
}
