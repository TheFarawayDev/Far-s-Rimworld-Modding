using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;
using HarmonyLib;
using UnityEngine;

namespace FarUtils
{
    // ==========================================
    // 1. DYNAMIC ALERT MANAGER
    // ==========================================
    public abstract class Alert_ConditionalUtility : Alert
    {
        private int lastScanTick = -1;
        private AlertReport cachedReport = AlertReport.Inactive;

        protected abstract int TargetThreshold { get; }

        protected abstract int GetTargetCount(Map map);

        public virtual IEnumerable<Thing> GetCulprits(Map map)
        {
            return null;
        }

        public override AlertReport GetReport()
        {
            int currentTick = Find.TickManager.TicksGame;
            if (lastScanTick == -1 || currentTick - lastScanTick >= 150)
            {
                lastScanTick = currentTick;
                int totalCount = 0;
                List<Thing> allCulprits = new List<Thing>();

                foreach (Map map in Find.Maps)
                {
                    totalCount += GetTargetCount(map);
                    IEnumerable<Thing> culprits = GetCulprits(map);
                    if (culprits != null)
                    {
                        allCulprits.AddRange(culprits);
                    }
                }

                if (totalCount > TargetThreshold)
                {
                    if (allCulprits.Count > 0)
                    {
                        cachedReport = AlertReport.CulpritsAre(allCulprits);
                    }
                    else
                    {
                        cachedReport = AlertReport.Active;
                    }
                }
                else
                {
                    cachedReport = AlertReport.Inactive;
                }
            }
            return cachedReport;
        }
    }

    // ==========================================
    // 2. PERSISTENT MAP COMPONENT
    // ==========================================
    public class FarUtilsMapComponent : MapComponent
    {
        private List<Pawn> workers = new List<Pawn>();
        private List<int> chargedCounts = new List<int>();
        private List<int> creditedCounts = new List<int>();

        public FarUtilsMapComponent(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref workers, "workers", LookMode.Reference);
            Scribe_Collections.Look(ref chargedCounts, "chargedCounts", LookMode.Value);
            Scribe_Collections.Look(ref creditedCounts, "creditedCounts", LookMode.Value);

            if (workers == null) workers = new List<Pawn>();
            if (chargedCounts == null) chargedCounts = new List<int>();
            if (creditedCounts == null) creditedCounts = new List<int>();
        }

        public void IncrementCharged(Pawn worker)
        {
            int index = workers.IndexOf(worker);
            if (index < 0)
            {
                workers.Add(worker);
                chargedCounts.Add(1);
                creditedCounts.Add(0);
            }
            else
            {
                chargedCounts[index]++;
            }
        }

        public void IncrementCredited(Pawn worker)
        {
            int index = workers.IndexOf(worker);
            if (index < 0)
            {
                workers.Add(worker);
                chargedCounts.Add(0);
                creditedCounts.Add(1);
            }
            else
            {
                creditedCounts[index]++;
            }
        }

        public void GetCounts(Pawn worker, out int charged, out int credited)
        {
            int index = workers.IndexOf(worker);
            if (index >= 0)
            {
                charged = chargedCounts[index];
                credited = creditedCounts[index];
            }
            else
            {
                charged = 0;
                credited = 0;
            }
        }

        public void ClearWorker(Pawn worker)
        {
            int index = workers.IndexOf(worker);
            if (index >= 0)
            {
                workers.RemoveAt(index);
                chargedCounts.RemoveAt(index);
                creditedCounts.RemoveAt(index);
            }
        }
    }

    // ==========================================
    // 3. WORK ECONOMY TRACKER
    // ==========================================
    public static class WorkEconomyTracker
    {
        public static void RegisterItemProcessed(Pawn worker, ThingDef itemDef)
        {
            if (worker == null || itemDef == null || worker.Map == null) return;

            var comp = worker.Map.GetComponent<FarUtilsMapComponent>();
            if (comp == null)
            {
                comp = new FarUtilsMapComponent(worker.Map);
                worker.Map.components.Add(comp);
            }

            bool isCredited = itemDef.defName.Contains("ChunkMechanoid") || 
                              itemDef.defName == "MechanoidScrap" || 
                              itemDef.defName == "MechScrap" || 
                              (itemDef.defName.StartsWith("Chunk") && itemDef.defName.Contains("Mech"));

            if (isCredited)
            {
                comp.IncrementCredited(worker);
            }
            else
            {
                comp.IncrementCharged(worker);
            }
        }

        public static void SettleInvoiceOnExit(Pawn worker, Map map, int costPerUnit, int creditPerUnit, int baseFee)
        {
            if (worker == null || map == null) return;

            var comp = map.GetComponent<FarUtilsMapComponent>();
            int itemsCharged = 0;
            int itemsCredited = 0;
            if (comp != null)
            {
                comp.GetCounts(worker, out itemsCharged, out itemsCredited);
                comp.ClearWorker(worker);
            }

            int finalBill = baseFee + (itemsCharged * costPerUnit) - (itemsCredited * creditPerUnit);
            Log.Message(string.Format("[FarUtils] SettleInvoiceOnExit for {0}: BaseFee={1}, Charged={2} (cost={3}), Credited={4} (credit={5}). Final={6}", worker.LabelShort, baseFee, itemsCharged, costPerUnit, itemsCredited, creditPerUnit, finalBill));

            if (finalBill > 0)
            {
                int remaining = finalBill;
                List<Thing> silverThings = new List<Thing>();
                foreach (Thing thing in map.listerThings.ThingsOfDef(ThingDefOf.Silver))
                {
                    if (thing.Faction == Faction.OfPlayer || thing.Faction == null)
                    {
                        IntVec3 cell = thing.Position;
                        Zone zone = map.zoneManager.ZoneAt(cell);
                        bool inStockpile = zone != null && zone is Zone_Stockpile;
                        bool inShelf = cell.GetFirstBuilding(map) is Building_Storage;
                        if (inStockpile || inShelf)
                        {
                            silverThings.Add(thing);
                        }
                    }
                }

                for (int i = silverThings.Count - 1; i >= 0 && remaining > 0; i--)
                {
                    Thing silver = silverThings[i];
                    if (silver.stackCount <= remaining)
                    {
                        remaining -= silver.stackCount;
                        silver.Destroy(DestroyMode.Vanish);
                    }
                    else
                    {
                        silver.stackCount -= remaining;
                        remaining = 0;
                    }
                }

                if (remaining > 0)
                {
                    Messages.Message(string.Format("Colony was charged {0} silver for contractor services, but could only pay {1} silver. Remaining debt: {2}.", finalBill, finalBill - remaining, remaining), MessageTypeDefOf.CautionInput);
                }
                else
                {
                    Messages.Message(string.Format("Colony paid {0} silver for contractor services.", finalBill), MessageTypeDefOf.PositiveEvent);
                }
            }
            else if (finalBill < 0)
            {
                int silverToSpawn = -finalBill;
                IntVec3 spawnCell = worker.Position;
                if (!spawnCell.IsValid || !spawnCell.InBounds(map))
                {
                    CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map), map, CellFinder.EdgeRoadChance_Neutral, out spawnCell);
                }

                int remainingToSpawn = silverToSpawn;
                while (remainingToSpawn > 0)
                {
                    int amountThisStack = Math.Min(remainingToSpawn, ThingDefOf.Silver.stackLimit);
                    Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                    silver.stackCount = amountThisStack;
                    GenSpawn.Spawn(silver, spawnCell, map, WipeMode.Vanish);
                    remainingToSpawn -= amountThisStack;
                }

                Messages.Message(string.Format("Colony earned {0} silver profit from contractor salvage services. Silver spawned at the map edge.", silverToSpawn), MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message("Contractor departed. Net cost was 0 silver.", MessageTypeDefOf.NeutralEvent);
            }
        }
    }

    // ==========================================
    // 4. ANIMAL/CREATURE SUMMONER
    // ==========================================
    public static class AnimalSummoner
    {
        public static bool IsFarUtilsWorker(Pawn pawn)
        {
            if (pawn == null) return false;
            HediffDef speedBuffDef = DefDatabase<HediffDef>.GetNamed("FarUtils_WorkerBuff", false);
            return speedBuffDef != null && pawn.health.hediffSet.HasHediff(speedBuffDef);
        }

        public static void SpawnCreatureWorker(Map map, PawnKindDef animalKind, DutyDef duty, string arrivalMessage)
        {
            if (map == null || animalKind == null)
            {
                Log.Warning("[FarUtils] Cannot spawn creature worker: Map or PawnKindDef is null.");
                return;
            }

            IntVec3 spawnCell;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out spawnCell, map, CellFinder.EdgeRoadChance_Neutral))
            {
                if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map), map, CellFinder.EdgeRoadChance_Neutral, out spawnCell))
                {
                    Log.Warning("[FarUtils] Could not find spawn cell for worker.");
                    return;
                }
            }

            Faction faction = Find.FactionManager.RandomNonHostileFaction(false, false, false, TechLevel.Undefined);
            if (faction == null)
            {
                faction = Find.FactionManager.FirstFactionOfDef(FactionDefOf.OutlanderCivil) ?? Find.FactionManager.OfAncients;
            }
            if (faction == null)
            {
                faction = Find.FactionManager.OfPlayer;
            }

            Pawn pawn = PawnGenerator.GeneratePawn(animalKind, faction);
            if (pawn == null)
            {
                Log.Warning("[FarUtils] Failed to generate worker pawn.");
                return;
            }

            // Customize humanoid naming if the mod kind is a custom human collector
            if (animalKind.defName == "GarbageCollector")
            {
                pawn.Name = new NameTriple("Scruffy", "Garbage Collector", "Sanitation");
            }

            // Apply movement speed buff hediff
            HediffDef speedBuffDef = DefDatabase<HediffDef>.GetNamed("FarUtils_WorkerBuff", false);
            if (speedBuffDef != null)
            {
                pawn.health.AddHediff(speedBuffDef);
            }

            // Dynamically add GarbageCollectorGear if it exists
            HediffDef gcGearDef = DefDatabase<HediffDef>.GetNamed("GarbageCollectorGear", false);
            if (gcGearDef != null)
            {
                Hediff addedHediff = pawn.health.AddHediff(gcGearDef);
                // Also set the arrival tick if the gear contains that field
                if (addedHediff != null)
                {
                    var field = addedHediff.GetType().GetField("arrivalTick");
                    if (field != null)
                    {
                        field.SetValue(addedHediff, Find.TickManager.TicksGame);
                    }
                }
            }

            // Spawn the pawn
            GenSpawn.Spawn(pawn, spawnCell, map, WipeMode.Vanish);

            // Create a Lord to process the duty
            if (duty != null)
            {
                LordMaker.MakeNewLord(faction, new LordJob_FarUtilsWorker(duty), map, new Pawn[] { pawn });
            }

            // Throw dust puffs at spawn point
            for (int i = 0; i < 5; i++)
            {
                FleckMaker.ThrowDustPuff(spawnCell.ToVector3Shifted() + new Vector3(Rand.Range(-0.5f, 0.5f), 0, Rand.Range(-0.5f, 0.5f)), map, 1.5f);
            }

            // Camera jump
            CameraJumper.TryJump(pawn);

            // Display message
            if (!arrivalMessage.NullOrEmpty())
            {
                Messages.Message(arrivalMessage, pawn, MessageTypeDefOf.PositiveEvent);
            }
        }
    }

    // ==========================================
    // 5. LORDJOB & LORDTOIL TO ENFORCE DUTY
    // ==========================================
    public class LordJob_FarUtilsWorker : LordJob
    {
        private DutyDef duty;

        public LordJob_FarUtilsWorker()
        {
        }

        public LordJob_FarUtilsWorker(DutyDef duty)
        {
            this.duty = duty;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            LordToil_FarUtilsWorker toil = new LordToil_FarUtilsWorker(duty);
            stateGraph.AddToil(toil);
            return stateGraph;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref duty, "duty");
        }
    }

    public class LordToil_FarUtilsWorker : LordToil
    {
        private DutyDef duty;

        public LordToil_FarUtilsWorker()
        {
        }

        public LordToil_FarUtilsWorker(DutyDef duty)
        {
            this.duty = duty;
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                pawn.mindState.duty = new PawnDuty(duty);
            }
        }

    }

    // ==========================================
    // 6. HARMONY PATCHES FOR SYSTEM BEHAVIORS
    // ==========================================
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            try
            {
                var harmony = new Harmony("thefarawaydev.farutils");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Message("[FarUtils] Successfully initialized Harmony patches.");
            }
            catch (Exception ex)
            {
                Log.Error("[FarUtils] Critical patch initialization error: " + ex);
            }
        }
    }

    // Non-hostile patches (Thing vs Thing)
    [HarmonyPatch(typeof(GenHostility), "HostileTo", new Type[] { typeof(Thing), typeof(Thing) })]
    public static class Patch_GenHostility_HostileTo_Thing_Thing
    {
        public static bool Prefix(Thing a, Thing b, ref bool __result)
        {
            Pawn pawnA = a as Pawn;
            Pawn pawnB = b as Pawn;
            if ((pawnA != null && AnimalSummoner.IsFarUtilsWorker(pawnA)) ||
                (pawnB != null && AnimalSummoner.IsFarUtilsWorker(pawnB)))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    // Non-hostile patches (Thing vs Faction)
    [HarmonyPatch(typeof(GenHostility), "HostileTo", new Type[] { typeof(Thing), typeof(Faction) })]
    public static class Patch_GenHostility_HostileTo_Thing_Faction
    {
        public static bool Prefix(Thing t, Faction fac, ref bool __result)
        {
            Pawn pawn = t as Pawn;
            if (pawn != null && AnimalSummoner.IsFarUtilsWorker(pawn))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    // Non-tamable patch (Designator_Tame)
    [HarmonyPatch(typeof(Designator_Tame), "CanDesignateThing")]
    public static class Patch_Designator_Tame_CanDesignateThing
    {
        public static bool Prefix(Thing t, ref AcceptanceReport __result)
        {
            Pawn pawn = t as Pawn;
            if (pawn != null && AnimalSummoner.IsFarUtilsWorker(pawn))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    // ==========================================
    // 7. MULTI-MAP SILVER UTILITIES
    // ==========================================
    public static class SilverHelper
    {
        public static int GetAvailableSilverOnMap(Map map)
        {
            if (map == null) return 0;
            int count = 0;
            foreach (Thing thing in map.listerThings.ThingsOfDef(ThingDefOf.Silver))
            {
                if (thing.Faction == Faction.OfPlayer || thing.Faction == null)
                {
                    IntVec3 cell = thing.Position;
                    Zone zone = map.zoneManager.ZoneAt(cell);
                    bool inStockpile = zone != null && zone is Zone_Stockpile;
                    bool inShelf = cell.GetFirstBuilding(map) is Building_Storage;
                    if (inStockpile || inShelf)
                    {
                        count += thing.stackCount;
                    }
                }
            }
            return count;
        }

        public static int GetTotalAvailableSilver()
        {
            int total = 0;
            foreach (Map map in Find.Maps)
            {
                total += GetAvailableSilverOnMap(map);
            }
            return total;
        }

        public static void DeductSilver(int amount, Map preferMap = null)
        {
            int remaining = amount;
            
            // 1. Try to deduct from preferred map first
            if (preferMap != null)
            {
                remaining = DeductFromMap(preferMap, remaining);
            }

            // 2. Deduct from other maps if needed
            if (remaining > 0)
            {
                foreach (Map map in Find.Maps)
                {
                    if (map == preferMap) continue;
                    remaining = DeductFromMap(map, remaining);
                    if (remaining <= 0) break;
                }
            }

            if (remaining > 0)
            {
                Log.Warning("[FarUtils.SilverHelper] Could not deduct full silver amount. Remaining: " + remaining);
            }
        }

        private static int DeductFromMap(Map map, int amount)
        {
            List<Thing> silverThings = new List<Thing>();
            foreach (Thing thing in map.listerThings.ThingsOfDef(ThingDefOf.Silver))
            {
                if (thing.Faction == Faction.OfPlayer || thing.Faction == null)
                {
                    IntVec3 cell = thing.Position;
                    Zone zone = map.zoneManager.ZoneAt(cell);
                    bool inStockpile = zone != null && zone is Zone_Stockpile;
                    bool inShelf = cell.GetFirstBuilding(map) is Building_Storage;
                    if (inStockpile || inShelf)
                    {
                        silverThings.Add(thing);
                    }
                }
            }

            int remaining = amount;
            for (int i = silverThings.Count - 1; i >= 0 && remaining > 0; i--)
            {
                Thing silver = silverThings[i];
                if (silver.stackCount <= remaining)
                {
                    remaining -= silver.stackCount;
                    silver.Destroy(DestroyMode.Vanish);
                }
                else
                {
                    silver.stackCount -= remaining;
                    remaining = 0;
                }
            }
            return remaining;
        }
    }

    // ==========================================
    // 8. DOOR ACCESS HOOK (DEEP STRUCTURE & SILO ACCESS)
    // ==========================================
    [HarmonyPatch(typeof(Building_Door), "PawnCanOpen")]
    public static class Patch_Building_Door_PawnCanOpen
    {
        public static bool Prefix(Pawn p, ref bool __result)
        {
            if (p != null && FarUtils.AnimalSummoner.IsFarUtilsWorker(p))
            {
                __result = true;
                return false; // Skip original check and allow opening
            }
            return true;
        }
    }
}
