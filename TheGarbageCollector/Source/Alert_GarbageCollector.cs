using System;
using System.Collections.Generic;
using System.Text;
using Verse;
using RimWorld;

namespace TheGarbageCollector
{
    public class Alert_GarbageCollector : FarUtils.Alert_ConditionalUtility
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
            DiaNode rootNode = MakeRootNode();
            Dialog_NodeTree dialog = new Dialog_NodeTree(rootNode, true, false, "Garbage Collector");
            Find.WindowStack.Add(dialog);
        }

        public static DiaNode MakeRootNode()
        {
            DiaNode rootNode = MakeRootNodeInternal();
            return rootNode;
        }

        private static DiaNode MakeRootNodeInternal()
        {
            DiaNode rootNode = new DiaNode(GetDialogueText());

            // Option 1: Toggle Storage
            string toggleLabel = pullFromStorage 
                ? "Exclude items in stockpiles & shelves (Currently: INCLUDED)" 
                : "Include items in stockpiles & shelves (Currently: IGNORED)";
            DiaOption toggleOpt = new DiaOption(toggleLabel);
            toggleOpt.resolveTree = false;
            toggleOpt.action = delegate
            {
                pullFromStorage = !pullFromStorage;
                toggleOpt.link = MakeRootNodeInternal();
            };
            rootNode.options.Add(toggleOpt);

            // Option 2: Clean All Colony Layers
            DiaOption allLayersOpt = new DiaOption("Contract Cleanup for ALL Colony Layers...");
            allLayersOpt.resolveTree = false;
            allLayersOpt.action = delegate
            {
                allLayersOpt.link = MakeAllLayersNode();
            };
            rootNode.options.Add(allLayersOpt);

            // Option 3: Clean a Specific Colony Layer
            DiaOption specificLayerOpt = new DiaOption("Contract Cleanup for a Specific Colony Layer...");
            specificLayerOpt.resolveTree = false;
            specificLayerOpt.action = delegate
            {
                specificLayerOpt.link = MakeSpecificLayerSelectNode();
            };
            rootNode.options.Add(specificLayerOpt);

            // Option 4: Configure Specific Debris Filters (Aggregated)
            DiaOption configOpt = new DiaOption("Configure Specific Debris Filters...");
            configOpt.resolveTree = false;
            configOpt.action = delegate
            {
                configOpt.link = MakeSubNode();
            };
            rootNode.options.Add(configOpt);

            // Option 5: Dismiss / Do Nothing
            DiaOption dismissOpt = new DiaOption("Dismiss / Do Nothing");
            dismissOpt.resolveTree = true;
            rootNode.options.Add(dismissOpt);

            return rootNode;
        }

        public static DiaNode MakeAllLayersNode()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ALL COLONY LAYERS CLEANUP CONTRACTS");
            sb.AppendLine("========================================");
            sb.AppendLine("Sanitation services will be scheduled simultaneously on all active colony layers that contain targeted debris.");
            
            DiaNode node = new DiaNode(sb.ToString());

            AddCleanAllOption(node, "Request Premium Clean (All Layers)", GarbageFilterMode.Premium);
            AddCleanAllOption(node, "Request Biohazard Clean (All Layers)", GarbageFilterMode.Biohazard);
            AddCleanAllOption(node, "Request Junk Yard Clean (All Layers)", GarbageFilterMode.JunkYard);
            if (ModsConfig.BiotechActive)
            {
                AddCleanAllOption(node, "Request Pollution Decontamination (All Layers)", GarbageFilterMode.Pollution);
            }

            DiaOption backOpt = new DiaOption("Go Back");
            backOpt.resolveTree = false;
            backOpt.action = delegate
            {
                backOpt.link = MakeRootNodeInternal();
            };
            node.options.Add(backOpt);

            return node;
        }

        public static DiaNode MakeSpecificLayerSelectNode()
        {
            DiaNode node = new DiaNode("Select a specific colony layer to schedule cleanup services:");

            foreach (Map m in Find.Maps)
            {
                string mapName = "Colony Layer";
                if (m.Parent != null)
                {
                    mapName = m.Parent.Label;
                }
                DiaOption mapOpt = new DiaOption(string.Format("Manage cleanup for {0} (Map ID: {1})", mapName, m.uniqueID));
                mapOpt.resolveTree = false;
                mapOpt.action = delegate
                {
                    mapOpt.link = MakeSpecificLayerMenuNode(m);
                };
                node.options.Add(mapOpt);
            }

            DiaOption backOpt = new DiaOption("Go Back");
            backOpt.resolveTree = false;
            backOpt.action = delegate
            {
                backOpt.link = MakeRootNodeInternal();
            };
            node.options.Add(backOpt);

            return node;
        }

        public static DiaNode MakeSpecificLayerMenuNode(Map m)
        {
            string mapName = "Colony Layer";
            if (m.Parent != null)
            {
                mapName = m.Parent.Label;
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Format("CLEANUP CONTRACTS FOR LAYER: {0}", mapName.ToUpper()));
            sb.AppendLine("========================================");
            sb.AppendLine("Select a cleanup type to schedule a contractor specifically for this vertical layer.");

            DiaNode node = new DiaNode(sb.ToString());

            AddCleanOption(node, m, "Request Premium Clean", GarbageFilterMode.Premium);
            AddCleanOption(node, m, "Request Biohazard Clean", GarbageFilterMode.Biohazard);
            AddCleanOption(node, m, "Request Junk Yard Clean", GarbageFilterMode.JunkYard);
            if (ModsConfig.BiotechActive)
            {
                AddCleanOption(node, m, "Request Decontamination", GarbageFilterMode.Pollution);
            }

            DiaOption backOpt = new DiaOption("Go Back");
            backOpt.resolveTree = false;
            backOpt.action = delegate
            {
                backOpt.link = MakeSpecificLayerSelectNode();
            };
            node.options.Add(backOpt);

            return node;
        }

        public static DiaNode MakeSubNode()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("SPECIFIC DEBRIS FILTER MENU (ALL LAYERS)");
            sb.AppendLine("=========================================");
            sb.AppendLine("Choose a specific trash category to clean up across all active layers. Storage filter configuration will still apply.");

            DiaNode subNode = new DiaNode(sb.ToString());

            AddCleanAllOption(subNode, "Clear Stone Slag Only", GarbageFilterMode.StoneSlag);
            AddCleanAllOption(subNode, "Clear Steel Slag Only", GarbageFilterMode.SteelSlag);
            AddCleanAllOption(subNode, "Clear Mechanoid Scrap Only", GarbageFilterMode.MechScrap);

            DiaOption backOpt = new DiaOption("Go Back to Main Menu");
            backOpt.resolveTree = false;
            backOpt.action = delegate
            {
                backOpt.link = MakeRootNodeInternal();
            };
            subNode.options.Add(backOpt);

            return subNode;
        }

        private static void AddCleanOption(DiaNode node, Map map, string label, GarbageFilterMode mode)
        {
            int cost = CalculateCost(map, mode, pullFromStorage);
            int availableSilver = FarUtils.SilverHelper.GetTotalAvailableSilver(); // Allow paying from any layer

            string optionText;
            if (mode == GarbageFilterMode.MechScrap)
            {
                optionText = string.Format("{0} (Net Cost: {1} silver)", label, cost);
            }
            else
            {
                optionText = string.Format("{0} (Cost: {1} silver)", label, cost);
            }

            DiaOption opt = new DiaOption(optionText);
            if (availableSilver < cost)
            {
                opt.disabled = true;
                opt.disabledReason = string.Format("Need {0} silver (have {1})", cost, availableSilver);
            }
            else if (mode == GarbageFilterMode.Pollution && GetPollutedCellCount(map) == 0 && GetDebrisWastepackCount(map, pullFromStorage) == 0)
            {
                opt.disabled = true;
                opt.disabledReason = "No pollution or toxic wastepacks detected on map.";
            }
            else
            {
                opt.resolveTree = true;
                opt.action = delegate
                {
                    DeductSilver(map, cost);
                    IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamed("Incident_SummonGarbageCollector", false);
                    if (incidentDef != null)
                    {
                        IncidentWorker_SummonGarbageCollector worker = (IncidentWorker_SummonGarbageCollector)incidentDef.Worker;
                        worker.TryExecuteWithParams(map, mode, pullFromStorage, cost);
                    }
                    else
                    {
                        Log.Error("[TheGarbageCollector] IncidentDef 'Incident_SummonGarbageCollector' not found!");
                    }
                };
            }
            node.options.Add(opt);
        }

        private static void AddCleanAllOption(DiaNode node, string label, GarbageFilterMode mode)
        {
            int totalCost = 0;
            List<Map> targetMaps = new List<Map>();

            foreach (Map m in Find.Maps)
            {
                int cost = CalculateCost(m, mode, pullFromStorage);
                bool hasTrash = false;
                if (mode == GarbageFilterMode.Pollution)
                {
                    hasTrash = GetPollutedCellCount(m) > 0 || GetDebrisWastepackCount(m, pullFromStorage) > 0;
                }
                else
                {
                    foreach (Thing thing in m.listerThings.AllThings)
                    {
                        if (thing.Spawned && !ShouldSkipDueToStorage(thing, m, pullFromStorage) && MatchesFilter(thing, mode))
                        {
                            hasTrash = true;
                            break;
                        }
                    }
                }

                if (hasTrash)
                {
                    totalCost += cost;
                    targetMaps.Add(m);
                }
            }

            int availableSilver = FarUtils.SilverHelper.GetTotalAvailableSilver();
            string optionText = string.Format("{0} (Cost: {1} silver for {2} layer(s))", label, totalCost, targetMaps.Count);

            DiaOption opt = new DiaOption(optionText);
            if (targetMaps.Count == 0)
            {
                opt.disabled = true;
                opt.disabledReason = "No matching debris found on any active layer.";
            }
            else if (availableSilver < totalCost)
            {
                opt.disabled = true;
                opt.disabledReason = string.Format("Need {0} silver (have {1})", totalCost, availableSilver);
            }
            else
            {
                opt.resolveTree = true;
                opt.action = delegate
                {
                    FarUtils.SilverHelper.DeductSilver(totalCost);
                    IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamed("Incident_SummonGarbageCollector", false);
                    if (incidentDef != null)
                    {
                        IncidentWorker_SummonGarbageCollector worker = (IncidentWorker_SummonGarbageCollector)incidentDef.Worker;
                        foreach (Map m in targetMaps)
                        {
                            int mapCost = CalculateCost(m, mode, pullFromStorage);
                            worker.TryExecuteWithParams(m, mode, pullFromStorage, mapCost);
                        }
                    }
                };
            }
            node.options.Add(opt);
        }

        private static string GetDialogueText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("THE GARBAGE COLLECTOR CONTRACTS");
            sb.AppendLine("========================================");
            sb.AppendLine(string.Format("Storage Target Scope: {0}", pullFromStorage ? "ENABLED (Items in Stockpiles & Shelves are TARGETED.)" : "DISABLED (Items in Stockpiles & Shelves are IGNORED.)"));
            sb.AppendLine();
            sb.AppendLine("Active Colony Layers Scan:");
            
            foreach (Map m in Find.Maps)
            {
                int corpseCount;
                int steelSlagCount;
                int stoneSlagCount;
                int mechScrapCount;
                int clutterCount;
                int wastepackCount;
                GetDebrisCounts(m, pullFromStorage, out corpseCount, out steelSlagCount, out stoneSlagCount, out mechScrapCount, out clutterCount, out wastepackCount);
                
                string mapName = "Colony Layer";
                if (m.Parent != null)
                {
                    mapName = m.Parent.Label;
                }
                sb.AppendLine(string.Format("- {0} (Map ID: {1}):", mapName, m.uniqueID));
                sb.AppendLine(string.Format("  * Corpses: {0} | Steel Slag: {1} | Stone Slag: {2}", corpseCount, steelSlagCount, stoneSlagCount));
                sb.AppendLine(string.Format("  * Mech Scrap: {0} | Clutter: {1}", mechScrapCount, clutterCount));
                if (ModsConfig.BiotechActive)
                {
                    sb.AppendLine(string.Format("  * Toxic Wastepacks: {0} | Polluted Cells: {1}", wastepackCount, GetPollutedCellCount(m)));
                }
            }
            sb.AppendLine();
            sb.AppendLine("Please select a sanitation contract. Prices include a base arrival fee of 150 silver per map contracted.");
            return sb.ToString();
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
            return FarUtils.SilverHelper.GetAvailableSilverOnMap(map);
        }

        public static void DeductSilver(Map map, int amount)
        {
            FarUtils.SilverHelper.DeductSilver(amount, map);
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
