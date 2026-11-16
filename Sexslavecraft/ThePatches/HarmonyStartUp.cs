using System;
using System.Reflection;
using Verse;
using HarmonyLib;
using RimWorld;

// SSC 的 Harmony 启动入口，依次安装注解补丁、主动注册的兼容组及仪式与凝胶手动钩子。
// 1.7 的延迟适配仍由原生长事件队列调度，诊断状态随实际完成时机更新。
namespace SexSlaveCraft
{
    [StaticConstructorOnStartup]
    public static class ModStartup
    {
        /// <summary>安装现有业务补丁，并在 1.7 启动回调结束后另行接入独立只读计时采样。</summary>
        static ModStartup()
        {
            var harmony = new Harmony("SexSlaveCraft.training.patch"); 
            SSCLog.Important($"[SSC Harmony] 开始加载Harmony补丁...");
#if SSC_RW17
            bool complete = SSCPatchRegistration.InstallAnnotated(harmony, typeof(ModStartup).Assembly);
            SSCPatchRegistration.Install(SSCPatchRegistration.WhipCooperation, () =>
            {
                Harmony_SSCPnaWhipCooperativeThreat.Register(harmony);
                return Harmony_SSCPnaWhipCooperativeThreat.Ready ? SSCPatchState.Ready : SSCPatchState.Fallback;
            }, target: "RimWorld.Verb_MeleeAttack.TryCastShot");
#else
            harmony.PatchAll();
            // 鞭束两处钩子作为一组安装。可选模组各自解析和注册，某组失配时仍能继续执行后面的安装入口。
            // 沿用当前 Harmony owner，使依赖该 owner 的补丁顺序约束继续有效。
            Harmony_SSCPnaWhipCooperativeThreat.Register(harmony);
#endif
            HumanCattleCompatibility.Register(harmony);
            EqualMilkingCompatibility.Register(harmony);
#if SSC_RW17
            SSCFertilityGenerationPatches.RegisterMenstruation(harmony);
            // 等本轮静态构造／原有补丁注册完成后安装只读计时采样；不改变冻结版入口。
            SSCPatchRegistration.Schedule("Orgasm progress", SSCOrgasmProgressPatches.Install,
                () => SSCOrgasmProgressPatches.RecoveryReady ? SSCPatchState.Ready : SSCPatchState.Fallback);
            // 可选 UAP 接入使用独立 owner，完整核对后才开放手动豁免；不随旧版 PatchAll 注册。
            SSCPatchRegistration.Schedule("UAP group", SSCUapGroupPatches.Install,
                () => SSCUapGroupApi.Ready ? SSCPatchState.Ready :
                    SSCUapGroupApi.Dialog == null ? SSCPatchState.Skipped : SSCPatchState.Fallback,
                SSCPatchSkipReason.MissingDependency);
            // 完整核对 RJW 评价与自愿入口后安装关系保底，保留原基础资格和主人侧意愿。
            SSCPatchRegistration.Schedule("Master attraction", SSCMasterAttractionPatches.Install,
                () => SSCMasterAttractionPatches.Ready ? SSCPatchState.Ready : SSCPatchState.Fallback);
            // 保留正在SSC调教中的受方倒地接收任务，仍执行原健康通知和其他中断条件。
            SSCPatchRegistration.Schedule("Downed training receiver", SSCTrainingDownedPatches.Install,
                () => SSCTrainingDownedPatches.Ready ? SSCPatchState.Ready : SSCPatchState.Fallback);
            if (!complete) Log.Warning("[SSC Harmony] Registration completed with failures. See Diagnostics and Compatibility.");
            else
#endif
            SSCLog.Important("[SexSlaveCraft.training.patch] Harmony patches loaded successfully.");

            // EN: Binding Ritual fixes are registered by hand because this bundle needs precise control over which ritual UI / quality getters are changed.
            // CN: “绑定仪式”修正采用手动注册，因为这组补丁需要精确控制要改哪些仪式 UI / 质量 getter。
            var patchClass = typeof(RitualFixPatches);
            int successCount = 0;

            // 1. RepeatPenaltyActive (属性 getter)
            // EN: Remove the vanilla repeat-penalty assumption from the Binding Ritual flow.
            // CN: 把原版“重复举办惩罚”这套假设从绑定仪式流程里剥掉。
            try
            {
                var original = AccessTools.PropertyGetter(typeof(Precept_Ritual), "RepeatPenaltyActive");
                harmony.Patch(original, postfix: new HarmonyMethod(patchClass, nameof(RitualFixPatches.RepeatPenaltyActive_Postfix)));
                SSCLog.Important($"[SSC RitualFix] Patch 注册成功: RepeatPenaltyActive");
                successCount++;
            }
            catch (Exception ex) { Log.Error($"[SSC RitualFix] RepeatPenaltyActive 注册失败: {ex}"); }

            // 2. RepeatPenaltyProgress (属性 getter)
            try
            {
                var original = AccessTools.PropertyGetter(typeof(Precept_Ritual), "RepeatPenaltyProgress");
                harmony.Patch(original, postfix: new HarmonyMethod(patchClass, nameof(RitualFixPatches.RepeatPenaltyProgress_Postfix)));
                SSCLog.Important($"[SSC RitualFix] Patch 注册成功: RepeatPenaltyProgress");
                successCount++;
            }
            catch (Exception ex) { Log.Error($"[SSC RitualFix] RepeatPenaltyProgress 注册失败: {ex}"); }

            // 3. RepeatQualityPenalty (属性 getter)
            try
            {
                var original = AccessTools.PropertyGetter(typeof(Precept_Ritual), "RepeatQualityPenalty");
                harmony.Patch(original, postfix: new HarmonyMethod(patchClass, nameof(RitualFixPatches.RepeatQualityPenalty_Postfix)));
                SSCLog.Important($"[SSC RitualFix] Patch 注册成功: RepeatQualityPenalty");
                successCount++;
            }
            catch (Exception ex) { Log.Error($"[SSC RitualFix] RepeatQualityPenalty 注册失败: {ex}"); }

            // 4. RepeatPenaltyTimeLeft (属性 getter)
            try
            {
                var original = AccessTools.PropertyGetter(typeof(Precept_Ritual), "RepeatPenaltyTimeLeft");
                harmony.Patch(original, postfix: new HarmonyMethod(patchClass, nameof(RitualFixPatches.RepeatPenaltyTimeLeft_Postfix)));
                SSCLog.Important($"[SSC RitualFix] Patch 注册成功: RepeatPenaltyTimeLeft");
                successCount++;
            }
            catch (Exception ex) { Log.Error($"[SSC RitualFix] RepeatPenaltyTimeLeft 注册失败: {ex}"); }

            // 5. Precept_Ritual.TipMainPart (修正重复惩罚 tooltip 的天数显示)
            try
            {
                var original = AccessTools.Method(typeof(Precept_Ritual), "TipMainPart");
                harmony.Patch(original, postfix: new HarmonyMethod(patchClass, nameof(RitualFixPatches.TipMainPart_Postfix)));
                SSCLog.Important($"[SSC RitualFix] Patch 注册成功: TipMainPart");
                successCount++;
            }
            catch (Exception ex) { Log.Error($"[SSC RitualFix] TipMainPart 注册失败: {ex}"); }

            // 6. Command_Ritual.DrawIcon (修正仪式按钮倒计时覆盖)
            try
            {
                var original = AccessTools.Method(typeof(Command_Ritual), "DrawIcon");
                harmony.Patch(original, prefix: new HarmonyMethod(patchClass, nameof(RitualFixPatches.CommandRitualDrawIcon_Prefix)));
                SSCLog.Important($"[SSC RitualFix] Patch 注册成功: Command_Ritual.DrawIcon");
                successCount++;
            }
            catch (Exception ex) { Log.Error($"[SSC RitualFix] Command_Ritual.DrawIcon 注册失败: {ex}"); }

            // 7. Dialog_BeginRitual.PopulateQualityFactors (移除期望加成 UI 项)
            // EN: The Binding Ritual has its own SSC quality model, so the vanilla expectation bonus row should disappear from the UI.
            // CN: 绑定仪式使用 SSC 自己的质量模型，所以 UI 里原版“期望加成”那一栏应该被移除。
            try
            {
                var original = AccessTools.Method(typeof(Dialog_BeginRitual), "PopulateQualityFactors");
                harmony.Patch(original, postfix: new HarmonyMethod(patchClass, nameof(RitualFixPatches.PopulateQualityFactors_Postfix)));
                SSCLog.Important($"[SSC RitualFix] Patch 注册成功: PopulateQualityFactors");
                successCount++;
            }
            catch (Exception ex) { Log.Error($"[SSC RitualFix] PopulateQualityFactors 注册失败: {ex}"); }

            // 8. Dialog_BeginRitual.PredictedQuality (从质量预测中扣除期望加成)
            try
            {
                var original = AccessTools.Method(typeof(Dialog_BeginRitual), "PredictedQuality");
                harmony.Patch(original, postfix: new HarmonyMethod(patchClass, nameof(RitualFixPatches.PredictedQuality_Postfix)));
                SSCLog.Important($"[SSC RitualFix] Patch 注册成功: PredictedQuality");
                successCount++;
            }
            catch (Exception ex) { Log.Error($"[SSC RitualFix] PredictedQuality 注册失败: {ex}"); }

            // 9. Dialog_BeginRitual.ExpectedDurationLabel (自定义持续时间显示)
            try
            {
                var original = AccessTools.Method(typeof(Dialog_BeginRitual), "ExpectedDurationLabel");
                harmony.Patch(original, postfix: new HarmonyMethod(patchClass, nameof(RitualFixPatches.ExpectedDurationLabel_Postfix)));
                SSCLog.Important($"[SSC RitualFix] Patch 注册成功: ExpectedDurationLabel");
                successCount++;
            }
            catch (Exception ex) { Log.Error($"[SSC RitualFix] ExpectedDurationLabel 注册失败: {ex}"); }

            try
            {
                Type bodyNodeType = AccessTools.TypeByName("Verse.PawnRenderNode_Body");
                var original = AccessTools.Method(bodyNodeType, "GraphicFor");
                if (original != null)
                {
                    // EN: This hook handles the body; the head is filtered separately at the final-material stage below.
                    // CN: 此入口只处理身体；头部在下方单独按最终材质和皮肤层白名单处理。
                    harmony.Patch(original, postfix: new HarmonyMethod(typeof(Harmony_FullGelatinizationBodyTint), nameof(Harmony_FullGelatinizationBodyTint.Postfix)));
                    SSCLog.Important("[SSC FullGel] Patch 注册成功: PawnRenderNode_Body.GraphicFor");
                }
                RecordManualPatch("FullGel body tint", "Verse.PawnRenderNode_Body.GraphicFor", original != null);
            }
            catch (Exception ex)
            {
                RecordManualPatch("FullGel body tint", "Verse.PawnRenderNode_Body.GraphicFor", false, ex);
                Log.Error($"[SSC FullGel] PawnRenderNode_Body.GraphicFor 注册失败: {ex}");
            }

            // 头部在最终材质阶段只替换皮肤，保留 FA 本帧五官和原队列；FA 未安装时支持原版头部。
            try
            {
                Harmony_FullGelatinizationHeadTint.Register(harmony);
                RecordManualPatch("FullGel head tint", "Verse.PawnRenderNodeWorker.GetFinalizedMaterial", true);
            }
            catch (Exception ex)
            {
                RecordManualPatch("FullGel head tint", "Verse.PawnRenderNodeWorker.GetFinalizedMaterial", false, ex);
                Log.Error($"[SSC FullGel] Head skin material hook 注册失败: {ex}");
            }

            // SA 的并行 GetGraphic 只取得原图；染色在最终材质阶段完成，避免工作线程访问 GraphicDatabase。
            try
            {
                Harmony_FullGelatinizationBodyTint.RegisterSizedApparel(harmony);
#if SSC_RW17
                bool workerPresent = AccessTools.TypeByName("SizedApparel.SizedApparelRenderNodeWorker") != null;
                RecordManualPatch("FullGel SizedApparel material", "Verse.PawnRenderNodeWorker.GetFinalizedMaterial",
                    workerPresent, missingDependency: !workerPresent);
#endif
            }
            catch (Exception ex)
            {
                RecordManualPatch("FullGel SizedApparel material", "Verse.PawnRenderNodeWorker.GetFinalizedMaterial", false, ex);
                Log.Error($"[SSC FullGel] SizedApparel final material hook 注册失败: {ex}");
            }

            try
            {
                Type sizedApparelType = AccessTools.TypeByName("SizedApparel.SizedApparelMain");
                if (sizedApparelType != null)
                {
                    var original = AccessTools.Method(typeof(ApparelGraphicRecordGetter), "TryGetGraphicApparel");
                    if (original != null)
                    {
                        // EN: Sized Apparel also swaps apparel graphics to chest-sized skin-like variants, so full-gel tint must run after that swap.
                        // CN: Sized Apparel 还会把服装图形替换成按胸型适配的类皮肤结果，因此全凝胶化必须在替换完成后再套 shader。
                        harmony.Patch(original, postfix: new HarmonyMethod(typeof(Harmony_FullGelatinizationBodyTint), nameof(Harmony_FullGelatinizationBodyTint.SizedApparelApparelPostfix)));
                        SSCLog.Important("[SSC FullGel] Patch 注册成功: ApparelGraphicRecordGetter.TryGetGraphicApparel (SizedApparel soft depend)");
                    }
                    RecordManualPatch("FullGel SizedApparel clothing", "Verse.ApparelGraphicRecordGetter.TryGetGraphicApparel", original != null);
                }
                else
                {
                    RecordManualPatch("FullGel SizedApparel clothing", "Verse.ApparelGraphicRecordGetter.TryGetGraphicApparel", false, missingDependency: true);
                    SSCLog.Important("[SSC FullGel] 未检测到 SizedApparel，跳过服装胸型图形软依赖补丁。");
                }
            }
            catch (Exception ex)
            {
                RecordManualPatch("FullGel SizedApparel clothing", "Verse.ApparelGraphicRecordGetter.TryGetGraphicApparel", false, ex);
                Log.Error($"[SSC FullGel] ApparelGraphicRecordGetter.TryGetGraphicApparel 注册失败: {ex}");
            }

            SSCLog.Important($"[SSC RitualFix] 补丁注册完成: {successCount}/9 成功");
#if SSC_RW17
            SSCPatchRegistration.Record("Binding ritual quality and UI",
                successCount == 9 ? SSCPatchState.Ready : SSCPatchState.Failed,
                detail: $"{successCount}/9");
#endif
        }

        // 手动安装入口负责区分外部依赖是否存在。无异常且未安装时，依赖缺席与目标接口不适用分别记录。
        private static void RecordManualPatch(string group, string target, bool installed, Exception error = null, bool missingDependency = false)
        {
#if SSC_RW17
            SSCPatchRegistration.Record(group, error != null ? SSCPatchState.Failed :
                installed ? SSCPatchState.Ready : SSCPatchState.Skipped,
                target, "Manual install", error?.GetBaseException().GetType().Name,
                error != null || installed ? SSCPatchSkipReason.None :
                    missingDependency ? SSCPatchSkipReason.MissingDependency : SSCPatchSkipReason.UnsupportedTarget);
#endif
        }
    }
}
