// AI 辅助生成：孕前选择执行真实身体载体、提交入口与原生Scribe，不生成婴儿或接管妊娠。
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Faction preconceptionPlayer;

    /// <summary>复用特化初始化，只补玩家派系和临产状态；逐项验证本批新增的保存与授权边界。</summary>
    private static void PreconceptionTests(Harmony harmony)
    {
        // 独立CLR未启动世界派系生成；给实际Faction.OfPlayer查询提供真实世界与玩家记录。
        AccessTools.Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map>());
        Current.Game.World.factionManager = new FactionManager();
        preconceptionPlayer = new Faction { def = new FactionDef { defName = "PreconceptionPlayer", isPlayer = true, techLevel = TechLevel.Industrial } };
        Current.Game.World.factionManager.Add(preconceptionPlayer);
        // 默认完整宿主可能已安装旧手术的玩家派系边界；沿当前有效玩家引用，不依赖执行顺序。
        preconceptionPlayer = Faction.OfPlayer;
        HediffDefOf.PregnancyLabor = DefDatabase<HediffDef>.GetNamedSilentFail("PregnancyLabor") ?? PregnancyDef("PregnancyLabor");
        HediffDefOf.PregnancyLaborPushing = DefDatabase<HediffDef>.GetNamedSilentFail("PregnancyLaborPushing") ?? PregnancyDef("PregnancyLaborPushing");

        Check("preconception readonly queries and fresh bodies keep natural defaults without creating carriers", () =>
        {
            var missing = ImprintPawn(PawnIdentity.Slave); missing.SetFactionDirect(preconceptionPlayer);
            int before = missing.health.hediffSet.hediffs.Count;
            var context = SSCFertilityPreconceptionUtility.Capture(missing);
            Require(!SSCFertilityPreconceptionUtility.CanSetGender(context, SSCOffspringGender.Male, out _));
            Require(!SSCFertilityPreconceptionUtility.TryGetApplicableSettings(missing, out _));
            var view = SSCAbilityToggleUtility.GetView(missing, SSCAbilityToggleUtility.OffspringImprint);
            Require(!view.Visible && !view.Enabled && !view.CanChange);
            Equal(before, missing.health.hediffSet.hediffs.Count);
            var pawn = PreconceptionPawn(); var owner = SSCUterusDevelopmentUtility.Find(pawn);
            Equal(SSCOffspringGender.Natural, owner.OffspringGender);
            Equal(SSCOffspringSpeciesSource.Natural, owner.OffspringSpeciesSource);
            Require(!owner.OffspringImprintEnabled);
            // 身体选择与人格同名键隔离，查询不得把字典值当作后代印记来源。
            pawn.TryGetComp<CompSexSlaveTraining>().abilityToggles.Set(SSCAbilityToggleUtility.OffspringImprint, true);
            Require(!SSCAbilityToggleUtility.GetEnabled(pawn, SSCAbilityToggleUtility.OffspringImprint));
        });

        Check("preconception native Scribe preserves choices and normalizes missing or invalid saved enums", () =>
        {
            var pawn = PreconceptionPawn(); SetPreconceptionChoices(pawn, SSCOffspringGender.Female, SSCOffspringSpeciesSource.Partner, true);
            string path = Save(pawn); var loaded = SSCUterusDevelopmentUtility.Find(Load(path));
            Equal(SSCOffspringGender.Female, loaded.OffspringGender);
            Equal(SSCOffspringSpeciesSource.Partner, loaded.OffspringSpeciesSource); Require(loaded.OffspringImprintEnabled);
            Edit(path, doc =>
            {
                doc.Descendants("sscOffspringGender").Single().Value = "99";
                doc.Descendants("sscOffspringSpeciesSource").Single().Value = "99";
            });
            loaded = SSCUterusDevelopmentUtility.Find(Load(path));
            Equal(SSCOffspringGender.Natural, loaded.OffspringGender);
            Equal(SSCOffspringSpeciesSource.Natural, loaded.OffspringSpeciesSource); Require(loaded.OffspringImprintEnabled);
            Edit(path, doc => doc.Descendants().Where(e => e.Name.LocalName is "sscOffspringGender" or "sscOffspringSpeciesSource" or "sscOffspringImprintEnabled").Remove());
            loaded = SSCUterusDevelopmentUtility.Find(Load(path));
            Equal(SSCOffspringGender.Natural, loaded.OffspringGender);
            Equal(SSCOffspringSpeciesSource.Natural, loaded.OffspringSpeciesSource); Require(!loaded.OffspringImprintEnabled);
        });

        Check("preconception unlocks gender species and imprint at their own effective stages", () =>
        {
            var pawn = PreconceptionPawn(.1999f); var comp = pawn.TryGetComp<CompSexSlaveTraining>();
            var context = SSCFertilityPreconceptionUtility.Capture(pawn);
            Require(!SSCFertilityPreconceptionUtility.CanSetGender(context, SSCOffspringGender.Male, out string lockedReason));
            Equal("SSC_Preconception_GenderLocked".Translate().ToString(), lockedReason);
            comp.specializationProgress = .2f;
            Require(SSCFertilityPreconceptionUtility.TrySetGender(context, SSCOffspringGender.Male, out string reason), reason);
            Require(!SSCFertilityPreconceptionUtility.CanSetSpeciesSource(context, SSCOffspringSpeciesSource.Self, out lockedReason));
            Equal("SSC_Preconception_SpeciesLocked".Translate().ToString(), lockedReason);
            comp.specializationProgress = .5f;
            Require(SSCFertilityPreconceptionUtility.TrySetSpeciesSource(context, SSCOffspringSpeciesSource.Self, out reason), reason);
            var lockedImprint = SSCAbilityToggleUtility.GetView(pawn, SSCAbilityToggleUtility.OffspringImprint);
            Require(!lockedImprint.CanChange);
            Equal("SSC_Preconception_ImprintLocked".Translate().ToString(), lockedImprint.ChangeReason);
            comp.specializationProgress = .999f;
            SetPreconceptionImprint(pawn, true);
            Require(!SSCFertilityPreconceptionUtility.TrySetGender(context, (SSCOffspringGender)99, out _));
            Require(!SSCFertilityPreconceptionUtility.TrySetSpeciesSource(context, (SSCOffspringSpeciesSource)99, out _));
            Equal(SSCOffspringGender.Male, SSCUterusDevelopmentUtility.Find(pawn).OffspringGender);
        });

        Check("preconception permits presets while fertility is off but applies only unlocked enabled choices", () =>
        {
            var pawn = PreconceptionPawn(); var owner = SSCUterusDevelopmentUtility.Find(pawn);
            SetPreconceptionChoices(pawn, SSCOffspringGender.Female, SSCOffspringSpeciesSource.Partner, true);
            var view = SSCAbilityToggleUtility.GetView(pawn, SSCAbilityToggleUtility.OffspringImprint);
            Require(view.Enabled && view.CanChange && !view.Active);
            Require(!SSCFertilityPreconceptionUtility.TryGetApplicableSettings(pawn, out _));
            Toggle(pawn, true);
            Require(SSCFertilityPreconceptionUtility.TryGetApplicableSettings(pawn, out var all));
            Equal(SSCOffspringGender.Female, all.Gender); Equal(SSCOffspringSpeciesSource.Partner, all.SpeciesSource); Require(all.ImprintEnabled);
            pawn.TryGetComp<CompSexSlaveTraining>().specializationProgress = .2f;
            Require(SSCFertilityPreconceptionUtility.TryGetApplicableSettings(pawn, out var partial));
            Equal(SSCOffspringGender.Female, partial.Gender); Equal(SSCOffspringSpeciesSource.Natural, partial.SpeciesSource); Require(!partial.ImprintEnabled);
            Equal(SSCOffspringSpeciesSource.Partner, owner.OffspringSpeciesSource); Require(owner.OffspringImprintEnabled);
        });

        Check("preconception losing direction identity or body qualification preserves choices and final restores cross direction", () =>
        {
            var pawn = PreconceptionPawn(); var owner = SSCUterusDevelopmentUtility.Find(pawn); var comp = pawn.TryGetComp<CompSexSlaveTraining>();
            SetPreconceptionChoices(pawn, SSCOffspringGender.Male, SSCOffspringSpeciesSource.Self, true); Toggle(pawn, true);
            comp.SetSpecialization(SexSlaveSpecializationType.None);
            var context = SSCFertilityPreconceptionUtility.Capture(pawn);
            Require(!SSCFertilityPreconceptionUtility.TrySetGender(context, SSCOffspringGender.Natural, out string directionReason));
            Equal("SSC_Preconception_DirectionRequired".Translate().ToString(), directionReason);
            var view = SSCAbilityToggleUtility.GetView(pawn, SSCAbilityToggleUtility.OffspringImprint);
            Require(view.Enabled && !view.CanChange && !view.Active);
            Require(!SSCAbilityToggleUtility.TrySet(pawn, SSCAbilityToggleUtility.OffspringImprint, false, view.Context, out _));
            Require(!SSCFertilityPreconceptionUtility.TryGetApplicableSettings(pawn, out _));
            pawn.health.AddHediff(FertilitySpecializationUtility.FinalDef); FertilitySpecializationUtility.Sync(pawn);
            Require(SSCFertilityPreconceptionUtility.CanSetSpeciesSource(SSCFertilityPreconceptionUtility.Capture(pawn), SSCOffspringSpeciesSource.Partner, out _));
            Require(SSCFertilityPreconceptionUtility.TryGetApplicableSettings(pawn, out var final)); Require(final.ImprintEnabled);
            owner.Severity = .5f;
            Require(!SSCFertilityPreconceptionUtility.TryGetApplicableSettings(pawn, out _));
            Equal(SSCOffspringGender.Male, owner.OffspringGender); Equal(SSCOffspringSpeciesSource.Self, owner.OffspringSpeciesSource); Require(owner.OffspringImprintEnabled);
            owner.Severity = 1f; comp.pawnIdentity = PawnIdentity.Unset;
            Require(!SSCFertilityPreconceptionUtility.CanSetGender(SSCFertilityPreconceptionUtility.Capture(pawn), SSCOffspringGender.Female, out _));
            // 原生育开关保持身体自身规则，不因新增特化设置加入身份门槛。
            Toggle(pawn, true); Require(owner.FertilityEnabled);
            comp.pawnIdentity = PawnIdentity.Slave;
            Require(SSCFertilityPreconceptionUtility.TryGetApplicableSettings(pawn, out final)); Require(final.ImprintEnabled);
        });

        Check("preconception conception and both labor stages reject changes from previously valid requests", () =>
        {
            foreach (var definition in new[] { HediffDefOf.PregnantHuman, HediffDefOf.PregnancyLabor, HediffDefOf.PregnancyLaborPushing })
            {
                var pawn = PreconceptionPawn(); SetPreconceptionChoices(pawn, SSCOffspringGender.Female, SSCOffspringSpeciesSource.Partner, true);
                var context = SSCFertilityPreconceptionUtility.Capture(pawn);
                var view = SSCAbilityToggleUtility.GetView(pawn, SSCAbilityToggleUtility.OffspringImprint);
                Require(view.CanChange);
                // 状态通过真实健康列表出现；不运行妊娠或临产推进，从而只检验提交时的准入竞态。
                var pregnant = new Hediff { def = definition, pawn = pawn }; pawn.health.hediffSet.hediffs.Add(pregnant);
                Require(!SSCFertilityPreconceptionUtility.TrySetGender(context, SSCOffspringGender.Natural, out _), definition.defName);
                // 三入口共用孕中锁：性别入口覆盖三状态，其余入口只在一种孕态检验接线，不做3×3矩阵。
                if (definition == HediffDefOf.PregnantHuman)
                {
                    Require(!SSCFertilityPreconceptionUtility.TrySetSpeciesSource(context, SSCOffspringSpeciesSource.Natural, out _));
                    Require(!SSCAbilityToggleUtility.TrySet(pawn, SSCAbilityToggleUtility.OffspringImprint, false, view.Context, out _));
                }
                var owner = SSCUterusDevelopmentUtility.Find(pawn);
                Equal(SSCOffspringGender.Female, owner.OffspringGender); Equal(SSCOffspringSpeciesSource.Partner, owner.OffspringSpeciesSource); Require(owner.OffspringImprintEnabled);
                pawn.health.hediffSet.hediffs.Remove(pregnant);
                Require(SSCFertilityPreconceptionUtility.CanSetGender(context, SSCOffspringGender.Male, out _));
            }
        });

        Check("preconception removed or same def replaced uterus rejects old enum and toggle owners", () =>
        {
            var pawn = PreconceptionPawn(); var owner = SSCUterusDevelopmentUtility.Find(pawn);
            SetPreconceptionChoices(pawn, SSCOffspringGender.Male, SSCOffspringSpeciesSource.Self, true);
            var context = SSCFertilityPreconceptionUtility.Capture(pawn);
            var view = SSCAbilityToggleUtility.GetView(pawn, SSCAbilityToggleUtility.OffspringImprint);
            pawn.health.RemoveHediff(owner); var replacement = Start(pawn, 1f);
            Require(!SSCFertilityPreconceptionUtility.IsCurrent(context));
            Require(!SSCFertilityPreconceptionUtility.TrySetGender(context, SSCOffspringGender.Female, out _));
            Require(!SSCAbilityToggleUtility.TrySet(pawn, SSCAbilityToggleUtility.OffspringImprint, true, view.Context, out _));
            Equal(SSCOffspringGender.Natural, replacement.OffspringGender); Require(!replacement.OffspringImprintEnabled);
            Require(SSCFertilityPreconceptionUtility.TrySetGender(SSCFertilityPreconceptionUtility.Capture(pawn), SSCOffspringGender.Female, out string reason), reason);
        });

        Check("preconception personality mutation loading and Comp replacement reject expired edit credentials", () =>
        {
            var pawn = PreconceptionPawn(); var context = SSCFertilityPreconceptionUtility.Capture(pawn);
            var view = SSCAbilityToggleUtility.GetView(pawn, SSCAbilityToggleUtility.OffspringImprint);
            Require(SSCPersonalityRuntimeUtility.TryBeginMutation(pawn, out var mutation));
            using (mutation)
                Require(!SSCFertilityPreconceptionUtility.CanSetGender(SSCFertilityPreconceptionUtility.Capture(pawn), SSCOffspringGender.Male, out _));
            Require(!SSCFertilityPreconceptionUtility.TrySetGender(context, SSCOffspringGender.Male, out _));
            Require(!SSCAbilityToggleUtility.TrySet(pawn, SSCAbilityToggleUtility.OffspringImprint, true, view.Context, out _));
            context = SSCFertilityPreconceptionUtility.Capture(pawn);
            var replacement = new CompSexSlaveTraining { parent = pawn, pawnIdentity = PawnIdentity.Slave, specializationType = SexSlaveSpecializationType.Fertility, specializationProgress = .999f };
            AccessTools.Field(typeof(ThingWithComps), "comps").SetValue(pawn, new List<ThingComp> { replacement });
            Require(!SSCFertilityPreconceptionUtility.TrySetSpeciesSource(context, SSCOffspringSpeciesSource.Partner, out _));
            context = SSCFertilityPreconceptionUtility.Capture(pawn);
            Scribe.mode = LoadSaveMode.LoadingVars;
            try { Require(!SSCFertilityPreconceptionUtility.TrySetGender(context, SSCOffspringGender.Male, out _)); }
            finally { Scribe.mode = LoadSaveMode.Inactive; }
            Equal(SSCOffspringGender.Natural, SSCUterusDevelopmentUtility.Find(pawn).OffspringGender);
        });

        Check("preconception duplicate uterus merging keeps the existing carrier choices", () =>
        {
            var pawn = PreconceptionPawn(); var owner = SSCUterusDevelopmentUtility.Find(pawn);
            SetPreconceptionChoices(pawn, SSCOffspringGender.Female, SSCOffspringSpeciesSource.Partner, true);
            var incoming = (Hediff_SSCUterusDevelopment)HediffMaker.MakeHediff(development, pawn, owner.Part);
            pawn.health.AddHediff(incoming, owner.Part);
            Require(ReferenceEquals(owner, SSCUterusDevelopmentUtility.Find(pawn)));
            Equal(1, pawn.health.hediffSet.hediffs.OfType<Hediff_SSCUterusDevelopment>().Count());
            Equal(SSCOffspringGender.Female, owner.OffspringGender); Equal(SSCOffspringSpeciesSource.Partner, owner.OffspringSpeciesSource); Require(owner.OffspringImprintEnabled);
        });

        Check("preconception snapshot cleanup and full insertion retain each body choices without transferring settings", () =>
        {
            var source = PreconceptionPawn(); var sourceOwner = SSCUterusDevelopmentUtility.Find(source);
            SetPreconceptionChoices(source, SSCOffspringGender.Female, SSCOffspringSpeciesSource.Partner, true);
            var data = new CompPersonalityStore(); data.StorePawnData(source); var copy = new CompPersonalityStore(); copy.CopyFrom(data);
            Require(!data.HasTag(development) && !copy.HasTag(development));
            Require(copy.abilityToggles == null || !copy.abilityToggles.HasEntries);
            ExcretionUtility.CleanConsumerData(source);
            Require(ReferenceEquals(sourceOwner, SSCUterusDevelopmentUtility.Find(source)));
            Equal(SSCOffspringGender.Female, sourceOwner.OffspringGender); Require(sourceOwner.OffspringImprintEnabled);
            var host = PreconceptionPawn(); var hostOwner = SSCUterusDevelopmentUtility.Find(host);
            SetPreconceptionChoices(host, SSCOffspringGender.Male, SSCOffspringSpeciesSource.Self, false);
            // 使用已支持的未绑定性奴宿主，不用有主缰绳的Master→Slave换人场景绕过现有身份锁。
            Require(ExcretionUtility.InheritEverything(host, copy));
            Require(ReferenceEquals(hostOwner, SSCUterusDevelopmentUtility.Find(host)));
            Equal(SSCOffspringGender.Male, hostOwner.OffspringGender); Equal(SSCOffspringSpeciesSource.Self, hostOwner.OffspringSpeciesSource); Require(!hostOwner.OffspringImprintEnabled);
            Require(SSCBondUtility.GetChain(host) == null && SSCBondUtility.GetBridle(host) == null);
            // 只对本批真正新增的编辑入口要求关系载体完全不变，不改写原人格植入的绑定恢复契约。
            var master = ImprintPawn(PawnIdentity.Master); Require(SSCBondUtility.Bind(master, host));
            var chain = SSCBondUtility.GetChain(host); var bridle = SSCBondUtility.GetBridle(master);
            SetPreconceptionChoices(host, SSCOffspringGender.Female, SSCOffspringSpeciesSource.Partner, true);
            Require(ReferenceEquals(chain, SSCBondUtility.GetChain(host)) && ReferenceEquals(bridle, SSCBondUtility.GetBridle(master)));
            Require(SSCBondUtility.IsBoundTo(host, master) && bridle.ValidTargets.Contains(host));
        });

        PreconceptionUiTests(harmony);
    }

    /// <summary>普通完成默认仍关闭生育，为所有本批编辑测试提供同一真实玩家与成年身体夹具。</summary>
    private static Pawn PreconceptionPawn(float progress = .999f)
    {
        var pawn = FertilityPawn(progress); pawn.SetFactionDirect(preconceptionPlayer); return pawn;
    }

    /// <summary>三种保存均走生产提交接口，不直接设置Hediff字段或人格字典。</summary>
    private static void SetPreconceptionChoices(Pawn pawn, SSCOffspringGender gender, SSCOffspringSpeciesSource species, bool imprint)
    {
        var context = SSCFertilityPreconceptionUtility.Capture(pawn);
        Require(SSCFertilityPreconceptionUtility.TrySetGender(context, gender, out string reason), reason);
        Require(SSCFertilityPreconceptionUtility.TrySetSpeciesSource(context, species, out reason), reason);
        SetPreconceptionImprint(pawn, imprint);
    }

    /// <summary>保存后代印记采用共用开关原上下文，不为测试绕过新子宫后端。</summary>
    private static void SetPreconceptionImprint(Pawn pawn, bool enabled)
    {
        var view = SSCAbilityToggleUtility.GetView(pawn, SSCAbilityToggleUtility.OffspringImprint);
        Require(SSCAbilityToggleUtility.TrySet(pawn, SSCAbilityToggleUtility.OffspringImprint, enabled, view.Context, out string reason), reason);
    }
}
