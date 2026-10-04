using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    // 用相同用例遍历整个宠物组，保证已有狗路线和合法旧猫、兔人格同受保护。
    // 本套件直接链接生产工具及补丁，不在这些测试方法中另写加工资格算法。
    private static readonly SexSlaveSpecializationType[] Pets =
    {
        SexSlaveSpecializationType.PetCat, SexSlaveSpecializationType.PetDog, SexSlaveSpecializationType.PetRabbit
    };

    /// <summary>注册组内六个最小定义，供真实宠物工具查询；不模拟游戏 XML 加载。</summary>
    private static void RegisterPetDefinitions()
    {
        foreach (var type in Pets)
            foreach (string suffix in new[] { "", "_Final" })
                DefDatabase<HediffDef>.AllDefs.Add(new HediffDef { defName = "SSC_Hediff_" + type + suffix });
    }

    /// <summary>分别执行资格、结算保护、产物复制和正常植入的回归用例组。</summary>
    private static void RunPetRecipeCases()
    {
        Run("宠物终极化统一检查完成容差、当前方向、普通标签及异常值", PetRecipeEligibility);
        Run("宠物筛料保留原账单拒绝结果并拒绝已销毁或堆叠来源", PetRecipeBillFilter);
        Run("三种配方拒绝其他宠物方向及任意已有宠物终极", PetRecipeAllDirections);
        Run("三种宠物乘八种凝胶按真实结算映射且只扣一次账单", PetRecipeAllBases);
        Run("宠物加工保留人格字段、全部历史及其他标签并深拷贝可变数据", PetRecipeCopyIsolation);
        Run("宠物制作中资格失效在分堆、消耗及计数前终止", PetRecipeChangedDuringWork);
        Run("宠物配方元数据缺失或矛盾不能回退通用放行", PetRecipeMetadata);
        Run("组内六个状态定义任一缺失都拒绝且保留原料", PetRecipeMissingDefinitions);
        Run("宠物结算拒绝缺失、超量、多人格和未知来源且保留材料", PetRecipeInvalidSources);
        Run("宠物加工产物定义或人格组件缺失时消耗前拒绝", PetRecipeProductDefinitions);
        Run("宠物半成品优先检查真实内装原料并保护容器", PetRecipeUnfinished);
        Run("宠物工作者独立复查资格但允许原版正常消耗后的来源", PetRecipeWorker);
        Run("普通宠物提取、终极加工与跨体植入不混入宿主成果", PetRecipePipeline);
        Run("恢复任务的最终步骤复查当前凝胶而非缓存筛料结果", PetRecipeResumedCompletion);
    }

    /// <summary>通过生产映射取得普通状态定义，避免测试另建名称映射。</summary>
    private static HediffDef PetBase(SexSlaveSpecializationType type) => PetSpecializationUtility.GetBaseHediffDef(type);
    /// <summary>通过生产映射取得终极定义，确保标签与资格使用相同对象。</summary>
    private static HediffDef PetFinal(SexSlaveSpecializationType type) => PetSpecializationUtility.GetFinalHediffDef(type);

    /// <summary>构造合法配方契约；实际 XML 的定义与列表一致性另行静态核验。</summary>
    private static RecipeDef_PSTag PetRecipe(SexSlaveSpecializationType type) => new RecipeDef_PSTag
    {
        defName = "SSC_PSEdit_" + type + "_Final", hediffToAdd = PetFinal(type), severity = 1f,
        requireSpecializationType = type, requireSpecializationComplete = true,
        exclusiveTags = Pets.Select(PetFinal).ToList()
    };

    /// <summary>创建一份普通培养完成且带独立人格快照的原料，用于正常加工与失效变体。</summary>
    private static CompPersonalityStore CompletedPetGel(SexSlaveSpecializationType type)
    {
        var gel = Gel(new Trait(Def("PetSavedTrait"), 2));
        gel.parent.def = SSCDefOf.SSC_PersonalitySlime;
        gel.parent.Comps[typeof(CompPersonalityStore)] = gel;
        gel.specializationType = type;
        gel.specializationProgress = 1f;
        gel.specializationProgressByType = new Dictionary<string, float> { [type.ToString()] = 1f, ["Cow"] = 0.22f };
        gel.SetTag(PetBase(type), 1f);
        return gel;
    }

    /// <summary>直接运行真实筛料 Postfix，同时允许验证它是否保留原账单的拒绝结果。</summary>
    private static bool PetIngredientAllowed(RecipeDef_PSTag recipe, CompPersonalityStore gel, bool original = true)
    {
        bool result = original;
        Patch_IsFixedOrAllowedIngredient.Postfix(new Bill { recipe = recipe }, gel?.parent, ref result);
        return result;
    }

    /// <summary>逐项破坏原料语义资格，复用于筛料、制作中变化及工作者直接调用。</summary>
    private static IEnumerable<Action<CompPersonalityStore>> PetInvalidations(SexSlaveSpecializationType type)
    {
        yield return g => g.specializationProgress = 0.9989f;
        yield return g => g.specializationProgress = float.NaN;
        yield return g => g.specializationProgress = float.PositiveInfinity;
        yield return g => g.specializationProgress = float.NegativeInfinity;
        yield return g => g.specializationProgress = 1.01f;
        yield return g => g.specializationType = SexSlaveSpecializationType.None;
        yield return g => g.specializationType = SexSlaveSpecializationType.Cow;
        yield return g => g.RemoveTag(PetBase(type));
        yield return g => g.hediffTags = null;
        foreach (var other in Pets)
        {
            var captured = other;
            yield return g => g.SetTag(PetFinal(captured), 1f);
        }
    }

    /// <summary>确认完成容差正常放行，异常进度、方向或标签在两个资格入口得到一致拒绝。</summary>
    private static void PetRecipeEligibility()
    {
        foreach (var type in Pets)
        {
            var recipe = PetRecipe(type);
            foreach (float progress in new[] { 0.999f, 1f })
            {
                var gel = CompletedPetGel(type); gel.specializationProgress = progress;
                Assert(PetFinalizationRecipeUtility.IsEligibleGel(recipe, gel) && PetIngredientAllowed(recipe, gel), "合法完成阈值被拒绝");
            }
            foreach (var invalidate in PetInvalidations(type))
            {
                var gel = CompletedPetGel(type); invalidate(gel);
                Assert(!PetFinalizationRecipeUtility.IsEligibleGel(recipe, gel) && !PetIngredientAllowed(recipe, gel), "无效资格在筛料或共用判断漏过");
            }
            Assert(!PetFinalizationRecipeUtility.IsEligibleGel(recipe, null) && !PetIngredientAllowed(recipe, null), "空凝胶漏过");
        }
    }

    /// <summary>验证原账单拒绝、物品存活及堆叠限制，区分筛料与消耗后的语义资格。</summary>
    private static void PetRecipeBillFilter()
    {
        foreach (var type in Pets)
        {
            var gel = CompletedPetGel(type); var recipe = PetRecipe(type);
            Assert(!PetIngredientAllowed(recipe, gel, false), "补丁覆盖了原版账单的拒绝结果");
            gel.parent.stackCount = 2;
            Assert(!PetIngredientAllowed(recipe, gel), "堆叠凝胶进入筛料");
            gel.parent.stackCount = 1; gel.parent.Destroy(DestroyMode.Vanish);
            Assert(!PetIngredientAllowed(recipe, gel), "已销毁凝胶进入筛料");
            Assert(PetFinalizationRecipeUtility.IsEligibleGel(recipe, gel), "语义资格误拒正常消耗后的工作者来源");
        }
    }

    /// <summary>遍历配方与当前方向矩阵，确认历史记录不能借用且任意宠物终极均阻断。</summary>
    private static void PetRecipeAllDirections()
    {
        foreach (var target in Pets)
            foreach (var current in Pets)
            {
                var gel = CompletedPetGel(current); var recipe = PetRecipe(target);
                gel.specializationProgressByType[target.ToString()] = 1f;
                gel.SetTag(PetBase(target), 1f);
                Assert(PetIngredientAllowed(recipe, gel) == (target == current), "借用历史进度或其他方向普通标签加工");
                foreach (var final in Pets)
                {
                    gel.SetTag(PetFinal(final), 1f);
                    Assert(!PetIngredientAllowed(recipe, gel), "已有宠物终极仍可加工");
                    gel.RemoveTag(PetFinal(final));
                }
            }
    }

    /// <summary>列出生产工具支持的八种原始及已编辑凝胶，用于实际映射回归。</summary>
    private static ThingDef[] PetGelBases() => new[]
    {
        SSCDefOf.SSC_PersonalitySlime, SSCDefOf.SSC_PS_P, SSCDefOf.SSC_PS_U, SSCDefOf.SSC_PS_P_U,
        SSCDefOf.SSC_PersonalitySlime_Edited, SSCDefOf.SSC_PS_P_Edited, SSCDefOf.SSC_PS_U_Edited, SSCDefOf.SSC_PS_P_U_Edited
    };

    /// <summary>经最终 Toil、原版时序替身及真实工作者完成加工，检查消耗、产物和一次计数。</summary>
    private static CompPersonalityStore CompletePetRecipe(RecipeDef_PSTag recipe, CompPersonalityStore source)
    {
        var crafter = RecipeCrafter(recipe, source.parent); var bill = crafter.CurJob.bill;
        GuardedRecipeCompletion(crafter).initAction();
        var output = GenPlace.LastPlaced?.TryGetComp<CompPersonalityStore>();
        Assert(source.parent.Destroyed && output != null && bill.completedIterations == 1 && bill.repeatCount == 0
            && Toils_Recipe.OriginalCompletionCalls == 1, "合法加工未生成人格或计数不正确");
        return output;
    }

    /// <summary>确认产物仅有目标宠物终极且严重度为一，不残留任意宠物普通标签。</summary>
    private static void AssertOnlyPetFinal(CompPersonalityStore gel, SexSlaveSpecializationType type)
    {
        foreach (var other in Pets)
            Assert(!gel.HasTag(PetBase(other)) && gel.HasTag(PetFinal(other)) == (other == type), "产物仍带其他宠物普通或终极标签");
        Assert(gel.GetTagSeverity(PetFinal(type)) == 1f, "终极标签严重度错误");
    }

    /// <summary>遍历三方向乘八基底，验证等级映射、标签互斥及源快照保留。</summary>
    private static void PetRecipeAllBases()
    {
        var bases = PetGelBases();
        var outputs = new[] { SSCDefOf.SSC_PersonalitySlime_Edited, SSCDefOf.SSC_PS_P_Edited, SSCDefOf.SSC_PS_U_Edited, SSCDefOf.SSC_PS_P_U_Edited };
        foreach (var type in Pets)
            for (int i = 0; i < bases.Length; i++)
            {
                var source = CompletedPetGel(type); source.parent.def = bases[i];
                // 陈旧的其他普通标签须从产物清掉，而不修改源快照或历史。
                foreach (var pet in Pets) source.SetTag(PetBase(pet), 0.5f);
                source.SetTag(SSCDefOf.SSC_Hediff_TrainerOfficer_Final, 1f);
                Assert(PetIngredientAllowed(PetRecipe(type), source), "合法凝胶基底被拒绝");
                var output = CompletePetRecipe(PetRecipe(type), source);
                Assert(output.parent.def == outputs[i % 4], "改变了凝胶等级映射");
                AssertOnlyPetFinal(output, type);
                Assert(source.HasTag(PetBase(type)) && !source.HasTag(PetFinal(type))
                    && output.HasTag(SSCDefOf.SSC_Hediff_TrainerOfficer_Final), "修改源标签或丢失组外终极");
            }
    }

    /// <summary>检查加工保留人格与历史，并通过修改产物验证可变数据不共享。</summary>
    private static void PetRecipeCopyIsolation()
    {
        foreach (var type in Pets)
        {
            var source = CompletedPetGel(type);
            source.firstName = "First"; source.nickName = "Pet"; source.lastName = "Last";
            source.corruptionLevel = 0.4f; source.highestCorruptionLevel = 0.8f;
            source.specializationProgressByType["Bus"] = 0.6f;
            var memory = new StoredMemoryData { age = 314, stageIndex = 2, memoryDataVersion = 1,
                hasOpinionOffset = true, opinionOffset = -7.5f, moodOffset = 8, durationTicksOverride = 1234, permanent = true };
            source.storedMemories.Add(memory);
            source.SetTag(SSCDefOf.SSC_Hediff_Combatant_Final, 0.8f);
            var output = CompletePetRecipe(PetRecipe(type), source);
            Assert(output.firstName == "First" && output.nickName == "Pet" && output.lastName == "Last"
                && output.corruptionLevel == 0.4f && output.highestCorruptionLevel == 0.8f
                && output.specializationType == type && output.specializationProgress == 1f
                && output.specializationProgressByType.Count == 3 && output.specializationProgressByType["Bus"] == 0.6f
                && output.GetTagSeverity(SSCDefOf.SSC_Hediff_Combatant_Final) == 0.8f, "加工丢失人格字段或历史");
            Assert(output.storedTraits[0].def == source.storedTraits[0].def && output.storedTraits[0].Degree == 2
                && !ReferenceEquals(output.storedTraits[0], source.storedTraits[0])
                && !ReferenceEquals(output.storedMemories[0], memory)
                && output.storedMemories[0].age == 314 && output.storedMemories[0].stageIndex == 2
                && output.storedMemories[0].opinionOffset == -7.5f && output.storedMemories[0].durationTicksOverride == 1234,
                "加工丢失或共享特质、记忆实例");
            output.specializationProgressByType["Bus"] = 0.9f;
            output.storedMemories[0].age = 9; output.storedTraits.Clear();
            output.RemoveTag(SSCDefOf.SSC_Hediff_Combatant_Final);
            Assert(source.specializationProgressByType["Bus"] == 0.6f && memory.age == 314 && source.storedTraits.Count == 1
                && source.HasTag(SSCDefOf.SSC_Hediff_Combatant_Final), "产物修改反向污染源人格");
        }
    }

    /// <summary>在筛料通过且最终步骤已创建后破坏资格，验证实际结算前保料及不扣次数。</summary>
    private static void PetRecipeChangedDuringWork()
    {
        foreach (var type in Pets)
            foreach (var invalidate in PetInvalidations(type))
            {
                var source = CompletedPetGel(type); var recipe = PetRecipe(type);
                Assert(PetIngredientAllowed(recipe, source), "开始加工时材料不合法");
                var crafter = RecipeCrafter(recipe, source.parent); var job = crafter.CurJob;
                var toil = GuardedRecipeCompletion(crafter);
                invalidate(source); toil.initAction(); AssertRejectedRecipe(crafter, job);
                Assert(!source.parent.Destroyed && source.parent.stackCount == 1 && job.placedThings[0].Count == 1, "资格失效时吞料或改变数量");
            }
    }

    /// <summary>检查拒绝发生在整个原版完成动作之前，来源物品及记录数量保持不变。</summary>
    private static void AssertPetRejectedBeforeConsume(RecipeDef_PSTag recipe, CompPersonalityStore source)
    {
        var crafter = RecipeCrafter(recipe, source.parent); var job = crafter.CurJob;
        GuardedRecipeCompletion(crafter).initAction(); AssertRejectedRecipe(crafter, job);
        Assert(!source.parent.Destroyed && job.placedThings[0].Count == 1, "异常定义导致吞料");
    }

    /// <summary>验证官方配方字段损坏仍受保护，同目标的合法外部配方也遵守组内资格。</summary>
    private static void PetRecipeMetadata()
    {
        foreach (var type in Pets)
            foreach (int scenario in Enumerable.Range(0, 7))
            {
                var source = CompletedPetGel(type); var recipe = PetRecipe(type);
                if (scenario == 0) recipe.hediffToAdd = null;
                if (scenario == 1) recipe.hediffToAdd = PetFinal(Pets.First(p => p != type));
                if (scenario == 2) recipe.requireSpecializationType = SexSlaveSpecializationType.None;
                if (scenario == 3) recipe.requireSpecializationComplete = false;
                if (scenario == 4) recipe.severity = float.NaN;
                if (scenario == 5) recipe.severity = 0.5f;
                if (scenario == 6) recipe.hediffToAdd = new HediffDef { defName = PetFinal(type).defName };
                recipe.filterIfTagExists = false;
                Assert(PetFinalizationRecipeUtility.IsFinalizationRecipe(recipe) && !PetIngredientAllowed(recipe, source), "已知宠物配方因缺定义或禁用过滤回退放行");
                AssertPetRejectedBeforeConsume(recipe, source);
            }
        // 第三方配方采用真实宠物终极 Def 时同样保护，不依赖官方配方名称。
        var external = PetRecipe(SexSlaveSpecializationType.PetDog); external.defName = "ExternalDogFinal";
        var valid = CompletedPetGel(SexSlaveSpecializationType.PetDog);
        Assert(PetIngredientAllowed(external, valid), "合法第三方同目标配方无法加工");
        valid.SetTag(PetFinal(SexSlaveSpecializationType.PetCat), 1f);
        AssertPetRejectedBeforeConsume(external, valid);
    }

    /// <summary>逐个移除组内定义验证保守拒绝，并在每次检查后恢复共享数据库。</summary>
    private static void PetRecipeMissingDefinitions()
    {
        foreach (var type in Pets)
            foreach (var missing in Pets.SelectMany(p => new[] { PetBase(p), PetFinal(p) }).ToArray())
            {
                var source = CompletedPetGel(type); var recipe = PetRecipe(type);
                DefDatabase<HediffDef>.AllDefs.Remove(missing);
                try
                {
                    Assert(!PetIngredientAllowed(recipe, source), "组内定义缺失仍可筛料");
                    AssertPetRejectedBeforeConsume(recipe, source);
                }
                finally { DefDatabase<HediffDef>.AllDefs.Add(missing); }
            }
    }

    /// <summary>覆盖原料集合、数量、堆叠和基底异常，并确认零数量记录不会被消费。</summary>
    private static void PetRecipeInvalidSources()
    {
        foreach (var type in Pets)
            for (int scenario = 0; scenario < 12; scenario++)
            {
                var source = CompletedPetGel(type); var extra = CompletedPetGel(type);
                var crafter = RecipeCrafter(PetRecipe(type), source.parent); var job = crafter.CurJob;
                if (scenario == 0) job.placedThings = null;
                if (scenario == 1) job.placedThings.Clear();
                if (scenario == 2) job.placedThings[0].Count = 0;
                if (scenario == 3) job.placedThings[0].Count = 2;
                if (scenario == 4) job.placedThings.Add(new ThingCountClass(extra.parent, 1));
                if (scenario == 5) source.parent.Destroy(DestroyMode.Vanish);
                if (scenario == 6) source.parent.stackCount = 2;
                if (scenario == 7) source.parent.def = new ThingDef { defName = "UnknownGel" };
                if (scenario == 8) source.parent.def = null;
                if (scenario == 9) job.placedThings[0].Count = -1;
                if (scenario == 10) job.placedThings = new List<ThingCountClass> { null, new ThingCountClass(null, 1) };
                if (scenario == 11) job.placedThings = new List<ThingCountClass> { new ThingCountClass(new Thing(), 1) };
                GuardedRecipeCompletion(crafter).initAction(); AssertRejectedRecipe(crafter, job);
                Assert(!extra.parent.Destroyed && (scenario == 5 || !source.parent.Destroyed), "异常集合吞料");
                if (scenario == 6) Assert(source.parent.stackCount == 2 && job.placedThings[0].Count == 1, "拒绝前进行了分堆");
            }
        // 原版仅消费正数量记录；一份合法来源可以忽略零数量及非人格材料。
        var gel = CompletedPetGel(SexSlaveSpecializationType.PetDog);
        var crafter2 = RecipeCrafter(PetRecipe(SexSlaveSpecializationType.PetDog), gel.parent);
        var ignored = CompletedPetGel(SexSlaveSpecializationType.PetDog);
        crafter2.CurJob.placedThings.Add(new ThingCountClass(ignored.parent, 0));
        GuardedRecipeCompletion(crafter2).initAction();
        Assert(gel.parent.Destroyed && !ignored.parent.Destroyed && GenPlace.LastPlaced != null, "零数量记录影响正常结算");
    }

    /// <summary>加工开始后移除产物定义或人格组件声明，确认消费前守卫重新读取定义。</summary>
    private static void PetRecipeProductDefinitions()
    {
        foreach (var type in Pets)
            foreach (int scenario in Enumerable.Range(0, 3))
            {
                var source = CompletedPetGel(type); var recipe = PetRecipe(type);
                var product = SSCDefOf.SSC_PersonalitySlime_Edited; var props = product.comps;
                var crafter = RecipeCrafter(recipe, source.parent); var job = crafter.CurJob;
                var toil = GuardedRecipeCompletion(crafter);
                Assert(PetIngredientAllowed(recipe, source), "初始定义不合法");
                if (scenario == 0) SSCDefOf.SSC_PersonalitySlime_Edited = null;
                if (scenario == 1) product.comps = null;
                if (scenario == 2) product.comps = new List<CompProperties> { null, new CompProperties { compClass = typeof(ThingComp) } };
                try
                {
                    Assert(!PetIngredientAllowed(recipe, source), "无人格产物仍可筛料");
                    toil.initAction(); AssertRejectedRecipe(crafter, job);
                    Assert(!source.parent.Destroyed && job.placedThings[0].Count == 1, "制作中产物定义失效仍吞料");
                }
                finally { SSCDefOf.SSC_PersonalitySlime_Edited = product; product.comps = props; }
            }
    }

    /// <summary>验证半成品优先级、异常容器保护及合法内装来源的正常消费。</summary>
    private static void PetRecipeUnfinished()
    {
        foreach (var type in Pets)
            for (int scenario = 0; scenario < 6; scenario++)
            {
                var source = CompletedPetGel(type); var outside = CompletedPetGel(type); var extra = CompletedPetGel(type);
                var unfinished = new UnfinishedThing { ingredients = new List<Thing> { source.parent } };
                var crafter = RecipeCrafter(PetRecipe(type), outside.parent); var job = crafter.CurJob;
                job.targetC = new LocalTargetInfo(unfinished);
                if (scenario == 0) source.specializationProgress = 0.5f;
                if (scenario == 1) unfinished.ingredients = null;
                if (scenario == 2) unfinished.ingredients.Clear();
                if (scenario == 3) unfinished.ingredients.Add(extra.parent);
                if (scenario == 4) source.parent.stackCount = 2;
                if (scenario == 5) unfinished.Destroy(DestroyMode.Vanish);
                GuardedRecipeCompletion(crafter).initAction(); AssertRejectedRecipe(crafter, job);
                Assert((scenario == 5 || !unfinished.Destroyed) && !source.parent.Destroyed && !outside.parent.Destroyed && !extra.parent.Destroyed,
                    "无效半成品或其内外凝胶被消耗");
            }
        foreach (var type in Pets)
        {
            var source = CompletedPetGel(type); var outside = CompletedPetGel(type);
            var unfinished = new UnfinishedThing { ingredients = new List<Thing> { source.parent } };
            var crafter = RecipeCrafter(PetRecipe(type), outside.parent); var bill = crafter.CurJob.bill;
            crafter.CurJob.targetC = new LocalTargetInfo(unfinished); GuardedRecipeCompletion(crafter).initAction();
            Assert(unfinished.Destroyed && source.parent.Destroyed && !outside.parent.Destroyed && bill.completedIterations == 1, "合法半成品消费了错误来源");
            AssertOnlyPetFinal(GenPlace.LastPlaced.TryGetComp<CompPersonalityStore>(), type);
        }
    }

    /// <summary>验证工作者独立拒绝非法来源，并允许原版已消耗、数量清零的合法组件。</summary>
    private static void PetRecipeWorker()
    {
        foreach (var type in Pets)
        {
            foreach (var invalidate in PetInvalidations(type))
            {
                var source = CompletedPetGel(type); invalidate(source); GenPlace.LastPlaced = null;
                new Recipe_PSTagWorker { recipe = PetRecipe(type) }.Notify_IterationCompleted(Body("Direct"), new List<Thing> { source.parent });
                Assert(GenPlace.LastPlaced == null && !source.parent.Destroyed, "直接工作者绕过资格或消耗无效来源");
            }
            var valid = CompletedPetGel(type); valid.parent.Destroy(DestroyMode.Vanish); GenPlace.LastPlaced = null;
            Assert(valid.parent.stackCount == 0, "测试边界必须遵守原版销毁后数量清零的契约");
            new Recipe_PSTagWorker { recipe = PetRecipe(type) }.Notify_IterationCompleted(Body("Consumed"), new List<Thing> { valid.parent });
            Assert(GenPlace.LastPlaced != null, "正常已消耗来源无法生成产物");
            AssertOnlyPetFinal(GenPlace.LastPlaced.TryGetComp<CompPersonalityStore>(), type);
            foreach (int scenario in Enumerable.Range(0, 4))
            {
                var source = CompletedPetGel(type); var extra = CompletedPetGel(type);
                var ingredients = new List<Thing> { source.parent };
                if (scenario == 0) source.parent.stackCount = 2;
                if (scenario == 1) ingredients.Add(extra.parent);
                if (scenario == 2) source.parent.def = new ThingDef();
                if (scenario == 3) ingredients = null;
                GenPlace.LastPlaced = null;
                new Recipe_PSTagWorker { recipe = PetRecipe(type) }.Notify_IterationCompleted(Body("Direct"), ingredients);
                Assert(GenPlace.LastPlaced == null && !source.parent.Destroyed && !extra.parent.Destroyed, "直接工作者绕过来源保护");
            }
        }
    }

    /// <summary>经真实提取、加工与植入替换宿主宠物状态及历史，并重复对账确认不会再生旧状态。</summary>
    private static void PetRecipePipeline()
    {
        foreach (var type in Pets)
        {
            var source = Body("PetSource", new Trait(Def("PetTrait"), 1)); var comp = Training(source);
            comp.pawnIdentity = PawnIdentity.Slave;
            comp.SetSpecialization(SexSlaveSpecializationType.Cow); comp.specializationProgress = 0.22f;
            comp.SetSpecialization(type); comp.specializationProgress = 1f;
            CompSexSlaveTraining.ReconcileSpecialization(source);
            var gel = Excrete(source);
            Assert(gel.HasTag(PetBase(type)) && PetIngredientAllowed(PetRecipe(type), gel), "真实抽取遗漏普通状态或进度");
            var output = CompletePetRecipe(PetRecipe(type), gel);
            var receiver = Body("Host"); var host = Training(receiver); host.pawnIdentity = PawnIdentity.Slave;
            var other = Pets.First(p => p != type);
            host.SetSpecialization(other); host.specializationProgress = 0.98f;
            receiver.health.AddHediff(PetFinal(other));
            receiver.health.AddHediff(PetBase(other));
            Assert(ExcretionUtility.InheritEverything(receiver, output), "终极凝胶植入失败");
            for (int i = 0; i < 3; i++) CompSexSlaveTraining.ReconcileSpecialization(receiver);
            foreach (var pet in Pets)
                Assert(!receiver.health.hediffSet.HasHediff(PetBase(pet)) && receiver.health.hediffSet.HasHediff(PetFinal(pet)) == (pet == type),
                    "宿主旧状态残留或对账再生普通状态");
            Assert(host.specializationType == type && host.specializationProgress == 1f && host.ExportSpecializationProgress().Count == 2
                && host.ExportSpecializationProgress()["Cow"] == 0.22f && !host.ExportSpecializationProgress().ContainsKey(other.ToString())
                && output.parent.Destroyed, "植入混入宿主历史或未消耗凝胶");
            AssertOnlyOrdinary(receiver, source.story.traits.allTraits.First(t => t.def.defName == "PetTrait").def, 1);
        }
    }

    /// <summary>重建最终步骤后验证资格按当前数据检查；此用例不模拟真实游戏存档往返。</summary>
    private static void PetRecipeResumedCompletion()
    {
        foreach (var type in Pets)
            foreach (bool changed in new[] { false, true })
            {
                var source = CompletedPetGel(type); var recipe = PetRecipe(type);
                Assert(PetIngredientAllowed(recipe, source), "保存前材料不合法");
                // 不模拟真实 Scribe；重建工厂动作并重新绑定当前任务，验证恢复时的判断时机。
                var crafter = RecipeCrafter(recipe, source.parent); var job = crafter.CurJob;
                var resumed = GuardedRecipeCompletion(crafter);
                if (changed) source.SetTag(PetFinal(Pets.First(p => p != type)), 1f);
                resumed.initAction();
                if (changed)
                {
                    AssertRejectedRecipe(crafter, job);
                    Assert(!source.parent.Destroyed, "恢复任务使用缓存资格吞料");
                }
                else Assert(source.parent.Destroyed && job.bill.completedIterations == 1 && GenPlace.LastPlaced != null, "合法恢复任务未正常结算");
            }
    }
}
