// AI 辅助生成：太空 PNA 集群的单次投送；普通护甲与适应性吸收使用不同入口。
using System;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>同一份基线同时驱动真实剂量、效力显示和修复预算，不保存弹药或修复额度。</summary>
    public sealed class SSCPnaSpacerExtension : DefModExtension
    {
        // 归一化负荷：0.20 是 20 个负荷百分点；不是原始 Hediff 严重度。
        public float baseLoad;
        // 仅引用既有高意识抗性快照；身体负荷载体仍由统一暴露入口选择。
        public HediffDef recordHediff;

        // 同时验证修复推导不会溢出；损坏配置不能显示有效药量却在治疗时产生无限值。
        internal bool IsValid => recordHediff != null && SSCPnaMath.IsFinite(baseLoad) &&
            baseLoad > 0f && SSCPnaMath.IsFinite(baseLoad * 5f);
        internal float RepairBudget => baseLoad * 5f;
    }

    /// <summary>
    /// 三种太空工作端共用的处理器。上游 TakeDamage 保留护盾、命中与原版攻击通知；
    /// 此处只选择一个受击部位，普通目标判甲一次，然后调用原版最终伤势写入接口。
    /// 不调用 base.Apply，不生成第二次 TakeDamage，也不执行刺伤的内部扩散。
    /// </summary>
    public class DamageWorker_SSCPnaSpacer : DamageWorker_AddInjury
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            // 准入只针对新工作端：不合格目标与配置返回空结果，不委托旧物理入口。
            var result = new DamageResult();
            // 正常主人入口已禁止分摊；防御性拒绝其他调用者转移来的纳米接触，
            // 不把未被实际命中的身体当成无伤吸收／修复对象。
            if (SSCPnaDamageShareUtility.IsRedirectedHit(thing)) return result;
            var pawn = thing as Pawn;
            var config = dinfo.Def?.GetModExtension<SSCPnaSpacerExtension>();
            // 损坏的新配置安全退出，不能回退成会伤及受保护身体的普通伤害。
            if (Scribe.mode != LoadSaveMode.Inactive || !SSCPnaExposureUtility.IsValidTarget(pawn) ||
                config?.IsValid != true || !SSCPnaMath.IsFinite(dinfo.Amount) || dinfo.Amount <= 0f ||
                (!DebugSettings.enablePlayerDamage && pawn.Faction == Faction.OfPlayer)) return result;

            // 所有状态只属于当前调用；独立连发、同 tick 和同步嵌套不会互相认领。
            var health = pawn.health;
            var set = health.hediffSet;
            var body = pawn.RaceProps.body;
            object token = SSCPersonalityRuntimeUtility.Capture(pawn);
            bool masterAtHit = SSCIdentityUtility.IsMaster(pawn);
            bool protectedHit = masterAtHit || SSCPnaExposureUtility.IsRooted(pawn);
            // 身体实例／人格凭据必须始终属于本次命中；保护标记则只增不减。
            // 因此回调中新获保护会阻止后续创伤，原有保护解绑后也不会补伤。
            bool Current()
            {
                if (Scribe.mode != LoadSaveMode.Inactive || !SSCPnaExposureUtility.IsValidTarget(pawn) ||
                    pawn.health != health || pawn.health.hediffSet != set || pawn.RaceProps.body != body ||
                    !SSCPersonalityRuntimeUtility.IsCurrent(pawn, token) ||
                    SSCPersonalityRuntimeUtility.IsMutating(pawn)) return false;
                // 新获保护立即生效；已获得的创伤保护在本次调用内不因解绑而撤销。
                protectedHit |= SSCIdentityUtility.IsMaster(pawn) || SSCPnaExposureUtility.IsRooted(pawn);
                return true;
            }

            // 先协调遗留身体负荷，再按当前身份选路；命中开始已是主人者不因协调失去免疫。
            // 原有保护若已在协调回调中失效，结束本次命中，不回退普通伤害。
            SSCPnaExposureUtility.RefreshBodyState(pawn);
            if (!Current() || masterAtHit || SSCIdentityUtility.IsMaster(pawn)) return result;
            bool absorption = SSCPnaExposureUtility.IsRooted(pawn);
            if (protectedHit && !absorption) return result;

            // 只选择一次真实部位：显式部位无效时直接拒绝，不重新随机到另一处组织。
            // organicAtHit 防止人工部件在回调中移除后被误认成原本就能受药。
            BodyPartRecord part = dinfo.HitPart ?? ChooseHitPart(dinfo, pawn);
            if (!Current() || !SSCPnaSpacerBodyUtility.HasPart(pawn, part)) return result;
            dinfo.SetHitPart(part);
            result.AddPart(pawn, part);
            bool organicAtHit = SSCPnaSpacerBodyUtility.IsOrganic(pawn, part);
            // 固定命中前意识，即使普通微创伤或衣甲回调改变了后续意识也不重新取样。
            var resistance = SSCPnaWeaponDoseUtility.Capture(pawn, config.recordHediff,
                SSCPnaWeaponResistancePolicy.Coating);
            float dose = SSCPnaWeaponDoseUtility.FromLoad(config.baseLoad, resistance);
            // 后续药效／修复共用继续条件；只读检查实际身体和部位，不要求伤口或负荷增量。
            // 已保护身体必须仍有有效链才能继续吸收，主人免疫始终优先。
            bool Delivered() => Current() && !SSCIdentityUtility.IsMaster(pawn) &&
                organicAtHit && SSCPnaSpacerBodyUtility.IsOrganic(pawn, part) &&
                (!protectedHit || SSCPnaExposureUtility.IsRooted(pawn));

            // 唯一普通护甲入口。有链吸收完全跳过本块，既无身体伤害也无衣甲磨损；
            // 护盾等上游吸收仍已由 TakeDamage 完成，此处不为整次攻击设置 IgnoreArmor。
            if (!absorption)
            {
                DamageDef postDef = dinfo.Def;
                float amount = dinfo.Amount;
                bool metalDeflect = false, metalDiminish = false;
                if (!dinfo.IgnoreArmor)
                    amount = ArmorUtility.GetPostArmorDamage(pawn, amount, dinfo.ArmorPenetrationInt,
                        part, ref postDef, out metalDeflect, out metalDiminish);
                // 在受伤倍率、外部部位保护等后续修正之前固定穿透事实。转钝即使仍有
                // 正伤害也不合格；最终未产生伤势则不反过来否决已经成立的纳米入体。
                bool penetrated = SSCPnaMath.IsFinite(amount) && amount > 0f && postDef == dinfo.Def;
                result.diminished = amount < dinfo.Amount;
                result.diminishedByMetalArmor = metalDiminish;
                result.deflected = !SSCPnaMath.IsFinite(amount) || amount <= 0f;
                result.deflectedByMetalArmor = metalDeflect;
                if (!Current() || !SSCPnaSpacerBodyUtility.HasPart(pawn, part)) return result;

                // 转钝保留普通物理伤害；投药依据护甲刚刚确认的穿透，不依据最终伤口。
                dinfo.Def = postDef;
                if (!protectedHit && !result.deflected)
                {
                    if (postDef.ExternalViolenceFor(pawn)) amount *= pawn.GetStatValue(StatDefOf.IncomingDamageFactor);
                    // 属性读取也可能经第三方回调改身份，实际写伤前再检查一次保护。
                    if (!Current()) return result;
                    if (!protectedHit && SSCPnaMath.IsFinite(amount) && amount > 0f)
                    {
                        amount = ReduceDamageToPreserveOutsideParts(amount, dinfo, pawn);
                        // 单部位结算不追加外层重复创伤；原版最终写入仍保留濒死保护和伤势通知。
                        if (amount > 0f) FinalizeAndAddInjury(pawn, amount, dinfo, result);
                    }
                }
                // 仅恢复受伤／弹开绘制通知，不对无伤吸收伪造 wounded 或伤害统计。
                if (pawn.Spawned && Current())
                {
                    if (result.wounded) pawn.Drawer.Notify_DamageApplied(dinfo);
                    else if (result.deflected) pawn.Drawer.Notify_DamageDeflected(dinfo);
                }
                if (!penetrated) return result;
            }

            if (!Delivered() || !SSCPnaMath.IsFinite(dose) || dose <= 0f) return result;
            // 成功准入才提交快照。返回值只代表本次新建实例；已有或嵌套命中建立的
            // 同类记录不由本次持有，失败清理时不能一并删除。新实例被回调移除则中止。
            Hediff snapshot = SSCPnaWeaponDoseUtility.StoreSnapshot(pawn, config.recordHediff, resistance);
            if (!Delivered() || (snapshot != null && !set.hediffs.Contains(snapshot)))
            {
                SSCPnaWeaponDoseUtility.RemoveUnusedSnapshot(pawn, snapshot);
                return result;
            }
            // 每个 Apply 只调用一次共享输入；传播请求不拆成多剂，独立连发则各走一次 Apply。
            // 继续条件在共享工具的同步健康回调边界重查，已合法写入的负荷不做伤后回滚。
            var exposure = SSCPnaExposureUtility.ApplyExposure(pawn, dose, SSCPnaExposureSource.Weapon, Delivered);
            if (!exposure.Accepted)
            {
                SSCPnaWeaponDoseUtility.RemoveUnusedSnapshot(pawn, snapshot);
                return result;
            }
            // 满负荷的 Accepted 仍有效；修复不取 AddedLoad，也不随意识抗性放大。
            if (exposure.Rooted && Delivered())
                SSCPnaSpacerBodyUtility.Repair(pawn, config.RepairBudget, Delivered);
            // Heal 也可能解绑、换身体或改为主人，显示前再次检查，避免给失效对象补反馈。
            if (Delivered()) SSCPnaWeaponEffectsUtility.ShowAbsorption(pawn);
            return result;
        }
    }

    internal static class SSCPnaSpacerBodyUtility
    {
        internal static bool HasPart(Pawn pawn, BodyPartRecord part)
        {
            if (part?.def == null || pawn.RaceProps?.body?.AllParts.Contains(part) != true) return false;
            // 原版 PartIsMissing 只检查直接记录，不能把缺失父部位的子部位视为现存组织。
            for (var current = part; current != null; current = current.parent)
                if (pawn.health.hediffSet.PartIsMissing(current)) return false;
            return true;
        }

        /// <summary>人工父部位覆盖其子部位；有机移植保持可投送／修复资格。</summary>
        internal static bool IsOrganic(Pawn pawn, BodyPartRecord part)
        {
            if (!HasPart(pawn, part) || part.def.IsSolid(part, pawn.health.hediffSet.hediffs)) return false;
            for (var current = part; current != null; current = current.parent)
            {
                var added = pawn.health.hediffSet.GetDirectlyAddedPartFor(current);
                if (added != null && !added.def.organicAddedBodypart) return false;
            }
            return true;
        }

        /// <summary>单次选最严重的一处当前伤势，同严重度保留列表首项；溢出直接丢弃。</summary>
        internal static void Repair(Pawn pawn, float budget, Func<bool> current)
        {
            if (!current() || !SSCPnaExposureUtility.IsRooted(pawn) || SSCIdentityUtility.IsMaster(pawn)) return;
            // 自然愈合只排除永久伤，另查实例归属、有限正严重度及整个部位祖先链。
            // 选择时不改健康列表；相同严重度保留首项，避免额外随机状态。
            Hediff_Injury best = null;
            foreach (Hediff record in pawn.health.hediffSet.hediffs)
                if (record is Hediff_Injury injury && injury.pawn == pawn && injury.CanHealNaturally() &&
                    SSCPnaMath.IsFinite(injury.Severity) && injury.Severity > 0f && IsOrganic(pawn, injury.Part) &&
                    (best == null || injury.Severity > best.Severity)) best = injury;
            if (best == null || !current() || !SSCPnaExposureUtility.IsRooted(pawn) ||
                SSCIdentityUtility.IsMaster(pawn) || !pawn.health.hediffSet.hediffs.Contains(best)) return;
            // 只治疗选中的一处，预算受剩余严重度限制；不跨伤口分配，也不留下未来额度。
            // 原版 Heal 自己发送健康通知，调用方负责其回调后的继续条件与视觉反馈。
            best.Heal(Math.Min(budget, best.Severity));
        }
    }
}
