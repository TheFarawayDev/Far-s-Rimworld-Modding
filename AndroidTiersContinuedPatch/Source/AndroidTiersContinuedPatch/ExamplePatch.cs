using System;
using HarmonyLib;
using Verse;
using RimWorld;

namespace AndroidTiersContinuedPatch
{
    // Example patch: this will run after a specified method in AndroidTiersContinued
    /*
    [HarmonyPatch(typeof(AndroidTiersContinued.SomeClass), "SomeMethod")]
    public static class ExamplePatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            Log.Message("Hello from Android Tiers Continued Patch!");
        }
    }
    */
}
