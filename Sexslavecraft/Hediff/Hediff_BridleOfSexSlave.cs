using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using RimWorld;
using Verse;

// EN: This hediff is the master-side handle of the slave bond.
// EN: It stores the master's bound sex slaves, exposes the share-damage toggle, and keeps the master's resonance field in sync.
// CN: 这个 Hediff 就是主从绑定里“主人这一侧”的握柄。
// CN: 它保存主人已经绑定的性奴列表、提供伤害分担开关，并负责让主人的“灵欲共振场”保持同步。
namespace SexSlaveCraft
{
    public class Hediff_BridleOfSexSlave : HediffWithComps
    {
        public List<Pawn> targets = new List<Pawn>();
        public bool shareDamageEnabled = true;

        public IEnumerable<Pawn> ValidTargets => (targets ?? Enumerable.Empty<Pawn>()).Where(p => p != null && !p.DestroyedOrNull());

        public void AddTarget(Pawn pawn)
        {
            if (pawn != null && !targets.Contains(pawn))
            {
                targets.Add(pawn);
                // EN: Refresh the master's resonance field whenever a new sex slave is added to the bridle.
                // CN: 每次有新的性奴被加进这个握柄，都要立刻刷新主人的“灵欲共振场”。
                SSCMasterBondUtility.RefreshMasterEmpowerment(this.pawn);
            }
        }

        public void RemoveTarget(Pawn pawn)
        {
            if (pawn != null)
            {
                targets.Remove(pawn);
                // EN: Removing a bound sex slave also changes the resonance field, so refresh it here too.
                // CN: 移除已绑定性奴后，“灵欲共振场”也会变化，所以这里同样要刷新。
                SSCMasterBondUtility.RefreshMasterEmpowerment(this.pawn);
            }
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            // 这里可以添加其他初始化逻辑
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref targets, "targets", LookMode.Reference);
            Scribe_Values.Look(ref shareDamageEnabled, "shareDamageEnabled", true);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (targets == null) targets = new List<Pawn>();
                targets.RemoveAll(target => target == null || target.DestroyedOrNull());
            }
        }

        public override void Tick()
        {
            base.Tick();
            if (pawn.IsHashIntervalTick(250))
            {
                // EN: Periodic refresh prevents the master's resonance field from drifting out of sync with the live bond list.
                // CN: 定期刷新可以防止主人的“灵欲共振场”和当前真实绑定列表脱节。
                SSCMasterBondUtility.RefreshMasterEmpowerment(pawn);
            }
        }

        public override string LabelBase
        {
            get
            {
                if (ValidTargets.Any())
                {
                    string names = string.Join(", ", ValidTargets.Select(p => p.LabelShortCap));
                    // 修改：使用本地化格式 "{0} ({1})"
                    return Strings.Bridle_LabelWithNames(base.LabelBase, names);
                }
                return base.LabelBase;
            }
        }

        public override string Description
        {
            get
            {
                // EN: Build the player-facing summary text for the master's current resonance field.
                // CN: 这里生成玩家能看到的“灵欲共振场”摘要文本。
                string empowerment = SSCMasterBondUtility.GetEmpowermentSummary(pawn);
                if (ValidTargets.Any())
                {
                    string names = string.Join(", ", ValidTargets.Select(p => p.LabelShortCap));
                    // 修改：使用本地化拼接描述
                    return base.Description + Strings.Bridle_DescSlaves(names) + "\n\n" + empowerment;
                }
                return base.Description + "\n\n" + empowerment;
            }
        }

        public override string TipStringExtra => null;

        public override bool ShouldRemove => !ValidTargets.Any();

        public override void PostRemoved()
        {
            base.PostRemoved();
            if (SSCDefOf.SSC_MasterBondEmpowerment != null)
            {
                Hediff empowerment = pawn?.health?.hediffSet?.GetFirstHediffOfDef(SSCDefOf.SSC_MasterBondEmpowerment);
                if (empowerment != null)
                {
                    pawn.health.RemoveHediff(empowerment);
                }
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (pawn?.Faction != Faction.OfPlayer) yield break;

            yield return new Command_Toggle
            {
                defaultLabel = "SSC_MasterShareDamage_Label".Translate(),
                defaultDesc = "SSC_MasterShareDamage_Desc".Translate(SSCMasterBondUtility.DamageShareRatio.ToStringPercent()),
                icon = ContentFinder<Texture2D>.Get("UI/Icons/Toggle/Icon_Enabled", true),
                isActive = () => shareDamageEnabled,
                toggleAction = delegate
                {
                    shareDamageEnabled = !shareDamageEnabled;
                }
            };
        }

        // EN: Create or reuse the master-side bridle, then attach all provided sex slaves to it.
        // CN: 创建或复用主人这侧的“握柄”，再把传入的所有性奴绑定到这条主从链里。
        public static Hediff_BridleOfSexSlave AddToPawn(Pawn Master, params Pawn[] SexSlave)
        {
            if (Master == null) return null;

            // 先查找已有的 Hediff
            HediffDef def = SSCDefOf.BridleOfSexSlave;
            if (def == null) return null;
            Hediff_BridleOfSexSlave hediff = Master.health.hediffSet.GetFirstHediffOfDef(def) as Hediff_BridleOfSexSlave;

            // 如果没有，创建一个新的
            if (hediff == null)
            {
                hediff = (Hediff_BridleOfSexSlave)HediffMaker.MakeHediff(
                    def,
                    Master
                );
                Master.health.AddHediff(hediff);
            }

            // 添加目标 pawn（AddTarget 已经会检查重复）
            foreach (var p in SexSlave)
                hediff.AddTarget(p);

            // EN: After the full bind list is updated, refresh the master's resonance field one final time.
            // CN: 在完整绑定列表更新完后，再最后刷新一次主人的“灵欲共振场”。
            SSCMasterBondUtility.RefreshMasterEmpowerment(Master);

            return hediff;
        }
        public static Hediff_BridleOfSexSlave IncreaseBridleSeverity(Pawn pawn, float amount)
        {
            if (pawn == null) return null;

            HediffDef def = SSCDefOf.BridleOfSexSlave;
            if (def == null) return null;
            Hediff_BridleOfSexSlave hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def) as Hediff_BridleOfSexSlave;

            if (hediff == null) return null;

            hediff.Severity = Mathf.Clamp(hediff.Severity + amount, 0f, hediff.def.maxSeverity);

            return hediff;
        }
    }
}
