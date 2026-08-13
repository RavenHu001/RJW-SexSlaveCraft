using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public class SSC_HealthSanitizerGameComponent : GameComponent
    {
        private const int ScanIntervalTicks = 600;
        private int nextScanTick;

        public SSC_HealthSanitizerGameComponent(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            nextScanTick = Find.TickManager.TicksGame + 120;
            SanitizeAllPlayerPawns();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            nextScanTick = Find.TickManager.TicksGame + 120;
            SanitizeAllPlayerPawns();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now < nextScanTick)
            {
                return;
            }

            nextScanTick = now + ScanIntervalTicks;
            SanitizeAllPlayerPawns();
        }

        private static void SanitizeAllPlayerPawns()
        {
            if (Current.Game == null)
            {
                return;
            }

            List<Map> maps = Find.Maps;
            if (maps == null)
            {
                return;
            }

            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                if (map?.mapPawns?.AllPawnsSpawned == null)
                {
                    continue;
                }

                var pawns = map.mapPawns.AllPawnsSpawned;
                for (int j = 0; j < pawns.Count; j++)
                {
                    Pawn pawn = pawns[j];
                    if (pawn == null || pawn.health == null)
                    {
                        continue;
                    }

                    if (pawn.Faction != Faction.OfPlayer && !pawn.IsPrisonerOfColony && !pawn.IsSlaveOfColony)
                    {
                        continue;
                    }

                    if (GelatinizationHealthSanitizer.SanitizeMissingPartInjuries(pawn))
                    {
                        pawn.health.hediffSet.DirtyCache();
                    }
                }
            }
        }
    }
}
