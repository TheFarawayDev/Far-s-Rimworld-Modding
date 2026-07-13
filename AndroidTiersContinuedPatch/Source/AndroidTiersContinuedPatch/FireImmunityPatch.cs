using System;
using HarmonyLib;
using Verse;
using RimWorld;

namespace AndroidTiersContinuedPatch
{
    public static class FireImmunityPatch
    {
        public static bool IsAndroidTier3OrHigher(Thing thing)
        {
            if (thing == null) return false;
            
            Pawn pawn = thing as Pawn;
            if (pawn == null && thing is Corpse corpse)
            {
                pawn = corpse.InnerPawn;
            }

            if (pawn?.def == null) return false;
            string defName = pawn.def.defName;
            
            return defName == "Android3Tier" || 
                   defName == "Android4Tier" || 
                   defName == "Android5Tier" || 
                   defName == "M7Mech" || 
                   defName == "M8Mech" || 
                   defName == "AT_HellUnit";
        }
    }

    [HarmonyPatch(typeof(StatExtension), "GetStatValue")]
    public static class Patch_StatExtension_GetStatValue
    {
        public static void Postfix(Thing thing, StatDef stat, ref float __result)
        {
            if (stat == StatDefOf.Flammability && FireImmunityPatch.IsAndroidTier3OrHigher(thing))
            {
                __result = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(RimWorld.FireUtility), "TryAttachFire")]
    public static class Patch_Thing_TryAttachFire
    {
        public static bool Prefix(Thing t)
        {
            if (FireImmunityPatch.IsAndroidTier3OrHigher(t))
            {
                return false;
            }
            return true;
        }
    }
}
