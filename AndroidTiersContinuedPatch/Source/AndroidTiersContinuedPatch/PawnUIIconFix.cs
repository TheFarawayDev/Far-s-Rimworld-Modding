using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace AndroidTiersContinuedPatch
{
    [StaticConstructorOnStartup]
    public static class PawnUIIconFix
    {
        static PawnUIIconFix()
        {
            // 1. Fix ThingDef icons for Pawns (Fixes Research & general UI)
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs.Where(d => d.race != null))
            {
                bool isAndroid = def.defName.StartsWith("Android") || def.defName.StartsWith("Robotic") || def.defName.Contains("Droid") || def.defName.EndsWith("boi") || def.defName == "Muffboi";
                
                if (isAndroid || def.defName.Contains("Robo"))
                {
                    var field = Traverse.Create(def).Field("uiIcon");
                    Texture2D tex = field.FieldExists() ? field.GetValue<Texture2D>() : null;
                    
                    if (tex == null || tex == BaseContent.BadTex)
                    {
                        PawnKindDef kindDef = DefDatabase<PawnKindDef>.AllDefs.FirstOrDefault(k => k.race == def);
                        if (kindDef != null && kindDef.lifeStages != null && kindDef.lifeStages.Count > 0)
                        {
                            string texPath = null;
                            if (kindDef.lifeStages.Last().bodyGraphicData != null)
                            {
                                texPath = kindDef.lifeStages.Last().bodyGraphicData.texPath;
                            }
                            if (!string.IsNullOrEmpty(texPath))
                            {
                                Texture2D newTex = null;
                                if (def.race.Animal)
                                {
                                    newTex = ContentFinder<Texture2D>.Get(texPath + "_east", false);
                                    if (newTex == null) newTex = ContentFinder<Texture2D>.Get(texPath + "_south", false);
                                }
                                else
                                {
                                    newTex = ContentFinder<Texture2D>.Get(texPath + "_south", false);
                                    if (newTex == null) newTex = ContentFinder<Texture2D>.Get(texPath, false);
                                }

                                if (newTex != null)
                                {
                                    field.SetValue(newTex);
                                }
                            }
                        }
                    }
                }
            }

            // 2. Fix RecipeDef icons explicitly (Fixes Crafting Menus)
            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefs)
            {
                var recipeIconField = Traverse.Create(recipe).Field("uiIcon");
                Texture2D rTex = recipeIconField.FieldExists() ? recipeIconField.GetValue<Texture2D>() : null;

                if (rTex == null || rTex == BaseContent.BadTex)
                {
                    string label = recipe.label != null ? recipe.label.ToLower() : "";
                    string defName = recipe.defName.ToLower();
                    string newIconPath = null;

                    if (label.Contains("m.u.f.f") || defName.Contains("muff"))
                        newIconPath = "Things/Pawn/Muffboi/RoboMUFF_east";
                    else if (label.Contains("chemical processing") || defName.Contains("cow") || label.Contains("milker"))
                        newIconPath = "Things/Pawn/Cowboi/RMilker_east";
                    else if (label.Contains("n.solution") || defName.Contains("chicken"))
                        newIconPath = "Things/Pawn/Chickenboi/RChicken_east";
                    else if (label.Contains("phytomining") || defName.Contains("sheep") || label.Contains("grower"))
                        newIconPath = "Things/Pawn/Sheepboi/RGrower_east";
                    else if (label.Contains("t1 android") || defName.Contains("t1"))
                        newIconPath = "Things/Droids/tier1/tier1";
                    else if (label.Contains("t2 android") || defName.Contains("t2"))
                        newIconPath = "Things/Droids/tier2/tier2";
                    else if (label.Contains("t3 android") || defName.Contains("t3"))
                        newIconPath = "Things/Droids/tier3/tier3";
                    else if (label.Contains("t4 android") || defName.Contains("t4"))
                        newIconPath = "Things/Droids/tier4/tier4";
                    else if (label.Contains("t5 android") || defName.Contains("t5"))
                        newIconPath = "Things/Droids/tier5/tier5";
                    else if (label.Contains("basic droid") || defName.Contains("basicdroid"))
                        newIconPath = "Things/Droids/tier1/tier1"; // Fallback for basic droids
                    
                    if (newIconPath != null)
                    {
                        Texture2D tex = ContentFinder<Texture2D>.Get(newIconPath, false);
                        // Try suffixes if direct fails
                        if (tex == null) tex = ContentFinder<Texture2D>.Get(newIconPath + "_south", false);
                        if (tex == null) tex = ContentFinder<Texture2D>.Get(newIconPath + "_east", false);

                        if (tex != null && recipeIconField.FieldExists())
                        {
                            recipeIconField.SetValue(tex);
                        }
                    }
                }
            }
        }
    }
}
