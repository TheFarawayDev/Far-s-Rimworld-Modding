using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;

namespace TheGarbageCollector
{
    public class Hediff_GarbageCollectorGear : Hediff
    {
        public int arrivalTick = -1;

        public override void PostTick()
        {
            base.PostTick();

            if (arrivalTick == -1)
            {
                arrivalTick = Find.TickManager.TicksGame;
            }

            if (pawn != null && pawn.Spawned && !pawn.Dead && pawn.IsHashIntervalTick(30))
            {
                RunAuraVacuum();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref arrivalTick, "arrivalTick", -1);
        }

        private void RunAuraVacuum()
        {
            Map map = pawn.Map;
            if (map == null) return;

            var tracker = map.GetComponent<GarbageCollectorTracker>();
            if (tracker == null) return;

            GarbageFilterMode mode;
            bool pullFromStorage;
            if (!tracker.TryGetSettings(pawn, out mode, out pullFromStorage))
            {
                return;
            }

            List<Thing> targetsToDestroy = new List<Thing>();

            // Find all matching garbage targets within a 5-tile radius (radial distance check)
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, 5f, true))
            {
                if (!cell.InBounds(map)) continue;

                List<Thing> cellThings = cell.GetThingList(map);
                for (int i = cellThings.Count - 1; i >= 0; i--)
                {
                    Thing thing = cellThings[i];
                    if (thing == pawn) continue;

                    if (Alert_GarbageCollector.ShouldSkipDueToStorage(thing, map, pullFromStorage)) continue;
                    if (Alert_GarbageCollector.MatchesFilter(thing, mode))
                    {
                        targetsToDestroy.Add(thing);
                    }
                }
            }

            if (targetsToDestroy.Count > 0)
            {
                Log.Message(string.Format("[TheGarbageCollector] Debug - Vacuum destroyed {0} targets around {1} at {2}.", targetsToDestroy.Count, pawn.Name, pawn.Position));
            }

            // Perform destruction and visual effect
            foreach (Thing thing in targetsToDestroy)
            {
                if (thing.Spawned)
                {
                    // Throw visual gas or spark effect using RimWorld.FleckMaker
                    RimWorld.FleckMaker.ThrowSmoke(thing.Position.ToVector3Shifted(), map, 0.8f);
                    RimWorld.FleckMaker.ThrowMicroSparks(thing.Position.ToVector3Shifted(), map);

                    thing.Destroy(DestroyMode.Vanish);
                }
            }

            // Decontaminate polluted cells in a 5-tile radius if Biotech is active and mode is Pollution or Premium
            if (ModsConfig.BiotechActive && map.pollutionGrid != null && (mode == GarbageFilterMode.Pollution || mode == GarbageFilterMode.Premium))
            {
                bool clearedAny = false;
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, 5f, true))
                {
                    if (cell.InBounds(map) && map.pollutionGrid.IsPolluted(cell))
                    {
                        map.pollutionGrid.SetPolluted(cell, false);
                        clearedAny = true;
                    }
                }
                if (clearedAny)
                {
                    RimWorld.FleckMaker.ThrowSmoke(pawn.Position.ToVector3Shifted(), map, 1.2f);
                }
            }
        }
    }

    public class LordJob_GarbageCollector : LordJob
    {
        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            LordToil_GarbageCollector toil = new LordToil_GarbageCollector();
            stateGraph.AddToil(toil);
            return stateGraph;
        }
    }

    public class LordToil_GarbageCollector : LordToil
    {
        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                DutyDef dutyDef = DefDatabase<DutyDef>.GetNamed("GarbageCollectorDuty", false);
                if (dutyDef != null)
                {
                    pawn.mindState.duty = new PawnDuty(dutyDef);
                }
            }
        }
    }
}
