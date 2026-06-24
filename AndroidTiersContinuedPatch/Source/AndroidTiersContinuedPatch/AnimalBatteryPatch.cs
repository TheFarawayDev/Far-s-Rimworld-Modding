using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AndroidTiersContinuedPatch
{
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class RemoveAnimalBatteryGizmoPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance.RaceProps.Animal)
            {
                bool isAndroidAnimal = __instance.def.defName.StartsWith("Android") || __instance.def.defName.StartsWith("Robotic");
                if (!isAndroidAnimal)
                {
                    foreach (var comp in __instance.AllComps)
                    {
                        if (comp.GetType().Name == "CompAndroidState")
                        {
                            isAndroidAnimal = true;
                            break;
                        }
                    }
                }

                if (isAndroidAnimal)
                {
                    var list = __result.ToList();
                    list.RemoveAll(delegate(Gizmo g) 
                    {
                        Command cmd = g as Command;
                        if (cmd != null)
                        {
                            string label = cmd.defaultLabel != null ? cmd.defaultLabel.ToLower() : "";
                            string desc = cmd.defaultDesc != null ? cmd.defaultDesc.ToLower() : "";
                            return label.Contains("battery") || label.Contains("biomass") || label.Contains("reactor") || label.Contains("charge") ||
                                   desc.Contains("battery") || desc.Contains("biomass") || desc.Contains("reactor") || desc.Contains("charge");
                        }
                        return false;
                    });
                    __result = list;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "Tick")]
    public static class ForceAnimalBioreactorPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance)
        {
            if (__instance.IsHashIntervalTick(250) && __instance.RaceProps.Animal)
            {
                foreach (var comp in __instance.AllComps)
                {
                    if (comp.GetType().Name == "CompAndroidState")
                    {
                        var traverse = Traverse.Create(comp);
                        var field = traverse.Field("useBattery");
                        if (field.FieldExists() && field.GetValue<bool>() == true)
                        {
                            field.SetValue(false);
                        }
                        
                        var prop = traverse.Property("UseBattery");
                        if (prop.PropertyExists() && prop.GetValue<bool>() == true)
                        {
                            prop.SetValue(false);
                        }
                    }
                }
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class AnimalDietPatch
    {
        static AnimalDietPatch()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs.Where(d => d.race != null && d.race.Animal))
            {
                bool isAndroidAnimal = def.defName.StartsWith("Android") || def.defName.StartsWith("Robotic");
                if (!isAndroidAnimal && def.comps != null)
                {
                    if (def.comps.Any(c => c.compClass != null && c.compClass.Name == "CompAndroidState"))
                    {
                        isAndroidAnimal = true;
                    }
                }

                if (isAndroidAnimal)
                {
                    def.race.foodType &= ~FoodTypeFlags.Meal;
                    def.race.foodType &= ~FoodTypeFlags.Processed;
                    def.race.foodType &= ~FoodTypeFlags.Liquor;
                }
            }
        }
    }


}
