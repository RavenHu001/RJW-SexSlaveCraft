using System.Xml;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static List<Pawn> traitLoadPawns;
    private static Pawn traitLoadContainerPawn;
    private static Pawn traitIdentityExitOnRemove;

    private static void SexSlaveTraitIdentityTests(string repo, Harmony harmony)
    {
        var xml = new XmlDocument();
        xml.Load(Path.Combine(repo, "1.7/Defs/TraitDefs/Trait_SexSlave.xml"));
        var previousDef = SSCDefOf.SexSlaveTrait;
        SSCDefOf.SexSlaveTrait = DirectXmlToObject.ObjectFromXml<TraitDef>(xml.DocumentElement["TraitDef"], false);
        var corruptionDef = new NeedDef { defName = "SSC_TraitTestCorruption", needClass = typeof(Need_Corruption) };
        Need_Corruption AddNeed(Pawn pawn)
        {
            var need = new Need_Corruption(pawn) { def = corruptionDef };
            pawn.needs.AllNeeds.Add(need);
            return need;
        }
        List<Trait> DerivedTraits(Pawn pawn) =>
            pawn.story.traits.allTraits.Where(trait => trait.def == SSCDefOf.SexSlaveTrait).ToList();

        try
        {
            Check("SSC trait follows formal identity and retains its corruption peak", () =>
            {
                var pawn = ImprintPawn();
                var need = AddNeed(pawn);
                need.RestoreCorruption(.35f, .95f);
                Equal(0, DerivedTraits(pawn).Count);
                Require(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Slave));
                Equal(4, DerivedTraits(pawn).Single().Degree);
                Require(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Unset));
                Equal(0, DerivedTraits(pawn).Count);
                pawn.TryGetComp<CompSexSlaveTraining>().ReplaceMasterQualification(true);
                Require(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master));
                need.SetCorruption(.55f);
                Equal(0, DerivedTraits(pawn).Count);
                Near(.55f, need.CurLevel);
                Near(.95f, need.HighestCorruptionLevel);
            });

            Check("SSC trait upgrade stops if the native removal callback exits the identity", () =>
            {
                var pawn = ImprintPawn(PawnIdentity.Slave);
                var need = AddNeed(pawn);
                need.SetCorruption(.15f);
                Equal(1, DerivedTraits(pawn).Single().Degree);
                var remove = AccessTools.Method(typeof(TraitSet), nameof(TraitSet.RemoveTrait));
                var callback = AccessTools.Method(typeof(Program), nameof(TraitRemovedDuringUpgrade));
                traitIdentityExitOnRemove = pawn;
                harmony.Patch(remove, postfix: new HarmonyMethod(callback));
                try { need.SetCorruption(.95f); }
                finally
                {
                    harmony.Unpatch(remove, callback);
                    traitIdentityExitOnRemove = null;
                }
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(pawn));
                Equal(0, DerivedTraits(pawn).Count);
                Near(.95f, need.HighestCorruptionLevel);
            });

            Check("SSC trait load finalization cleans container leftovers without a need and restores a formal slave", () =>
            {
                var leftover = ImprintPawn();
                leftover.story.traits.allTraits.Add(new Trait(SSCDefOf.SexSlaveTrait, 1) { suppressedByTrait = true });
                leftover.story.traits.allTraits.Add(new Trait(SSCDefOf.SexSlaveTrait, 4) { suppressedByTrait = true });
                var formal = ImprintPawn(PawnIdentity.Slave);
                var need = AddNeed(formal);
                need.CurLevel = .55f;
                var legacy = ImprintPawn();
                AddNeed(legacy).RestoreCorruption(.35f, .95f);
                legacy.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave
                {
                    def = SSCDefOf.ChainOfSexSlave, pawn = legacy, target = ImprintPawn(PawnIdentity.Master)
                });
                legacy.story.traits.allTraits.Add(new Trait(SSCDefOf.SexSlaveTrait, 4) { suppressedByTrait = true });
                var snapshot = AccessTools.Method(typeof(SSCRestrictionGameComponent), nameof(SSCRestrictionGameComponent.AllPawns));
                var holders = AccessTools.Method(typeof(SSCBondOverviewWorld), nameof(SSCBondOverviewWorld.CollectCandidates));
                traitLoadPawns = new List<Pawn> { formal, legacy };
                traitLoadContainerPawn = leftover;
                harmony.Patch(snapshot, prefix: new HarmonyMethod(typeof(Program), nameof(TraitLoadSnapshot)));
                harmony.Patch(holders, prefix: new HarmonyMethod(typeof(Program), nameof(TraitLoadContainer)));
                try { new SSCRestrictionGameComponent(Current.Game).FinalizeInit(); }
                finally
                {
                    harmony.Unpatch(snapshot, AccessTools.Method(typeof(Program), nameof(TraitLoadSnapshot)));
                    harmony.Unpatch(holders, AccessTools.Method(typeof(Program), nameof(TraitLoadContainer)));
                    traitLoadPawns = null;
                    traitLoadContainerPawn = null;
                }
                Equal(0, DerivedTraits(leftover).Count);
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(leftover));
                Equal(3, DerivedTraits(formal).Single().Degree);
                Equal(0, DerivedTraits(legacy).Count);
                Require(SSCIdentityUtility.TryRestoreLegacySlaveIdentity(legacy));
                Equal(4, DerivedTraits(legacy).Single().Degree);
            });

            Check("SSC trait in full personality insertion follows the incoming identity and saved peak", () =>
            {
                var source = ImprintPawn(PawnIdentity.Slave);
                AddNeed(source).RestoreCorruption(.35f, .95f);
                var gel = new CompPersonalityStore();
                gel.StorePawnData(source);
                var host = ImprintPawn();
                var hostNeed = AddNeed(host);
                Require(ExcretionUtility.InheritEverything(host, gel));
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(host));
                Equal(4, DerivedTraits(host).Single().Degree);
                Near(.35f, hostNeed.CurLevel);
                Near(.95f, hostNeed.HighestCorruptionLevel);

                var ordinary = ImprintPawn();
                AddNeed(ordinary).RestoreCorruption(.5f, .95f);
                gel = new CompPersonalityStore();
                gel.StorePawnData(ordinary);
                gel.hasSexSlaveTrait = true; // 模拟旧载荷中的展示标记。
                Require(ExcretionUtility.InheritEverything(host, gel));
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(host));
                Equal(0, DerivedTraits(host).Count);
                Near(.5f, hostNeed.CurLevel);
                Near(.95f, hostNeed.HighestCorruptionLevel);
            });

            Check("SSC trait native duplication removes the copied trait at the real completion notification", () =>
            {
                var source = ImprintPawn(PawnIdentity.Slave);
                source.story.Childhood = new BackstoryDef { spawnCategories = new List<string>() };
                AddNeed(source).RestoreCorruption(.55f, .95f);
                var clone = ImprintPawn();
                // 独立宿主从原版复制各载体的步骤起跑，省去地图与全量 Pawn 生成。
                Hidden(typeof(GameComponent_PawnDuplicator), null, "CopyStoryAndTraits", source, clone);
                Hidden(typeof(GameComponent_PawnDuplicator), null, "CopyNeeds", source, clone);
                Equal(4, DerivedTraits(clone).Single().Degree);
                // 兼容来源角色的种族配置在副本上缺少 SSC 训练组件的情形。
                var compsField = AccessTools.Field(typeof(ThingWithComps), "comps");
                var cloneComps = compsField.GetValue(clone);
                compsField.SetValue(clone, null);
                var notify = AccessTools.Method(typeof(Pawn), nameof(Pawn.Notify_DuplicatedFrom));
                var postfix = AccessTools.Method(typeof(Harmony_SexSlaveTraitDuplication), "Postfix");
                harmony.CreateClassProcessor(typeof(Harmony_SexSlaveTraitDuplication)).Patch();
                try { clone.Notify_DuplicatedFrom(source); }
                finally { harmony.Unpatch(notify, postfix); }
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(clone));
                Equal(0, DerivedTraits(clone).Count);
                Equal(4, DerivedTraits(source).Single().Degree);
                var cloneNeed = clone.needs.TryGetNeed<Need_Corruption>();
                Near(.55f, cloneNeed.CurLevel);
                Near(.55f, cloneNeed.HighestCorruptionLevel);

                // 兔克隆在原版复制之后仍有正式身份恢复步骤，并按副本实际恶堕重算。
                compsField.SetValue(clone, cloneComps);
                Hidden(typeof(RabbitCloneUtility), null, "PrepareFreshRabbitCloneBody", source, clone);
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(clone));
                Equal(3, DerivedTraits(clone).Single().Degree);
                Equal(4, DerivedTraits(source).Single().Degree);
            });

            Check("native duplication restores the identity of a copy that inherits a mature slave imprint", () =>
            {
                var source = ImprintPawn(PawnIdentity.Slave);
                source.story.Childhood = new BackstoryDef { spawnCategories = new List<string>() };
                AddNeed(source).RestoreCorruption(.55f, .95f);
                var originalImprint = Mark(source, SSCImprintUtility.SlaveDefName);
                var clone = ImprintPawn();
                // 原版先复制健康和需求，随后通知各 Comp；副本起初没有 SSC 身份。
                Hidden(typeof(GameComponent_PawnDuplicator), null, "CopyStoryAndTraits", source, clone);
                Hidden(typeof(GameComponent_PawnDuplicator), null, "CopyHediffs", source, clone);
                Hidden(typeof(GameComponent_PawnDuplicator), null, "CopyNeeds", source, clone);
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(clone));

                var notify = AccessTools.Method(typeof(Pawn), nameof(Pawn.Notify_DuplicatedFrom));
                var postfix = AccessTools.Method(typeof(Harmony_SexSlaveTraitDuplication), "Postfix");
                harmony.CreateClassProcessor(typeof(Harmony_SexSlaveTraitDuplication)).Patch();
                try { clone.Notify_DuplicatedFrom(source); }
                finally { harmony.Unpatch(notify, postfix); }

                Equal(SSCImprintKind.Slave, SSCImprintUtility.GetKind(clone));
                Require(SSCImprintUtility.Find(clone) != originalImprint);
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(clone));
                Equal(3, DerivedTraits(clone).Single().Degree);
                Require(SSCBondUtility.GetBoundMaster(clone) == null);
                Require(!SSCIdentityUtility.TrySetIdentity(clone, PawnIdentity.Unset));
                Equal(4, DerivedTraits(source).Single().Degree);
            });
        }
        finally { SSCDefOf.SexSlaveTrait = previousDef; }
    }

    private static bool TraitLoadSnapshot(ref List<Pawn> __result)
    {
        __result = traitLoadPawns;
        return false;
    }

    private static bool TraitLoadContainer(Action<Pawn> add)
    {
        add(traitLoadContainerPawn);
        return false;
    }

    private static void TraitRemovedDuringUpgrade(TraitSet __instance)
    {
        Pawn pawn = traitIdentityExitOnRemove;
        if (pawn?.story?.traits != __instance) return;
        traitIdentityExitOnRemove = null;
        Require(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Unset));
    }
}
