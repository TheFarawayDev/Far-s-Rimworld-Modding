using HarmonyLib;
using RimWorld;
using Verse;

namespace AndroidTiersContinuedPatch
{
    [HarmonyPatch(typeof(StatPart_Age), "TransformValue")]
    public static class AgePenaltyPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(StatRequest req, ref float val)
        {
            if (req.HasThing)
            {
                Pawn pawn = req.Thing as Pawn;
                if (pawn != null)
                {
                    // Check if the pawn is one of the Android Tiers
                    if (pawn.def.defName.StartsWith("Android") && pawn.def.defName.EndsWith("Tier"))
                    {
                        // Returning false skips the original method,
                        // meaning no age-based multiplier is applied to their work speed!
                        return false;
                    }
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(StatPart_Age), "ExplanationPart")]
    public static class AgePenaltyExplanationPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(StatRequest req, ref string __result)
        {
            if (req.HasThing)
            {
                Pawn pawn = req.Thing as Pawn;
                if (pawn != null)
                {
                    if (pawn.def.defName.StartsWith("Android") && pawn.def.defName.EndsWith("Tier"))
                    {
                        // Don't show the age penalty in the stat breakdown UI
                        __result = null;
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
