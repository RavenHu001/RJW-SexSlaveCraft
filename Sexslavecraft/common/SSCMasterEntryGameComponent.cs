using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>按存档保存初次任务历史和无地图请求；每次加载只做一次研究/主人自动条件检查。</summary>
    public sealed class SSCMasterEntryGameComponent : GameComponent
    {
        private readonly Game owner;
        // 这些字段随存档保存：历史标记表示曾成功创建，并不表示玩家已接受或领取。
        // 手动请求来源使“已创建过自动任务，但本次手动创建失败”的请求仍能跨读档重试。
        private bool initialQuestCreated;
        private bool creationPending;
        private bool manualRequestPending;
        private Quest entryQuest;
        // 运行期状态每次加载重新初始化；不把本次初始化完成状态保存到下一次读档。
        private bool ready;
        private bool initializationRequested;
        private int nextAttemptTick;
        private const int RetryInterval = 1200;

        public SSCMasterEntryGameComponent(Game game) { owner = game; }

        public override void ExposeData()
        {
            base.ExposeData();
            // 旧档缺字段默认没有创建历史；任务引用由原版跨引用恢复，丢失时可查任务全集。
            Scribe_Values.Look(ref initialQuestCreated, "sscInitialMasterQuestCreated", false);
            Scribe_Values.Look(ref creationPending, "sscInitialMasterQuestPending", false);
            Scribe_Values.Look(ref manualRequestPending, "sscInitialMasterQuestManualPending", false);
            Scribe_References.Look(ref entryQuest, "sscInitialMasterQuest");
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // ExecuteWhenFinished 在没有长事件时可能立即执行；只登记请求，留到运行更新再判定。
            // 这样即便回调同步执行，也要等其他组件全部完成迁移；旧游戏回调不能影响新存档。
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (Current.Game == owner) initializationRequested = true;
            });
        }

        public override void GameComponentUpdate()
        {
            // Update 在暂停时也运行，读档后无需先解除暂停才能看到可用的设置按钮。
            if (initializationRequested && !ready) FinishInitialization();
        }

        /// <summary>一次性恢复去重状态、判定新的自动请求，并尝试创建已保留的请求。</summary>
        private void FinishInitialization()
        {
            // 同一实例只初始化一次，且不能把旧实例的延迟操作写入另一个游戏。
            if (ready || Current.Game != owner) return;
            ready = true;
            // 任务引用缺失时从原版任务历史恢复去重，不凭引用为空补发。
            if (Find.QuestManager != null)
                foreach (Quest quest in Find.QuestManager.QuestsListForReading)
                    if (SSCMasterEntryUtility.IsEntryQuest(quest))
                    {
                        initialQuestCreated = true;
                        if (!quest.Historical) entryQuest = quest;
                    }
            // 活跃任务优先接管；历史创建会取消自动补发请求，但保留明确的手动失败请求。
            Quest pending = SSCMasterEntryUtility.FindPendingQuest();
            if (pending != null) CommitCreated(pending);
            if (initialQuestCreated && !manualRequestPending) creationPending = false;
            // 只有尚无历史/请求时才扫描 Pawn；已记录的请求不在加载或重试时重算研究/主人条件。
            if (!initialQuestCreated && !creationPending && SSCDefOf.SSC_BasicTraining != null &&
                !SSCDefOf.SSC_BasicTraining.IsFinished &&
                !SSCMasterEntryUtility.HasExistingMaster(SSCRestrictionGameComponent.AllPawns()))
                creationPending = true;
            // 地图或生成条件暂不可用时，保留请求而不是提前写入成功创建标记。
            TryCreatePendingQuest();
        }

        public override void GameComponentTick()
        {
            // Tick 也可完成首次初始化，随后与暂停时的 Update 共用同一个 ready 防重标记。
            if (initializationRequested && !ready) FinishInitialization();
            // 只有已记录的请求才轻量重试地图与任务登记，不周期扫描 Pawn 或重算自动资格。
            if (!ready || Current.Game != owner || !creationPending || Find.TickManager.TicksGame < nextAttemptTick)
                return;
            TryCreatePendingQuest();
        }

        /// <summary>兑现请求；返回 true 仅表示本次新建任务，接管已有任务不会再发通知。</summary>
        private bool TryCreatePendingQuest()
        {
            // 先限频再查询/生成；成功提交清空 pending 后，后续 Tick 不再进入重试。
            if (!creationPending) return false;
            nextAttemptTick = Find.TickManager.TicksGame + RetryInterval;
            Quest pending = SSCMasterEntryUtility.FindPendingQuest();
            if (pending != null)
            {
                // 原版已经有同类任务时只恢复组件记录，不生成第二份任务或第二封任务信。
                CommitCreated(pending);
                return false;
            }
            // 失败保持现有请求来源；成功先提交持久状态，再调用可单独失败的通知边界。
            Map map = SSCMasterEntryUtility.FindDeliveryMap(null, SSCDefOf.SSC_InitialMasterQualification);
            if (!SSCMasterEntryUtility.TryCreateQuest(map, out Quest created)) return false;
            CommitCreated(created);
            SSCMasterEntryUtility.NotifyAvailable(created);
            return true;
        }

        /// <summary>同步提交创建历史、请求清理和任务引用；任务完成不会撤销这份创建历史。</summary>
        private void CommitCreated(Quest quest)
        {
            initialQuestCreated = true;
            creationPending = false;
            manualRequestPending = false;
            entryQuest = quest;
        }

        /// <summary>手动入口忽略研究、主人与历史标记，但不并行创建待处理的同类任务。</summary>
        public AcceptanceReport CanRequestManually()
        {
            // 只读检查游戏/初始化及必需 Def；手动入口不检查已完成研究或是否已有主人。
            if (!ready || Current.Game != owner || Find.QuestManager == null ||
                SSCDefOf.SSC_InitialMasterQualification == null || SSCDefOf.SSC_MasterQualificationItem == null)
                return "SSC_MasterEntry_Unavailable".Translate();
            // 任务状态和可投放基地分开返回原因，供按钮禁用提示及最终执行复查共用。
            if (SSCMasterEntryUtility.FindPendingQuest() != null)
                return "SSC_MasterEntry_AlreadyPending".Translate();
            if (SSCMasterEntryUtility.FindDeliveryMap(null, SSCDefOf.SSC_InitialMasterQualification) == null)
                return "SSC_MasterEntry_NoMap".Translate();
            return true;
        }

        /// <summary>点击后重新准入；保存手动请求，再立即尝试同一自动/手动共享创建流程。</summary>
        public bool TryRequestManually(out string reason)
        {
            // 菜单绘制和点击之间可能出现任务或地图变化，不能沿用之前的可用结果。
            AcceptanceReport report = CanRequestManually();
            reason = report.Accepted ? null : report.Reason;
            if (!report.Accepted) return false;
            // 失败保留请求，让已满足地图条件的手动请求也能在原版生成边界失败后重试。
            creationPending = true;
            manualRequestPending = true;
            bool created = TryCreatePendingQuest();
            if (!created) reason = "SSC_MasterEntry_CreateFailed".Translate();
            return created;
        }
    }
}
