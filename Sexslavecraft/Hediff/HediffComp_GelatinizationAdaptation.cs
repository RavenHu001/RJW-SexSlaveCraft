using System;
using RimWorld;
using UnityEngine;
using Verse;

// EN: This hediff comp advances limb-level gelatinization adaptation.
// EN: It converts corruption into adaptation growth and, once complete, swaps
// EN: the temporary hediff into a stable gelatinized limb hediff.
// CN: 这个 HediffComp 负责推进肢体级胶化的适应过程。
// CN: 它把腐化度转换为适应增长，并在完成后把临时 Hediff 替换成稳定的胶化肢体 Hediff。
namespace SexSlaveCraft
{
    public class HediffComp_GelatinizationAdaptation : HediffComp
    {
        public HediffCompProperties_GelatinizationAdaptation Props => 
            (HediffCompProperties_GelatinizationAdaptation)props;
        
        private int ticksSinceLastScan = 0;
        
        /// <summary>累计有效过程的实际经过时间，每达到配置间隔推进一次局部适应。</summary>
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            // parent.pawn 在移除状态后仍存在，以健康列表中的对象身份判断是否还能推进。
            if (!GelatinizationProcessUtility.IsActive(parent)) return;
            ticksSinceLastScan += delta;
            
            if (ticksSinceLastScan >= Props.scanInterval)
            {
                ticksSinceLastScan = 0;
                DoScan();
            }
        }
        
        /// <summary>沿用腐化度决定的适应概率和增长区间，适应度满值时进入成功结算。</summary>
        private void DoScan()
        {
            var gelHediff = parent as Hediff_GelatinizationTemporary;
            if (gelHediff == null || gelHediff.pawn.Dead) return;
            
            float corruptionLevel = gelHediff.CorruptionLevel;
            
            if (corruptionLevel >= 0.7f)
            {
                // 高腐化角色每次都取得一次区间增长；低腐化角色仍须通过下方概率判断。
                float adaptGain = Props.adaptGainRange.RandomInRange;
                gelHediff.adaptation += adaptGain;
            }
            else
            {
                // EN: Corruption increases the chance that the limb adapts instead of resisting.
                // CN: 腐化度越高，肢体越容易适应而不是继续抗拒胶化。
                float adaptChance = Props.baseAdaptChance + corruptionLevel * Props.corruptionBonusMultiplier;
                
                if (Rand.Chance(adaptChance))
                {
                    float adaptGain = Props.adaptGainRange.RandomInRange;
                    gelHediff.adaptation += adaptGain;
                }
            }
            
            // 百分比只保留 0..1；本组件不直接添加永久结果，统一交给带终结检查的成功入口。
            gelHediff.adaptation = Mathf.Clamp01(gelHediff.adaptation);
            
            if (gelHediff.adaptation >= 1f)
            {
                // EN: Completion replaces the temporary process with a permanent limb outcome.
                // CN: 适应完成后，要把临时过程替换成稳定的肢体结果。
                CompleteSuccessfully();
            }
        }
        
        /// <summary>先结束临时过程，再按仍有效的肢体类型添加永久结果。</summary>
        private void CompleteSuccessfully()
        {
            // 此处也会被开发按钮调用，必须独立检查终结资格，不能只依赖扫描入口的检查。
            if (!GelatinizationProcessUtility.TryComplete(parent)) return;
            
            // 无部位或部位已缺失时，过程已清理完毕，不再为无效部位制造义肢或追加缺肢状态。
            BodyPartRecord part = parent.Part;
            if (part == null)
            {
                return;
            }

            if (parent.pawn.health.hediffSet.PartIsMissing(part))
            {
                return;
            }
            
            // EN: Resolve the permanent result by limb type so arms and legs get different end states.
            // CN: 按肢体类型决定永久结果，让手臂和腿走不同的终态 Hediff。
            string targetHediffDefName = GetGelatinizedHediffDefNameForPart(part);
            var gelDef = DefDatabase<HediffDef>.GetNamedSilentFail(targetHediffDefName);
            
            if (gelDef != null)
            {
                var gelHediff = HediffMaker.MakeHediff(gelDef, parent.pawn, part);
                parent.pawn.health.AddHediff(gelHediff);
            }
            else
            {
                // 定义缺失时保留既有缺肢兜底；共享终结检查仍保证该兜底不会重复执行。
                Log.Warning($"[SSC] {targetHediffDefName} not found, using MissingBodyPart as fallback");
                var missingPartDef = DefDatabase<HediffDef>.GetNamedSilentFail("MissingBodyPart");
                if (missingPartDef != null)
                {
                    var missingPart = HediffMaker.MakeHediff(missingPartDef, parent.pawn, part) as Hediff_MissingPart;
                    if (missingPart != null)
                    {
                        missingPart.lastInjury = HediffDefOf.SurgicalCut;
                        missingPart.IsFresh = false;
                        parent.pawn.health.AddHediff(missingPart);
                    }
                }
            }
        }

        /// <summary>为当前有效过程强制适应成功，旧界面保留的按钮不能重新结算。</summary>
        public void DebugForceCompleteSuccess()
        {
            var gelHediff = parent as Hediff_GelatinizationTemporary;
            if (gelHediff == null || !GelatinizationProcessUtility.IsActive(parent))
            {
                return;
            }

            gelHediff.adaptation = 1f;
            CompleteSuccessfully();
            // 结算完成后通知健康界面刷新；移除状态不依赖清空 parent.pawn。
            parent.pawn.health.Notify_HediffChanged(parent);
        }
        
        /// <summary>使用原版肢体标签区分臂和腿，其余情况沿用原来的手臂定义兜底。</summary>
        private string GetGelatinizedHediffDefNameForPart(BodyPartRecord part)
        {
            if (part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbCore))
            {
                return "Hediff_GelatinizedArm";
            }
            else if (part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore))
            {
                return "Hediff_GelatinizedLeg";
            }
            
            return "Hediff_GelatinizedArm"; // fallback
        }
        
        /// <summary>只读输出开发用适应度与腐化度，不在查看提示时推进过程。</summary>
        public override string CompDebugString()
        {
            var gelHediff = parent as Hediff_GelatinizationTemporary;
            return $"Adaptation: {gelHediff?.adaptation.ToStringPercent()} (Corruption: {gelHediff?.CorruptionLevel.ToStringPercent()})";
        }

        /// <summary>为普通健康提示提供当前进度，类型不匹配时不增加额外提示。</summary>
        public override string CompTipStringExtra
        {
            get
            {
                var gelHediff = parent as Hediff_GelatinizationTemporary;
                if (gelHediff == null) return null;
                return $"适应度: {gelHediff.adaptation.ToStringPercent()}\n腐化度: {gelHediff.CorruptionLevel.ToStringPercent()}";
            }
        }
    }
}
