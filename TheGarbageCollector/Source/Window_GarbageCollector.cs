using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace TheGarbageCollector
{
    public class Window_GarbageCollector : Window
    {
        private Vector2 scrollPositionPackages;
        private GarbageFilterMode selectedMode = GarbageFilterMode.Premium;
        private bool isSpecificLayerSelected;
        private Map selectedMap;

        public override Vector2 InitialSize
        {
            get { return new Vector2(800f, 600f); }
        }

        public Window_GarbageCollector()
        {
            this.doCloseX = true;
            this.doCloseButton = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;

            this.selectedMap = Find.CurrentMap;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 35f);
            Widgets.Label(titleRect, "Garbage Collector Contracts");
            Text.Font = GameFont.Small;

            Widgets.DrawLineHorizontal(inRect.x, inRect.y + 35f, inRect.width);

            int availableSilver = SilverHelper.GetTotalAvailableSilver();
            Rect statusRect = new Rect(inRect.x, inRect.y + 45f, inRect.width, 30f);
            Widgets.Label(statusRect, string.Format("Colony Silver: {0}", availableSilver));

            // Tabs
            Rect tabsRect = new Rect(inRect.x, inRect.y + 80f, inRect.width, 30f);
            float tabWidth = inRect.width / 2f;
            
            GUI.color = !isSpecificLayerSelected ? Color.white : Color.gray;
            if (Widgets.ButtonText(new Rect(tabsRect.x, tabsRect.y, tabWidth, tabsRect.height), "All Colony Layers", true, false, true))
            {
                isSpecificLayerSelected = false;
            }
            
            GUI.color = isSpecificLayerSelected ? Color.white : Color.gray;
            if (Widgets.ButtonText(new Rect(tabsRect.x + tabWidth, tabsRect.y, tabWidth, tabsRect.height), "Specific Layer", true, false, true))
            {
                isSpecificLayerSelected = true;
            }
            GUI.color = Color.white;

            // Specific layer map selector
            if (isSpecificLayerSelected)
            {
                Rect mapSelectorRect = new Rect(tabsRect.x + tabWidth, tabsRect.y + 35f, tabWidth, 24f);
                if (Widgets.ButtonText(mapSelectorRect, selectedMap != null ? (selectedMap.Parent != null ? selectedMap.Parent.LabelCap : "Colony Layer") : "Select Layer"))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (Map m in Find.Maps)
                    {
                        Map localMap = m;
                        string label = localMap.Parent != null ? localMap.Parent.LabelCap : "Colony Layer";
                        options.Add(new FloatMenuOption(string.Format("{0} (ID: {1})", label, localMap.uniqueID), delegate
                        {
                            selectedMap = localMap;
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }

            // Left panel: Package list
            Rect leftPanel = new Rect(inRect.x, inRect.y + 120f, inRect.width * 0.4f, inRect.height - 180f);
            Widgets.DrawMenuSection(leftPanel);
            
            List<GarbageFilterMode> modes = new List<GarbageFilterMode>
            {
                GarbageFilterMode.Premium,
                GarbageFilterMode.Biohazard,
                GarbageFilterMode.JunkYard,
                GarbageFilterMode.StoneSlag,
                GarbageFilterMode.SteelSlag,
                GarbageFilterMode.MechScrap
            };

            if (ModsConfig.BiotechActive)
            {
                modes.Add(GarbageFilterMode.Pollution);
            }

            Rect outRect = leftPanel.ContractedBy(4f);
            float viewHeight = modes.Count * 30f;
            float viewWidth = viewHeight > outRect.height ? outRect.width - 16f : outRect.width;
            Rect viewRect = new Rect(0, 0, viewWidth, viewHeight);
            
            Widgets.BeginScrollView(outRect, ref scrollPositionPackages, viewRect);
            for (int i = 0; i < modes.Count; i++)
            {
                GarbageFilterMode m = modes[i];
                Rect row = new Rect(0, i * 30f, viewRect.width, 30f);
                if (Widgets.ButtonInvisible(row))
                {
                    selectedMode = m;
                }
                
                if (selectedMode == m)
                {
                    Widgets.DrawHighlightSelected(row);
                }
                else if (Mouse.IsOver(row))
                {
                    Widgets.DrawHighlight(row);
                }
                
                Widgets.Label(new Rect(row.x + 5f, row.y + 5f, row.width - 10f, row.height - 5f), GetModeLabel(m));
            }
            Widgets.EndScrollView();

            // Right panel: Options
            Rect rightPanel = new Rect(inRect.x + inRect.width * 0.4f + 10f, inRect.y + 120f, inRect.width * 0.6f - 10f, inRect.height - 180f);
            DrawDispatchOptions(rightPanel, availableSilver);
        }

        private string GetModeLabel(GarbageFilterMode mode)
        {
            switch (mode)
            {
                case GarbageFilterMode.Premium: return "Premium Clean";
                case GarbageFilterMode.Biohazard: return "Biohazard Clean";
                case GarbageFilterMode.JunkYard: return "Junk Yard Clean";
                case GarbageFilterMode.StoneSlag: return "Stone Slag Only";
                case GarbageFilterMode.SteelSlag: return "Steel Slag Only";
                case GarbageFilterMode.MechScrap: return "Mechanoid Scrap Only";
                case GarbageFilterMode.Pollution: return "Pollution Decontamination";
                default: return mode.ToString();
            }
        }

        private string GetModeDescription(GarbageFilterMode mode)
        {
            switch (mode)
            {
                case GarbageFilterMode.Premium: return "Cleans Stone Slag, Steel Slag, and all Corpses.";
                case GarbageFilterMode.Biohazard: return "Cleans all Corpses on the map.";
                case GarbageFilterMode.JunkYard: return "Cleans all Stone and Steel Slag.";
                case GarbageFilterMode.StoneSlag: return "Clears only Stone Slag chunks.";
                case GarbageFilterMode.SteelSlag: return "Clears only Steel Slag chunks.";
                case GarbageFilterMode.MechScrap: return "Clears only Mechanoid Scraps/Chunks.";
                case GarbageFilterMode.Pollution: return "Removes Pollution from terrain and cleans up Toxic Wastepacks.";
                default: return "";
            }
        }

        private void DrawDispatchOptions(Rect rect, int availableSilver)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 30f), string.Format("Contract: {0}", GetModeLabel(selectedMode)));
            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(rect.x, rect.y + 35f, rect.width);

            float y = rect.y + 50f;

            Widgets.Label(new Rect(rect.x, y, rect.width, 40f), GetModeDescription(selectedMode));
            y += 50f;

            // Storage Toggle
            string storageLabel = Alert_GarbageCollector.pullFromStorage ? "Target Items in Storage: ENABLED" : "Target Items in Storage: DISABLED";
            Rect storageToggleRect = new Rect(rect.x, y, rect.width * 0.6f, 30f);
            if (Widgets.ButtonText(storageToggleRect, storageLabel))
            {
                Alert_GarbageCollector.pullFromStorage = !Alert_GarbageCollector.pullFromStorage;
            }
            y += 50f;

            // Calculate costs and targeted maps
            List<Map> targetMaps = new List<Map>();
            int totalCost = 0;
            bool hasDebris = false;

            if (isSpecificLayerSelected)
            {
                if (selectedMap != null)
                {
                    totalCost = Alert_GarbageCollector.CalculateCost(selectedMap, selectedMode, Alert_GarbageCollector.pullFromStorage);
                    hasDebris = HasDebris(selectedMap, selectedMode);
                    if (hasDebris) targetMaps.Add(selectedMap);
                }
            }
            else
            {
                foreach (Map m in Find.Maps)
                {
                    if (HasDebris(m, selectedMode))
                    {
                        totalCost += Alert_GarbageCollector.CalculateCost(m, selectedMode, Alert_GarbageCollector.pullFromStorage);
                        targetMaps.Add(m);
                        hasDebris = true;
                    }
                }
            }

            // Draw status
            Widgets.Label(new Rect(rect.x, y, rect.width, 30f), string.Format("Total Cost: {0} silver", totalCost));
            y += 30f;
            Widgets.Label(new Rect(rect.x, y, rect.width, 30f), string.Format("Layers Targeted: {0}", targetMaps.Count));
            y += 40f;

            bool canAfford = availableSilver >= totalCost;
            bool canExecute = hasDebris && canAfford;
            string buttonReason = "";

            if (!hasDebris)
            {
                buttonReason = "No matching debris found.";
                if (selectedMode == GarbageFilterMode.Pollution && ModsConfig.BiotechActive)
                {
                    buttonReason = "No pollution or wastepacks found.";
                }
            }
            else if (!canAfford)
            {
                buttonReason = "Insufficient Silver";
            }

            Rect dispatchRect = new Rect(rect.x, y, rect.width, 60f);
            GUI.color = canExecute ? Color.white : Color.grey;
            if (Widgets.ButtonText(dispatchRect, "Dispatch Garbage Collector"))
            {
                if (canExecute)
                {
                    ConfirmDispatch(totalCost, targetMaps, selectedMode);
                }
                else if (!string.IsNullOrEmpty(buttonReason))
                {
                    Messages.Message(buttonReason, MessageTypeDefOf.RejectInput, false);
                }
            }
            GUI.color = Color.white;
            
            if (!canExecute && !string.IsNullOrEmpty(buttonReason))
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(dispatchRect, "\n\n<color=#FF5555>" + buttonReason + "</color>");
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private bool HasDebris(Map m, GarbageFilterMode mode)
        {
            if (mode == GarbageFilterMode.Pollution)
            {
                return Alert_GarbageCollector.GetPollutedCellCount(m) > 0 || Alert_GarbageCollector.GetDebrisWastepackCount(m, Alert_GarbageCollector.pullFromStorage) > 0;
            }
            else
            {
                foreach (Thing thing in m.listerThings.AllThings)
                {
                    if (thing.Spawned && !Alert_GarbageCollector.ShouldSkipDueToStorage(thing, m, Alert_GarbageCollector.pullFromStorage) && Alert_GarbageCollector.MatchesFilter(thing, mode))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void ConfirmDispatch(int cost, List<Map> targetMaps, GarbageFilterMode mode)
        {
            SilverHelper.DeductSilver(cost);
            IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamed("Incident_SummonGarbageCollector", false);
            if (incidentDef != null)
            {
                IncidentWorker_SummonGarbageCollector worker = (IncidentWorker_SummonGarbageCollector)incidentDef.Worker;
                foreach (Map m in targetMaps)
                {
                    int mapCost = Alert_GarbageCollector.CalculateCost(m, mode, Alert_GarbageCollector.pullFromStorage);
                    worker.TryExecuteWithParams(m, mode, Alert_GarbageCollector.pullFromStorage, mapCost);
                }
            }
            else
            {
                Log.Error("[TheGarbageCollector] IncidentDef 'Incident_SummonGarbageCollector' not found!");
            }
            this.Close();
        }
    }
}
