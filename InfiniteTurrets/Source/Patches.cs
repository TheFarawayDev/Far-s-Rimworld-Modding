using HarmonyLib;
using RimWorld;
using Verse;

namespace InfiniteTurrets
{
    [HarmonyPatch(typeof(CompRefuelable), "ConsumeFuel")]
    public static class CompRefuelable_ConsumeFuel_Patch
    {
        public static void Prefix(CompRefuelable __instance, ref float amount)
        {
            if (__instance.Props.consumeFuelOnlyWhenUsed && __instance.parent is Building_Turret)
            {
                if (InfiniteTurretsMod.Settings.infiniteDurability)
                {
                    amount = 0f;
                }
                else
                {
                    amount /= InfiniteTurretsMod.Settings.durabilityMultiplier;
                }
            }
        }
    }
}
