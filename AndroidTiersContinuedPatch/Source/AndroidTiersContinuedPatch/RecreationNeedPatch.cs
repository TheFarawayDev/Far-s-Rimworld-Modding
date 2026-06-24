using HarmonyLib;
using RimWorld;
using Verse;

namespace AndroidTiersContinuedPatch
{
    [HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
    public static class RecreationNeedPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn_NeedsTracker __instance, NeedDef nd, ref bool __result)
        {
            // If the game already determined they don't need it, we don't need to do anything
            if (!__result) return;

            // Target only the Joy/Recreation need
            if (nd.defName == "Joy")
            {
                // Access the private pawn field using Traverse
                Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
                
                if (pawn != null)
                {
                    // Check if they are a T1 or T2 Android
                    if (pawn.def.defName == "Android1Tier" || pawn.def.defName == "Android2Tier")
                    {
                        // Set the result to false, meaning they shouldn't have the Joy need
                        __result = false;
                    }
                }
            }
        }
    }
}
