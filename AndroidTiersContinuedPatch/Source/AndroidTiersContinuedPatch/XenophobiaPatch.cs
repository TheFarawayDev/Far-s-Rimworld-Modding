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
        public static void Postfix(SituationalThoughtHandler __instance, Pawn otherPawn, List<ISocialThought> outThoughts)
        {
            // Access the private pawn field using Traverse
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            
            if (pawn != null && otherPawn != null)
            {
                // If one of them is an Android, and the other is a Human (or anything else)
                bool isAndroidInteraction = pawn.def.defName.StartsWith("Android") || otherPawn.def.defName.StartsWith("Android") || pawn.def.defName.Contains("Droid") || otherPawn.def.defName.Contains("Droid") || pawn.def.defName.Contains("boi") || otherPawn.def.defName.Contains("boi");
                
                if (isAndroidInteraction)
                {
                    // Iterate backwards so we can safely remove elements
                    for (int i = outThoughts.Count - 1; i >= 0; i--)
                    {
                        Thought thought = outThoughts[i] as Thought;
                        if (thought != null && thought.def != null && thought.def.defName != null)
                        {
                            string defName = thought.def.defName;
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
