using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SexSlaveCraft;

// Game lifecycle model based on local RimWorld 1.6 IL. Production hooks are installed with real Harmony.
// Unity rendering/pathfinding and RJW internals are counted boundaries, not timing substitutes.
namespace Verse
{
    public class Pawn
    {
        public bool Spawned = true, IsColonist = true, Eligible = true, Reachable = true, HasComp = true;
        public bool Dead, Destroyed, Downed, IsSlave, IsPrisonerOfColony, Master, Trainer, Child, VanillaBlocked;
        public string LabelShort = "Adult";
        public Pawn BoundMaster, AssignedTrainer;
        public readonly HashSet<Pawn> PermittedHosts = new HashSet<Pawn>();
        public RaceProperties RaceProps = new RaceProperties();
        public AgeTracker ageTracker = new AgeTracker();
        public ApparelTracker apparel = new ApparelTracker();
        public ThingDef def = new ThingDef();
        public T TryGetComp<T>() where T : class, new() => HasComp ? new T() : null;
        public bool CanReach(LocalTargetInfo target, Verse.AI.PathEndMode mode, Danger danger)
        { Counters.Reach++; return Reachable; }
    }
    public class RaceProperties { public bool Humanlike = true, Animal; }
    public class AgeTracker { public int AgeBiologicalYears = 30; public float Growth = 1; public LifeStage CurLifeStage = new LifeStage(); }
    public class LifeStage { public bool reproductive = true; public string defName = "Adult"; }
    public class ThingDef { public string defName = "Human", label = "Human"; }
    public class Apparel { public ThingDef def = new ThingDef(); }
    public class ApparelTracker { public List<Apparel> WornApparel = new List<Apparel>(); }
    public class Map { public List<Pawn> Pawns = new List<Pawn>(); }
    public struct TargetInfo { public bool IsValid; public int Key; }
    public struct LocalTargetInfo { public static explicit operator LocalTargetInfo(TargetInfo target) => default; }
    public enum Danger { Deadly }
    public static class Translator
    {
        public static string Translate(this string key, params object[] args)
        {
            Counters.Translations++;
            if (key == "SSC_RJW_ReportHeader") Counters.Reports++;
            return key;
        }
    }
    public static class Log { public static void Warning(string value) { Counters.Warnings++; } }
    public static class Messages
    {
        public static string Last;
        public static void Message(string value, object type, bool historical) { Counters.Messages++; Last = value; }
    }
    public class Window
    {
        public bool Closed;
        [MethodImpl(MethodImplOptions.NoInlining)] public virtual void PostClose() { }
        public void Close() { Closed = true; PostClose(); }
    }
}
namespace UnityEngine { public struct Rect { } }
namespace Verse.AI { public enum PathEndMode { Touch } }
namespace Verse
{
    public class FloatMenuOption { public Action action; }
    public class FloatMenu
    {
        public List<FloatMenuOption> Options;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public FloatMenu(List<FloatMenuOption> options) { Options = options; }
    }
}

namespace RimWorld
{
    public static class MessageTypeDefOf { public static readonly object RejectInput = new object(); }
    public class LordJob_Ritual { public Verse.Pawn Master, Slave; public Verse.Pawn PawnWithRole(string role) => role == "master" ? Master : Slave; }
    public class Precept_Role { }
    public class RitualObligation { }
    public class RitualOutcomeEffectDef { }
    public class Precept_Ritual { public RitualBehaviorWorker behavior = new RitualBehaviorWorker(); }
    public class RitualBehaviorDef { public string defName; public List<RitualRole> roles = new List<RitualRole>(); }
    public class RitualRole
    {
        public string id;
        public bool required = true, substitutable;
        public int maxCount = 1;
        public virtual bool AppliesToPawn(Verse.Pawn p, out string reason, Verse.TargetInfo selectedTarget, LordJob_Ritual ritual = null,
            RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false) { reason = null; return true; }
        public virtual bool AppliesToRole(Precept_Role role, out string reason, Precept_Ritual ritual = null, Verse.Pawn p = null, bool skipReason = false) { reason = null; return true; }
        public bool AppliesIfChild(Verse.Pawn p, out string reason, bool skipReason) { reason = p.Child ? "child" : null; return !p.Child; }
    }
    public class PsychicRitualRoleDef
    {
        public enum Context { Ritual }
        public struct Reason { public static Reason None => default; }
    }

    public class RitualBehaviorWorker
    {
        public RitualBehaviorDef def = new RitualBehaviorDef();
        public int Executions;
        public bool ThrowAvailability;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string CanStartRitualNow(Verse.TargetInfo target, Precept_Ritual ritual, Verse.Pawn organizer = null,
            Dictionary<string, Verse.Pawn> forced = null)
        {
            if (ThrowAvailability) throw new InvalidOperationException("availability probe");
            foreach (var role in def.roles.Where(r => r.required && !r.substitutable))
                foreach (var pawn in AvailabilityPawns)
                    role.AppliesToPawn(pawn, out _, target, precept: ritual, skipReason: true);
            return null;
        }
        public List<Verse.Pawn> AvailabilityPawns = new List<Verse.Pawn>();
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void TryExecuteOn(Verse.TargetInfo target, Verse.Pawn organizer, Precept_Ritual ritual,
            RitualObligation obligation, RitualRoleAssignments assignments, bool playerForced)
        {
            CanStartRitualNow(target, ritual);
            // Represents ending existing pawn jobs and creating the ritual lord.
            Executions++;
        }
    }

    public class RitualRoleAssignments
    {
        private readonly Precept_Ritual ritual;
        private readonly Verse.TargetInfo ritualTarget;
        private List<Verse.Pawn> allPawns = new List<Verse.Pawn>();
        private readonly Dictionary<string, List<Verse.Pawn>> assignedRoles = new Dictionary<string, List<Verse.Pawn>>();
        private readonly List<Verse.Pawn> spectators = new List<Verse.Pawn>();
        public Dictionary<string, Verse.Pawn> ForcedRolesForReading = new Dictionary<string, Verse.Pawn>();
        public Verse.Pawn SelectedPawn;
        public Verse.Pawn RejectAssignPawn;
        public int Writes;
        public Precept_Ritual Ritual => ritual;
        public List<RitualRole> AllRolesForReading => ritual.behavior.def.roles;
        public List<Verse.Pawn> SpectatorsForReading => spectators;
        public List<Verse.Pawn> AllCandidatePawns => allPawns;
        public RitualRoleAssignments(Precept_Ritual ritual, Verse.TargetInfo target) { this.ritual = ritual; ritualTarget = target; }
        public void Setup(List<Verse.Pawn> pawns, Dictionary<string, Verse.Pawn> forced, Verse.Pawn selected)
        { allPawns = pawns; ForcedRolesForReading = forced ?? new Dictionary<string, Verse.Pawn>(); SelectedPawn = selected; }
        public Verse.Pawn FirstAssignedPawn(string id)
            => ForcedRolesForReading.TryGetValue(id, out var forced) ? forced : assignedRoles.TryGetValue(id, out var list) ? list.FirstOrDefault() : null;
        public Verse.Pawn FirstAssignedPawn(RitualRole role) => FirstAssignedPawn(role.id);
        public IEnumerable<Verse.Pawn> AssignedPawns(RitualRole role)
            => FirstAssignedPawn(role) is Verse.Pawn pawn ? new[] { pawn } : Array.Empty<Verse.Pawn>();
        public RitualRole RoleForPawn(Verse.Pawn pawn, bool includeForced = true)
            => pawn == null ? null : AllRolesForReading.FirstOrDefault(role => FirstAssignedPawn(role) == pawn);
        [MethodImpl(MethodImplOptions.NoInlining)] public bool TryUnassignAnyRole(Verse.Pawn pawn)
        {
            bool changed = false;
            foreach (var list in assignedRoles.Values) changed |= list.Remove(pawn);
            if (changed) { Writes++; if (!pawn.IsPrisonerOfColony) spectators.Add(pawn); }
            return changed;
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public void RemoveParticipant(Verse.Pawn pawn)
        { TryUnassignAnyRole(pawn); spectators.Remove(pawn); allPawns.Remove(pawn); allPawns.Add(pawn); }
        [MethodImpl(MethodImplOptions.NoInlining)] public bool TryAssignSpectate(Verse.Pawn pawn, Verse.Pawn insertBefore = null)
        {
            if (pawn.IsPrisonerOfColony || pawn.Dead || pawn.Downed) return false;
            TryUnassignAnyRole(pawn);
            if (!spectators.Contains(pawn)) spectators.Add(pawn);
            return true;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool TryAssign(Verse.Pawn pawn, RitualRole role, out PsychicRitualRoleDef.Reason reason,
            PsychicRitualRoleDef.Context context = PsychicRitualRoleDef.Context.Ritual, Verse.Pawn insertBefore = null)
        {
            reason = PsychicRitualRoleDef.Reason.None;
            if (pawn == RejectAssignPawn) return false;
            if (ForcedRolesForReading.ContainsValue(pawn)) return false;
            if (PawnNotAssignableReason(pawn, role, ritual, this, ritualTarget, out _) != null) return false;
            if (role == null) return TryAssignSpectate(pawn);
            if (!role.AppliesToPawn(pawn, out _, ritualTarget, assignments: this, skipReason: true)) return false;
            if (FirstAssignedPawn(role) != null) return false;
            TryUnassignAnyRole(pawn);
            spectators.Remove(pawn);
            assignedRoles[role.id] = new List<Verse.Pawn> { pawn };
            Writes++;
            return true;
        }
        public static string PawnNotAssignableReason(Verse.Pawn pawn, RitualRole role, Precept_Ritual ritual,
            RitualRoleAssignments assignments, Verse.TargetInfo target, out bool stillAddToPawnList)
        {
            stillAddToPawnList = false;
            if (pawn.VanillaBlocked || pawn.Dead || pawn.Downed) return "vanilla";
            return role != null && !role.AppliesToPawn(pawn, out var reason, target, assignments: assignments) ? reason ?? "role" : null;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void FillPawns(Dialog_BeginRitual.PawnFilter filter, Verse.TargetInfo target)
        {
            // Two original automatic-role loops; the production transpiler must replace both.
            foreach (var role in AllRolesForReading)
                if (SelectedPawn != null) TryAssign(SelectedPawn, role, out _);
            foreach (var role in AllRolesForReading)
                foreach (var pawn in allPawns)
                {
                    if (FirstAssignedPawn(role) != null) break;
                    if (filter == null || filter(pawn, true, false)) TryAssign(pawn, role, out _);
                }
            foreach (var pawn in allPawns)
                if (RoleForPawn(pawn) == null) TryAssignSpectate(pawn);
        }
    }

    public interface IPawnRoleSelectionWidget
    { void DrawPawnList(UnityEngine.Rect rect); void WindowUpdate(); }
    public class PawnRoleSelectionWidgetBase<T> : IPawnRoleSelectionWidget where T : class
    {
        protected object assignments;
        public int Notifications, UnrelatedCalls;
        public virtual void Notify_AssignmentsChanged() { Notifications++; }
        public Action DrawAction;
        public void DrawPawnList(UnityEngine.Rect rect) => DrawAction?.Invoke();
        public void WindowUpdate() { }
        public PawnRoleSelectionWidgetBase(object assignments) { this.assignments = assignments; }
        public bool Select(Verse.Pawn pawn, IEnumerable<T> roles) => TryAssign(pawn, roles, true, null, true, false);
        public bool Replace(Verse.Pawn pawn, IEnumerable<T> roles, Verse.Pawn replacing) => TryAssignReplace(pawn, roles, replacing);
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryAssign(Verse.Pawn pawn, IEnumerable<T> roles, bool showMessages, Verse.Pawn insertBefore, bool doSound, bool insertLast)
        {
            if (!(assignments is RitualRoleAssignments a)) { UnrelatedCalls++; return true; }
            foreach (var role in roles.Cast<RitualRole>())
            {
                // Model the original's mutation-before-DoTryAssign path.
                foreach (var previous in a.AssignedPawns(role).ToArray()) a.TryUnassignAnyRole(previous);
                if (a.TryAssign(pawn, role, out _, insertBefore: insertBefore)) { Notify_AssignmentsChanged(); return true; }
            }
            return false;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryAssignReplace(Verse.Pawn pawn, IEnumerable<T> roles, Verse.Pawn replacing)
        {
            if (!(assignments is RitualRoleAssignments a)) { UnrelatedCalls++; return true; }
            var oldRole = a.RoleForPawn(pawn);
            a.RemoveParticipant(replacing);
            bool result = TryAssign(pawn, roles, true, null, true, true);
            // Original attempts the second assignment even when the first failed.
            if (oldRole != null) { if (a.TryAssign(replacing, oldRole, out _)) Notify_AssignmentsChanged(); }
            else a.TryAssignSpectate(replacing);
            return result;
        }
    }
    public class PawnRitualRoleSelectionWidget : PawnRoleSelectionWidgetBase<RitualRole>
    {
        public Verse.Pawn CachedTarget;
        public PawnRitualRoleSelectionWidget(RitualRoleAssignments assignments) : base(assignments) { }
        public override void Notify_AssignmentsChanged()
        { base.Notify_AssignmentsChanged(); CachedTarget = ((RitualRoleAssignments)assignments).FirstAssignedPawn("slave"); }
    }
    public class Dialog_BeginLordJob : Verse.Window
    {
        protected IPawnRoleSelectionWidget participantsDrawer;
        public virtual bool CanBegin { [MethodImpl(MethodImplOptions.NoInlining)] get => true; }
    }
    public class Dialog_BeginRitual : Dialog_BeginLordJob
    {
        public delegate bool ActionCallback(RitualRoleAssignments assignments);
        public delegate bool PawnFilter(Verse.Pawn pawn, bool role, bool required);
        protected RitualRoleAssignments assignments;
        protected Verse.TargetInfo target;
        protected ActionCallback action;
        protected List<string> extraInfos;
        public RitualRoleAssignments Assignments => assignments;
        public PawnRitualRoleSelectionWidget Widget;
        public List<string> ExtraInfos => extraInfos;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Dialog_BeginRitual(string label, Precept_Ritual ritual, Verse.TargetInfo target, Verse.Map map,
            ActionCallback action, Verse.Pawn organizer = null, RitualObligation obligation = null, PawnFilter filter = null,
            string confirmText = null, List<Verse.Pawn> required = null, Dictionary<string, Verse.Pawn> forced = null,
            RitualOutcomeEffectDef outcome = null, List<string> extraInfos = null, Verse.Pawn selected = null)
        {
            this.target = target; this.action = action; this.extraInfos = extraInfos;
            assignments = CreateRitualRoleAssignments(ritual, target, map, filter, required, forced, selected);
            Widget = new PawnRitualRoleSelectionWidget(assignments);
            participantsDrawer = Widget;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static RitualRoleAssignments CreateRitualRoleAssignments(Precept_Ritual ritual, Verse.TargetInfo target, Verse.Map map,
            PawnFilter filter, List<Verse.Pawn> required, Dictionary<string, Verse.Pawn> forced, Verse.Pawn selected)
        {
            if (map == null) throw new InvalidOperationException("candidate-list probe");
            var result = new RitualRoleAssignments(ritual, target);
            foreach (var pawn in map.Pawns)
                foreach (var role in ritual.behavior.def.roles)
                    RitualRoleAssignments.PawnNotAssignableReason(pawn, role, ritual, result, target, out _);
            result.Setup(map.Pawns, forced, selected);
            return result;
        }
        public void Open() => assignments.FillPawns(null, target);
        public void Draw(Action action = null)
        {
            Widget.DrawAction = action;
            try { participantsDrawer.DrawPawnList(default); }
            finally { Widget.DrawAction = null; }
        }
        public bool Select(Verse.Pawn pawn, IEnumerable<RitualRole> roles)
        { bool result = false; Draw(() => result = Widget.Select(pawn, roles)); return result; }
        public bool Replace(Verse.Pawn pawn, IEnumerable<RitualRole> roles, Verse.Pawn replacing)
        { bool result = false; Draw(() => result = Widget.Replace(pawn, roles, replacing)); return result; }
        public bool Confirm()
        {
            bool accepted = action?.Invoke(assignments) == true;
            if (accepted) Close();
            return accepted;
        }
    }
}

namespace rjw
{
    public enum AgeCategory { Child, Adult }
    public static class PawnExtensions { public static AgeCategory GetAgeCategory(this Verse.Pawn pawn) => pawn.Child ? AgeCategory.Child : AgeCategory.Adult; }
    public static class RJWSettings { public static bool AllowYouthSex = false, rape_enabled = true; }
    public static class xxx
    {
        public static bool Throw;
        public static bool can_be_fucked(Verse.Pawn pawn)
        { Counters.Eligibility++; if (Throw) throw new InvalidOperationException("eligibility probe"); return pawn.Eligible; }
        public static bool can_do_loving(Verse.Pawn pawn) => !pawn.Child;
        public static bool is_human(Verse.Pawn pawn) => true;
        public static bool is_animal(Verse.Pawn pawn) => false;
        public static bool is_mechanoid(Verse.Pawn pawn) => false;
    }
    public static class Genital_Helper
    {
        public static bool has_anus(Verse.Pawn pawn) => true;
        public static bool has_vagina(Verse.Pawn pawn) => false;
        public static bool has_mouth(Verse.Pawn pawn) => true;
        public static bool anus_blocked(Verse.Pawn pawn) => false;
        public static bool vagina_blocked(Verse.Pawn pawn) => false;
        public static bool oral_blocked(Verse.Pawn pawn) => false;
    }
}

namespace SexSlaveCraft
{
    public static class SSCMod { public static Settings settings = new Settings(); }
    public class Settings { public bool useRJWOriginalEligibility = true, enableSexSlaveProtectionRules = true; }
    public class CompSexSlaveTraining { public SSCRestrictionConfig restrictionConfig; }
    public static class SSCIdentityUtility
    {
        public static bool IsSupportedVanillaStatus(Verse.Pawn pawn) => pawn.IsColonist || pawn.IsSlave || pawn.IsPrisonerOfColony;
        public static bool IsMaster(Verse.Pawn pawn) => pawn?.Master == true;
        public static bool IsTrainer(Verse.Pawn pawn) => pawn?.HasComp == true && (pawn.Master || pawn.Trainer);
    }
    public static class SSCBondUtility { public static Verse.Pawn GetBoundMaster(Verse.Pawn pawn) => pawn?.BoundMaster; }
    public static class TrainerAssignmentUtility
    {
        public static Verse.Pawn GetActiveAssignedTrainer(Verse.Pawn pawn)
        { var trainer = pawn?.AssignedTrainer; return trainer != null && !trainer.Dead && !trainer.Destroyed && SSCIdentityUtility.IsTrainer(trainer) ? trainer : null; }
    }
    public enum SSCInteractionKind { DailyTraining, RitualTraining, BindingPreparation }
    public enum SSCRestrictionReason { BoundOwner, Allowed, RuleDenied }
    public enum SSCRestrictionRule { ReceiveTraining }
    public enum SSCRestrictionValue { Deny, Allow }
    public class SSCRestrictionConfig { public object rules; public bool IsValid() => true; }
    public class SSCRestrictionResolution { public bool Valid; public SSCRestrictionValue Value; }
    public static class SSCRestrictionResolver
    {
        public static bool IsApplicable(Verse.Pawn pawn) => pawn?.BoundMaster != null;
        public static SSCRestrictionResolution Resolve(Verse.Pawn pawn, object rules, SSCRestrictionRule rule) => new SSCRestrictionResolution();
    }
    public class SSCRestrictionRequest
    {
        public Verse.Pawn Initiator, Receiver;
        public SSCInteractionKind Kind;
        public SSCRestrictionRequest(Verse.Pawn actor, Verse.Pawn target, SSCInteractionKind kind, bool direction)
        { Initiator = actor; Receiver = target; Kind = kind; }
    }
    public class SSCRestrictionDecision { public bool Allowed; public SSCRestrictionReason Reason; }
    // Explicit permission fixture. The complete production rule resolver is covered by TrainerIdentity/RestrictionCore.
    public static class SSCRestrictionPolicy
    {
        public static SSCRestrictionDecision Evaluate(SSCRestrictionRequest request)
        {
            Counters.Permissions++;
            var target = request.Receiver;
            bool owner = target?.BoundMaster != null && target.BoundMaster == request.Initiator;
            bool allowed = owner || target?.PermittedHosts.Contains(request.Initiator) == true;
            return new SSCRestrictionDecision { Allowed = allowed, Reason = owner ? SSCRestrictionReason.BoundOwner : allowed ? SSCRestrictionReason.Allowed : SSCRestrictionReason.RuleDenied };
        }
    }
    public static class Strings
    {
        public const string Ritual_MustBeColonist = "colonist", Ritual_MustBeColonistOrSlave = "status", RJW_Short_TargetNull = "null",
            RJW_Short_Pass = "pass", RJW_Short_Fail_Mechanoid = "mech", RJW_Short_Fail_Age = "age",
            RJW_Short_Fail_Warcasket = "apparel", RJW_Short_Fail_CanDoLoving = "loving",
            RJW_Short_Fail_NoUsableOrifice = "slots", RJW_Short_Fail_CanBeFucked = "eligibility",
            Legacy_Short_RequireRapeEnabled = "setting", Legacy_Short_FailCanBeFucked = "eligibility", Legacy_Short_Pass = "pass";
    }
}

internal static class Counters
{
    public static int Eligibility, Reports, Warnings, Reach, Permissions, Translations, Messages;
    public static void Reset() { Eligibility = Reports = Warnings = Reach = Permissions = Translations = Messages = 0; }
}
