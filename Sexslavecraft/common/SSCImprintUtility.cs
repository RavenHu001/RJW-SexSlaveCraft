// AI 辅助生成：人格印记以 Hediff 为唯一运行时记录，离体时复用人格凝胶健康标签。
using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>从印记定义读取的形态；仅作查询结果，不另外序列化到人格组件。</summary>
    public enum SSCImprintKind { None, Initial, Master, Slave }

    /// <summary>集中处理人格印记的识别、授予、定型、年龄纠正及凝胶流转。</summary>
    public static class SSCImprintUtility
    {
        // 稳定 Def 名同时用于运行时识别与凝胶快照；本地化名称不参与分化或锁定判断。
        public const string InitialDefName = "SSC_ImprintInitial";
        public const string MasterDefName = "SSC_ImprintMaster";
        public const string SlaveDefName = "SSC_ImprintSlave";
        // 只限制生理年龄，历法年龄、原有疾病和器官损伤不作为印记数据搬运。
        public const long YoungAgeTicks = 25L * GenDate.TicksPerYear;

        /// <summary>按稳定 Def 名识别三态；空值或其他健康状态返回无印记。</summary>
        public static SSCImprintKind Kind(HediffDef def)
            => def?.defName == InitialDefName ? SSCImprintKind.Initial :
               def?.defName == MasterDefName ? SSCImprintKind.Master :
               def?.defName == SlaveDefName ? SSCImprintKind.Slave : SSCImprintKind.None;

        /// <summary>供健康标签采集和恢复过滤辨认印记，不依据严重度或显示名称判断。</summary>
        public static bool IsImprintDef(HediffDef def) => Kind(def) != SSCImprintKind.None;

        /// <summary>只读取得身体上的现有印记；不因查询而授予、分化或补建记录。</summary>
        public static Hediff Find(Pawn pawn)
            => pawn?.health?.hediffSet?.hediffs?.FirstOrDefault(h => IsImprintDef(h.def));

        /// <summary>返回当前人格携带的印记形态，不按身体年龄或当前身份切换形态。</summary>
        public static SSCImprintKind GetKind(Pawn pawn) => Kind(Find(pawn)?.def);

        /// <summary>由分化态推导永久身份方向；Unset 表示没有印记锁，不表示角色当前无身份。</summary>
        public static PawnIdentity GetImprintLockedIdentity(Pawn pawn)
        {
            SSCImprintKind kind = GetKind(pawn);
            return kind == SSCImprintKind.Master ? PawnIdentity.Master :
                kind == SSCImprintKind.Slave ? PawnIdentity.Slave : PawnIdentity.Unset;
        }

        /// <summary>只检查目标身份是否符合印记锁；主人资格和原有绑定锁仍由身份服务检查。</summary>
        public static bool AllowsIdentity(Pawn pawn, PawnIdentity target)
        {
            PawnIdentity locked = GetImprintLockedIdentity(pawn);
            return locked == PawnIdentity.Unset || locked == target;
        }

        /// <summary>初始印记者在游戏 Adult 阶段获得主人选择准入，不据此写入永久资格或提前分化。</summary>
        public static bool HasBornMasterEligibility(Pawn pawn)
            => GetKind(pawn) == SSCImprintKind.Initial && pawn.ageTracker != null &&
               pawn.DevelopmentalStage == DevelopmentalStage.Adult;

        /// <summary>种子使用前确认三态资源与实现类齐备，避免只更新 DLL 时发生不完整授予。</summary>
        public static bool DefinitionsAvailable => new[] { InitialDefName, MasterDefName, SlaveDefName }
            .All(name => DefDatabase<HediffDef>.GetNamedSilentFail(name)?.hediffClass == typeof(Hediff_SSCImprint));

        /// <summary>为尚无印记者授予初始态；预留给后续普通婴儿生成，不改身份或覆盖现有印记。</summary>
        public static bool TryGrantInitial(Pawn pawn)
            => GetKind(pawn) == SSCImprintKind.None && Add(pawn, InitialDefName);

        /// <summary>先完成身份提交，再添加对应定型印记；不能反转已分化方向。</summary>
        internal static bool TryGrantDifferentiated(Pawn pawn, PawnIdentity identity)
        {
            // 目标身份必须已由正常身份／绑定入口写入；无印记和初始态才可取得新的定型事实。
            if (SSCIdentityUtility.GetIdentity(pawn) != identity ||
                (identity != PawnIdentity.Master && identity != PawnIdentity.Slave)) return false;
            SSCImprintKind kind = GetKind(pawn);
            if (kind != SSCImprintKind.None && kind != SSCImprintKind.Initial) return false;
            string name = identity == PawnIdentity.Master ? MasterDefName : SlaveDefName;
            if (DefDatabase<HediffDef>.GetNamedSilentFail(name) == null) return false;
            // 移除初始态和添加分化态都会发出健康回调；始终核对原人格、原 Comp 与目标身份。
            object token = SSCPersonalityRuntimeUtility.Capture(pawn);
            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            Clear(pawn);
            if (!SSCPersonalityRuntimeUtility.IsCurrent(pawn, token) || pawn.TryGetComp<CompSexSlaveTraining>() != comp ||
                SSCIdentityUtility.GetIdentity(pawn) != identity || GetKind(pawn) != SSCImprintKind.None) return false;
            if (!Add(pawn, name)) return false;
            if (!SSCPersonalityRuntimeUtility.IsCurrent(pawn, token) || pawn.TryGetComp<CompSexSlaveTraining>() != comp ||
                SSCIdentityUtility.GetIdentity(pawn) != identity) return false;
            // 天生初始者此前只有成年选择准入；主人定型后补成原有的永久主人资格记录。
            if (identity == PawnIdentity.Master) comp.ReplaceMasterQualification(true);
            return SSCPersonalityRuntimeUtility.IsCurrent(pawn, token);
        }

        /// <summary>通过原版健康入口添加指定印记，并确认最终挂载结果；互斥和即时回春由 Hediff 处理。</summary>
        private static bool Add(Pawn pawn, string name)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(name);
            if (pawn?.health?.hediffSet == null || pawn.TryGetComp<CompSexSlaveTraining>() == null || def == null) return false;
            pawn.health.AddHediff(def);
            return Find(pawn)?.def == def;
        }

        /// <summary>仅在新关系实际建立后调用，恢复旧链和反向索引修复不调用。</summary>
        internal static void DifferentiateAfterBinding(Pawn master, Pawn slave)
        {
            // 实际主人由双向关系确定，不能用仪式主持者或仅创建成功的一端作为依据。
            object slaveToken = SSCPersonalityRuntimeUtility.Capture(slave);
            CompSexSlaveTraining slaveComp = slave.TryGetComp<CompSexSlaveTraining>();
            if (!SSCBondUtility.IsBoundTo(slave, master) ||
                SSCBondUtility.GetBridle(master)?.ValidTargets.Contains(slave) != true) return;
            if (GetKind(master) == SSCImprintKind.Initial) TryGrantDifferentiated(master, PawnIdentity.Master);
            // 主人定型的健康回调也可能换入另一个受方人格，不能沿用旧绑定继续给后来者定型。
            if (SSCPersonalityRuntimeUtility.IsCurrent(slave, slaveToken) && slave.TryGetComp<CompSexSlaveTraining>() == slaveComp &&
                SSCBondUtility.IsBoundTo(slave, master) &&
                GetKind(slave) == SSCImprintKind.Initial) TryGrantDifferentiated(slave, PawnIdentity.Slave);
        }

        /// <summary>只纠正生理年龄；原版 setter 同步成长与生命阶段，历法年龄和健康列表不动。</summary>
        public static void CorrectAge(Pawn pawn)
        {
            if (GetImprintLockedIdentity(pawn) != PawnIdentity.Unset && pawn.ageTracker?.AgeBiologicalTicks > YoungAgeTicks)
                pawn.ageTracker.AgeBiologicalTicks = YoungAgeTicks;
        }

        /// <summary>移出印记，供分化替换或人格清源使用；普通解绑不调用此入口。</summary>
        internal static void Clear(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;
            // 移除会修改原健康列表，先建立快照；清理时一并移出可能残留的重复记录。
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs.Where(h => IsImprintDef(h.def)).ToList())
                pawn.health.RemoveHediff(hediff);
        }

        /// <summary>向已初始化的凝胶健康标签字典精确写入当前印记，不依赖加工配方枚举。</summary>
        internal static void Store(CompPersonalityStore data, Pawn pawn)
        {
            // 载荷可能被重复采集；先删除旧印记，确保无印记来源也能覆盖这份凝胶的旧结果。
            foreach (HediffDef def in data.hediffTags.Keys.Where(IsImprintDef).ToList()) data.hediffTags.Remove(def);
            Hediff imprint = Find(pawn);
            if (imprint != null) data.hediffTags[imprint.def] = imprint.Severity;
        }

        /// <summary>坏快照在清空宿主前拒绝；缺字段是合法的无印记旧凝胶。</summary>
        internal static bool CanRestore(Pawn pawn, CompPersonalityStore data)
        {
            var tags = data.hediffTags?.Where(p => IsImprintDef(p.Key)).ToList();
            if (tags == null || tags.Count == 0) return true;
            // 三态均以严重度 1 保存；拒绝多个印记、损坏数值或无法承载身份的接收身体。
            if (tags.Count != 1 || tags[0].Value != 1f || pawn?.health == null ||
                pawn.TryGetComp<CompSexSlaveTraining>() == null) return false;
            // 初始态尚未定型；分化态必须与凝胶已保存的 SSC 身份一致，不能植入后再猜方向。
            SSCImprintKind kind = Kind(tags[0].Key);
            return kind == SSCImprintKind.Initial ||
                (kind == SSCImprintKind.Master && data.sscIdentity == PawnIdentity.Master) ||
                (kind == SSCImprintKind.Slave && data.sscIdentity == PawnIdentity.Slave);
        }

        /// <summary>恢复来源的精确形态；调用方须先移出宿主印记并恢复来源身份，不执行重新分化。</summary>
        internal static bool Restore(Pawn pawn, CompPersonalityStore data)
        {
            if (!CanRestore(pawn, data)) return false;
            HediffDef def = data.hediffTags?.Keys.FirstOrDefault(IsImprintDef);
            return def == null || Add(pawn, def.defName);
        }

        /// <summary>从离体载荷生成形态与永久锁说明，供凝胶检查文字和人格卡共用。</summary>
        public static string GelLabel(CompPersonalityStore data)
        {
            // 只解释凝胶内的印记；不查询宿主年龄、当前主人或全局身份选择设置。
            HediffDef def = data?.hediffTags?.Keys.FirstOrDefault(IsImprintDef);
            string state = def?.LabelCap.ToString() ?? "SSC_Imprint_None".Translate().ToString();
            string locked = Kind(def) == SSCImprintKind.Master ? "SSC_Imprint_MasterLocked" :
                Kind(def) == SSCImprintKind.Slave ? "SSC_Imprint_SlaveLocked" : "SSC_Imprint_Unlocked";
            return "SSC_Imprint_GelStatus".Translate(state, locked.Translate()).ToString();
        }

        /// <summary>人格换入时中止其前任尚未完成的种子任务，不影响其他任务或物品。</summary>
        internal static void CancelPendingSeedUse(Pawn pawn)
        {
            // 只取消当前针对欲望之种的 UseItem 任务；不消耗物品，也不在人格替换中启动新任务。
            if (pawn?.jobs?.curDriver is JobDriver_UseItem &&
                pawn.CurJob.targetA.Thing?.def == SSCDefOf.SSC_MasterQualificationItem)
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
        }
    }
}
