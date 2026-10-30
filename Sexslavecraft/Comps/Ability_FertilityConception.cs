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
        public Ability_FertilityConception() { }
        public Ability_FertilityConception(Pawn pawn) : base(pawn) { }
        public Ability_FertilityConception(Pawn pawn, AbilityDef def) : base(pawn, def) { }

        /// <summary>沿原Ability保存成功冷却，加载后修复原Verb的拥有者引用。</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && verb is Verb_CastAbility v) v.ability = this;
        }
        /// <summary>孕态及身体失格只禁用按钮；已有技能实例与成功冷却继续保留。</summary>
        public override AcceptanceReport CanCast => FertilityAbilityUtility.CanUse(pawn, out string reason) ? base.CanCast : reason.Translate();

        /// <summary>目标确定时固定双方原上下文，性交正常完成前不建孕。</summary>
        public override Job GetJob(LocalTargetInfo target, LocalTargetInfo destination)
        {
            Job job = base.GetJob(target, destination);
            FertilityAbilityUtility.Register(job, this, target.Pawn);
            return job;
        }
        /// <summary>原版在效果前预扣冷却；本技能把整个预激活延到确认建孕成功后，失败不产生临时冷却。</summary>
        protected override void PreActivate(LocalTargetInfo? target) { }

        /// <summary>仅接受同场正常完成的一次原请求；效果成功后才执行原预激活及7天冷却。</summary>
        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            var c = FertilityAbilityUtility.Request(pawn?.CurJob);
            if (activating || pawn?.jobs?.curDriver is not JobDriver_FertilityConception { SubmittingCompletion: true } ||
                !CanCast || !FertilityAbilityUtility.IsCurrent(c) || c.Ability != this || c.Target != target.Pawn ||
                !FertilityAbilityUtility.CanApply(pawn, target.Pawn)) return false;
            var effects = EffectComps.OfType<CompAbilityEffect_FertilityConception>().Take(2).ToArray();
            if (effects.Length != 1) return false;
            var effect = effects[0];
            activating = true;
            effect.Succeeded = false;
            try
            {
                base.Activate(target, dest);
                return effect.Succeeded;
            }
            finally
            {
                // 公共建孕落地后即使后置回调抛异常，成功冷却也不能退款。
                // 仍由原PreActivate处理冷却和装备通知；执行期间保留重入锁。
                try { if (effect.Succeeded) base.PreActivate(target); }
                finally { activating = false; }
            }
        }
    }
    public class CompProperties_FertilityConception : CompProperties_AbilityEffect
    {
        /// <summary>XML仅配置一个受孕效果，由所属Ability管理一次性提交。</summary>
        public CompProperties_FertilityConception() { compClass = typeof(CompAbilityEffect_FertilityConception); }
    }
    /// <summary>效果仅接受同场正常完成中的一次建孕提交。</summary>
    public class CompAbilityEffect_FertilityConception : CompAbilityEffect
    {
        internal bool Succeeded;

        /// <summary>地图选人复用公共准入；这里不以自然受孕配对筛掉同性或不育对象。</summary>
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            bool valid = FertilityAbilityUtility.CanTarget(parent.pawn, target.Pawn, out string reason);
            if (!valid && throwMessages) Messages.Message(reason.Translate(), parent.pawn, MessageTypeDefOf.RejectInput, false);
            return valid;
        }
        /// <summary>效果预览仍只查询目标资格，不建立场景或消耗冷却。</summary>
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Valid(target);

        /// <summary>只消费当前原请求；建孕服务还须核对Ability正在正常完成提交。</summary>
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            var c = FertilityAbilityUtility.Request(parent.pawn.CurJob);
            Succeeded = c?.Ability == parent && c.Target == target.Pawn && FertilityAbilityUtility.TryConceive(c);
            if (!Succeeded) Messages.Message("SSC_FertilitySkill_Failed".Translate(), parent.pawn, MessageTypeDefOf.RejectInput, false);
        }
    }
    /// <summary>沿原版接触技能选择目标；实际效果由同场任务正常完成后提交，无独立预热。</summary>
    public class Verb_FertilityConception : Verb_CastAbilityTouch
    {
        /// <summary>目标选择允许同图全图；接触距离由任务接近及最终提交另行核对。</summary>
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            bool valid = FertilityAbilityUtility.CanTarget(ability.pawn, target.Pawn, out string reason);
            if (!valid && showMessages) Messages.Message(reason.Translate(), ability.pawn, MessageTypeDefOf.RejectInput, false);
            return valid;
        }
        /// <summary>全图目标预览采用与选人一致的资格。</summary>
        public override bool IsApplicableTo(LocalTargetInfo target, bool showMessages = false) => ValidateTarget(target, showMessages);

        /// <summary>实际提交仍要求施法者已经接触原目标，不远程补交。</summary>
        public override bool CanHitTarget(LocalTargetInfo target) => FertilityAbilityUtility.CanApply(ability.pawn, target.Pawn);

        /// <summary>原生接触技能的位置查询不改变全图选择范围。</summary>
        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo target) =>
            ValidateTarget(target, false) && ReachabilityImmediate.CanReachImmediate(root, target, ability.pawn.Map, PathEndMode.Touch, ability.pawn);

        /// <summary>取消独立CastVerb预热入口；同场任务正常完成后直接Activate，不能跳过性交。</summary>
        public override bool TryStartCastOn(LocalTargetInfo target, LocalTargetInfo dest,
            bool surpriseAttack = false, bool canHitNonTargetPawns = true, bool preventFriendlyFire = false,
            bool nonInterruptingSelfCast = false) => false;
    }
}
