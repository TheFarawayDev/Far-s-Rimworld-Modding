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
            
            // Task 2: Conditional Patch for Yayo's Animation
            if (ModLister.HasActiveModWithName("Yayo's Animation") || ModLister.HasActiveModWithName("Yayo's Animation (Continued)"))
            {
                System.Reflection.MethodInfo originalRender = null;
                System.Reflection.MethodInfo yayoCompatPrefix = null;

                if (RimWorld.VersionControl.CurrentMajor == 1 && RimWorld.VersionControl.CurrentMinor >= 6)
                {
                    originalRender = AccessTools.Method(typeof(PawnRenderer), "RenderPawnInternal", new[] { typeof(Verse.PawnDrawParms) });
                    yayoCompatPrefix = AccessTools.Method(typeof(YayosAnimationCompatibilityPatch_16), nameof(YayosAnimationCompatibilityPatch_16.Prefix));
                }
                else
                {
                    originalRender = AccessTools.Method(typeof(PawnRenderer), "RenderPawnInternal", new[] { 
                        typeof(UnityEngine.Vector3), typeof(float), typeof(bool), typeof(Rot4), typeof(RotDrawMode), typeof(PawnRenderFlags) 
                    });
                    yayoCompatPrefix = AccessTools.Method(typeof(YayosAnimationCompatibilityPatch), nameof(YayosAnimationCompatibilityPatch.Prefix));
                }
                
                if (originalRender != null && yayoCompatPrefix != null)
                {
                    harmony.Patch(originalRender, prefix: new HarmonyMethod(yayoCompatPrefix));
                    Log.Message("[AndroidTiersContinuedPatch] Successfully injected Yayo's Animation compatibility patch.");
                }
                else
                {
                    Log.Warning("[AndroidTiersContinuedPatch] Failed to find RenderPawnInternal. Yayo's Animation compatibility patch aborted.");
                }
            }
            
            Log.Message("[AndroidTiersContinuedPatch] Harmony Patches applied.");
        }
    }
}
