// AI 辅助生成
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只选两人的相邻位置；职责、朝向维持及位置保存均交给原版。</summary>
    public class RitualPosition_Cosmetic : RitualPosition
    {
        public override PawnStagePosition GetCell(IntVec3 spot, Pawn pawn, LordJob_Ritual ritual)
        {
            // 原版 Notify_AddedToLord 按阶段缓存位置，此时 CurrentStage 始终为第一阶段。
            // 后续阶段直接复用首阶段缓存，不因取料、观众走动或再次寻路重新选点。
            PawnStagePosition saved = ritual.PawnPositionForStage(pawn, ritual.CurrentStage);
            if (saved != null) return saved;
            if (TryGetPositions(ritual.selectedTarget, ritual.PawnWithRole(SSCCosmeticUtility.HostRole),
                ritual.PawnWithRole(SSCCosmeticUtility.RecipientRole), out var host, out var recipient))
                return pawn == ritual.PawnWithRole(SSCCosmeticUtility.HostRole) ? host : recipient;
            Log.Error("SSC cosmetic ritual has no valid adjacent role positions.");
            return new PawnStagePosition(IntVec3.Invalid, null, Rot4.Invalid, highlight);
        }

        public static bool TryGetPositions(TargetInfo target, Pawn host, Pawn recipient,
            out PawnStagePosition hostPosition, out PawnStagePosition recipientPosition)
        {
            hostPosition = recipientPosition = null;
            if (!target.IsValid || target.Map == null || host == null || recipient == null) return false;
            Map map = target.Map;
            IntVec3 spot = target.CenterCell;
            CellRect rect = target.HasThing ? target.Thing.OccupiedRect() : CellRect.CenteredOn(spot, 0);
            var hostValid = CommonRitualCellPredicates.DefaultValidator(spot, map, host, rect);
            var recipientValid = CommonRitualCellPredicates.DefaultValidator(spot, map, recipient, rect);
            // 与原版 Down 观众区域使用同一旋转；无建筑目标时默认朝南。
            Rot4 facing = new Rot4((2 + (target.HasThing ? target.Thing.Rotation.AsInt : 0)) % 4);
            IntVec3 forward = facing.FacingCell;
            IntVec3 side = new Rot4((facing.AsInt + 1) % 4).FacingCell;
            IntVec3 edge = spot;
            if (forward.x != 0) edge.x = forward.x > 0 ? rect.maxX : rect.minX;
            else edge.z = forward.z > 0 ? rect.maxZ : rect.minZ;

            // 固定次序且不随机；同一初始化中两名角色分别查询也会取得同一对位置。
            for (int distance = 1; distance <= 2; distance++)
                for (int offset = 0; offset < 5; offset++)
                {
                    int lateral = offset == 0 ? 0 : (offset + 1) / 2 * (offset % 2 == 1 ? 1 : -1);
                    IntVec3 cell = edge + forward * distance + side * lateral;
                    if (!cell.InBounds(map) || !recipientValid(cell)) continue;
                    for (int sign = 1; sign >= -1; sign -= 2)
                    {
                        IntVec3 hostCell = cell + side * sign;
                        if (!hostCell.InBounds(map) || !hostValid(hostCell)) continue;
                        hostPosition = new PawnStagePosition(hostCell, null, Rot4.FromIntVec3(cell - hostCell), false);
                        recipientPosition = new PawnStagePosition(cell, null, facing, false);
                        return true;
                    }
                }
            return false;
        }
    }
}
