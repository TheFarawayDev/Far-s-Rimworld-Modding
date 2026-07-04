using System.Linq;
using Verse;
using RimWorld;
using HarmonyLib;

namespace AndroidTiersContinuedPatch
{
    [HarmonyPatch(typeof(RecipeDef), "get_UIIconThing")]
    public static class RecipeIconFixPatch
    {
        [HarmonyPostfix]
        public static void Postfix(RecipeDef __instance, ref ThingDef __result)
        {
            if (__result == null && __instance.products.NullOrEmpty())
            {
                if (__instance.defName.StartsWith("Create") || __instance.defName.StartsWith("ATPP_Create") || (__instance.workerClass != null && (__instance.workerClass.Name.Contains("Android") || __instance.workerClass.Name.Contains("Droid"))))
                {
                    ThingDef matchedRace = null;
                            
                    foreach (var pawnDef in DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.category == ThingCategory.Pawn))
                    {
                        if (__instance.defName.Contains(pawnDef.defName))
                        {
                            if (matchedRace == null || pawnDef.defName.Length > matchedRace.defName.Length)
                            {
                                matchedRace = pawnDef;
                            }
                        }
                    }

                    if (matchedRace == null)
                    {
                        if (__instance.defName.Contains("M8")) matchedRace = DefDatabase<ThingDef>.GetNamedSilentFail("M8Mech");
                        else if (__instance.defName.Contains("M7")) matchedRace = DefDatabase<ThingDef>.GetNamedSilentFail("M7Mech");
                    }

                    if (matchedRace != null)
                    {
                        __result = matchedRace;
                    }
                }
            }
        }
    }
}
