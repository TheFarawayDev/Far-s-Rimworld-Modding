using System;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using HarmonyLib;
using UnityEngine;

namespace TheGarbageCollector
{
    public static class GarbageCollectorSummoner
    {
        public static bool IsGarbageCollectorWorker(Pawn pawn)
        {
            if (pawn == null) return false;
            return pawn.kindDef != null && pawn.kindDef.defName == "GarbageCollector";
        }

        public static void SpawnCreatureWorker(Map map, PawnKindDef animalKind, DutyDef duty, string arrivalMessage)
        {
            if (map == null || animalKind == null)
            {
                Log.Warning("[TheGarbageCollector] Cannot spawn creature worker: Map or PawnKindDef is null.");
                return;
            }

            IntVec3 spawnCell;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out spawnCell, map, CellFinder.EdgeRoadChance_Neutral))
            {
                if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map), map, CellFinder.EdgeRoadChance_Neutral, out spawnCell))
                {
                    Log.Warning("[TheGarbageCollector] Could not find spawn cell for worker.");
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
                Log.Warning("[TheGarbageCollector] Failed to generate worker pawn.");
                return;
            }

            if (animalKind.defName == "GarbageCollector")
            {
                pawn.Name = new NameTriple("Scruffy", "Garbage Collector", "Sanitation");
            }

            HediffDef speedBuffDef = DefDatabase<HediffDef>.GetNamed("FarUtils_WorkerBuff", false);
            if (speedBuffDef == null)
                speedBuffDef = DefDatabase<HediffDef>.GetNamed("GarbageCollector_WorkerBuff", false);
            
            if (speedBuffDef != null)
            {
                pawn.health.AddHediff(speedBuffDef);
            }

            HediffDef gcGearDef = DefDatabase<HediffDef>.GetNamed("GarbageCollectorGear", false);
            if (gcGearDef != null)
            {
                Hediff addedHediff = pawn.health.AddHediff(gcGearDef);
                if (addedHediff != null)
                {
                    var field = addedHediff.GetType().GetField("arrivalTick");
                    if (field != null)
                    {
                        field.SetValue(addedHediff, Find.TickManager.TicksGame);
                    }
                }
            }

            GenSpawn.Spawn(pawn, spawnCell, map, WipeMode.Vanish);

            if (duty != null)
            {
                LordMaker.MakeNewLord(faction, new LordJob_GarbageCollectorWorker(duty), map, new Pawn[] { pawn });
            }

            for (int i = 0; i < 5; i++)
            {
                FleckMaker.ThrowDustPuff(spawnCell.ToVector3Shifted() + new Vector3(Rand.Range(-0.5f, 0.5f), 0, Rand.Range(-0.5f, 0.5f)), map, 1.5f);
            }

            CameraJumper.TryJump(pawn);

            if (!arrivalMessage.NullOrEmpty())
            {
                Messages.Message(arrivalMessage, pawn, MessageTypeDefOf.PositiveEvent);
            }
        }
    }

    public class LordJob_GarbageCollectorWorker : LordJob
    {
        private DutyDef duty;

        public LordJob_GarbageCollectorWorker() { }

        public LordJob_GarbageCollectorWorker(DutyDef duty)
        {
            this.duty = duty;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            LordToil_GarbageCollectorWorker toil = new LordToil_GarbageCollectorWorker(duty);
            stateGraph.AddToil(toil);
            return stateGraph;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref duty, "duty");
        }
    }

    public class LordToil_GarbageCollectorWorker : LordToil
    {
        private DutyDef duty;

        public LordToil_GarbageCollectorWorker() { }

        public LordToil_GarbageCollectorWorker(DutyDef duty)
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

}
