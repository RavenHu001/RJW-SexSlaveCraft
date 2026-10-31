// AI 辅助生成：仅包装真实婴儿生成请求，不保存孕次或改写原遗传流程。
using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>三项胎儿调控的生成消费者；身体预设与人格印记继续使用各自唯一保存源。</summary>
    internal static class SSCFertilityOffspringUtility
    {
        /// <summary>原版及月经入口共用：读取生成时资格，生成后只授初始印记，不修改身份或绑定。</summary>
        internal static Pawn Generate(PawnGenerationRequest request, Pawn mother, Pawn father)
        {
            var original = request;
            var context = SSCFertilityPreconceptionUtility.Capture(mother);
            // 查询不创建身体记录，不保留受孕时授权；每次真实生成只读取一次当前可用项。
            if (mother?.RaceProps?.Humanlike != true || (father != null && father.RaceProps?.Humanlike != true) ||
                !request.AllowedDevelopmentalStages.Newborn() ||
                !SSCFertilityPreconceptionUtility.TryGetApplicableSettings(mother, out var settings))
                return PawnGenerator.GeneratePawn(original);
            if (!SSCFertilityPreconceptionUtility.IsCurrent(context)) return PawnGenerator.GeneratePawn(original);

            if (settings.Gender != SSCOffspringGender.Natural)
                request.FixedGender = settings.Gender == SSCOffspringGender.Male ? Gender.Male : Gender.Female;
            ThingDef expectedRace = null;
            if (settings.SpeciesSource != SSCOffspringSpeciesSource.Natural)
            {
                Pawn source = settings.SpeciesSource == SSCOffspringSpeciesSource.Self ? mother : father;
                if (TryResolveKind(source, request.KindDef, out PawnKindDef kind))
                {
                    expectedRace = source.def;
                    // getter优先于KindDef字段；仅明确物种时清除此请求的动态选种，不改共享Def或基因参数。
                    request.PawnKindDefGetter = null;
                    request.KindDef = kind;
                }
                else Warn(mother, "SSC_Birth_SpeciesUnavailable");
            }
            if (!SSCFertilityPreconceptionUtility.IsCurrent(context)) return PawnGenerator.GeneratePawn(original);
            Pawn baby = PawnGenerator.GeneratePawn(request);
            if (baby == null || !SSCFertilityPreconceptionUtility.IsCurrent(context)) return baby;
            if ((expectedRace != null && baby.def != expectedRace) ||
                (settings.Gender != SSCOffspringGender.Natural && baby.gender != request.FixedGender))
                Warn(mother, "SSC_Birth_ResultMismatch");
            // 可见消息也会经过外部回调；不能据旧人格的选项继续向婴儿授予印记。
            if (!SSCFertilityPreconceptionUtility.IsCurrent(context)) return baby;
            if (settings.ImprintEnabled && baby.RaceProps?.Humanlike == true &&
                SSCImprintUtility.GetKind(baby) == SSCImprintKind.None && !SSCImprintUtility.TryGrantInitial(baby))
                Warn(mother, "SSC_Birth_ImprintUnavailable");
            return baby;
        }

        /// <summary>保留同物种请求，检查HAR子代kind，再用该亲代kind；不随机取同race第一个定义。</summary>
        internal static bool TryResolveKind(Pawn source, PawnKindDef original, out PawnKindDef result)
        {
            result = null;
            if (source?.def?.race?.Humanlike != true) return false;
            if (original?.race == source.def) result = original;
            else
            {
                // HAR为可选依赖，只读取已核对的字段链；不执行种族ResolveReferences或自然杂交选择。
                object alien = ReadField(source.def, "alienRace");
                object general = ReadField(alien, "generalSettings");
                object reproduction = ReadField(general, "reproduction");
                var child = ReadField(reproduction, "childKindDef") as PawnKindDef;
                if (child?.race == source.def) result = child;
                else if (source.kindDef?.race == source.def) result = source.kindDef;
                else if (source.def == ThingDefOf.Human && PawnKindDefOf.Colonist?.race == source.def) result = PawnKindDefOf.Colonist;
            }
            return result?.race == source.def;
        }

        /// <summary>只读取可选HAR字段，普通ThingDef缺字段时返回空，不安装新依赖或改写配置。</summary>
        private static object ReadField(object value, string name)
            => value == null ? null : AccessTools.Field(value.GetType(), name)?.GetValue(value);

        /// <summary>调控失败保留原生成，用可见提示说明；同次分娩同原因不重复刷消息。</summary>
        internal static void Warn(Pawn mother, string reasonKey)
        {
            if (mother == null || !SSCFertilityBirthUtility.ShouldWarn(mother, reasonKey)) return;
            Messages.Message("SSC_Birth_ControlUnapplied".Translate(mother.LabelShort, reasonKey.Translate()),
                mother, MessageTypeDefOf.RejectInput, false);
        }
    }
}
