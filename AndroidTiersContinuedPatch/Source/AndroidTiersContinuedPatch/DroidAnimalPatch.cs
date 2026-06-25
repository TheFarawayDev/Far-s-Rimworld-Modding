using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

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
            var harmony = new Harmony("AndroidTiersContinuedPatch.EnergyDrain");

            // Patch Need.CurLevel setter (handles Need_Food and others that don't override it)
            var needSetter = AccessTools.PropertySetter(typeof(RimWorld.Need), "CurLevel");
            if (needSetter != null)
            {
                harmony.Patch(needSetter, prefix: new HarmonyMethod(typeof(AnimalEnergyDrainPatch), "Prefix"));
            }

            // Safely patch Need_Energy only if it overrides CurLevel
            var needEnergyType = GenTypes.AllTypes.FirstOrDefault(t => t.Name == "Need_Energy");
            if (needEnergyType != null)
            {
                var energySetter = AccessTools.PropertySetter(needEnergyType, "CurLevel");
                if (energySetter != null && energySetter.DeclaringType == needEnergyType)
                {
                    harmony.Patch(energySetter, prefix: new HarmonyMethod(typeof(AnimalEnergyDrainPatch), "Prefix"));
                }
            }

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
                    
                    // Natively makes the 'battery' (food) last 3x longer without relying on Harmony hooks!
                    def.race.baseHungerRate *= 0.333f;

                    if (def.comps != null)
                    {
                        foreach (var comp in def.comps)
                        {
                            var type = comp.GetType();
                            if (type.Name.Contains("Milkable") || type.Name.Contains("Shearable") || type.Name.Contains("EggLayer") || type.Name.Contains("Gatherable"))
                            {
                                foreach (var fieldInfo in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy))
                                {
                                    string fName = fieldInfo.Name.ToLower();
                                    if (fName.Contains("days") || fName.Contains("ticks") || fName.Contains("interval"))
                                    {
                                        object objVal = fieldInfo.GetValue(comp);
                                        if (objVal is float)
                                        {
                                            float val = (float)objVal;
                                            fieldInfo.SetValue(comp, val * 0.5f);
                                            Log.Message(string.Format("[AndroidTiersContinuedPatch] Halved interval float field {0} on {1}", fName, def.defName));
                                        }
                                        else if (objVal is int)
                                        {
                                            int val = (int)objVal;
                                            fieldInfo.SetValue(comp, (int)(val * 0.5f));
                                            Log.Message(string.Format("[AndroidTiersContinuedPatch] Halved interval int field {0} on {1}", fName, def.defName));
                                        }
                                    }
                                    else if (fName.Contains("amount") || fName.Contains("count") || fName == "egglaycount")
                                    {
                                        object objVal = fieldInfo.GetValue(comp);
                                        if (objVal is float)
                                        {
                                            float val = (float)objVal;
                                            fieldInfo.SetValue(comp, val * 2f);
                                            Log.Message(string.Format("[AndroidTiersContinuedPatch] Doubled amount float field {0} on {1}", fName, def.defName));
                                        }
                                        else if (objVal is int)
                                        {
                                            int val = (int)objVal;
                                            fieldInfo.SetValue(comp, (int)(val * 2));
                                            Log.Message(string.Format("[AndroidTiersContinuedPatch] Doubled amount int field {0} on {1}", fName, def.defName));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public static class AnimalEnergyDrainPatch
    {
        public static void Prefix(Need __instance, ref float value)
        {
            string typeName = __instance.GetType().Name;
            if (typeName == "Need_Energy" || typeName == "Need_Food")
            {
                Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
                if (pawn != null && pawn.RaceProps.Animal)
                {
                    bool isAndroidAnimal = pawn.def.defName.StartsWith("Android") || pawn.def.defName.StartsWith("Robotic");
                    if (!isAndroidAnimal && pawn.AllComps != null)
                    {
                        foreach (var comp in pawn.AllComps)
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
                        if (value < __instance.CurLevel)
                        {
                            float drop = __instance.CurLevel - value;
                            // Reduce energy drain by 75% (battery lasts 4x longer)
                            value = __instance.CurLevel - (drop * 0.25f);
                        }
                        else if (value > __instance.CurLevel)
                        {
                            float gain = value - __instance.CurLevel;
                            // Multiply energy gain so one meal effectively fully charges the battery
                            value = __instance.CurLevel + (gain * 10f);
                            if (value > __instance.MaxLevel) value = __instance.MaxLevel;
                        }
                    }
                }
            }
        }
    }
}
