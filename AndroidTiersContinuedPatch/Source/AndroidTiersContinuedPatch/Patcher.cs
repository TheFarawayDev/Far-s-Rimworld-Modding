using System;
using HarmonyLib;
using Verse;
using RimWorld;

namespace AndroidTiersContinuedPatch
{
    [StaticConstructorOnStartup]
    public static class Patcher
    {
        static Patcher()
        {
            Log.Message("[AndroidTiersContinuedPatch] Initializing Harmony Patches...");
            var harmony = new Harmony("meast.AndroidTiersContinuedPatch");
            harmony.PatchAll();
            Log.Message("[AndroidTiersContinuedPatch] Harmony Patches applied.");
        }
    }
}
