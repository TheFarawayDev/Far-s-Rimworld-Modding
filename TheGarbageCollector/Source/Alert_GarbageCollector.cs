using System;
using System.Collections.Generic;
using System.Text;
using Verse;
using RimWorld;

namespace TheGarbageCollector
{
    public class Alert_GarbageCollector : Alert_ConditionalUtility
    {
        public static bool pullFromStorage = false;

        public Alert_GarbageCollector()
        {
            this.defaultLabel = "Garbage Collector Contracts";
            this.defaultExplanation = "Click to open the Garbage Collector sanitation dialogue and contract trash removal services.";
            this.defaultPriority = AlertPriority.Medium;
        }

        protected override int TargetThreshold
        {
            get
            {
                return -1;
            }
        }

        protected override int GetTargetCount(Map map)
        {
            int corpseCount;
            int steelSlagCount;
            int stoneSlagCount;
            int mechScrapCount;
            int clutterCount;
            int wastepackCount;
            GetDebrisCounts(map, pullFromStorage, out corpseCount, out steelSlagCount, out stoneSlagCount, out mechScrapCount, out clutterCount, out wastepackCount);
            return corpseCount + steelSlagCount + stoneSlagCount + mechScrapCount + clutterCount + wastepackCount;
        }

        protected override void OnClick()
        {
            Find.WindowStack.Add(new Window_GarbageCollector());
        }

        public static void GetDebrisCounts(Map map, bool pullStorage, out int corpseCount, out int steelSlagCount, out int stoneSlagCount, out int mechScrapCount, out int clutterCount, out int wastepackCount)
        {
            corpseCount = 0;
            steelSlagCount = 0;
            stoneSlagCount = 0;
            mechScrapCount = 0;
            clutterCount = 0;
            wastepackCount = 0;

            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (!thing.Spawned) continue;
                if (ShouldSkipDueToStorage(thing, map, pullStorage)) continue;

                if (IsCorpse(thing)) corpseCount++;
                else if (IsSteelSlag(thing)) steelSlagCount++;
                else if (IsStoneSlag(thing)) stoneSlagCount++;
                else if (IsMechanoidScrap(thing)) mechScrapCount++;
                else if (IsLowConditionClutter(thing)) clutterCount++;
                else if (IsToxicWastepack(thing)) wastepackCount++;
            }
        }

        public static int GetPollutedCellCount(Map map)
        {
            if (!ModsConfig.BiotechActive || map.pollutionGrid == null) return 0;
            int count = 0;
            foreach (IntVec3 cell in map.AllCells)
            {
                if (map.pollutionGrid.IsPolluted(cell))
                {
                    count++;
                }
            }
            return count;
        }

        public static int GetDebrisWastepackCount(Map map, bool pullStorage)
        {
            int count = 0;
            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (thing.Spawned && !ShouldSkipDueToStorage(thing, map, pullStorage) && IsToxicWastepack(thing))
                {
                    count++;
                }
            }
            return count;
        }

        public static int CalculateCost(Map map, GarbageFilterMode mode, bool pullStorage)
        {
            if (mode == GarbageFilterMode.Pollution)
            {
                int pollutedCount = GetPollutedCellCount(map);
                int wastepackCount = GetDebrisWastepackCount(map, pullStorage);
                return 150 + (2 * pollutedCount) + (5 * wastepackCount);
            }

            int stoneAndSteelAndCorpses = 0;
            int mechScraps = 0;
            int wastepacks = 0;

            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (!thing.Spawned) continue;
                if (ShouldSkipDueToStorage(thing, map, pullStorage)) continue;
                if (!MatchesFilter(thing, mode)) continue;

                if (IsStoneSlag(thing) || IsSteelSlag(thing) || IsCorpse(thing))
                {
                    stoneAndSteelAndCorpses++;
                }
                else if (IsMechanoidScrap(thing))
                {
                    mechScraps++;
                }
                else if (IsToxicWastepack(thing))
                {
                    wastepacks++;
                }
            }

            int cost = 150 + (5 * stoneAndSteelAndCorpses) + (5 * wastepacks) - (12 * mechScraps);
            return Math.Max(0, cost);
        }

        public static bool IsCorpse(Thing t)
        {
            return t is Corpse;
        }

        public static bool IsStoneSlag(Thing t)
        {
            if (t.def == null) return false;
            return t.def.thingCategories != null && t.def.thingCategories.Contains(ThingCategoryDefOf.StoneChunks);
        }

        public static bool IsSteelSlag(Thing t)
        {
            return t.def == ThingDefOf.ChunkSlagSteel;
        }

        public static bool IsMechanoidScrap(Thing t)
        {
            if (t.def == null) return false;
            return t.def.defName.Contains("ChunkMechanoid") || t.def.defName == "MechanoidScrap" || t.def.defName == "MechScrap" || (t.def.defName.StartsWith("Chunk") && t.def.defName.Contains("Mech"));
        }

        public static bool IsLowConditionClutter(Thing t)
        {
            Apparel app = t as Apparel;
            if (app != null)
            {
                return app.WornByCorpse && app.def.useHitPoints && ((float)app.HitPoints / app.MaxHitPoints < 0.20f);
            }
            if (t.def != null && t.def.IsWeapon)
            {
                return t.def.useHitPoints && ((float)t.HitPoints / t.MaxHitPoints < 0.20f);
            }
            return false;
        }

        public static bool IsToxicWastepack(Thing t)
        {
            return t.def != null && t.def.defName == "Wastepack";
        }

        public static bool IsInStorage(Thing thing, Map map)
        {
            if (thing == null || map == null) return false;

            // Check if there is a stockpile at the cell
            Zone zone = map.zoneManager.ZoneAt(thing.Position);
            if (zone != null && zone is Zone_Stockpile)
            {
                return true;
            }

            // Check if the thing is held inside a storage building (like shelves)
            if (thing.ParentHolder is Building_Storage)
            {
                return true;
            }

            // Check if there is a storage building at the cell
            Building building = thing.Position.GetFirstBuilding(map);
            if (building != null && building is Building_Storage)
            {
                return true;
            }

            return false;
        }

        public static bool ShouldSkipDueToStorage(Thing thing, Map map, bool pullStorage)
        {
            if (pullStorage)
            {
                return false;
            }
            return IsInStorage(thing, map);
        }

        public static bool MatchesFilter(Thing t, GarbageFilterMode mode)
        {
            switch (mode)
            {
                case GarbageFilterMode.Premium:
                    return IsCorpse(t) || IsStoneSlag(t) || IsSteelSlag(t) || IsMechanoidScrap(t) || IsLowConditionClutter(t) || IsToxicWastepack(t);
                case GarbageFilterMode.Biohazard:
                    return IsCorpse(t);
                case GarbageFilterMode.JunkYard:
                    return IsLowConditionClutter(t);
                case GarbageFilterMode.StoneSlag:
                    return IsStoneSlag(t);
                case GarbageFilterMode.SteelSlag:
                    return IsSteelSlag(t);
                case GarbageFilterMode.MechScrap:
                    return IsMechanoidScrap(t);
                case GarbageFilterMode.Pollution:
                    return IsToxicWastepack(t);
                default:
                    return false;
            }
        }

        public static int GetAvailableSilver(Map map)
        {
            return SilverHelper.GetAvailableSilverOnMap(map);
        }

        public static void DeductSilver(Map map, int amount)
        {
            SilverHelper.DeductSilver(amount, map);
        }
    }

    public enum GarbageFilterMode
    {
        Premium,
        Biohazard,
        JunkYard,
        StoneSlag,
        SteelSlag,
        MechScrap,
        Pollution
    }
}
