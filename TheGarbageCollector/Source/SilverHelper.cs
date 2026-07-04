using System.Collections.Generic;
using RimWorld;
using Verse;

namespace TheGarbageCollector
{
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
                Log.Warning("[TheGarbageCollector.SilverHelper] Could not deduct full silver amount. Remaining: " + remaining);
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
}
