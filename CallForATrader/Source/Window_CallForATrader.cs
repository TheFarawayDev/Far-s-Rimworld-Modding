using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;

namespace CallForATrader
{
    public class Window_CallForATrader : Window
    {
        private Map map;
        private Building building;
        private Pawn negotiator;
        private bool allowOrbital;

        private Vector2 scrollPositionTraders;
        private TraderKindDef selectedTrader;
        private bool isCaravanSelected;
        
        // Settings cached
        private int availableSilver;
        private bool onCooldown;
        private int remainingTicks;

        public override Vector2 InitialSize
        {
            get { return new Vector2(800f, 600f); }
        }

        public Window_CallForATrader(Map map, Building building, Pawn negotiator, bool allowOrbital)
        {
            this.map = map;
            this.building = building;
            this.negotiator = negotiator;
            this.allowOrbital = allowOrbital;

            this.doCloseX = true;
            this.doCloseButton = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;

            // Default selection based on what's allowed
            this.isCaravanSelected = !allowOrbital;
            
            CacheData();
        }

        private void CacheData()
        {
            availableSilver = CallForATraderUtility.GetAvailableSilver(map);
            var tracker = Find.World.GetComponent<CallForATraderTracker>();
            if (tracker != null)
            {
                onCooldown = tracker.IsOnCooldown(map, out remainingTicks);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 35f);
            Widgets.Label(titleRect, "Traders Hub Network");
            Text.Font = GameFont.Small;

            Widgets.DrawLineHorizontal(inRect.x, inRect.y + 35f, inRect.width);

            Rect statusRect = new Rect(inRect.x, inRect.y + 45f, inRect.width, 30f);
            string statusStr = string.Format("Colony Silver: {0}", availableSilver);
            if (onCooldown)
            {
                statusStr += string.Format(" | <color=#FF5555>Network Cooldown: {0}</color>", CallForATraderMapComponent.FormatTicksToTime(remainingTicks));
            }
            Widgets.Label(statusRect, statusStr);

            // Tabs
            Rect tabsRect = new Rect(inRect.x, inRect.y + 80f, inRect.width, 30f);
            float tabWidth = inRect.width / 2f;
            
            if (allowOrbital)
            {
                GUI.color = !isCaravanSelected ? Color.white : Color.gray;
                if (Widgets.ButtonText(new Rect(tabsRect.x, tabsRect.y, tabWidth, tabsRect.height), "Orbital Traders", true, false, true))
                {
                    isCaravanSelected = false;
                    selectedTrader = null;
                    scrollPositionTraders = Vector2.zero;
                }
                GUI.color = Color.white;
            }
            else
            {
                Widgets.ButtonText(new Rect(tabsRect.x, tabsRect.y, tabWidth, tabsRect.height), "Orbital (Unavailable)", true, false, false);
            }

            GUI.color = isCaravanSelected ? Color.white : Color.gray;
            if (Widgets.ButtonText(new Rect(tabsRect.x + tabWidth, tabsRect.y, tabWidth, tabsRect.height), "On-Ground Caravans", true, false, true))
            {
                isCaravanSelected = true;
                selectedTrader = null;
                scrollPositionTraders = Vector2.zero;
            }
            GUI.color = Color.white;

            // Left panel: Trader list
            Rect leftPanel = new Rect(inRect.x, inRect.y + 120f, inRect.width * 0.4f, inRect.height - 180f);
            Widgets.DrawMenuSection(leftPanel);
            
            List<TraderKindDef> traders = DefDatabase<TraderKindDef>.AllDefs
                .Where(t => t.orbital != isCaravanSelected && !t.label.NullOrEmpty())
                .OrderBy(t => t.label)
                .ToList();

            Rect outRect = leftPanel.ContractedBy(4f);
            float viewHeight = traders.Count * 30f;
            float viewWidth = viewHeight > outRect.height ? outRect.width - 16f : outRect.width;
            Rect viewRect = new Rect(0, 0, viewWidth, viewHeight);
            Widgets.BeginScrollView(outRect, ref scrollPositionTraders, viewRect);
            for (int i = 0; i < traders.Count; i++)
            {
                TraderKindDef t = traders[i];
                Rect row = new Rect(0, i * 30f, viewRect.width, 30f);
                if (Widgets.ButtonInvisible(row))
                {
                    selectedTrader = t;
                }
                
                if (selectedTrader == t)
                {
                    Widgets.DrawHighlightSelected(row);
                }
                else if (Mouse.IsOver(row))
                {
                    Widgets.DrawHighlight(row);
                }
                
                Widgets.Label(new Rect(row.x + 5f, row.y + 5f, row.width - 10f, row.height - 5f), t.LabelCap);
            }
            Widgets.EndScrollView();

            // Right panel: Options
            Rect rightPanel = new Rect(inRect.x + inRect.width * 0.4f + 10f, inRect.y + 120f, inRect.width * 0.6f - 10f, inRect.height - 180f);
            if (selectedTrader != null)
            {
                DrawDispatchOptions(rightPanel);
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(rightPanel, "Select a trader from the list.");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private void DrawDispatchOptions(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 30f), string.Format("Dispatch: {0}", selectedTrader.LabelCap));
            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(rect.x, rect.y + 35f, rect.width);

            float y = rect.y + 50f;

            int socialLevel = negotiator.skills.GetSkill(SkillDefOf.Social).Level;
            int flatDiscount = socialLevel * 10;

            if (!isCaravanSelected)
            {
                // Economy Route
                int ecoCost = Math.Max(0, CallForATraderMod.settings.economyCost - flatDiscount);
                bool ecoCanAfford = availableSilver >= ecoCost && !onCooldown;
                DrawOption(new Rect(rect.x, y, rect.width, 60f), 
                    "Economy Cargo Route", 
                    ecoCost == 0 ? "<color=#44FF44>Free</color>" : string.Format("{0} silver", ecoCost), 
                    "1-10 days", 
                    ecoCanAfford,
                    onCooldown ? "On Cooldown" : "Insufficient Silver",
                    delegate { ConfirmDispatch(ecoCost, Rand.RangeInclusive(1, 10) * 60000, false); });
                
                y += 70f;

                // Priority Hyper-Jump
                int priorityCost = Math.Max(0, CallForATraderMod.settings.priorityCost - flatDiscount);
                bool prioCanAfford = availableSilver >= priorityCost && !onCooldown;
                DrawOption(new Rect(rect.x, y, rect.width, 60f), 
                    "Priority Hyper-Jump", 
                    priorityCost == 0 ? "<color=#44FF44>Free</color>" : string.Format("{0} silver", priorityCost), 
                    "2-6 hours", 
                    prioCanAfford,
                    onCooldown ? "On Cooldown" : "Insufficient Silver",
                    delegate { ConfirmDispatch(priorityCost, Rand.RangeInclusive(2, 6) * 2500, false); });
                
                y += 70f;

                // Negotiate Broker Fee
                float discountFactor = socialLevel / 20f;
                int negotiatedCost = (int)(CallForATraderMod.settings.negotiateCost * (1f - (discountFactor * 0.5f)));
                bool negCanAfford = availableSilver >= negotiatedCost && !onCooldown;
                DrawOption(new Rect(rect.x, y, rect.width, 60f), 
                    string.Format("Negotiate Fee ({0})", negotiator.LabelShort), 
                    string.Format("Est. {0} silver", negotiatedCost), 
                    "12-24 hours", 
                    negCanAfford,
                    onCooldown ? "On Cooldown" : "Insufficient Silver",
                    delegate { Negotiate(negotiatedCost); });
            }
            else
            {
                // Caravan Route
                int caravanCost = Math.Max(0, CallForATraderMod.settings.caravanCost - flatDiscount);
                bool carCanAfford = availableSilver >= caravanCost && !onCooldown;
                DrawOption(new Rect(rect.x, y, rect.width, 60f), 
                    "On-Ground Caravan", 
                    caravanCost == 0 ? "<color=#44FF44>Free</color>" : string.Format("{0} silver", caravanCost), 
                    "1-3 days", 
                    carCanAfford,
                    onCooldown ? "On Cooldown" : "Insufficient Silver",
                    delegate { ConfirmDispatch(caravanCost, Rand.RangeInclusive(1, 3) * 60000, true); });
            }
        }

        private void DrawOption(Rect rect, string title, string cost, string delay, bool enabled, string disableReason, Action onAccept)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(5f);
            
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width - 120f, 25f), title);
            
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inner.x, inner.y + 25f, inner.width - 120f, 20f), string.Format("Cost: {0} | Delay: {1}", cost, delay));
            Text.Font = GameFont.Small;

            Rect btnRect = new Rect(inner.xMax - 110f, inner.y + 5f, 110f, 40f);
            if (enabled)
            {
                if (Widgets.ButtonText(btnRect, "Confirm"))
                {
                    if (onAccept != null)
                    {
                        onAccept.Invoke();
                    }
                }
            }
            else
            {
                GUI.color = Color.grey;
                Widgets.ButtonText(btnRect, disableReason);
                GUI.color = Color.white;
            }
        }

        private void ConfirmDispatch(int cost, int delayTicks, bool isCaravan)
        {
            if (cost > 0)
            {
                CallForATraderUtility.DeductSilver(map, cost);
            }
            
            var comp = map.GetComponent<CallForATraderMapComponent>();
            if (comp != null)
            {
                comp.ScheduleArrival(selectedTrader, delayTicks, isCaravan);
            }
            
            var tracker = Find.World.GetComponent<CallForATraderTracker>();
            if (tracker != null)
            {
                tracker.TriggerCooldown(map);
            }

            string letterType = isCaravan ? "Caravan" : "Orbital Trader";
            string delayStr = CallForATraderMapComponent.FormatTicksToTime(delayTicks);
            Find.LetterStack.ReceiveLetter(
                "Trader Requested",
                string.Format("Your request for a {0} {1} has been confirmed. Arrival in {2}.", selectedTrader.label, letterType, delayStr),
                LetterDefOf.PositiveEvent,
                new LookTargets(building)
            );

            this.Close();
        }

        private void Negotiate(int baseCost)
        {
            float roll = Rand.Range(-0.2f, 0.1f);
            int finalCost = Math.Max(50, (int)(baseCost * (1f + roll)));
            int delayTicks = Rand.RangeInclusive(12, 24) * 2500;
            bool success = roll <= 0f;

            string text = success
                ? string.Format("{0} successfully bargained! They agreed to a reduced fee of {1} silver.", negotiator.LabelShort, finalCost)
                : string.Format("The brokers were stubborn and demanded a higher fee of {0} silver.", finalCost);
            
            text += string.Format("\n\nThe ship will arrive in {0}.", CallForATraderMapComponent.FormatTicksToTime(delayTicks));

            DiaNode resultNode = new DiaNode(text);
            
            DiaOption optConfirm = new DiaOption(string.Format("Pay and Confirm ({0} silver)", finalCost));
            if (CallForATraderUtility.GetAvailableSilver(map) < finalCost)
            {
                optConfirm.disabled = true;
                optConfirm.disabledReason = "Insufficient silver";
            }
            else
            {
                optConfirm.resolveTree = true;
                optConfirm.action = delegate { ConfirmDispatch(finalCost, delayTicks, false); };
            }
            resultNode.options.Add(optConfirm);

            DiaOption optCancel = new DiaOption("Cancel Call");
            optCancel.resolveTree = true;
            resultNode.options.Add(optCancel);

            Find.WindowStack.Add(new Dialog_NodeTree(resultNode, true, false, "Negotiation Results"));
            this.Close();
        }
    }
}
