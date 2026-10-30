// AI 辅助生成：历史载荷模型不创建生育特化；新生育机制与真实凝胶路径由原生专项验证。
#if SSC_RW17
namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining { public int fertilityCareTicks; }
    public static class FertilitySpecializationUtility
    {
        public static bool IsTag(Verse.HediffDef def) => false;
        public static Verse.HediffDef OrdinaryDef => null;
        public static void Sync(Verse.Pawn pawn) { }
    }
    public static class FertilitySpecializationGelUtility
    {
        public static void StoreExclusiveTags(CompPersonalityStore data, Verse.Pawn pawn) { }
        public static void RemoveAllStates(Verse.Pawn pawn) { }
        public static void DetachAfterExtraction(Verse.Pawn pawn) { }
        public static void ApplyExclusiveTags(Verse.Pawn pawn, CompPersonalityStore data) { }
    }
    // 新技能的真实冷却与植入由原生宿主验证；此历史模型只保留无技能载荷边界。
    public static class FertilityAbilityUtility
    {
        public static void Maintain(Verse.Pawn pawn) { }
        public static int CaptureCooldown(Verse.Pawn pawn) => 0;
        public static void RestoreCooldown(Verse.Pawn pawn, int deadline) { }
    }
    internal static class FertilityFinalizationRecipeUtility
    {
        internal static bool IsFinalizationRecipe(RecipeDef_PSTag recipe) => false;
        internal static bool CanCreateProduct(Verse.ThingDef def) => false;
        internal static bool IsEligibleGel(RecipeDef_PSTag recipe, CompPersonalityStore data) => false;
    }
}
#endif
