// AI 辅助生成：复用 RJW 原生发现及评价流程的 PNA 来源偏好。
using rjw.Modules.Attraction;
using Verse;

namespace SexSlaveCraft
{
    public sealed class AttractionPreference_SSCPna : AttractionPreference
    {
        public const string PreferenceKey = "SSC_PnaSourceCraving";

        private AttractionPreference_SSCPna()
            : base(AttractionMode.Physical, PreferenceKey, FactorOperation.Multiply) { }

        [StandardPreference]
        public static void ApplyTo(ref AttractionRequest request)
        {
            // RJW 扫描非抽象偏好子类的标记方法；只为当前有效偏好创建条目。
            // 唯一 key 由当前请求管理，不保存角色状态，也不另行注册启动补丁。
            if (GetMultiplier(ref request) > 1f)
                request.SetPreference(new AttractionPreference_SSCPna());
        }

        private static float GetMultiplier(ref AttractionRequest request)
        {
            // 不区分评价目的，General、ForFucking、ForRape 共用同一来源偏好。
            // 各调用入口原有的资格、接受及最终选取仍交给 RJW。
            // 评价只读身体状态，不能通过悬停或择偶扫描清药、补需求或结算积累时间。
            Pawn observer = request.Pawn;
            if (observer == request.Target || !SSCPnaExposureUtility.IsValidTarget(observer) || observer.needs == null ||
                SSCIdentityUtility.IsMaster(observer) || SSCPnaExposureUtility.IsRooted(observer)) return 1f;

            Hediff addiction = SSCPnaExposureUtility.GetAddiction(observer);
            if (addiction == null || !SSCPnaSourceUtility.IsPnaProducer(request.Target)) return 1f;
            // 原版阶段 1 表示戒断；不重写需求阈值，不把剩余成瘾严重度当作渴望强度。
            // 无合法化学需求时原版返回普通档，直接沿用该结果。
            return addiction.CurStageIndex == 1 ? 1.5f : 1.25f;
        }

        protected override float GetOperand(ref AttractionRequest request, float factor)
            // 请求建立后身体也可能改变；每次取当前状态，不把注册时的倍率存进对象。
            => GetMultiplier(ref request);

        public override TaggedString Explain(ref AttractionRequest request, ref float factor)
        {
            // RJW 默认解释直接展示内部 key，因此在本条目翻译完整文本。
            // Evaluate 只执行一次，避免打开解释面板时把本项乘数重复计入。
            float multiplier = GetMultiplier(ref request);
            Evaluate(ref request, ref factor);
            return "SSC_Pna_AttractionExplanation".Translate(multiplier.ToString("0.###"), factor.ToString("0.###"));
        }
    }
}
