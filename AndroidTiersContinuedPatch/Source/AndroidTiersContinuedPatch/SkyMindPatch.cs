using HarmonyLib;
using Verse;
using MOARANDROIDS;

namespace AndroidTiersContinuedPatch
{
    [HarmonyPatch(typeof(CompSkyMind), "canBeConnectedToSkyMind")]
    public static class CompSkyMind_canBeConnectedToSkyMind_Patch
    {
        public static void Postfix(CompSkyMind __instance, ref bool __result)
        {
            if (!__result && __instance.parent is Pawn pawn)
            {
                if (pawn.VX0ChipPresent())
                {
                    __result = true;
                }
            }
        }
    }
}
