using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace QuarryCo
{
    public class WorkGiver_Quarry : WorkGiver_Scanner
    {
        private static JobDef quarryJobDef;

        public override ThingRequest PotentialWorkThingRequest { get { return ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial); } }

        public override PathEndMode PathEndMode { get { return PathEndMode.Touch; } }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (var building in pawn.Map.listerBuildings.allBuildingsColonist)
            {
                if (building is Building_Quarry)
                    yield return building;
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            foreach (var building in pawn.Map.listerBuildings.allBuildingsColonist)
            {
                var quarry = building as Building_Quarry;
                if (quarry != null && quarry.IsActive)
                    return false;
            }
            return true;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var quarry = t as Building_Quarry;
            if (quarry == null)
                return false;

            if (!quarry.IsActive)
                return false;

            if (t.IsForbidden(pawn))
                return false;

            if (!pawn.CanReach(t, PathEndMode.Touch, Danger.Some))
                return false;

            if (CountQuarryWorkers(pawn.Map, t) >= quarry.MaxWorkers)
                return false;

            if (QuarryCoMod.Settings.MinMiningSkill > 0)
            {
                int skill = 0;
                if (pawn.skills != null)
                {
                    var skillRecord = pawn.skills.GetSkill(SkillDefOf.Mining);
                    if (skillRecord != null)
                        skill = skillRecord.Level;
                }
                if (skill < QuarryCoMod.Settings.MinMiningSkill)
                    return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (quarryJobDef == null)
                quarryJobDef = DefDatabase<JobDef>.GetNamed("QuarryCo_QuarryMine");
            return JobMaker.MakeJob(quarryJobDef, t);
        }

        private int CountQuarryWorkers(Map map, Thing quarry)
        {
            if (quarryJobDef == null)
                quarryJobDef = DefDatabase<JobDef>.GetNamed("QuarryCo_QuarryMine");

            int count = 0;
            foreach (Pawn p in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
            {
                if (p.CurJob != null && p.CurJob.def == quarryJobDef && p.CurJob.targetA.Thing == quarry)
                    count++;
            }
            return count;
        }
    }
}
