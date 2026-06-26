using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using RimWorld;
using HarmonyLib;

namespace TheGarbageCollector
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            try
            {
                var harmony = new Harmony("thefarawaydev.garbagecollector");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Message("[TheGarbageCollector] Successfully initialized Harmony patches.");
            }
            catch (Exception ex)
            {
                Log.Error("[TheGarbageCollector] Critical patch initialization error: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(MemoryThoughtHandler), "TryGainMemory", new Type[] { typeof(Thought_Memory), typeof(Pawn) })]
    public static class Patch_MemoryThoughtHandler_TryGainMemory
    {
        public static bool Prefix(Pawn ___pawn, Thought_Memory newThought)
        {
            if (___pawn != null && ___pawn.kindDef != null && ___pawn.kindDef.defName == "GarbageCollector")
            {
                // Block observed rotting and clean corpse thoughts
                if (newThought.def != null && (newThought.def.defName.Contains("Corpse") || newThought.def.defName.Contains("Rotting")))
                {
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn), "PreApplyDamage")]
    public static class Patch_Pawn_PreApplyDamage
    {
        public static bool Prefix(Pawn __instance, ref DamageInfo dinfo, out bool absorbed)
        {
            if (__instance != null && __instance.kindDef != null && __instance.kindDef.defName == "GarbageCollector")
            {
                absorbed = true;
                return false; // Absorb and skip damage
            }
            absorbed = false;
            return true;
        }
    }

    [HarmonyPatch(typeof(GenHostility), "HostileTo", new Type[] { typeof(Thing), typeof(Thing) })]
    public static class Patch_GenHostility_HostileTo_Thing_Thing
    {
        public static bool Prefix(Thing a, Thing b, ref bool __result)
        {
            Pawn pawnA = a as Pawn;
            Pawn pawnB = b as Pawn;
            if ((pawnA != null && pawnA.kindDef != null && pawnA.kindDef.defName == "GarbageCollector") ||
                (pawnB != null && pawnB.kindDef != null && pawnB.kindDef.defName == "GarbageCollector"))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(GenHostility), "HostileTo", new Type[] { typeof(Thing), typeof(Faction) })]
    public static class Patch_GenHostility_HostileTo_Thing_Faction
    {
        public static bool Prefix(Thing t, Faction fac, ref bool __result)
        {
            Pawn pawn = t as Pawn;
            if (pawn != null && pawn.kindDef != null && pawn.kindDef.defName == "GarbageCollector")
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn), "SpawnSetup")]
    public static class Patch_Pawn_SpawnSetup
    {
        public static void Postfix(Pawn __instance, Map map, bool respawningAfterLoad)
        {
            if (respawningAfterLoad) return;
            if (__instance.kindDef != null && __instance.kindDef.defName == "GarbageCollector")
            {
                var tracker = map.GetComponent<GarbageCollectorTracker>();
                if (tracker == null)
                {
                    tracker = new GarbageCollectorTracker(map);
                    map.components.Add(tracker);
                }
                tracker.Register(__instance, IncidentWorker_SummonGarbageCollector.currentSummonMode, IncidentWorker_SummonGarbageCollector.currentSummonPullFromStorage);
            }
        }
    }

}

