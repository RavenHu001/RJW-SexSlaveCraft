using System;
using System.Reflection;
using Verse;
using HarmonyLib;
using RimWorld;

// EN: This file is SSC's Harmony startup entry.
// EN: It installs every attribute-based patch first, then manually hooks the Binding Ritual fixes and the `全凝胶化完成` body-tint patch.
// CN: 这个文件是 SSC 的 Harmony 启动入口。
// CN: 它会先安装所有注解式补丁，再手动挂上“绑定仪式”修正和“全凝胶化完成”染色补丁。
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
            harmony.PatchAll();
#if SSC_RW17
            // 等本轮静态构造／原有补丁注册完成后安装只读计时采样；不改变冻结版入口。
            LongEventHandler.ExecuteWhenFinished(SSCOrgasmProgressPatches.Install);
            // 可选 UAP 接入使用独立 owner，完整核对后才开放手动豁免；不随旧版 PatchAll 注册。
            LongEventHandler.ExecuteWhenFinished(SSCUapGroupPatches.Install);
            // 完整核对 RJW 评价与自愿入口后安装关系保底，保留原基础资格和主人侧意愿。
            LongEventHandler.ExecuteWhenFinished(SSCMasterAttractionPatches.Install);
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
            }
            catch (Exception ex) { Log.Error($"[SSC FullGel] PawnRenderNode_Body.GraphicFor 注册失败: {ex}"); }

            // 头部在最终材质阶段只替换皮肤，保留 FA 本帧五官和原队列；FA 未安装时支持原版头部。
            try { Harmony_FullGelatinizationHeadTint.Register(harmony); }
            catch (Exception ex) { Log.Error($"[SSC FullGel] Head skin material hook 注册失败: {ex}"); }

            // SA 的并行 GetGraphic 只取得原图；染色在最终材质阶段完成，避免工作线程访问 GraphicDatabase。
            try { Harmony_FullGelatinizationBodyTint.RegisterSizedApparel(harmony); }
            catch (Exception ex) { Log.Error($"[SSC FullGel] SizedApparel final material hook 注册失败: {ex}"); }

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
                }
                else
                {
                    SSCLog.Important("[SSC FullGel] 未检测到 SizedApparel，跳过服装胸型图形软依赖补丁。");
                }
            }
            catch (Exception ex) { Log.Error($"[SSC FullGel] ApparelGraphicRecordGetter.TryGetGraphicApparel 注册失败: {ex}"); }

            SSCLog.Important($"[SSC RitualFix] 补丁注册完成: {successCount}/9 成功");
        }
    }
}
