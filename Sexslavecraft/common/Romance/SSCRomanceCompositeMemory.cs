// AI 辅助生成：综合心情保存与显示载体；本批不连接真实 worker，不执行原版替换。
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>分支文案的独立入口；不缓存人物状态或已翻译文本，切换语言不会改写历史身份。</summary>
    public static class SSCRomanceMoodText
    {
        /// <summary>按目录名称键取占位；缺键使用明确的未知占位，不猜另一分支。</summary>
        public static string Name(SSCRomanceMoodBranch branch) => Read(branch?.NameKey, "SSC_RomanceMood_Unknown_Name");
        /// <summary>名称与描述分别取键；相同数值的不同分支也使用独立描述。</summary>
        public static string Description(SSCRomanceMoodBranch branch) => Read(branch?.DescriptionKey, "SSC_RomanceMood_Unknown_Description");
        /// <summary>缺语言或缺键时只返回开发占位，不生成事件、查询概率或向控制台刷屏。</summary>
        private static string Read(string key, string fallback)
        {
            if (LanguageDatabase.activeLanguage != null)
            {
                if (key != null && key.TryTranslate(out var text) && !string.IsNullOrWhiteSpace(text.ToString())) return text.ToString();
                if (fallback.TryTranslate(out var unknown) && !string.IsNullOrWhiteSpace(unknown.ToString())) return unknown.ToString();
            }
            return "[文案待定]";
        }
    }

    /// <summary>两个新数值载体共用的记忆类；分支身份在 Data 中，旧记忆类及旧 Def 阶段不重排。</summary>
    public sealed class Thought_MemorySSCCompositeRomance : Thought_MemorySSCRomance, ISSCRomanceHistoricalRestore
    {
        public const string ShortDefName = "SSC_Romance_CompositeShort";
        public const string LongDefName = "SSC_Romance_CompositeLong";
        // 只属于这次复制／恢复的实例，不是新事件；首次进入合并即消费，不进入 Scribe 或 Data.Copy。
        private bool historicalRestore;

        /// <summary>只识别本系统的两个载体，历史回退也不能借其他 Def 创建伪装记忆。</summary>
        private bool IsCarrier => def?.defName == ShortDefName || def?.defName == LongDefName;

        /// <summary>载体与保存组匹配；数值保存和文案解析均可复用，后者另需完整分支校验。</summary>
        private bool MatchingCarrier => Data?.Group == SSCRomanceMoodGroup.Short ? def?.defName == ShortDefName :
            Data?.Group == SSCRomanceMoodGroup.Long && def?.defName == LongDefName;

        /// <summary>同时校验格式和保存组；未知 ID／版本不能借载体被解释为一般或零值。</summary>
        private bool TryBranch(out SSCRomanceMoodBranch branch)
        {
            branch = null;
            return MatchingCarrier && Data.Value != 0 && SSCRomanceMoodCatalog.TryReadSnapshot(Data, out branch);
        }

        /// <summary>从冻结值提供基准，原版屏蔽、倍率、实例偏移及衰减仍由原 MoodOffset 处理。</summary>
        protected override float BaseMoodOffset => IsCarrier && Data?.IsComposite == true && Data.Value != 0 && Data.Value >= -15 && Data.Value <= 15 ?
            Data.Value : base.BaseMoodOffset;

        /// <summary>沿原生 Scribe 保存历史；运行期的历史领取标记不存档，读取时明确清除。</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.LoadingVars) historicalRestore = false;
        }

        /// <summary>原生克隆已复制一条真实历史，只标记这条新实例；基类同时清除旧 CanApply 运行守卫。</summary>
        public override void CopyFrom(Thought_Memory other)
        { base.CopyFrom(other); historicalRestore = true; }

        /// <summary>普通人格恢复在原版准入周围显式标记当前实例，失败也由 finally 清除。</summary>
        void ISSCRomanceHistoricalRestore.BeginHistoricalRestore() => historicalRestore = true;
        /// <summary>结束当前历史恢复；标记不延续到后续新的发放尝试。</summary>
        void ISSCRomanceHistoricalRestore.EndHistoricalRestore() => historicalRestore = false;

        /// <summary>原生年龄／期限保存不变；长组沿用已有同强度较晚到期逻辑，跨旧组接入留第二批。</summary>
        protected override bool IsLongTermGroup => Data?.Group == SSCRomanceMoodGroup.Long;

        /// <summary>原准入后区分已有历史和新请求；未知分支只能恢复历史，不成为有效的新事件。</summary>
        public override bool TryMergeWithExistingMemory(out bool showBubble)
        {
            bool restoring = historicalRestore; historicalRestore = false;
            if (!TryBranch(out _))
            {
                // 新的无效请求仍拒绝；已有不可解释历史按原生容量恢复，保留年龄而不进入新事件强弱计算。
                showBubble = false; return !(restoring && IsCarrier);
            }
            return base.TryMergeWithExistingMemory(out showBubble);
        }

        /// <summary>文案只读已保存分支，不用当前阶段、特质或好感重新选名字。</summary>
        public override string LabelCap
        {
            get
            {
                TryBranch(out var branch);
                string label = SSCRomanceMoodText.Name(branch);
                label = otherPawn == null ? label.CapitalizeFirst() : label.Formatted(otherPawn.LabelShort, otherPawn).CapitalizeFirst();
                return pawn != null && def?.Worker != null ? def.Worker.PostProcessLabel(pawn, label) : label;
            }
        }

        /// <summary>共享原版可能使用的标签入口，不派生社交记忆或新增好感偏移。</summary>
        public override string LabelCapSocial => LabelCap;

        /// <summary>替换正文来源并保留 1.7 原版人物格式化、文化来源与思想屏蔽说明。</summary>
        public override string Description
        {
            get
            {
                TryBranch(out var branch);
                string text = SSCRomanceMoodText.Description(branch);
                if (pawn == null) return text;
                text = def?.Worker != null ? def.Worker.PostProcessDescription(pawn, text) : otherPawn != null ?
                    text.Formatted(pawn.Named("PAWN"), otherPawn.Named("OTHERPAWN")).Resolve() : text.Formatted(pawn.Named("PAWN")).Resolve();
                if (ModsConfig.IdeologyActive && sourcePrecept != null && !Find.IdeoManager.classicMode) text += CausedByBeliefInPrecept;
                string nullified = ThoughtUtility.ThoughtNullifiedMessage(pawn, def);
                return string.IsNullOrEmpty(nullified) ? text : text + "\n\n(" + nullified + ")";
            }
        }

        /// <summary>仅构造未入队的保存／显示实例；不验证实际事件授权，不调用 TryGainMemory。</summary>
        public static bool TryCreate(SSCRomanceMoodSelection selection, out Thought_MemorySSCCompositeRomance memory)
            => TryCreate(selection, null, out memory);

        /// <summary>保留调用方已冻结的旧关系／主人／规则上下文；不从事件名称推断实际关系或命令权限。</summary>
        public static bool TryCreate(SSCRomanceMoodSelection selection, SSCRomanceMoodData context, out Thought_MemorySSCCompositeRomance memory)
        {
            memory = null;
            if (selection == null || !selection.HasMemory) return false;
            // Lookup／ThoughtMaker／Init 都可能经过外部补丁；在首个回调前冻结调用方给出的历史事实。
            var snapshot = selection.CreateSnapshot(context);
            string name = selection.Group == SSCRomanceMoodGroup.Long ? LongDefName : ShortDefName;
            var carrier = DefDatabase<ThoughtDef>.GetNamedSilentFail(name);
            int index = selection.Branch.Value + 15;
            if (!ValidCarrier(carrier, name, index, selection.Branch.Value)) return false;
            var created = ThoughtMaker.MakeThought(carrier, index) as Thought_MemorySSCCompositeRomance;
            var registered = DefDatabase<ThoughtDef>.GetNamedSilentFail(name);
            // 不能仅凭工厂返回了同类型就宣称成功；保存 Def、阶段或注册被回调替换时返回失败。
            if (created == null || !ReferenceEquals(registered, carrier) || !ReferenceEquals(created.def, carrier) ||
                !ValidCarrier(carrier, name, index, selection.Branch.Value) || created.CurStageIndex != index) return false;
            created.Data = snapshot; created.durationTicksOverride = selection.DurationTicks;
            created.historicalRestore = false;
            memory = created;
            return true;
        }

        /// <summary>工厂回调前后使用同一保存载体契约，不回退成其他数值、其他 Def 或普通分支。</summary>
        private static bool ValidCarrier(ThoughtDef carrier, string name, int index, int value) =>
            carrier?.defName == name && carrier.thoughtClass == typeof(Thought_MemorySSCCompositeRomance) && carrier.stages != null &&
            index >= 0 && index < carrier.stages.Count && carrier.stages[index] != null && carrier.stages[index].baseMoodEffect == value;
    }
}
