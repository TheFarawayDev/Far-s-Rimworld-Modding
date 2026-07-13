using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;

namespace TheFarawayDev.MatterAnywhere
{
    public class ITab_QuantumRelay : ITab
    {
        private Vector2 scrollPosition = Vector2.zero;
        private string filterText = "";

        public ITab_QuantumRelay()
        {
            this.size = new Vector2(850f, 600f);
            this.labelKey = "MN_NetworkStorageTab"; // Map to "Network" tab translation label key
        }

        public override bool IsVisible
        {
            get
            {
                var comp = SelThing?.TryGetComp<CompNetworkRelay>();
                return comp != null && comp.PowerOn;
            }
        }

        protected override void FillTab()
        {
            CompNetworkRelay comp = SelThing?.TryGetComp<CompNetworkRelay>();
            if (comp == null) return;

            Rect inRect = new Rect(0f, 0f, this.size.x, this.size.y).ContractedBy(15f);

            // Title & Subtitle
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, 500f, 35f), "Quantum Network Relay");
            Text.Font = GameFont.Small;
            
            var networkBuilding = comp.parent as SK_Matter_Network.NetworkBuilding;
            var network = networkBuilding?.ParentNetwork;
            var controller = network?.ActiveController;
            string networkIdStr = network != null ? network.NetworkId.ToString() : "No active network link";
            
            GUI.color = Color.gray;
            Widgets.Label(new Rect(inRect.x, inRect.y + 35f, 500f, 25f), $"Network ID: {networkIdStr}");
            GUI.color = Color.white;

            // Online indicator (right side)
            Rect statusRect = new Rect(inRect.x + inRect.width - 150f, inRect.y, 150f, 30f);
            bool isOnline = comp.PowerOn && network != null;
            GUI.color = isOnline ? Color.green : Color.red;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(statusRect, isOnline ? "ONLINE" : "OFFLINE");
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            // Draw line divider
            Widgets.DrawLineHorizontal(inRect.x, inRect.y + 65f, inRect.width);

            // Draw Top Stats Boxes
            float boxWidth = (inRect.width - 40f) / 5f;
            float boxHeight = 70f;
            float startY = inRect.y + 75f;

            var tracker = MatterNetworkWorldTracker.Instance;
            int totalLedgerItems = tracker != null ? tracker.globalResourceLedger.Values.Sum() : 0;
            int uniqueDefs = tracker != null ? tracker.globalResourceLedger.Count : 0;

            DrawStatBox(new Rect(inRect.x, startY, boxWidth, boxHeight), "Mode", comp.currentMode.ToString());
            
            int maxLimit = comp.GetMaxBandwidthLimit();
            string limitStr = comp.bandwidthLimit == int.MaxValue ? "Max" : comp.bandwidthLimit.ToString();
            DrawStatBox(new Rect(inRect.x + boxWidth + 10f, startY, boxWidth, boxHeight), "Bandwidth Limit", $"{limitStr} / {(maxLimit >= 99999 ? "Max" : maxLimit.ToString())}");
            
            string predictedPower = isOnline ? $"{comp.GetPredictedPowerDraw():F0} W" : "0 W";
            DrawStatBox(new Rect(inRect.x + (boxWidth + 10f) * 2f, startY, boxWidth, boxHeight), "Predicted Power", predictedPower);
            
            DrawStatBox(new Rect(inRect.x + (boxWidth + 10f) * 3f, startY, boxWidth, boxHeight), "Stored Units", totalLedgerItems.ToString());
            
            DrawStatBox(new Rect(inRect.x + (boxWidth + 10f) * 4f, startY, boxWidth, boxHeight), "Unique Defs", uniqueDefs.ToString());

            // Controls section
            float controlY = startY + boxHeight + 20f;
            
            // Left Column: Configuration
            Rect leftCol = new Rect(inRect.x, controlY, 400f, 150f);
            Widgets.DrawMenuSection(leftCol);
            Rect leftInner = leftCol.ContractedBy(10f);
            
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(leftInner.x, leftInner.y, 200f, 20f), "DIRECTION CONTROL");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // Mode Toggle Buttons
            float btnY = leftInner.y + 20f;
            float btnWidth = (leftInner.width - 15f) / 4f;
            float btnHeight = 30f;

            if (Widgets.ButtonText(new Rect(leftInner.x, btnY, btnWidth, btnHeight), "Isolated", true, true, comp.currentMode == NetworkMode.Isolated))
            {
                comp.currentMode = NetworkMode.Isolated;
                HarmonyPatches.globalLedgerDirty = true;
            }
            if (Widgets.ButtonText(new Rect(leftInner.x + btnWidth + 5f, btnY, btnWidth, btnHeight), "Push", true, true, comp.currentMode == NetworkMode.PushOnly))
            {
                comp.currentMode = NetworkMode.PushOnly;
                HarmonyPatches.globalLedgerDirty = true;
            }
            if (Widgets.ButtonText(new Rect(leftInner.x + (btnWidth + 5f) * 2f, btnY, btnWidth, btnHeight), "Pull", true, true, comp.currentMode == NetworkMode.PullOnly))
            {
                comp.currentMode = NetworkMode.PullOnly;
                HarmonyPatches.globalLedgerDirty = true;
            }
            if (Widgets.ButtonText(new Rect(leftInner.x + (btnWidth + 5f) * 3f, btnY, btnWidth, btnHeight), "BiDi", true, true, comp.currentMode == NetworkMode.BiDirectional))
            {
                comp.currentMode = NetworkMode.BiDirectional;
                HarmonyPatches.globalLedgerDirty = true;
            }

            // Bandwidth Limit Slider
            float sliderY = btnY + btnHeight + 20f;
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(leftInner.x, sliderY, 200f, 20f), "BANDWIDTH LIMIT");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            float valSliderY = sliderY + 15f;
            float limitFloat = comp.bandwidthLimit == int.MaxValue ? maxLimit : comp.bandwidthLimit;
            float maxSliderVal = maxLimit >= 99999 ? 1000f : maxLimit;
            
            float newLimitFloat = Widgets.HorizontalSlider(new Rect(leftInner.x, valSliderY, leftInner.width - 60f, 20f), limitFloat, 0f, maxSliderVal, true, null, null, null, 1f);
            int newLimit = Mathf.RoundToInt(newLimitFloat);
            
            if (newLimit >= maxSliderVal && maxLimit >= 99999)
            {
                comp.bandwidthLimit = int.MaxValue;
            }
            else
            {
                comp.bandwidthLimit = newLimit;
            }
            
            string limitDisplay = comp.bandwidthLimit == int.MaxValue ? "Max" : comp.bandwidthLimit.ToString();
            Widgets.Label(new Rect(leftInner.x + leftInner.width - 50f, valSliderY - 5f, 50f, 25f), limitDisplay);

            // Right Column: Status & Info
            Rect rightCol = new Rect(inRect.x + 420f, controlY, inRect.width - 420f, 150f);
            Widgets.DrawMenuSection(rightCol);
            Rect rightInner = rightCol.ContractedBy(10f);
            
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rightInner.x, rightInner.y, 200f, 20f), "SYSTEM STATUS");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            float statusY = rightInner.y + 20f;
            DrawStatusLine(rightInner.x, ref statusY, rightInner.width, "Relay State", isOnline ? "Online" : "Offline", isOnline ? Color.green : Color.red);
            DrawStatusLine(rightInner.x, ref statusY, rightInner.width, "Local Controller", controller != null ? "Linked" : "Missing", controller != null ? Color.green : Color.red);
            float actualDraw = isOnline ? -comp.PowerComp.PowerOutput : 0f;
            DrawStatusLine(rightInner.x, ref statusY, rightInner.width, "Actual Power Draw", $"{actualDraw:F0} W", Color.white);
            DrawStatusLine(rightInner.x, ref statusY, rightInner.width, "Planetary Hub Link", tracker != null ? "Established" : "Failed", tracker != null ? Color.green : Color.red);

            // Bottom List: Ledger Contents
            float listY = controlY + 165f;
            float listHeight = inRect.height - (listY - inRect.y);
            Rect listRect = new Rect(inRect.x, listY, inRect.width, listHeight);
            
            Widgets.DrawMenuSection(listRect);
            Rect listInner = listRect.ContractedBy(10f);

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(listInner.x, listInner.y, 150f, 20f), "PLANETARY LEDGER CONTENTS");

            // Search Input Field
            Rect searchRect = new Rect(listInner.x + 160f, listInner.y - 2f, 150f, 18f);
            filterText = Widgets.TextField(searchRect, filterText);

            Widgets.Label(new Rect(listInner.x + listInner.width - 150f, listInner.y, 150f, 20f), "Total Stored");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            float itemStartY = listInner.y + 20f;
            float itemHeight = 35f;

            // Calculate filtered list
            var filteredItems = new List<KeyValuePair<string, int>>();
            if (tracker != null)
            {
                foreach (var kvp in tracker.globalResourceLedger.OrderByDescending(k => k.Value))
                {
                    ThingDef def = DefDatabase<ThingDef>.GetNamed(kvp.Key, false);
                    if (def != null && (string.IsNullOrEmpty(filterText) || def.label.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        filteredItems.Add(kvp);
                    }
                }
            }

            Rect viewRect = new Rect(0f, 0f, listInner.width - 16f, Math.Max(filteredItems.Count * itemHeight, listInner.height - 30f));
            Rect outRect = new Rect(listInner.x, itemStartY, listInner.width, listInner.height - 25f);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect, true);

            if (filteredItems.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(10f, 10f, viewRect.width - 20f, 30f), "No matching resources stored in the planetary ledger.");
                GUI.color = Color.white;
            }
            else
            {
                float currentItemY = 0f;
                foreach (var kvp in filteredItems)
                {
                    ThingDef def = DefDatabase<ThingDef>.GetNamed(kvp.Key, false);
                    if (def == null) continue;

                    Rect itemRowRect = new Rect(0f, currentItemY, viewRect.width, itemHeight);

                    if (currentItemY / itemHeight % 2 == 0)
                    {
                        Widgets.DrawLightHighlight(itemRowRect);
                    }

                    // Icon
                    Rect iconRect = new Rect(5f, currentItemY + 2f, 30f, 30f);
                    Widgets.ThingIcon(iconRect, def);

                    // Name
                    Rect labelRect = new Rect(45f, currentItemY + 5f, 250f, 25f);
                    Widgets.Label(labelRect, def.LabelCap);

                    // Count
                    Rect countRect = new Rect(viewRect.width - 250f, currentItemY + 5f, 90f, 25f);
                    Text.Anchor = TextAnchor.MiddleRight;
                    GUI.color = new Color(0.9f, 0.75f, 0.2f);
                    Widgets.Label(countRect, kvp.Value.ToString("N0"));
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;

                    // Drop 1 Button
                    Rect drop1Rect = new Rect(viewRect.width - 145f, currentItemY + 5f, 65f, 24f);
                    if (Widgets.ButtonText(drop1Rect, "Drop 1", true, true, true))
                    {
                        DropItem(def, 1, comp.parent);
                    }

                    // Drop Stack Button
                    Rect dropStackRect = new Rect(viewRect.width - 75f, currentItemY + 5f, 75f, 24f);
                    if (Widgets.ButtonText(dropStackRect, "Drop Stack", true, true, true))
                    {
                        int amount = Math.Min(kvp.Value, def.stackLimit);
                        DropItem(def, amount, comp.parent);
                    }

                    currentItemY += itemHeight;
                }
            }
            
            Widgets.EndScrollView();
        }

        private void DrawStatBox(Rect rect, string label, string value)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(5f);

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 15f), label.ToUpper());

            Text.Font = GameFont.Medium;
            GUI.color = Color.white;
            Widgets.Label(new Rect(inner.x, inner.y + 15f, inner.width, 35f), value);
            
            Text.Font = GameFont.Small;
        }

        private void DrawStatusLine(float x, ref float y, float width, string label, string val, Color valColor)
        {
            Rect labelRect = new Rect(x, y, width - 150f, 22f);
            Rect valRect = new Rect(x + width - 150f, y, 150f, 22f);
            
            Widgets.Label(labelRect, label);
            
            GUI.color = valColor;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(valRect, val);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            y += 24f;
        }

        private void DropItem(ThingDef def, int count, Thing parent)
        {
            var tracker = MatterNetworkWorldTracker.Instance;
            if (tracker == null || parent == null || !parent.Spawned) return;

            string key = def.defName;
            if (tracker.globalResourceLedger.TryGetValue(key, out int currentCount))
            {
                int toSpawn = Math.Min(count, currentCount);
                if (toSpawn <= 0) return;

                tracker.globalResourceLedger[key] -= toSpawn;
                if (tracker.globalResourceLedger[key] <= 0)
                {
                    tracker.globalResourceLedger.Remove(key);
                }
                HarmonyPatches.globalLedgerDirty = true;

                Thing spawned = ThingMaker.MakeThing(def);
                spawned.stackCount = toSpawn;
                if (GenPlace.TryPlaceThing(spawned, parent.Position, parent.Map, ThingPlaceMode.Near))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            }
        }
    }
}
