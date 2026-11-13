// AI 辅助生成。1.7 调教员指派与旧单人存档迁移。
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        private List<Pawn> assignedTrainers = new List<Pawn>();

        // 兼容既有单值读取。名单管理使用明确的逐人入口。
        public Pawn selectedTrainer
        {
            get => AssignedTrainerCount == 0 ? null : assignedTrainers[0];
#if SSC_TRAINER_WORK_TEST
            set => ReplaceAssignedTrainers(value == null ? Array.Empty<Pawn>() : new[] { value });
#endif
        }

        internal int AssignedTrainerCount => assignedTrainers?.Count ?? 0;
        internal int TrainerAssignmentRevision { get; private set; }
        internal bool HasAssignedTrainer(Pawn trainer) => trainer != null && assignedTrainers?.Contains(trainer) == true;
        internal Pawn[] CopyAssignedTrainers() => assignedTrainers?.ToArray() ?? Array.Empty<Pawn>();

        internal static bool IsRetainableTrainer(Pawn target, Pawn trainer)
            => trainer != null && trainer != target && !trainer.Dead && !trainer.Destroyed && !trainer.Discarded;

        internal void ReplaceAssignedTrainers(IEnumerable<Pawn> trainers)
        {
            var next = new List<Pawn>(trainers);
            if (assignedTrainers != null && assignedTrainers.SequenceEqual(next)) return;
#if SSC_RW17
            List<Pawn> previous = assignedTrainers;
#endif
            assignedTrainers = next;
            TrainerAssignmentRevision++;
#if SSC_RW17
            if (previous != null && Scribe.mode == LoadSaveMode.Inactive && parent is Pawn target)
                foreach (Pawn removed in previous)
                    if (!next.Contains(removed))
                        SSCDailyTrainingOccupancy.RemoveUnauthorizedQueuedJobs(target, removed);
#endif
        }

        internal void ClearAssignedTrainers() => ReplaceAssignedTrainers(Array.Empty<Pawn>());

        internal void RemoveAssignedTrainer(Pawn trainer)
        {
            if (HasAssignedTrainer(trainer)) ReplaceAssignedTrainers(assignedTrainers.Where(member => member != trainer));
        }

        internal void NormalizeAssignedTrainers(Pawn target)
        {
            if (AssignedTrainerCount == 0) return;
            ReplaceAssignedTrainers(assignedTrainers.Where(trainer => IsRetainableTrainer(target, trainer)).Distinct());
        }

#if SSC_RW17
        internal SSCDailyTrainingOccupancy DailyTrainingOccupancy { get; set; }

        private const int CurrentTrainerAssignmentsVersion = 1;
        private int trainerAssignmentsVersion = CurrentTrainerAssignmentsVersion;
        private Pawn legacySelectedTrainer;

        private void ExposeTrainerAssignments()
        {
            Scribe_Values.Look(ref trainerAssignmentsVersion, "sscTrainerAssignmentsVersion", 0);
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // 清理序列化副本，存档期间不更改进行中的界面请求或名单版本。
                Pawn target = parent as Pawn;
                var saved = assignedTrainers.Where(trainer => IsRetainableTrainer(target, trainer)).ToList();
                Scribe_Collections.Look(ref saved, "sscAssignedTrainers", LookMode.Reference);
            }
            else if (trainerAssignmentsVersion == 0 &&
                (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.ResolvingCrossRefs))
                Scribe_References.Look(ref legacySelectedTrainer, "selectedTrainer");
            else if (trainerAssignmentsVersion != 0)
                Scribe_Collections.Look(ref assignedTrainers, "sscAssignedTrainers", LookMode.Reference);

            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            if (trainerAssignmentsVersion == 0)
            {
                ReplaceAssignedTrainers(legacySelectedTrainer == null ? Array.Empty<Pawn>() : new[] { legacySelectedTrainer });
                legacySelectedTrainer = null;
                trainerAssignmentsVersion = CurrentTrainerAssignmentsVersion;
            }
            assignedTrainers ??= new List<Pawn>();
            NormalizeAssignedTrainers(parent as Pawn);
        }
#endif
    }
}
