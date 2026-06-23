using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace BlightedAlert
{
    public class Alert_Blighted : FarUtils.Alert_ConditionalUtility
    {
        public Alert_Blighted()
        {
            this.defaultLabel = "Blighted plants";
            this.defaultExplanation = "There are blighted plants on the map. Cut them immediately to prevent the blight from spreading!";
        }

        protected override int TargetThreshold
        {
            get
            {
                return 0;
            }
        }

        protected override int GetTargetCount(Map map)
        {
            if (map == null) return 0;

            int count = 0;
            List<Thing> plants = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant);
            if (plants != null)
            {
                for (int i = 0; i < plants.Count; i++)
                {
                    Plant plant = plants[i] as Plant;
                    if (plant != null)
                    {
                        if (GridsUtility.GetFirstBlight(plant.Position, map) != null)
                        {
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        public override IEnumerable<Thing> GetCulprits(Map map)
        {
            if (map == null) return null;

            List<Thing> culprits = new List<Thing>();
            List<Thing> plants = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant);
            if (plants != null)
            {
                for (int i = 0; i < plants.Count; i++)
                {
                    Plant plant = plants[i] as Plant;
                    if (plant != null)
                    {
                        if (GridsUtility.GetFirstBlight(plant.Position, map) != null)
                        {
                            culprits.Add(plant);
                        }
                    }
                }
            }
            return culprits;
        }
    }
}
