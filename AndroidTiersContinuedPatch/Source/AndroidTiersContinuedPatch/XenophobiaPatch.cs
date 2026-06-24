using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AndroidTiersContinuedPatch
{
    [HarmonyPatch(typeof(SituationalThoughtHandler), "AppendSocialThoughts")]
    public static class XenophobiaPatch
    {
        [HarmonyPostfix]
        public static void Postfix(SituationalThoughtHandler __instance, Pawn otherPawn, List<ThoughtDef> outThoughts)
        {
            // Access the private pawn field using Traverse
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            
            if (pawn != null && otherPawn != null)
            {
                // If one of them is an Android, and the other is a Human (or anything else)
                bool isAndroidInteraction = pawn.def.defName.StartsWith("Android") || otherPawn.def.defName.StartsWith("Android");
                
                if (isAndroidInteraction)
                {
                    // Iterate backwards so we can safely remove elements
                    for (int i = outThoughts.Count - 1; i >= 0; i--)
                    {
                        ThoughtDef thought = outThoughts[i];
                        if (thought != null)
                        {
                            string defName = thought.defName;
                            // Check if the thought is related to xenophobia or being an alien
                            if (defName.Contains("Xenophobia") || defName.Contains("Alien"))
                            {
                                outThoughts.RemoveAt(i);
                            }
                        }
                    }
                }
            }
        }
    }
}
