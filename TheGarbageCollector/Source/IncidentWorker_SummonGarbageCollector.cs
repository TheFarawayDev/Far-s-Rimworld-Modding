using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;

namespace TheGarbageCollector
{
    public class IncidentWorker_SummonGarbageCollector : IncidentWorker
    {
        public static GarbageFilterMode currentSummonMode;
        public static bool currentSummonPullFromStorage;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            return TryExecuteWithParams(map, GarbageFilterMode.Premium, false, 150);
        }

        public bool TryExecuteWithParams(Map map, GarbageFilterMode mode, bool pullFromStorage, int silverCost)
        {
            Log.Message(string.Format("[TheGarbageCollector] Debug - TryExecuteWithParams start. Mode: {0}, PullFromStorage: {1}", mode, pullFromStorage));
            PawnKindDef pawnKind = DefDatabase<PawnKindDef>.GetNamed("GarbageCollector", false);
            if (pawnKind == null)
            {
                Log.Warning("[TheGarbageCollector] Debug - PawnKindDef 'GarbageCollector' not found!");
                return false;
            }

            DutyDef dutyDef = DefDatabase<DutyDef>.GetNamed("GarbageCollectorDuty", false);
            string arrivalMessage = "Specialized Garbage Collector has arrived on the map edge.";

            currentSummonMode = mode;
            currentSummonPullFromStorage = pullFromStorage;

            FarUtils.AnimalSummoner.SpawnCreatureWorker(map, pawnKind, dutyDef, arrivalMessage);

            Pawn spawnedPawn = null;
            foreach (Pawn p in map.mapPawns.AllPawns)
            {
                if (p.kindDef == pawnKind && !p.Dead)
                {
                    spawnedPawn = p;
                    break;
                }
            }

            // Notify player with a letter
            try
            {
                Find.LetterStack.ReceiveLetter(
                    "Contractor Arrived",
                    string.Format("A specialized Garbage Collector has arrived to clean up targeted debris. Contract cost: {0} silver.", silverCost),
                    LetterDefOf.PositiveEvent,
                    spawnedPawn != null ? new LookTargets(spawnedPawn) : null
                );
            }
            catch (Exception ex)
            {
                Log.Warning("[TheGarbageCollector] Debug - ReceiveLetter failed: " + ex);
            }

            return true;
        }
    }

    public class GarbageCollectorTracker : MapComponent
    {
        public List<Pawn> collectors = new List<Pawn>();
        public List<GarbageFilterMode> filterModes = new List<GarbageFilterMode>();
        public List<bool> pullFromStorages = new List<bool>();

        public GarbageCollectorTracker(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref collectors, "collectors", LookMode.Reference);
            Scribe_Collections.Look(ref filterModes, "filterModes", LookMode.Value);
            Scribe_Collections.Look(ref pullFromStorages, "pullFromStorages", LookMode.Value);

            // Ensure lists are initialized on load in case they were empty
            if (collectors == null) collectors = new List<Pawn>();
            if (filterModes == null) filterModes = new List<GarbageFilterMode>();
            if (pullFromStorages == null) pullFromStorages = new List<bool>();
        }

        public void Register(Pawn pawn, GarbageFilterMode mode, bool pullStorage)
        {
            collectors.Add(pawn);
            filterModes.Add(mode);
            pullFromStorages.Add(pullStorage);
        }

        public bool TryGetSettings(Pawn pawn, out GarbageFilterMode mode, out bool pullStorage)
        {
            int index = collectors.IndexOf(pawn);
            if (index >= 0)
            {
                mode = filterModes[index];
                pullStorage = pullFromStorages[index];
                return true;
            }
            mode = GarbageFilterMode.Premium;
            pullStorage = false;
            return false;
        }
    }
}
