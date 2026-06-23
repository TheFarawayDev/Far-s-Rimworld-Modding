using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;
using RimWorld.Planet;
using HarmonyLib;
using UnityEngine;

namespace CallForATrader
{
    // ==========================================
    // 1. UTILITY METHODS
    // ==========================================
    public static class CallForATraderUtility
    {
        public static int GetAvailableSilver(Map map)
        {
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

        public static void DeductSilver(Map map, int amount)
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
        }
    }

    public static class PassingShipReflectionHelper
    {
        private static FieldInfo loadIdField = typeof(PassingShip).GetField("loadID", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static FieldInfo ticksLeftField = typeof(PassingShip).GetField("ticksLeft", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        public static int GetLoadID(PassingShip ship)
        {
            if (ship == null) return -1;
            if (loadIdField != null)
            {
                return (int)loadIdField.GetValue(ship);
            }
            return ship.GetHashCode();
        }

        public static int GetTicksLeft(PassingShip ship)
        {
            if (ship == null) return 0;
            if (ticksLeftField != null)
            {
                return (int)ticksLeftField.GetValue(ship);
            }
            return 0;
        }
    }

    // ==========================================
    // 2. MOD SETTINGS & REGISTRATION
    // ==========================================
    public class CallForATraderSettings : ModSettings
    {
        public int economyCost = 150;
        public int priorityCost = 500;
        public int negotiateCost = 300;
        public int caravanCost = 200;
        public int cooldownTicks = 60000; // 24 hours
        public int letterTimeoutTicks = 10000; // 4 hours

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref economyCost, "economyCost", 150);
            Scribe_Values.Look(ref priorityCost, "priorityCost", 500);
            Scribe_Values.Look(ref negotiateCost, "negotiateCost", 300);
            Scribe_Values.Look(ref caravanCost, "caravanCost", 200);
            Scribe_Values.Look(ref cooldownTicks, "cooldownTicks", 60000);
            Scribe_Values.Look(ref letterTimeoutTicks, "letterTimeoutTicks", 10000);
        }
    }

    public class CallForATraderMod : Mod
    {
        public static CallForATraderSettings settings;

        public CallForATraderMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<CallForATraderSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            
            // Economy Cost
            listingStandard.Label("Economy Cargo Route Cost: " + settings.economyCost + " Silver");
            settings.economyCost = (int)listingStandard.Slider(settings.economyCost, 0f, 1000f);

            // Priority Cost
            listingStandard.Label("Priority Hyper-Jump Cost: " + settings.priorityCost + " Silver");
            settings.priorityCost = (int)listingStandard.Slider(settings.priorityCost, 0f, 2000f);

            // Negotiate Cost
            listingStandard.Label("Bargain Negotiation Base Cost: " + settings.negotiateCost + " Silver");
            settings.negotiateCost = (int)listingStandard.Slider(settings.negotiateCost, 0f, 1500f);

            // Caravan Cost
            listingStandard.Label("On-Ground Caravan Cost: " + settings.caravanCost + " Silver");
            settings.caravanCost = (int)listingStandard.Slider(settings.caravanCost, 0f, 1000f);

            // Cooldown
            int cooldownHours = settings.cooldownTicks / 2500;
            listingStandard.Label("Call Cooldown: " + cooldownHours + " Hours (" + settings.cooldownTicks + " Ticks)");
            float cooldownSlider = listingStandard.Slider(cooldownHours, 0f, 120f);
            settings.cooldownTicks = (int)cooldownSlider * 2500;

            // Letter Timeout
            int timeoutHours = settings.letterTimeoutTicks / 2500;
            listingStandard.Label("Offer Letter Expiry: " + timeoutHours + " Hours (" + settings.letterTimeoutTicks + " Ticks)");
            float timeoutSlider = listingStandard.Slider(timeoutHours, 0f, 24f);
            settings.letterTimeoutTicks = (int)timeoutSlider * 2500;

            listingStandard.End();
        }

        public override string SettingsCategory()
        {
            return "Call for a Trader";
        }
    }

    // ==========================================
    // 3. COOLDOWN TRACKER (WORLD COMPONENT)
    // ==========================================
    public class CallForATraderTracker : WorldComponent
    {
        private List<int> mapKeys = new List<int>();
        private List<int> mapTicks = new List<int>();

        public CallForATraderTracker(World world) : base(world)
        {
        }

        public bool IsOnCooldown(Map map, out int remainingTicks)
        {
            remainingTicks = 0;
            if (map == null) return false;
            int idx = mapKeys.IndexOf(map.uniqueID);
            if (idx >= 0)
            {
                int nextTick = mapTicks[idx];
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick < nextTick)
                {
                    remainingTicks = nextTick - currentTick;
                    return true;
                }
            }
            return false;
        }

        public void TriggerCooldown(Map map)
        {
            if (map == null) return;
            int cooldown = CallForATraderMod.settings.cooldownTicks;
            int nextTick = Find.TickManager.TicksGame + cooldown;

            int idx = mapKeys.IndexOf(map.uniqueID);
            if (idx >= 0)
            {
                mapTicks[idx] = nextTick;
            }
            else
            {
                mapKeys.Add(map.uniqueID);
                mapTicks.Add(nextTick);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref mapKeys, "mapKeys", LookMode.Value);
            Scribe_Collections.Look(ref mapTicks, "mapTicks", LookMode.Value);

            if (mapKeys == null) mapKeys = new List<int>();
            if (mapTicks == null) mapTicks = new List<int>();
        }
    }

    // ==========================================
    // 4. MAP COMPONENT & PENDING ARRIVALS
    // ==========================================
    public class PendingTraderArrival : IExposable
    {
        public int uniqueID;
        public int arrivalTick;
        public TraderKindDef trader;
        public bool spawned;
        public int shipLoadID = -1;
        public string shipName = "";
        public bool isCaravan;

        public PendingTraderArrival()
        {
        }

        public PendingTraderArrival(int uniqueID, int arrivalTick, TraderKindDef trader, bool isCaravan)
        {
            this.uniqueID = uniqueID;
            this.arrivalTick = arrivalTick;
            this.trader = trader;
            this.spawned = false;
            this.shipLoadID = -1;
            this.shipName = "";
            this.isCaravan = isCaravan;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref uniqueID, "uniqueID", 0);
            Scribe_Values.Look(ref arrivalTick, "arrivalTick", 0);
            Scribe_Defs.Look(ref trader, "trader");
            Scribe_Values.Look(ref spawned, "spawned", false);
            Scribe_Values.Look(ref shipLoadID, "shipLoadID", -1);
            Scribe_Values.Look(ref shipName, "shipName", "");
            Scribe_Values.Look(ref isCaravan, "isCaravan", false);
        }
    }

    public class CallForATraderMapComponent : MapComponent
    {
        public List<PendingTraderArrival> pendingArrivals = new List<PendingTraderArrival>();
        private int nextArrivalID = 0;
        private int lastUpdateTick = 0;

        public CallForATraderMapComponent(Map map) : base(map)
        {
        }

        public void ScheduleArrival(TraderKindDef trader, int ticksDelay, bool isCaravan = false)
        {
            if (trader == null) return;
            nextArrivalID++;
            int tick = Find.TickManager.TicksGame + ticksDelay;
            PendingTraderArrival arrival = new PendingTraderArrival(nextArrivalID, tick, trader, isCaravan);
            pendingArrivals.Add(arrival);

            CreateTrackingLetter(arrival);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            int currentTick = Find.TickManager.TicksGame;

            for (int i = pendingArrivals.Count - 1; i >= 0; i--)
            {
                PendingTraderArrival arrival = pendingArrivals[i];
                if (!arrival.spawned)
                {
                    if (currentTick >= arrival.arrivalTick)
                    {
                        if (arrival.isCaravan)
                        {
                            SpawnCaravan(arrival.trader);
                            pendingArrivals.RemoveAt(i);
                            DismissTrackingLetter(arrival);
                        }
                        else
                        {
                            TradeShip tradeShip = SpawnTrader(arrival.trader);
                            if (tradeShip != null)
                            {
                                arrival.spawned = true;
                                arrival.shipLoadID = PassingShipReflectionHelper.GetLoadID(tradeShip);
                                arrival.shipName = tradeShip.name;
                            }
                            else
                            {
                                pendingArrivals.RemoveAt(i);
                                DismissTrackingLetter(arrival);
                            }
                        }
                    }
                }
                else
                {
                    TradeShip ship = FindSpawnedShip(arrival.shipLoadID);
                    if (ship == null || PassingShipReflectionHelper.GetTicksLeft(ship) <= 0)
                    {
                        pendingArrivals.RemoveAt(i);
                        DismissTrackingLetter(arrival);
                    }
                }
            }

            if (currentTick - lastUpdateTick >= 60)
            {
                lastUpdateTick = currentTick;
                UpdateTrackingLetters();
            }
        }

        private void CreateTrackingLetter(PendingTraderArrival arrival)
        {
            try
            {
                LetterDef letterDef = DefDatabase<LetterDef>.GetNamed("Letter_TraderTracking", false);
                ChoiceLetter letter;
                if (letterDef != null)
                {
                    letter = (ChoiceLetter_TraderTracking)LetterMaker.MakeLetter(letterDef);
                }
                else
                {
                    letter = (ChoiceLetter)LetterMaker.MakeLetter(LetterDefOf.PositiveEvent);
                }
                
                int remTicks = Math.Max(0, arrival.arrivalTick - Find.TickManager.TicksGame);
                string timeStr = FormatTicksToTime(remTicks);

                string typeStr = arrival.isCaravan ? "Caravan" : "Trade Ship";
                string verbStr = arrival.isCaravan ? "approaching" : "arriving";

                letter.Label = string.Format("{0} {1} ({2})", arrival.trader.LabelCap.ToString(), verbStr, timeStr);
                letter.Text = string.Format("A requested {0} ({1}) is currently en route to the colony. It will arrive in {2} (live countdown).\n\nUse this letter to locate the Comms Console.\n\n{3}", 
                    arrival.trader.label, typeStr, timeStr, GetLetterIDTag(arrival));
                
                Building commsConsole = FindCommsConsole();
                letter.lookTargets = commsConsole != null ? new LookTargets(commsConsole) : LookTargets.Invalid;

                Find.LetterStack.ReceiveLetter(letter);
            }
            catch (Exception ex)
            {
                Log.Warning("[CallForATrader] Failed to create tracking letter: " + ex);
            }
        }

        private void UpdateTrackingLetters()
        {
            int currentTick = Find.TickManager.TicksGame;
            for (int i = 0; i < pendingArrivals.Count; i++)
            {
                PendingTraderArrival arrival = pendingArrivals[i];
                ChoiceLetter letter = FindTrackingLetter(arrival);
                if (letter != null)
                {
                    if (!arrival.spawned)
                    {
                        int remTicks = Math.Max(0, arrival.arrivalTick - currentTick);
                        string timeStr = FormatTicksToTime(remTicks);

                        string typeStr = arrival.isCaravan ? "Caravan" : "Trade Ship";
                        string verbStr = arrival.isCaravan ? "approaching" : "arriving";

                        letter.Label = string.Format("{0} {1} ({2})", arrival.trader.LabelCap.ToString(), verbStr, timeStr);
                        letter.Text = string.Format("A requested {0} ({1}) is currently en route to the colony. It will arrive in {2} (live countdown).\n\nUse this letter to locate the Comms Console.\n\n{3}", 
                            arrival.trader.label, typeStr, timeStr, GetLetterIDTag(arrival));
                    }
                    else
                    {
                        TradeShip ship = FindSpawnedShip(arrival.shipLoadID);
                        int remTicks = ship != null ? Math.Max(0, PassingShipReflectionHelper.GetTicksLeft(ship)) : 0;
                        string timeStr = FormatTicksToTime(remTicks);

                        string labelText = !string.IsNullOrEmpty(arrival.shipName) ? arrival.shipName : arrival.trader.LabelCap.ToString();
                        letter.Label = string.Format("{0} in orbit ({1})", labelText, timeStr);
                        letter.Text = string.Format("The requested orbital trader {0} ({1}) is currently in orbit.\nIt will depart in {2} (live countdown).\n\nUse this letter to locate the Comms Console.\n\n{3}", 
                            labelText, arrival.trader.label, timeStr, GetLetterIDTag(arrival));
                    }
                }
            }
        }

        private void DismissTrackingLetter(PendingTraderArrival arrival)
        {
            ChoiceLetter letter = FindTrackingLetter(arrival);
            if (letter != null)
            {
                Find.LetterStack.RemoveLetter(letter);
            }
        }

        private ChoiceLetter FindTrackingLetter(PendingTraderArrival arrival)
        {
            string tag = GetLetterIDTag(arrival);
            List<Letter> letters = Find.LetterStack.LettersListForReading;
            for (int i = 0; i < letters.Count; i++)
            {
                ChoiceLetter choiceLetter = letters[i] as ChoiceLetter;
                if (choiceLetter != null && choiceLetter.Text != null && choiceLetter.Text.ToString().Contains(tag))
                {
                    return choiceLetter;
                }
            }
            return null;
        }

        private string GetLetterIDTag(PendingTraderArrival arrival)
        {
            return string.Format("[ID: CallForATrader_Arrival_{0}]", arrival.uniqueID);
        }

        private Building FindCommsConsole()
        {
            if (map == null) return null;
            foreach (Building b in map.listerBuildings.allBuildingsColonist)
            {
                if (b is Building_CommsConsole)
                {
                    return b;
                }
            }
            return null;
        }

        public static string FormatTicksToTime(int ticks)
        {
            if (ticks >= 60000)
            {
                float days = ticks / 60000f;
                return days.ToString("F1") + " days";
            }
            else if (ticks >= 2500)
            {
                float hours = ticks / 2500f;
                return hours.ToString("F1") + " hours";
            }
            else
            {
                int minutes = (int)(ticks / 41.67f);
                return minutes + " minutes";
            }
        }

        private TradeShip FindSpawnedShip(int loadID)
        {
            if (map == null || map.passingShipManager == null) return null;
            foreach (PassingShip ship in map.passingShipManager.passingShips)
            {
                TradeShip tradeShip = ship as TradeShip;
                if (tradeShip != null && PassingShipReflectionHelper.GetLoadID(tradeShip) == loadID)
                {
                    return tradeShip;
                }
            }
            return null;
        }

        private TradeShip SpawnTrader(TraderKindDef trader)
        {
            if (map.passingShipManager.passingShips.Count >= 5)
            {
                Messages.Message("Trade ship of type " + trader.label + " could not enter orbit because there are too many ships in range.", MessageTypeDefOf.CautionInput, false);
                return null;
            }

            TradeShip tradeShip = new TradeShip(trader, null);
            map.passingShipManager.AddShip(tradeShip);
            tradeShip.GenerateThings();

            Building commsConsole = FindCommsConsole();

            Find.LetterStack.ReceiveLetter(
                tradeShip.def.LabelCap,
                "An orbital trader of type " + trader.label + " has entered comms range.",
                LetterDefOf.PositiveEvent,
                commsConsole != null ? new LookTargets(commsConsole) : LookTargets.Invalid
            );

            return tradeShip;
        }

        private Faction FindFriendlyFaction()
        {
            foreach (Faction f in Find.FactionManager.AllFactions)
            {
                if (!f.IsPlayer && !f.defeated && !f.HostileTo(Faction.OfPlayer) && f.def != null && !f.def.hidden)
                {
                    return f;
                }
            }
            foreach (Faction f in Find.FactionManager.AllFactions)
            {
                if (!f.IsPlayer && !f.defeated && !f.HostileTo(Faction.OfPlayer))
                {
                    return f;
                }
            }
            return null;
        }

        private bool SpawnCaravan(TraderKindDef trader)
        {
            IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamed("TraderCaravanArrival", false);
            if (incidentDef == null) return false;

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, map);
            parms.faction = FindFriendlyFaction();
            parms.traderKind = trader;

            return incidentDef.Worker.TryExecute(parms);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextArrivalID, "nextArrivalID", 0);
            Scribe_Collections.Look(ref pendingArrivals, "pendingArrivals", LookMode.Deep);
            if (pendingArrivals == null) pendingArrivals = new List<PendingTraderArrival>();
        }
    }

    // ==========================================
    // 5. CHOICE LETTER FOR RANDOM INCIDENT
    // ==========================================
    public class ChoiceLetter_CallForATrader : ChoiceLetter
    {
        public int cost;
        public Map map;
        public TraderKindDef selectedTrader;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (this.ArchivedOnly)
                {
                    yield return this.Option_Close;
                    yield break;
                }

                DiaOption accept = new DiaOption("Accept (Pay " + cost + " silver)");
                accept.resolveTree = true;
                
                int availableSilver = CallForATraderUtility.GetAvailableSilver(map);
                if (availableSilver < cost)
                {
                    accept.disabled = true;
                    accept.disabledReason = "Need " + cost + " silver (have " + availableSilver + ")";
                }
                else
                {
                    accept.action = delegate
                    {
                        CallForATraderUtility.DeductSilver(map, cost);
                        var comp = map.GetComponent<CallForATraderMapComponent>();
                        if (comp != null)
                        {
                            int delayDays = Rand.RangeInclusive(1, 10);
                            int delayTicks = delayDays * 60000;
                            comp.ScheduleArrival(selectedTrader, delayTicks, false);
                            
                            Messages.Message("Trade ship requested via Economy Route. It will arrive in " + delayDays + " days. (Delay is randomized between 1-10 days).", MessageTypeDefOf.PositiveEvent, false);
                        }
                        Find.LetterStack.RemoveLetter(this);
                    };
                }
                yield return accept;

                DiaOption reject = new DiaOption("Reject");
                reject.resolveTree = true;
                reject.action = delegate
                {
                    Find.LetterStack.RemoveLetter(this);
                };
                yield return reject;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cost, "cost", 0);
            Scribe_References.Look(ref map, "map");
            Scribe_Defs.Look(ref selectedTrader, "selectedTrader");
        }
    }

    // ==========================================
    // 5b. CHOICE LETTER FOR TRADER TRACKING (STICKS ON SCREEN)
    // ==========================================
    public class ChoiceLetter_TraderTracking : ChoiceLetter
    {
        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (this.ArchivedOnly)
                {
                    yield return this.Option_Close;
                    yield break;
                }

                // 1. Jump to Location
                DiaOption optJump = new DiaOption("Jump to location");
                optJump.action = delegate
                {
                    if (this.lookTargets.IsValid)
                    {
                        CameraJumper.TryJumpAndSelect(this.lookTargets.TryGetPrimaryTarget());
                    }
                };
                optJump.resolveTree = true;
                yield return optJump;

                // 2. Close (closes pop-up, but does NOT call Find.LetterStack.RemoveLetter)
                DiaOption optClose = new DiaOption("Close");
                optClose.resolveTree = true;
                yield return optClose;
            }
        }
    }

    // ==========================================
    // 6. RANDOM INCIDENT WORKER
    // ==========================================
    public class IncidentWorker_OrbitalTraderOffer : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            if (map == null) return false;

            // Check if comms console exists on map and is powered
            bool hasConsole = false;
            foreach (Building b in map.listerBuildings.allBuildingsColonist)
            {
                if (b is Building_CommsConsole)
                {
                    var power = b.GetComp<CompPowerTrader>();
                    if (power == null || power.PowerOn)
                    {
                        hasConsole = true;
                        break;
                    }
                }
            }
            if (!hasConsole) return false;

            // Check if map cooldown is active
            var tracker = Find.World.GetComponent<CallForATraderTracker>();
            int unusedRemaining;
            if (tracker != null && tracker.IsOnCooldown(map, out unusedRemaining))
            {
                return false;
            }

            return true;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;

            List<TraderKindDef> traders = new List<TraderKindDef>();
            foreach (TraderKindDef def in DefDatabase<TraderKindDef>.AllDefs)
            {
                if (def.orbital)
                {
                    traders.Add(def);
                }
            }

            if (traders.Count == 0) return false;
            TraderKindDef selected = traders.RandomElement();

            int cost = CallForATraderMod.settings.economyCost;

            LetterDef letterDef = DefDatabase<LetterDef>.GetNamed("Letter_OrbitalTraderOffer", false);
            if (letterDef == null) return false;

            ChoiceLetter_CallForATrader letter = (ChoiceLetter_CallForATrader)LetterMaker.MakeLetter(letterDef);
            letter.Label = "Orbital Trader Offer";
            letter.title = "Orbital Trader Offer";
            letter.Text = "The Orbital Traders Hub has sent an offer. They can route a " + selected.LabelCap + " to your colony via standard cargo routes in exchange for " + cost + " silver. (Economy route: will take 1-10 days to arrive). Offer expires soon.";
            letter.cost = cost;
            letter.map = map;
            letter.selectedTrader = selected;

            Building commsConsole = null;
            foreach (Building b in map.listerBuildings.allBuildingsColonist)
            {
                if (b is Building_CommsConsole)
                {
                    commsConsole = b;
                    break;
                }
            }
            if (commsConsole != null)
            {
                letter.lookTargets = new LookTargets(commsConsole);
            }

            letter.StartTimeout(CallForATraderMod.settings.letterTimeoutTicks);

            Find.LetterStack.ReceiveLetter(letter);

            var tracker = Find.World.GetComponent<CallForATraderTracker>();
            if (tracker != null)
            {
                tracker.TriggerCooldown(map);
            }

            return true;
        }
    }

    // ==========================================
    // 7. DYNAMIC ALERT (USES FARUTILS BASE CLASS)
    // ==========================================
    public class Alert_OrbitalTraderInOrbit : FarUtils.Alert_ConditionalUtility
    {
        public Alert_OrbitalTraderInOrbit()
        {
            this.defaultLabel = "Orbital Trader in Orbit";
            this.defaultExplanation = "An orbital trade ship is currently in range! Interact with the comms console to trade.";
            this.defaultPriority = AlertPriority.Medium;
        }

        protected override int TargetThreshold
        {
            get { return 0; }
        }

        protected override int GetTargetCount(Map map)
        {
            return map.passingShipManager.passingShips.Count;
        }
    }

    // ==========================================
    // 8. HARMONY CONSOLE HOOK & INITS
    // ==========================================
    [StaticConstructorOnStartup]
    public static class CallForATraderHarmony
    {
        static CallForATraderHarmony()
        {
            try
            {
                var harmony = new Harmony("thefarawaydev.callforatrader");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Message("[CallForATrader] Successfully initialized Harmony patches.");
            }
            catch (Exception ex)
            {
                Log.Error("[CallForATrader] Critical patch initialization error: " + ex);
            }
        }
    }

    public class OrbitalTradersHub : ICommunicable
    {
        private Building_CommsConsole console;

        public OrbitalTradersHub(Building_CommsConsole console)
        {
            this.console = console;
        }

        public string GetCallLabel()
        {
            return "Call the Traders Hub";
        }

        public string GetInfoText()
        {
            return "A central communication relay for trade routes.";
        }

        public void TryOpenComms(Pawn negotiator)
        {
            Find.WindowStack.Add(new Dialog_NodeTree(CallForATraderDialogCreator.MakeTraderCallNode(negotiator.Map, console, negotiator), true, false, "Traders Hub"));
        }

        public Faction GetFaction()
        {
            return null;
        }

        public FloatMenuOption CommFloatMenuOption(Building_CommsConsole console, Pawn negotiator)
        {
            return new FloatMenuOption("Call the Traders Hub", delegate
            {
                Job job = JobMaker.MakeJob(JobDefOf.UseCommsConsole, console);
                job.commTarget = this;
                negotiator.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }, MenuOptionPriority.InitiateSocial);
        }
    }

    [HarmonyPatch(typeof(Building_CommsConsole), "GetCommTargets")]
    public static class Patch_Building_CommsConsole_GetCommTargets
    {
        public static IEnumerable<ICommunicable> Postfix(IEnumerable<ICommunicable> __result, Building_CommsConsole __instance, Pawn myPawn)
        {
            List<ICommunicable> list = new List<ICommunicable>(__result);
            list.Add(new OrbitalTradersHub(__instance));
            return list;
        }
    }

    public static class CallForATraderDialogCreator
    {
        // 1. Root Node: Choice between Orbital Trade Ship and On-Ground Caravan
        public static DiaNode MakeTraderCallNode(Map map, Building_CommsConsole console, Pawn negotiator)
        {
            DiaNode root = new DiaNode("You have connected to the Traders Hub. Would you like to request an Orbital Trade Ship or an On-Ground Caravan?");

            DiaOption optOrbital = new DiaOption("Request an Orbital Trade Ship");
            optOrbital.resolveTree = false;
            optOrbital.action = delegate
            {
                optOrbital.link = MakeOrbitalTraderSelectNode(map, console, negotiator);
            };
            root.options.Add(optOrbital);

            DiaOption optCaravan = new DiaOption("Request an On-Ground Caravan");
            optCaravan.resolveTree = false;
            optCaravan.action = delegate
            {
                optCaravan.link = MakeCaravanTraderSelectNode(map, console, negotiator);
            };
            root.options.Add(optCaravan);

            DiaOption cancel = new DiaOption("Disconnect");
            cancel.resolveTree = true;
            root.options.Add(cancel);

            return root;
        }

        // 2. Orbital Trader Select Node
        public static DiaNode MakeOrbitalTraderSelectNode(Map map, Building_CommsConsole console, Pawn negotiator)
        {
            DiaNode node = new DiaNode("Which orbital trade ship would you like to request to jump to this system?");

            List<TraderKindDef> traders = new List<TraderKindDef>();
            foreach (TraderKindDef def in DefDatabase<TraderKindDef>.AllDefs)
            {
                if (def.orbital && !def.label.NullOrEmpty())
                {
                    traders.Add(def);
                }
            }

            foreach (TraderKindDef def in traders)
            {
                DiaOption opt = new DiaOption(def.LabelCap.ToString());
                opt.resolveTree = false;
                opt.action = delegate
                {
                    opt.link = MakeOrbitalDispatchChannelNode(map, console, negotiator, def);
                };
                node.options.Add(opt);
            }

            DiaOption back = new DiaOption("Go Back");
            back.resolveTree = false;
            back.action = delegate
            {
                back.link = MakeTraderCallNode(map, console, negotiator);
            };
            node.options.Add(back);

            return node;
        }

        // 3. Caravan Trader Select Node
        public static DiaNode MakeCaravanTraderSelectNode(Map map, Building_CommsConsole console, Pawn negotiator)
        {
            DiaNode node = new DiaNode("Which caravan trader would you like to request to visit this colony?");

            List<TraderKindDef> traders = new List<TraderKindDef>();
            foreach (TraderKindDef def in DefDatabase<TraderKindDef>.AllDefs)
            {
                if (!def.orbital && !def.label.NullOrEmpty())
                {
                    traders.Add(def);
                }
            }

            foreach (TraderKindDef def in traders)
            {
                DiaOption opt = new DiaOption(def.LabelCap.ToString());
                opt.resolveTree = false;
                opt.action = delegate
                {
                    opt.link = MakeCaravanDispatchChannelNode(map, console, negotiator, def);
                };
                node.options.Add(opt);
            }

            DiaOption back = new DiaOption("Go Back");
            back.resolveTree = false;
            back.action = delegate
            {
                back.link = MakeTraderCallNode(map, console, negotiator);
            };
            node.options.Add(back);

            return node;
        }

        // 4. Orbital Dispatch Channel Selection
        public static DiaNode MakeOrbitalDispatchChannelNode(Map map, Building_CommsConsole console, Pawn negotiator, TraderKindDef trader)
        {
            int availableSilver = CallForATraderUtility.GetAvailableSilver(map);
            var tracker = Find.World.GetComponent<CallForATraderTracker>();
            int remainingTicks = 0;
            bool onCooldown = tracker != null && tracker.IsOnCooldown(map, out remainingTicks);

            int economyCost = CallForATraderMod.settings.economyCost;
            int priorityCost = CallForATraderMod.settings.priorityCost;

            int socialLevel = negotiator.skills.GetSkill(SkillDefOf.Social).Level;
            float discountFactor = socialLevel / 20f;
            int negotiatedCost = (int)(CallForATraderMod.settings.negotiateCost * (1f - (discountFactor * 0.5f)));

            string cooldownStr = "";
            if (onCooldown)
            {
                cooldownStr = "\n\n[WARNING] Comm channels are on cooldown. " + CallForATraderMapComponent.FormatTicksToTime(remainingTicks) + " remaining.";
            }

            string nodeText = string.Format(
                "Select a dispatch channel for the requested orbital {0}.\n" +
                "Colony Silver Available: {1}\n\n" +
                "1. Economy Cargo Route\n" +
                "   Cost: {2} silver | Delay: 1-10 days\n\n" +
                "2. Priority Hyper-Jump\n" +
                "   Cost: {3} silver | Delay: 2-6 hours\n\n" +
                "3. Negotiate Broker Fee\n" +
                "   Negotiator: {4} (Social: {5})\n" +
                "   Base Cost: {6} silver | Est. Cost: {7} silver\n" +
                "   Delay: 12-24 hours{8}",
                trader.LabelCap.ToString(),
                availableSilver,
                economyCost,
                priorityCost,
                negotiator.LabelShort,
                socialLevel,
                CallForATraderMod.settings.negotiateCost,
                negotiatedCost,
                cooldownStr
            );

            DiaNode node = new DiaNode(nodeText);

            // Channel 1: Economy Route
            DiaOption optEconomy = new DiaOption("Economy Cargo Route");
            if (onCooldown)
            {
                optEconomy.disabled = true;
                optEconomy.disabledReason = "Cooldown: " + CallForATraderMapComponent.FormatTicksToTime(remainingTicks) + " left";
            }
            else if (availableSilver < economyCost)
            {
                optEconomy.disabled = true;
                optEconomy.disabledReason = "Need " + economyCost + " silver";
            }
            else
            {
                optEconomy.resolveTree = true;
                optEconomy.action = delegate
                {
                    CallForATraderUtility.DeductSilver(map, economyCost);
                    int delayDays = Rand.RangeInclusive(1, 10);
                    int delayTicks = delayDays * 60000;
                    
                    var comp = map.GetComponent<CallForATraderMapComponent>();
                    if (comp != null)
                    {
                        comp.ScheduleArrival(trader, delayTicks, false);
                    }
                    if (tracker != null)
                    {
                        tracker.TriggerCooldown(map);
                    }

                    Find.LetterStack.ReceiveLetter(
                        "Trade Ship Economy Request",
                        "The request for a " + trader.label + " has been registered at the economy route rate. It will arrive in " + delayDays + " days. (Delay is randomized between 1-10 days).",
                        LetterDefOf.PositiveEvent,
                        new LookTargets(console)
                    );
                };
            }
            node.options.Add(optEconomy);

            // Channel 2: Priority Jump
            DiaOption optPriority = new DiaOption("Priority Hyper-Jump");
            if (onCooldown)
            {
                optPriority.disabled = true;
                optPriority.disabledReason = "Cooldown: " + CallForATraderMapComponent.FormatTicksToTime(remainingTicks) + " left";
            }
            else if (availableSilver < priorityCost)
            {
                optPriority.disabled = true;
                optPriority.disabledReason = "Need " + priorityCost + " silver";
            }
            else
            {
                optPriority.resolveTree = true;
                optPriority.action = delegate
                {
                    CallForATraderUtility.DeductSilver(map, priorityCost);
                    int delayHours = Rand.RangeInclusive(2, 6);
                    int delayTicks = delayHours * 2500;
                    
                    var comp = map.GetComponent<CallForATraderMapComponent>();
                    if (comp != null)
                    {
                        comp.ScheduleArrival(trader, delayTicks, false);
                    }
                    if (tracker != null)
                    {
                        tracker.TriggerCooldown(map);
                    }

                    Find.LetterStack.ReceiveLetter(
                        "Trade Ship Priority Request",
                        "The request for a " + trader.label + " has been registered at the priority jump rate. It will arrive in " + delayHours + " hours. (Delay is randomized between 2-6 hours).",
                        LetterDefOf.PositiveEvent,
                        new LookTargets(console)
                    );
                };
            }
            node.options.Add(optPriority);

            // Channel 3: Broker Negotiation
            DiaOption optNegotiate = new DiaOption("Negotiate Broker Fee");
            if (onCooldown)
            {
                optNegotiate.disabled = true;
                optNegotiate.disabledReason = "Cooldown: " + CallForATraderMapComponent.FormatTicksToTime(remainingTicks) + " left";
            }
            else if (availableSilver < negotiatedCost)
            {
                optNegotiate.disabled = true;
                optNegotiate.disabledReason = "Need " + negotiatedCost + " silver";
            }
            else
            {
                optNegotiate.resolveTree = false;
                optNegotiate.action = delegate
                {
                    optNegotiate.link = MakeNegotiationResultNode(map, console, negotiator, trader, negotiatedCost);
                };
            }
            node.options.Add(optNegotiate);

            // Go back
            DiaOption back = new DiaOption("Go Back");
            back.resolveTree = false;
            back.action = delegate
            {
                back.link = MakeOrbitalTraderSelectNode(map, console, negotiator);
            };
            node.options.Add(back);

            return node;
        }

        // 5. Caravan Dispatch Channel Selection
        public static DiaNode MakeCaravanDispatchChannelNode(Map map, Building_CommsConsole console, Pawn negotiator, TraderKindDef trader)
        {
            int availableSilver = CallForATraderUtility.GetAvailableSilver(map);
            var tracker = Find.World.GetComponent<CallForATraderTracker>();
            int remainingTicks = 0;
            bool onCooldown = tracker != null && tracker.IsOnCooldown(map, out remainingTicks);

            int caravanCost = CallForATraderMod.settings.caravanCost;

            string cooldownStr = "";
            if (onCooldown)
            {
                cooldownStr = "\n\n[WARNING] Comm channels are on cooldown. " + CallForATraderMapComponent.FormatTicksToTime(remainingTicks) + " remaining.";
            }

            string nodeText = string.Format(
                "Select a dispatch channel for the requested {0} caravan.\n" +
                "Colony Silver Available: {1}\n\n" +
                "1. On-Ground Caravan Route\n" +
                "   Cost: {2} silver | Delay: 1-3 days{3}",
                trader.LabelCap.ToString(),
                availableSilver,
                caravanCost,
                cooldownStr
            );

            DiaNode node = new DiaNode(nodeText);

            // Channel 4: On Ground Caravan
            DiaOption optCaravan = new DiaOption("On-Ground Caravan Route");
            if (onCooldown)
            {
                optCaravan.disabled = true;
                optCaravan.disabledReason = "Cooldown: " + CallForATraderMapComponent.FormatTicksToTime(remainingTicks) + " left";
            }
            else if (availableSilver < caravanCost)
            {
                optCaravan.disabled = true;
                optCaravan.disabledReason = "Need " + caravanCost + " silver";
            }
            else
            {
                optCaravan.resolveTree = true;
                optCaravan.action = delegate
                {
                    CallForATraderUtility.DeductSilver(map, caravanCost);
                    int delayDays = Rand.RangeInclusive(1, 3);
                    int delayTicks = delayDays * 60000;

                    var comp = map.GetComponent<CallForATraderMapComponent>();
                    if (comp != null)
                    {
                        comp.ScheduleArrival(trader, delayTicks, true);
                    }
                    if (tracker != null)
                    {
                        tracker.TriggerCooldown(map);
                    }

                    Find.LetterStack.ReceiveLetter(
                        "Trade Caravan Request",
                        "The request for a " + trader.label + " caravan has been registered. They will arrive on foot in " + delayDays + " days. (Delay is randomized between 1-3 days).",
                        LetterDefOf.PositiveEvent,
                        new LookTargets(console)
                    );
                };
            }
            node.options.Add(optCaravan);

            // Go back
            DiaOption back = new DiaOption("Go Back");
            back.resolveTree = false;
            back.action = delegate
            {
                back.link = MakeCaravanTraderSelectNode(map, console, negotiator);
            };
            node.options.Add(back);

            return node;
        }

        // 6. Negotiation Result Node
        public static DiaNode MakeNegotiationResultNode(Map map, Building_CommsConsole console, Pawn negotiator, TraderKindDef trader, int cost)
        {
            float roll = Rand.Range(-0.2f, 0.1f);
            int finalCost = Math.Max(50, (int)(cost * (1f + roll)));
            
            int delayHours = Rand.RangeInclusive(12, 24);
            int delayTicks = delayHours * 2500;

            string text = "";
            bool success = roll <= 0f;
            if (success)
            {
                text = negotiator.LabelShort + " successfully bargained with the orbital brokers! They have agreed to schedule pathing for a reduced fee of " + finalCost + " silver. The ship will arrive in " + delayHours + " hours.";
            }
            else
            {
                text = "The brokers were stubborn and refused the discount, demanding a higher pathing fee of " + finalCost + " silver instead. The ship will arrive in " + delayHours + " hours.";
            }

            DiaNode resultNode = new DiaNode(text);

            DiaOption optConfirm = new DiaOption("Pay and Confirm (" + finalCost + " silver)");
            int availableSilver = CallForATraderUtility.GetAvailableSilver(map);
            if (availableSilver < finalCost)
            {
                optConfirm.disabled = true;
                optConfirm.disabledReason = "Need " + finalCost + " silver (have " + availableSilver + ")";
            }
            else
            {
                optConfirm.resolveTree = true;
                optConfirm.action = delegate
                {
                    CallForATraderUtility.DeductSilver(map, finalCost);
                    
                    var comp = map.GetComponent<CallForATraderMapComponent>();
                    if (comp != null)
                    {
                        comp.ScheduleArrival(trader, delayTicks, false);
                    }
                    var tracker = Find.World.GetComponent<CallForATraderTracker>();
                    if (tracker != null)
                    {
                        tracker.TriggerCooldown(map);
                    }

                    Find.LetterStack.ReceiveLetter(
                        "Trade Ship Negotiated Request",
                        "The request for a " + trader.label + " has been bargained by " + negotiator.LabelShort + " for " + finalCost + " silver. It will arrive in " + delayHours + " hours.",
                        LetterDefOf.PositiveEvent,
                        new LookTargets(console)
                    );
                };
            }
            resultNode.options.Add(optConfirm);

            DiaOption optCancel = new DiaOption("Cancel Call");
            optCancel.resolveTree = true;
            resultNode.options.Add(optCancel);

            return resultNode;
        }
    }
}
