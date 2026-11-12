// AI 辅助生成。显式指派与工作启用共用原版工作设置；查询不初始化、不补写保存状态。
using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    internal enum SSCTrainerWorkState { Active, Off, Forbidden, Uninitialized, Inactive, Downed, Away }

    internal static class SSCTrainerWorkUtility
    {
        internal static WorkTypeDef WorkType => DefDatabase<WorkTypeDef>.GetNamedSilentFail("TrainSexSlave");
        private static readonly HashSet<Pawn> submitting = new HashSet<Pawn>();
        // 原版GetPriority在勾选模式把所有正数归一为3；过期检查和撤回必须比较实际保存值。
        // 仅读取已核验的1.7字段，写入仍全部走原版SetPriority；字段不匹配时拒绝提交。
        private static readonly FieldInfo prioritiesField = typeof(Pawn_WorkSettings).GetField("priorities", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static int SavedPriority(Pawn_WorkSettings settings, WorkTypeDef work)
            => settings?.Initialized == true && work != null && prioritiesField?.GetValue(settings) is DefMap<WorkTypeDef, int> priorities
                ? priorities[work] : -1;

        /// <summary>区分身份、临时工作条件与保存优先级；不改变任职或已有指派。</summary>
        internal static SSCTrainerWorkState GetState(Pawn trainer, Pawn source = null)
        {
            if (trainer == null || trainer.Dead || trainer.Destroyed || trainer.Discarded ||
                !SSCIdentityUtility.IsTrainer(trainer)) return SSCTrainerWorkState.Inactive;
            if (trainer.Downed) return SSCTrainerWorkState.Downed;
            if (source != null && (source.Map == null || trainer.Map != source.Map ||
                !source.Map.mapPawns.FreeColonists.Contains(trainer))) return SSCTrainerWorkState.Away;
            WorkTypeDef work = WorkType;
            if (work == null || trainer.workSettings == null || !trainer.workSettings.Initialized)
                return SSCTrainerWorkState.Uninitialized;
            if (trainer.WorkTypeIsDisabled(work)) return SSCTrainerWorkState.Forbidden;
            return trainer.workSettings.WorkIsActive(work) ? SSCTrainerWorkState.Active : SSCTrainerWorkState.Off;
        }

        internal static string Reason(SSCTrainerWorkState state, Pawn trainer = null)
        {
            string text = ("SSC_TrainerWork_State_" + state).Translate();
            if (state == SSCTrainerWorkState.Forbidden && trainer != null && WorkType != null)
            {
                List<string> reasons = trainer.GetReasonsForDisabledWorkType(WorkType);
                if (reasons?.Count > 0) text += ": " + string.Join("; ", reasons);
            }
            return text;
        }

        /// <summary>只放宽工作开关，身份、地图和目标许可仍与原指派一致；禁止工作者留给菜单显示原因。</summary>
        internal static bool CanConsider(Pawn source, Pawn trainer)
            => CanConsider(source, trainer, out _);

        /// <summary>候选资格和授权预览使用同一次原规则预检。</summary>
        private static bool CanConsider(Pawn source, Pawn trainer, out bool opensNonOwnerTraining)
        {
            opensNonOwnerTraining = false;
            return SSCIdentityUtility.IsSexSlave(source) && source != trainer && source.Map != null &&
                trainer != null && !trainer.Dead && !trainer.Destroyed && !trainer.Discarded && !trainer.Downed &&
                SSCIdentityUtility.IsTrainer(trainer) && trainer.Map == source.Map &&
                source.Map.mapPawns.FreeColonists.Contains(trainer) &&
                SSCRestrictionTrainerAssignment.CanAssign(source, trainer, out opensNonOwnerTraining);
        }

        /// <summary>捕获原角色、人格和工作载体。提交不能重新捕获来授权旧请求。</summary>
        internal sealed class Request
        {
            internal readonly Pawn Source, Trainer;
            private readonly Game game;
            private readonly CompSexSlaveTraining sourceComp, trainerComp;
            private readonly object sourceToken, trainerToken;
            private readonly Pawn previous, owner;
            private readonly Map map;
            private readonly Pawn_WorkSettings settings;
            private readonly WorkTypeDef work;
            private readonly int priority;
            private readonly SSCRestrictionConfig config;
            private readonly SSCRestrictionRules rules;
            private readonly bool receiveTraining, enableOnly;
            private readonly Func<bool> current;

            /// <summary>在用户发起操作时固定原指派、人格和工作设置引用。</summary>
            internal Request(Pawn source, Pawn trainer, bool enableOnly = false, Func<bool> current = null)
            {
                Source = source; Trainer = trainer; this.enableOnly = enableOnly; this.current = current;
                game = Current.Game; sourceComp = source?.TryGetComp<CompSexSlaveTraining>();
                trainerComp = trainer?.TryGetComp<CompSexSlaveTraining>();
                sourceToken = SSCPersonalityRuntimeUtility.Capture(source);
                trainerToken = SSCPersonalityRuntimeUtility.Capture(trainer);
                previous = sourceComp?.selectedTrainer; owner = SSCBondUtility.GetBoundMaster(source); map = source?.Map;
                config = sourceComp?.restrictionConfig; rules = config?.rules;
                receiveTraining = rules?.receiveTraining ?? false;
                settings = trainer?.workSettings; work = WorkType;
                priority = SavedPriority(settings, work);
            }

            /// <summary>复查来源、界面上下文以及请求捕获的指派与许可。</summary>
            private bool SourceCurrent()
                => game != null && ReferenceEquals(game, Current.Game) && Current.ProgramState == ProgramState.Playing &&
                    Scribe.mode == LoadSaveMode.Inactive && current?.Invoke() != false &&
                    Source != null && !Source.Dead && !Source.Destroyed && !Source.Discarded && sourceComp != null &&
                    Source.TryGetComp<CompSexSlaveTraining>() == sourceComp && Source.Map == map &&
                    SSCPersonalityRuntimeUtility.IsCurrent(Source, sourceToken) && !SSCPersonalityRuntimeUtility.IsMutating(Source) &&
                    sourceComp.restrictionRestoreDepth == 0 && sourceComp.selectedTrainer == previous &&
                    SSCBondUtility.GetBoundMaster(Source) == owner && sourceComp.restrictionConfig == config &&
                    config?.rules == rules && (rules?.receiveTraining ?? false) == receiveTraining;

            /// <summary>复查调教员的组件、人格和原工作设置载体。</summary>
            private bool TrainerCurrent()
                => Trainer != null && trainerComp != null && trainerComp.restrictionRestoreDepth == 0 &&
                    Trainer.TryGetComp<CompSexSlaveTraining>() == trainerComp &&
                    SSCPersonalityRuntimeUtility.IsCurrent(Trainer, trainerToken) && !SSCPersonalityRuntimeUtility.IsMutating(Trainer) &&
                    ReferenceEquals(Trainer.workSettings, settings) && settings?.Initialized == true && work == WorkType;

            /// <summary>为菜单提交和拖放落区提供同一套只读预检。</summary>
            internal bool CanSubmit(out string reason) => CanSubmit(out reason, out _);

            /// <summary>只读复查原请求，并返回普通指派是否附带授权。撤销与单独启用工作不改个人规则。</summary>
            internal bool CanSubmit(out string reason, out bool opensNonOwnerTraining)
            {
                opensNonOwnerTraining = false;
                reason = "SSC_TrainerWork_Changed".Translate();
                if (!SourceCurrent() || submitting.Contains(Source) || Trainer != null && submitting.Contains(Trainer)) return false;
                if (Trainer == null)
                {
                    // 撤销只检查原指派和清空许可，现任暂时无法工作仍可撤销。
                    if (!enableOnly && SSCRestrictionTrainerAssignment.CanAssign(Source, null)) return true;
                    reason = "SSC_Restrictions_AssignFailed".Translate();
                    return false;
                }
                if (!TrainerCurrent() || priority < 0 || SavedPriority(settings, work) != priority) return false;
                SSCTrainerWorkState state = GetState(Trainer, Source);
                if (state != SSCTrainerWorkState.Active && state != SSCTrainerWorkState.Off)
                { reason = Reason(state, Trainer); return false; }
                // 仅普通指派走规则预检。就地启用只检查已指派的调教员和原工作值。
                if (enableOnly ? Source == Trainer || previous != Trainer || priority != 0 || !SSCIdentityUtility.IsSexSlave(Source)
                    : !CanConsider(Source, Trainer, out opensNonOwnerTraining))
                { reason = "SSC_Restrictions_AssignFailed".Translate(); return false; }
                return true;
            }

            /// <summary>写工作优先级后的外部回调须复查，失败只撤回本请求仍持有的新增启用。</summary>
            internal bool TrySubmit(out string reason)
            {
                if (!CanSubmit(out reason)) return false;
                if (Trainer == null) return SSCBondUtility.TryAssignTrainer(Source, null);

                // 标记参与者后再进入原版 setter，防止同步回调重入同一笔事务。
                bool wrote = false, success = false;
                submitting.Add(Source); submitting.Add(Trainer);
                try
                {
                    if (priority == 0)
                    {
                        // void setter可能被外部补丁拒绝或在写后抛异常；先标记本次尝试，finally再按实际值判断。
                        wrote = true;
                        settings.SetPriority(work, Pawn_WorkSettings.DefaultPriority);
                    }
                    if (!SourceCurrent() || !TrainerCurrent() ||
                        SavedPriority(settings, work) != (wrote ? Pawn_WorkSettings.DefaultPriority : priority)) return false;
                    SSCTrainerWorkState state = GetState(Trainer, Source);
                    if (state != SSCTrainerWorkState.Active) { reason = Reason(state, Trainer); return false; }
                    // 就地启用不重放授权；普通指派仍经原权威入口再次检查并提交。
                    success = enableOnly || SSCBondUtility.TryAssignTrainer(Source, Trainer);
                    if (!success) reason = "SSC_Restrictions_AssignFailed".Translate();
                    return success;
                }
                catch (Exception ex)
                {
                    Log.Error("[SSC] Trainer work submission failed: " + ex);
                    reason = "SSC_TrainerWork_Failed".Translate();
                    return false;
                }
                finally
                {
                    try
                    {
                        // 人格／工作载体换新，或外部已写入其他优先级时，不能用旧请求覆盖它。
                        if (!success && wrote && TrainerCurrent() && SavedPriority(settings, work) == Pawn_WorkSettings.DefaultPriority)
                            settings.SetPriority(work, 0);
                    }
                    catch (Exception ex) { Log.Error("[SSC] Trainer work rollback failed: " + ex); }
                    finally { submitting.Remove(Source); submitting.Remove(Trainer); }
                }
            }
        }
    }
}
