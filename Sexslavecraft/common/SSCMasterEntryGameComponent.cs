using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>按存档保存初次任务历史和无地图请求；每次加载只做一次研究/主人自动条件检查。</summary>
    public sealed class SSCMasterEntryGameComponent : GameComponent
    {
        private readonly Game owner;
        private bool initialQuestCreated;
        private bool creationPending;
        private bool manualRequestPending;
        private Quest entryQuest;
        private bool ready;
        private bool initializationRequested;
        private int nextAttemptTick;
        private const int RetryInterval = 1200;

        public SSCMasterEntryGameComponent(Game game) { owner = game; }

        public override void ExposeData()
        {
            base.ExposeData();
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

        private void FinishInitialization()
        {
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
            Quest pending = SSCMasterEntryUtility.FindPendingQuest();
            if (pending != null) CommitCreated(pending);
            if (initialQuestCreated && !manualRequestPending) creationPending = false;
            if (!initialQuestCreated && !creationPending && SSCDefOf.SSC_BasicTraining != null &&
                !SSCDefOf.SSC_BasicTraining.IsFinished &&
                !SSCMasterEntryUtility.HasExistingMaster(SSCRestrictionGameComponent.AllPawns()))
                creationPending = true;
            TryCreatePendingQuest();
        }

        public override void GameComponentTick()
        {
            if (initializationRequested && !ready) FinishInitialization();
            // 只有已记录的请求才轻量重试地图与任务登记，不周期扫描 Pawn 或重算自动资格。
            if (!ready || Current.Game != owner || !creationPending || Find.TickManager.TicksGame < nextAttemptTick)
                return;
            TryCreatePendingQuest();
        }

        private bool TryCreatePendingQuest()
        {
            if (!creationPending) return false;
            nextAttemptTick = Find.TickManager.TicksGame + RetryInterval;
            Quest pending = SSCMasterEntryUtility.FindPendingQuest();
            if (pending != null)
            {
                CommitCreated(pending);
                return false;
            }
            Map map = SSCMasterEntryUtility.FindDeliveryMap(null, SSCDefOf.SSC_InitialMasterQualification);
            if (!SSCMasterEntryUtility.TryCreateQuest(map, out Quest created)) return false;
            CommitCreated(created);
            SSCMasterEntryUtility.NotifyAvailable(created);
            return true;
        }

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
            if (!ready || Current.Game != owner || Find.QuestManager == null ||
                SSCDefOf.SSC_InitialMasterQualification == null || SSCDefOf.SSC_MasterQualificationItem == null)
                return "SSC_MasterEntry_Unavailable".Translate();
            if (SSCMasterEntryUtility.FindPendingQuest() != null)
                return "SSC_MasterEntry_AlreadyPending".Translate();
            if (SSCMasterEntryUtility.FindDeliveryMap(null, SSCDefOf.SSC_InitialMasterQualification) == null)
                return "SSC_MasterEntry_NoMap".Translate();
            return true;
        }

        public bool TryRequestManually(out string reason)
        {
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
