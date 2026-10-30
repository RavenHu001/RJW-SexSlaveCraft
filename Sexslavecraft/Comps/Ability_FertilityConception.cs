// AI 辅助生成
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>成功建孕才保留冷却；原版Ability负责其余序列化与按钮。</summary>
    public class Ability_FertilityConception : Ability
    {
        private bool activating;
        internal bool Submitting => activating;
        internal int SuccessfulJobId = -1;
        public Ability_FertilityConception() { }
        public Ability_FertilityConception(Pawn pawn) : base(pawn) { }
        public Ability_FertilityConception(Pawn pawn, AbilityDef def) : base(pawn, def) { }
        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && verb is Verb_CastAbility v) v.ability = this;
        }
        public override AcceptanceReport CanCast => FertilityAbilityUtility.CanUse(pawn, out string reason) ? base.CanCast : reason.Translate();
        /// <summary>目标确定时固定原上下文，排队及预热不提前建孕。</summary>
        public override Job GetJob(LocalTargetInfo target, LocalTargetInfo destination)
        {
            Job job = base.GetJob(target, destination);
            FertilityAbilityUtility.Register(job, this, target.Pawn);
            return job;
        }
        /// <summary>原提交复查后沿原版激活；失败及异常均撤回未成功的冷却。</summary>
        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            var c = FertilityAbilityUtility.Request(pawn.CurJob);
            if (activating || !CanCast || !FertilityAbilityUtility.IsCurrent(c) || c.Ability != this || c.Target != target.Pawn ||
                !FertilityAbilityUtility.CanTarget(pawn, target.Pawn, out _)) return false;
            var effects = EffectComps.OfType<CompAbilityEffect_FertilityConception>().Take(2).ToArray();
            if (effects.Length != 1) return false;
            var effect = effects[0];
            int originalJobId = pawn.CurJob.loadID;
            activating = true; effect.Succeeded = false;
            try
            {
                base.Activate(target, dest);
                if (!effect.Succeeded) { ResetCooldown(); return false; }
                SuccessfulJobId = originalJobId;
                return true;
            }
            finally { if (!effect.Succeeded) ResetCooldown(); activating = false; }
        }
    }
    public class CompProperties_FertilityConception : CompProperties_AbilityEffect
    {
        public CompProperties_FertilityConception() { compClass = typeof(CompAbilityEffect_FertilityConception); }
    }
    /// <summary>效果只提交一次建孕；联动在施法任务完成之后独立交接。</summary>
    public class CompAbilityEffect_FertilityConception : CompAbilityEffect
    {
        internal bool Succeeded;
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            bool valid = FertilityAbilityUtility.CanTarget(parent.pawn, target.Pawn, out string reason);
            if (!valid && throwMessages) Messages.Message(reason.Translate(), parent.pawn, MessageTypeDefOf.RejectInput, false);
            return valid;
        }
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Valid(target);
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            var c = FertilityAbilityUtility.Request(parent.pawn.CurJob);
            Succeeded = c?.Ability == parent && c.Target == target.Pawn && FertilityAbilityUtility.TryConceive(c);
            if (!Succeeded) Messages.Message("SSC_FertilitySkill_Failed".Translate(), parent.pawn, MessageTypeDefOf.RejectInput, false);
        }
    }
    /// <summary>只认实际同图目标资格，不让原版射程、接触或LOS把采样变成走近工作。</summary>
    public class Verb_FertilityConception : Verb_CastAbility
    {
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            bool valid = FertilityAbilityUtility.CanTarget(ability.pawn, target.Pawn, out string reason);
            if (!valid && showMessages) Messages.Message(reason.Translate(), ability.pawn, MessageTypeDefOf.RejectInput, false);
            return valid;
        }
        public override bool IsApplicableTo(LocalTargetInfo target, bool showMessages = false) => ValidateTarget(target, showMessages);
        public override bool CanHitTarget(LocalTargetInfo target) => ValidateTarget(target, false);
        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo target) => ValidateTarget(target, false);
    }
}
