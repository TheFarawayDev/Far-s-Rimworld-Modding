using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TheFarawayDev.MatterAnywhere
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        public static HashSet<Thing> virtualPlanetaryThings = new HashSet<Thing>();
        public static Dictionary<SK_Matter_Network.DataNetwork, int> lastSyncTicks = new Dictionary<SK_Matter_Network.DataNetwork, int>();
        public static bool globalLedgerDirty = true;

        static HarmonyPatches()
        {
            var harmony = new Harmony("thefarawaydev.matteranywhere");
            harmony.PatchAll();
        }

        public static bool IsRelayActiveForPull(SK_Matter_Network.DataNetwork network)
        {
            if (network.Buildings == null) return false;
            foreach (var b in network.Buildings)
            {
                if (b.def.defName == "Building_QuantumNetworkRelay" && b is Building_QuantumNetworkRelay relay)
                {
                    var comp = relay.GetComp<CompNetworkRelay>();
                    if (comp != null && comp.PowerOn && (comp.currentMode == NetworkMode.PullOnly || comp.currentMode == NetworkMode.BiDirectional))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static void SyncVirtualThings(SK_Matter_Network.DataNetwork network)
        {
            if (network == null) return;

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (lastSyncTicks.TryGetValue(network, out int lastTick) && !globalLedgerDirty)
            {
                if (currentTick - lastTick < 30) // Throttle sync checks to once every 30 ticks (half a second)
                {
                    return;
                }
            }

            lastSyncTicks[network] = currentTick;
            globalLedgerDirty = false;

            var controller = network.ActiveController;
            if (controller == null) return;

            bool isPullActive = IsRelayActiveForPull(network);
            var tracker = MatterNetworkWorldTracker.Instance;

            if (tracker != null && tracker.globalResourceLedger.Count > 0)
            {
                var keysToRemove = new List<string>();
                foreach (var kvp in tracker.globalResourceLedger)
                {
                    ThingDef def = DefDatabase<ThingDef>.GetNamed(kvp.Key, false);
                    if (def == null || def.stackLimit <= 1)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
                if (keysToRemove.Count > 0)
                {
                    foreach (var key in keysToRemove)
                    {
                        tracker.globalResourceLedger.Remove(key);
                    }
                }
            }

            if (Prefs.DevMode)
            {
                Log.Message($"[MatterAnywhere] SyncVirtualThings: Network={network.NetworkId}, isPullActive={isPullActive}, Tracker={(tracker != null ? "not null" : "null")}, LedgerCount={(tracker != null ? tracker.globalResourceLedger.Count.ToString() : "0")}");
            }

            if (!isPullActive || tracker == null || tracker.globalResourceLedger.Count == 0)
            {
                RemoveAllVirtualThings(controller);
                return;
            }

            // Sync virtual things
            foreach (var kvp in tracker.globalResourceLedger)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamed(kvp.Key, false);
                if (def == null) continue;

                int ledgerCount = kvp.Value;
                Thing virtualThing = GetVirtualThing(controller, def);

                if (ledgerCount <= 0)
                {
                    if (virtualThing != null)
                    {
                        controller.innerContainer.Remove(virtualThing);
                        virtualPlanetaryThings.Remove(virtualThing);
                    }
                    continue;
                }

                if (virtualThing == null)
                {
                    virtualThing = ThingMaker.MakeThing(def);
                    virtualThing.stackCount = ledgerCount;
                    controller.innerContainer.TryAdd(virtualThing, false);
                    virtualPlanetaryThings.Add(virtualThing);
                }
                else
                {
                    virtualThing.stackCount = ledgerCount;
                }
            }

            // Remove any virtual things that are no longer in the ledger
            var toRemove = new List<Thing>();
            foreach (var vt in virtualPlanetaryThings)
            {
                if (vt.holdingOwner == controller.innerContainer && !tracker.globalResourceLedger.ContainsKey(vt.def.defName))
                {
                    toRemove.Add(vt);
                }
            }
            foreach (var vt in toRemove)
            {
                controller.innerContainer.Remove(vt);
                virtualPlanetaryThings.Remove(vt);
            }
        }

        private static void RemoveAllVirtualThings(SK_Matter_Network.NetworkBuildingController controller)
        {
            var toRemove = new List<Thing>();
            foreach (var vt in virtualPlanetaryThings)
            {
                if (vt.holdingOwner == controller.innerContainer)
                {
                    toRemove.Add(vt);
                }
            }
            foreach (var vt in toRemove)
            {
                controller.innerContainer.Remove(vt);
                virtualPlanetaryThings.Remove(vt);
            }
        }

        private static Thing GetVirtualThing(SK_Matter_Network.NetworkBuildingController controller, ThingDef def)
        {
            foreach (var t in controller.innerContainer)
            {
                if (t.def == def && virtualPlanetaryThings.Contains(t))
                {
                    return t;
                }
            }
            return null;
        }
    }

    [HarmonyPatch("SK_Matter_Network.Patches.NetworkItemSearchUtility", "AllNetworkItems")]
    public static class Patch_AllNetworkItems
    {
        public static void Prefix(Map map)
        {
            var comp = map.GetComponent<SK_Matter_Network.NetworksMapComponent>();
            if (comp?.Networks == null) return;
            foreach (var network in comp.Networks)
            {
                HarmonyPatches.SyncVirtualThings(network);
            }
        }
    }

    [HarmonyPatch(typeof(SK_Matter_Network.DataNetwork), "DropActiveItem")]
    public static class Patch_DropActiveItem
    {
        public static void Prefix(SK_Matter_Network.DataNetwork __instance, Thing item, ref int count)
        {
            if (item != null && HarmonyPatches.virtualPlanetaryThings.Contains(item))
            {
                string defName = item.def.defName;
                var tracker = MatterNetworkWorldTracker.Instance;
                if (tracker != null && tracker.globalResourceLedger.TryGetValue(defName, out int ledgerCount))
                {
                    int toDeduct = Math.Min(count, ledgerCount);
                    tracker.globalResourceLedger[defName] -= toDeduct;
                    if (tracker.globalResourceLedger[defName] <= 0)
                    {
                        tracker.globalResourceLedger.Remove(defName);
                    }
                    HarmonyPatches.globalLedgerDirty = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(SK_Matter_Network.NetworkBuildingController), "ExposeData")]
    public static class Patch_NetworkBuildingController_ExposeData
    {
        public static void Prefix(SK_Matter_Network.NetworkBuildingController __instance, out List<Thing> __state)
        {
            __state = new List<Thing>();
            if (__instance.innerContainer == null) return;

            var toRemove = new List<Thing>();
            foreach (var t in __instance.innerContainer)
            {
                if (HarmonyPatches.virtualPlanetaryThings.Contains(t))
                {
                    toRemove.Add(t);
                }
            }
            foreach (var t in toRemove)
            {
                __instance.innerContainer.Remove(t);
                __state.Add(t);
            }
        }

        public static void Postfix(SK_Matter_Network.NetworkBuildingController __instance, List<Thing> __state)
        {
            if (__instance.innerContainer == null || __state == null) return;

            foreach (var t in __state)
            {
                __instance.innerContainer.TryAdd(t, false);
            }
        }
    }

    [HarmonyPatch(typeof(SK_Matter_Network.DataNetwork), "get_ItemCountByDef")]
    public static class Patch_ItemCountByDef
    {
        public static void Prefix(SK_Matter_Network.DataNetwork __instance)
        {
            HarmonyPatches.SyncVirtualThings(__instance);
        }

        public static void Postfix(SK_Matter_Network.DataNetwork __instance, ref IReadOnlyDictionary<ThingDef, int> __result)
        {
            if (__result == null || !__instance.IsOperational) return;

            if (HarmonyPatches.IsRelayActiveForPull(__instance))
            {
                var tracker = MatterNetworkWorldTracker.Instance;
                if (tracker != null && tracker.globalResourceLedger.Count > 0)
                {
                    var newDict = new Dictionary<ThingDef, int>();
                    foreach (var kvp in __result)
                    {
                        newDict[kvp.Key] = kvp.Value;
                    }
                    foreach (var kvp in tracker.globalResourceLedger)
                    {
                        ThingDef def = DefDatabase<ThingDef>.GetNamed(kvp.Key, false);
                        if (def != null)
                        {
                            if (newDict.ContainsKey(def))
                                newDict[def] += kvp.Value;
                            else
                                newDict[def] = kvp.Value;
                        }
                    }
                    __result = newDict;
                }
            }
        }
    }

    [HarmonyPatch(typeof(SK_Matter_Network.DataNetwork), "get_ItemDefToStackCount")]
    public static class Patch_ItemDefToStackCount
    {
        public static void Prefix(SK_Matter_Network.DataNetwork __instance)
        {
            HarmonyPatches.SyncVirtualThings(__instance);
        }

        public static void Postfix(SK_Matter_Network.DataNetwork __instance, ref Dictionary<ThingDef, int> __result)
        {
            if (__result == null || !__instance.IsOperational) return;

            if (HarmonyPatches.IsRelayActiveForPull(__instance))
            {
                var tracker = MatterNetworkWorldTracker.Instance;
                if (tracker != null && tracker.globalResourceLedger.Count > 0)
                {
                    var newDict = new Dictionary<ThingDef, int>();
                    foreach (var kvp in __result)
                    {
                        newDict[kvp.Key] = kvp.Value;
                    }
                    foreach (var kvp in tracker.globalResourceLedger)
                    {
                        ThingDef def = DefDatabase<ThingDef>.GetNamed(kvp.Key, false);
                        if (def != null)
                        {
                            if (newDict.ContainsKey(def))
                                newDict[def] += kvp.Value;
                            else
                                newDict[def] = kvp.Value;
                        }
                    }
                    __result = newDict;
                }
            }
        }
    }

    [HarmonyPatch(typeof(SK_Matter_Network.DataNetwork), "get_StoredItems")]
    public static class Patch_StoredItems
    {
        public static void Prefix(SK_Matter_Network.DataNetwork __instance)
        {
            HarmonyPatches.SyncVirtualThings(__instance);
        }
    }
}
