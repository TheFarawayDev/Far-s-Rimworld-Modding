using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;

namespace TheGarbageCollector
{
    public class JobGiver_GarbageCollectorAuraClean : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            Map map = pawn.Map;
            if (map == null) return null;

            var tracker = map.GetComponent<GarbageCollectorTracker>();
            if (tracker == null) return null;

            GarbageFilterMode mode;
            bool pullFromStorage;
            if (!tracker.TryGetSettings(pawn, out mode, out pullFromStorage))
            {
                Log.Message(string.Format("[TheGarbageCollector] Debug - TryGiveJob for pawn {0} ({1}): Settings not found in tracker.", pawn.Name, pawn.thingIDNumber));
                return null;
            }

            LocalTargetInfo target = LocalTargetInfo.Invalid;

            if (mode == GarbageFilterMode.Pollution)
            {
                Thing wastepack = FindClosestGarbage(pawn, mode, pullFromStorage);
                if (wastepack != null)
                {
                    target = wastepack;
                }
                else
                {
                    IntVec3 cell = FindClosestPollutedCell(pawn);
                    if (cell.IsValid)
                    {
                        target = cell;
                    }
                }
            }
            else
            {
                Thing garbage = FindClosestGarbage(pawn, mode, pullFromStorage);
                if (garbage != null)
                {
                    target = garbage;
                }
            }

            if (target.IsValid)
            {
                Log.Message(string.Format("[TheGarbageCollector] Debug - TryGiveJob for pawn {0} ({1}): Found target at {2}.", pawn.Name, pawn.thingIDNumber, target.Cell));
                Job job = JobMaker.MakeJob(JobDefOf.Goto, target);
                job.expiryInterval = 240; // Re-evaluate pathing every 4 seconds
                return job;
            }

            // If no garbage found, wait/wander for up to 5000 ticks from arrival
            HediffDef gearDef = DefDatabase<HediffDef>.GetNamed("GarbageCollectorGear", false);
            if (gearDef != null)
            {
                var gear = pawn.health.hediffSet.GetFirstHediffOfDef(gearDef) as Hediff_GarbageCollectorGear;
                if (gear != null)
                {
                    int elapsed = Find.TickManager.TicksGame - gear.arrivalTick;
                    Log.Message(string.Format("[TheGarbageCollector] Debug - TryGiveJob for pawn {0} ({1}): No garbage found. Ticks since arrival: {2}/5000. Wait/Wander fallback.", pawn.Name, pawn.thingIDNumber, elapsed));
                    if (gear.arrivalTick == -1 || elapsed < 5000)
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.Wait);
                        job.expiryInterval = 120; // Wait 2 seconds and re-evaluate
                        return job;
                    }
                }
                else
                {
                    Log.Message(string.Format("[TheGarbageCollector] Debug - TryGiveJob for pawn {0} ({1}): Hediff_GarbageCollectorGear not found on pawn!", pawn.Name, pawn.thingIDNumber));
                }
            }
            else
            {
                Log.Message(string.Format("[TheGarbageCollector] Debug - TryGiveJob for pawn {0} ({1}): GarbageCollectorGear HediffDef not found in database!", pawn.Name, pawn.thingIDNumber));
            }

            Log.Message(string.Format("[TheGarbageCollector] Debug - TryGiveJob for pawn {0} ({1}): Exiting map.", pawn.Name, pawn.thingIDNumber));
            return null;
        }

        private IntVec3 FindClosestPollutedCell(Pawn pawn)
        {
            Map map = pawn.Map;
            if (map == null || map.pollutionGrid == null || !ModsConfig.BiotechActive) return IntVec3.Invalid;

            Queue<IntVec3> queue = new Queue<IntVec3>();
            HashSet<IntVec3> visited = new HashSet<IntVec3>();

            queue.Enqueue(pawn.Position);
            visited.Add(pawn.Position);

            int cellsProcessed = 0;
            while (queue.Count > 0 && cellsProcessed < 10000)
            {
                IntVec3 current = queue.Dequeue();
                cellsProcessed++;

                if (map.pollutionGrid.IsPolluted(current))
                {
                    if (pawn.CanReach(current, PathEndMode.Touch, Danger.Deadly))
                    {
                        return current;
                    }
                }

                for (int i = 0; i < 4; i++)
                {
                    IntVec3 neighbor = current + GenAdj.CardinalDirections[i];
                    if (neighbor.InBounds(map) && !visited.Contains(neighbor) && neighbor.Walkable(map))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return IntVec3.Invalid;
        }

        private Thing FindClosestGarbage(Pawn pawn, GarbageFilterMode mode, bool pullFromStorage)
        {
            Map map = pawn.Map;
            Thing closest = null;
            float minDistance = float.MaxValue;
            IntVec3 pos = pawn.Position;

            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (!thing.Spawned) continue;
                if (Alert_GarbageCollector.ShouldSkipDueToStorage(thing, map, pullFromStorage)) continue;
                if (!Alert_GarbageCollector.MatchesFilter(thing, mode)) continue;

                // Ensure the pawn can actually reach the thing
                if (!pawn.CanReach(thing, PathEndMode.Touch, Danger.Deadly)) continue;

                float dist = pos.DistanceToSquared(thing.Position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = thing;
                }
            }

            return closest;
        }
    }
}
