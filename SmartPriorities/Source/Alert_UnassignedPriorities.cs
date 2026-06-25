using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using FarUtils;

namespace SmartPriorities
{
    public class Alert_UnassignedPriorities : Alert_ConditionalUtility
    {
        public Alert_UnassignedPriorities()
        {
            this.defaultLabel = "Unassigned Priorities";
            this.defaultExplanation = "One or more colonists have absolutely no work assigned! They will sit idle.\n\nOpen the Work tab and click 'Auto-Assign All' to automatically generate smart priorities for them based on their skills and passions.";
            this.defaultPriority = AlertPriority.High;
        }

        protected override int TargetThreshold { get { return 0; } }

        protected override int GetTargetCount(Map map)
        {
            int count = 0;
            if (map == null || map.mapPawns == null) return 0;

            foreach (Pawn pawn in map.mapPawns.FreeColonists)
            {
                if (IsUnassigned(pawn))
                {
                    count++;
                }
            }
            return count;
        }

        public override IEnumerable<Thing> GetCulprits(Map map)
        {
            List<Thing> culprits = new List<Thing>();
            if (map == null || map.mapPawns == null) return culprits;

            foreach (Pawn pawn in map.mapPawns.FreeColonists)
            {
                if (IsUnassigned(pawn))
                {
                    culprits.Add(pawn);
                }
            }
            return culprits;
        }

        private bool IsUnassigned(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.workSettings == null || !pawn.workSettings.EverWork)
            {
                return false;
            }

            // Check if manual priorities are enabled globally
            if (!Current.Game.playSettings.useWorkPriorities)
            {
                return false; // If manual priorities are disabled, they technically have work (checked automatically by vanilla).
            }

            foreach (WorkTypeDef workDef in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (pawn.workSettings.GetPriority(workDef) > 0)
                {
                    return false; // Found at least one assigned job
                }
            }

            return true; // No jobs assigned
        }
    }
}
