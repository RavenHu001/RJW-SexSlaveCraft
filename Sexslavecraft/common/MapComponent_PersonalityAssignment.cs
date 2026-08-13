using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

// EN: This map component stores the live assignment between personality gel and hollow pawns.
// EN: It guarantees one gel per hollow pawn, cleans dead references over time, and gives WorkGivers / ITabs one place to query insertion targets.
// CN: 这个地图组件负责保存“人格凝胶 -> 空壳 Pawn”的实时分配关系。
// CN: 它保证一个凝胶只对应一个空壳、定期清理失效引用，并给 WorkGiver / ITab 提供统一的查询入口。
namespace SexSlaveCraft
{
    public class MapComponent_PersonalityAssignment : MapComponent
    {
        // EN: Key = personality gel, Value = target hollow pawn.
        // CN: Key = 人格凝胶，Value = 目标空壳 Pawn。
        private Dictionary<Thing, Pawn> assignments = new Dictionary<Thing, Pawn>();

        private int cleanupTick = 0;
        private const int CleanupInterval = 2500; // 约1游戏小时清理一次

        public MapComponent_PersonalityAssignment(Map map) : base(map) { }

        // EN: Step 1: assignment operations keep the one-gel / one-hollow relationship intact.
        // CN: 步骤 1：分配操作必须一直维持“一份凝胶 / 一个空壳”的关系。
        public void Assign(Thing gel, Pawn hollow)
        {
            if (gel == null || hollow == null) return;

            // EN: Remove the gel's old target first.
            // CN: 先移除这份凝胶原来对应的空壳。
            if (assignments.ContainsKey(gel))
                assignments.Remove(gel);

            // EN: Then remove any old gel assigned to the same hollow pawn.
            // CN: 然后再移除这个空壳 Pawn 先前被分配的旧凝胶。
            Thing oldGel = assignments.FirstOrDefault(x => x.Value == hollow).Key;
            if (oldGel != null)
                assignments.Remove(oldGel);

            assignments[gel] = hollow;
        }

        public void Unassign(Thing gel)
        {
            // EN: Removing by gel is the usual path when personality gel gets consumed, destroyed, or manually reassigned.
            // CN: 按凝胶取消分配，是人格凝胶被消耗、销毁或重新指定时最常见的路径。
            if (gel != null)
                assignments.Remove(gel);
        }

        public void UnassignByPawn(Pawn hollow)
        {
            // EN: Removing by hollow pawn is the usual path when the body is no longer a valid insertion target.
            // CN: 按空壳 Pawn 取消分配，通常发生在这个身体已经不再适合作为植入目标时。
            if (hollow == null) return;
            Thing key = assignments.FirstOrDefault(x => x.Value == hollow).Key;
            if (key != null)
                assignments.Remove(key);
        }

        // EN: Step 2: query helpers let ITabs and WorkGivers ask either side of the gel / hollow mapping.
        // CN: 步骤 2：查询辅助方法让 ITab 和 WorkGiver 都能从凝胶或空壳任意一侧反查映射关系。
        public Pawn GetAssignedTarget(Thing gel)
        {
            // EN: Query from the gel side when UI or hauling logic wants to know which hollow pawn this gel belongs to.
            // CN: 从凝胶这一侧查询，通常用于 UI 或搬运逻辑判断这份凝胶归哪个空壳 Pawn。
            if (gel != null && assignments.TryGetValue(gel, out Pawn p))
                return p;
            return null;
        }

        public Thing GetAssignedGel(Pawn hollow)
        {
            // EN: Query from the hollow-pawn side when insertion logic needs to locate the assigned personality gel.
            // CN: 从空壳 Pawn 这一侧查询，通常用于人格植入逻辑寻找对应的人格凝胶。
            if (hollow == null) return null;
            foreach (var kvp in assignments)
            {
                if (kvp.Value == hollow)
                    return kvp.Key;
            }
            return null;
        }

        public bool HasAssignment(Thing gel)
        {
            return gel != null && assignments.ContainsKey(gel);
        }

        public bool PawnHasAssignment(Pawn hollow)
        {
            return GetAssignedGel(hollow) != null;
        }

        public IEnumerable<KeyValuePair<Thing, Pawn>> AllAssignments => assignments;

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            cleanupTick++;
            if (cleanupTick < CleanupInterval) return;
            cleanupTick = 0;

            // EN: Step 3: periodically remove dead gels, dead pawns, and pawns that are no longer hollow.
            // CN: 步骤 3：定期清理失效凝胶、死亡 Pawn，以及那些已经不再是空壳的 Pawn。
            List<Thing> toRemove = null;
            foreach (var kvp in assignments)
            {
                bool invalid = false;

                // EN: Personality gel no longer exists on the map.
                // CN: 人格凝胶已经不再有效存在于地图上。
                if (kvp.Key == null || kvp.Key.Destroyed || !kvp.Key.Spawned)
                    invalid = true;

                // EN: Hollow pawn is dead or gone.
                // CN: 空壳 Pawn 已经死亡或不在地图上。
                if (kvp.Value == null || kvp.Value.Dead || !kvp.Value.Spawned)
                    invalid = true;

                // EN: Pawn is no longer in the `人格排泄(完成)` state, so this assignment is stale.
                // CN: Pawn 已经不再处于“人格排泄(完成)”状态，所以这条分配关系已经过期。
                if (!invalid && !kvp.Value.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done))
                    invalid = true;

                if (invalid)
                {
                    if (toRemove == null) toRemove = new List<Thing>();
                    toRemove.Add(kvp.Key);
                }
            }

            if (toRemove != null)
            {
                foreach (var key in toRemove)
                    assignments.Remove(key);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // EN: Save the gel -> hollow-pawn mapping by references, because both ends are live world objects.
            // CN: 用引用模式保存“凝胶 -> 空壳 Pawn”映射，因为两边都是地图上的活对象。
            Scribe_Collections.Look(ref assignments, "personalityAssignments",
                LookMode.Reference, LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (assignments == null)
                    assignments = new Dictionary<Thing, Pawn>();

                // EN: Post-load repair removes null references left by destroyed gels or missing pawns.
                // CN: 读档修复阶段会清理掉那些由被销毁凝胶或缺失 Pawn 留下的空引用。
                var nullKeys = assignments.Where(x => x.Key == null || x.Value == null)
                    .Select(x => x.Key).ToList();
                foreach (var key in nullKeys)
                    assignments.Remove(key);
            }
        }
    }
}
