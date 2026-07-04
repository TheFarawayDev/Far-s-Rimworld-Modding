using System.Collections.Generic;
using RimWorld;
using Verse;

namespace TheGarbageCollector
{
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
}
