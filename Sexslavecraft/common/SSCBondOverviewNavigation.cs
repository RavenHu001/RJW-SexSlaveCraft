// AI 辅助生成。统一导航边界；只读复查关系，重新解析点击对象的位置。
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace SexSlaveCraft
{
    internal enum SSCBondOverviewNavigationFailure { None, Unavailable, SourceInvalid, RelationChanged, Unknown, CannotJump }

    internal static class SSCBondOverviewNavigation
    {
        internal static bool IsTrainerCurrent(SSCBondOverviewSnapshot snapshot)
            => snapshot != null && snapshot.State == SSCBondOverviewQueryState.Ready && ReferenceEquals(snapshot.Game, Current.Game) &&
                Current.ProgramState == ProgramState.Playing && Scribe.mode == LoadSaveMode.Inactive &&
                SSCBondOverviewUtility.IsValidPawn(snapshot.Source) && SSCBondOverviewUtility.IsValidPawn(snapshot.AssignedTrainer) &&
                !ReferenceEquals(snapshot.Source, snapshot.AssignedTrainer) &&
                ReferenceEquals(snapshot.Source.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer, snapshot.AssignedTrainer);

        internal static bool TryNavigateTrainer(SSCBondOverviewSnapshot snapshot, out SSCBondOverviewNavigationFailure failure)
        {
            failure = SSCBondOverviewNavigationFailure.RelationChanged;
            if (!IsTrainerCurrent(snapshot)) return false;
            // 调教员不是锁链条目；复查指定引用后，只重解这一人的位置，不伪造绑定或使用旧船。
            SSCBondOverviewLocation location;
            using (var world = SSCBondOverviewWorld.Rent())
            { world.AddLocationRequest(snapshot.AssignedTrainer); world.IndexShipLocations(); location = world.GetLocation(snapshot.AssignedTrainer); }
            if (!TryTarget(location, out GlobalTargetInfo target)) { failure = SSCBondOverviewNavigationFailure.Unknown; return false; }
            if (!CameraJumper.CanJump(target)) { failure = SSCBondOverviewNavigationFailure.CannotJump; return false; }
            if (!IsTrainerCurrent(snapshot)) return false;
            if (CanSelect(target)) CameraJumper.TryJumpAndSelect(target); else CameraJumper.TryJump(target);
            failure = SSCBondOverviewNavigationFailure.None; return true;
        }

        internal static bool TryNavigate(SSCBondOverviewSnapshot snapshot, SSCBondOverviewEntry entry,
            out SSCBondOverviewNavigationFailure failure)
        {
            failure = SSCBondOverviewNavigationFailure.Unavailable;
            if (snapshot == null || !ReferenceEquals(snapshot.Game, Current.Game) || Current.ProgramState != ProgramState.Playing ||
                Scribe.mode != LoadSaveMode.Inactive) return false;
            if (!SSCBondOverviewUtility.IsValidPawn(snapshot.Source))
            { failure = SSCBondOverviewNavigationFailure.SourceInvalid; return false; }
            if (!SSCBondOverviewUtility.IsCurrent(snapshot, entry))
            { failure = SSCBondOverviewNavigationFailure.RelationChanged; return false; }

            // IsCurrent 不刷新位置。只重解这名角色，沿用第一批防环和位置优先级；不收集世界候选。
            SSCBondOverviewLocation location;
            using (var world = SSCBondOverviewWorld.Rent())
            {
                world.AddLocationRequest(entry.Pawn);
                world.IndexShipLocations();
                location = world.GetLocation(entry.Pawn);
            }
            if (!TryTarget(location, out GlobalTargetInfo target))
            { failure = SSCBondOverviewNavigationFailure.Unknown; return false; }
            if (!CameraJumper.CanJump(target))
            { failure = SSCBondOverviewNavigationFailure.CannotJump; return false; }
            if (!SSCBondOverviewUtility.IsCurrent(snapshot, entry))
            { failure = SSCBondOverviewNavigationFailure.RelationChanged; return false; }
            if (CanSelect(target)) CameraJumper.TryJumpAndSelect(target); else CameraJumper.TryJump(target);
            failure = SSCBondOverviewNavigationFailure.None;
            return true; // 仅表示已分派；原版接口为 void，不声称已观察到选中成功。
        }

        internal static bool CanPreview(SSCBondOverviewLocation location, out bool select)
        {
            select = false;
            if (!TryTarget(location, out GlobalTargetInfo target) || !CameraJumper.CanJump(target)) return false;
            select = CanSelect(target);
            return true;
        }

        private static bool CanSelect(GlobalTargetInfo target)
            => target.HasThing ? target.Thing.def.selectable : target.HasWorldObject && target.WorldObject.SelectableNow;

        private static bool TryTarget(SSCBondOverviewLocation location, out GlobalTargetInfo target)
        {
            target = GlobalTargetInfo.Invalid;
            if (location == null) return false;
            // 只传已确定的最终目标，避免原版 GetAdjustedTarget 再沿未知／循环持有链行走。
            switch (location.Kind)
            {
                case SSCBondOverviewLocationKind.Map:
                case SSCBondOverviewLocationKind.MapContainer:
                    Thing thing = location.Kind == SSCBondOverviewLocationKind.Map ? location.Subject : location.Container;
                    if (thing == null || thing.Destroyed || !thing.Spawned || !ReferenceEquals(thing.Map, location.Map)) return false;
                    target = new GlobalTargetInfo(thing); return true;
                case SSCBondOverviewLocationKind.Caravan:
                case SSCBondOverviewLocationKind.Transport:
                case SSCBondOverviewLocationKind.WorldObject:
                    if (location.WorldObject?.Spawned != true) return false;
                    target = new GlobalTargetInfo(location.WorldObject); return true;
                case SSCBondOverviewLocationKind.WorldTile:
                    if (location.Subject == null || location.Subject.Destroyed || !location.Subject.Tile.Valid) return false;
                    target = new GlobalTargetInfo(location.Subject.Tile); return true;
                default: return false;
            }
        }

        internal static void Highlight(SSCBondOverviewLocation location)
        {
            // 悬停不重建飞船索引；局部持有链已变化就跳过旧高亮，等待受控刷新。
            if (location == null || !ReferenceEquals(location.Map, Find.CurrentMap) ||
                (location.Kind != SSCBondOverviewLocationKind.Map && location.Kind != SSCBondOverviewLocationKind.MapContainer)) return;
            if (!CurrentMapSubject(location.Subject, out Thing current) ||
                !ReferenceEquals(current, location.Kind == SSCBondOverviewLocationKind.Map ? location.Subject : location.Container)) return;
            TargetHighlighter.Highlight(new GlobalTargetInfo(current), arrow: true, colonistBar: false);
        }

        private static bool CurrentMapSubject(Thing subject, out Thing current)
        {
            current = null;
            if (subject == null || subject.Destroyed) return false;
            if (subject.Spawned) { current = subject; return ReferenceEquals(subject.Map, Find.CurrentMap); }
            // 复用有界、归还即清空的路径容器，避免每次悬停另建集合。
            Pawn pawn = subject is Corpse corpse ? corpse.InnerPawn : subject as Pawn;
            if (!SSCBondOverviewUtility.IsValidPawn(pawn)) return false;
            using var world = SSCBondOverviewWorld.Rent();
            SSCBondOverviewLocation live = world.GetLocation(pawn);
            current = live.Kind == SSCBondOverviewLocationKind.Map ? live.Subject : live.Container;
            return current?.Spawned == true && ReferenceEquals(current.Map, Find.CurrentMap);
        }
    }
}
