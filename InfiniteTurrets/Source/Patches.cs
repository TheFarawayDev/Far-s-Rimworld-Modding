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
            // Only affect things that consume fuel on use (like turrets firing)
            // and where the parent is actually a turret.
            if (__instance.Props.consumeFuelOnlyWhenUsed && __instance.parent is Building_Turret)
            {
                if (InfiniteTurretsMod.Settings.infiniteDurability)
                {
                    amount = 0f;
                }
                else
                {
                    // If multiplier is e.g. 2, amount consumed is halved.
                    amount /= InfiniteTurretsMod.Settings.durabilityMultiplier;
                }
            }
        }
    }
}
