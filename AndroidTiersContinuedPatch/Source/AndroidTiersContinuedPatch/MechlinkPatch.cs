using HarmonyLib;
using RimWorld;
using System.Linq;
using Verse;

namespace AndroidTiersContinuedPatch
{
    [HarmonyPatch(typeof(CompUseEffect_InstallImplantMechlink), "CanBeUsedBy")]
    public static class CompUseEffect_InstallImplantMechlink_CanBeUsedBy_Patch
    {
        public static void Postfix(CompUseEffect_InstallImplantMechlink __instance, Pawn p, ref AcceptanceReport __result)
        {
            if (__instance.Props.hediffDef != null && __instance.Props.hediffDef.defName == "MechlinkImplant")
            {
                bool isAndroid = p.def.defName.StartsWith("Android") || p.def.defName.StartsWith("Robotic") || p.def.defName.Contains("Droid");
                if (isAndroid)
                {
                    // If the item originally failed to be used
                    if (!__result.Accepted)
                    {
                        if (p.health.hediffSet.HasHediff(__instance.Props.hediffDef))
                        {
                            __result = "InstallImplantAlreadyInstalled".Translate();
                            return;
                        }

                        if (!p.IsFreeColonist || p.HasExtraHomeFaction())
                        {
                            // Keep the original failure
                            return;
                        }

                        // Androids don't have a "Brain" part, so we check for an Android equivalent
                        BodyPartRecord part = p.RaceProps.body.AllParts.FirstOrDefault(x => x.def.defName == "ArtificialBrain" || x.def.defName == "AndroidBrain" || x.def.tags.Contains(BodyPartTagDefOf.ConsciousnessSource));
                        if (part == null)
                        {
                            __result = "InstallImplantNoBodyPart".Translate() + ": " + "ArtificialBrain";
                            return;
                        }

                        // Override the default restrictions (missing "Brain" and Psychic Sensitivity = 0)
                        __result = true;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(CompUseEffect_InstallImplant), "DoEffect")]
    public static class CompUseEffect_InstallImplant_DoEffect_Patch
    {
        public static bool Prefix(CompUseEffect_InstallImplant __instance, Pawn user)
        {
            if (__instance is CompUseEffect_InstallImplantMechlink && __instance.Props.hediffDef != null && __instance.Props.hediffDef.defName == "MechlinkImplant")
            {
                bool isAndroid = user.def.defName.StartsWith("Android") || user.def.defName.StartsWith("Robotic") || user.def.defName.Contains("Droid");
                if (isAndroid)
                {
                    BodyPartRecord part = user.RaceProps.body.AllParts.FirstOrDefault(x => x.def.defName == "ArtificialBrain" || x.def.defName == "AndroidBrain" || x.def.tags.Contains(BodyPartTagDefOf.ConsciousnessSource));
                    if (part != null)
                    {
                        user.health.AddHediff(__instance.Props.hediffDef, part, null, null);
                        return false; // Skip the original DoEffect, which would throw an error looking for "Brain"
                    }
                }
            }
            return true;
        }
    }
}
