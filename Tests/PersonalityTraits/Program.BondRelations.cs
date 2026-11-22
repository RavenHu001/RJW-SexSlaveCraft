// AI 辅助生成
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>运行完整生产植入流程，仅由边界控制锁链能否恢复，关系过滤执行生产规则。</summary>
    private static void RunBondRelationTransferCases()
    {
#if SSC_RW17
        DefDatabase<HediffDef>.AllDefs.Add(new HediffDef { defName = Hediff_SSCOwnerlessSlave.DefName,
            hediffClass = typeof(Hediff_SSCOwnerlessSlave), maxSeverity = 1f });
        DefDatabase<HediffDef>.AllDefs.Add(new HediffDef { defName = Hediff_SSCBondReleaseAftereffect.DefName,
            hediffClass = typeof(Hediff_SSCBondReleaseAftereffect) });
#endif
        // 同一份旧凝胶同时包含正确主人、错误主人和普通关系。
        // 主人有效时只保留正确奖励；失效时两份主人关系都必须过滤。
        foreach (bool restoreBond in new[] { false, true })
            Run("人格植入按实际恢复锁链过滤主人关系；绑定成功=" + restoreBond, () =>
            {
                Pawn master = Body("Master"); Training(master).pawnIdentity = PawnIdentity.Master;
#if SSC_RW17
                // 主人已死亡时进入无主恢复。有效主人的提交失败则中止植入，语义不同。
                master.Dead = !restoreBond;
#endif
                Pawn stale = Body("FalseMaster"); Pawn friend = Body("Friend"); Pawn receiver = Body("Receiver");
                Training(receiver); receiver.relations = new RelationsTracker();
                var friendDef = new PawnRelationDef();
                var gel = Gel(); gel.sscIdentity = PawnIdentity.Slave;
                gel.chainHediffData = new StoredChainHediffData { masterPawn = master, severity = .5f };
                gel.storedRelations = new List<StoredRelationData>
                {
                    new StoredRelationData { def = SSCDefOf.SSC_FlawedLovers, otherPawn = master },
                    new StoredRelationData { def = SSCDefOf.SSC_FlawedLovers, otherPawn = stale },
                    new StoredRelationData { def = friendDef, otherPawn = friend }
                };
                SSCBondUtility.AllowBondRestore = restoreBond;
                Assert(ExcretionUtility.InheritEverything(receiver, gel), "完整人格恢复应成功");
                var relations = receiver.relations.DirectRelations;
                Assert(relations.Any(r => r.def == SSCDefOf.SSC_FlawedLovers && r.otherPawn == master) == restoreBond,
                    "仅成功恢复的实际主人能保留已赚取的奖励关系");
                Assert(!relations.Any(r => r.def == SSCDefOf.SSC_FlawedLovers && r.otherPawn == stale), "旧凝胶中的错误主人须过滤");
                Assert(relations.Any(r => r.def == friendDef && r.otherPawn == friend), "普通社会关系仍恢复");
            });
        // 主人人格可以换身体，但本次修复不能擅自把性奴的锁链从原身体转到接收者。
        Run("主人人格移植不复制仍属于原身体的主从社交关系", () =>
        {
            Pawn original = Body("OriginalMaster"); Pawn slave = Body("Slave"); Pawn receiver = Body("Receiver");
            Training(receiver); receiver.relations = new RelationsTracker(); SSCBondUtility.SeedBinding(original, slave);
            var gel = Gel(); gel.sscIdentity = PawnIdentity.Master;
            gel.storedRelations = new List<StoredRelationData> { new StoredRelationData { def = SSCDefOf.SSC_FlawedLovers, otherPawn = slave } };
            Assert(ExcretionUtility.InheritEverything(receiver, gel), "主人人格恢复应成功");
            Assert(!receiver.relations.DirectRelations.Any(r => r.def == SSCDefOf.SSC_FlawedLovers), "新身体不能冒充原锁链主人");
            Assert(SSCBondUtility.GetBoundMaster(slave) == original, "不能改写性奴锁链以迎合旧关系");
        });
    }
}
