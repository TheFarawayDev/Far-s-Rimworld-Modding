using HarmonyLib;
using RimWorld;
using Verse;
using System.Linq;

namespace AndroidTiersContinuedPatch
{
    [HarmonyPatch(typeof(Pawn), "SpawnSetup")]
    public static class SurrogateOldSaveFixPatch
    {
        public static void Postfix(Pawn __instance, Map map, bool respawningAfterLoad)
        {
            if (respawningAfterLoad && __instance != null)
            {
                // Check if this pawn is a surrogate
                bool isSurrogate = false;
                if (__instance.AllComps != null)
                {
                    foreach (var comp in __instance.AllComps)
                    {
                        if (comp.GetType().Name == "CompAndroidState")
                        {
                            var val = Traverse.Create(comp).Field("isSurrogate").GetValue();
                            if (val != null && (bool)val)
                            {
                                isSurrogate = true;
                                break;
                            }
                        }
                    }
                }

                if (isSurrogate)
                {
                    // Ensure dynamic components exist for pawns
                    PawnComponentsUtility.AddAndRemoveDynamicComponents(__instance, true);

                    // Explicitly initialize missing UI trackers if AddAndRemove didn't
                    if (__instance.RaceProps != null && __instance.RaceProps.Humanlike)
                    {
                        if (__instance.workSettings == null)
                        {
                            __instance.workSettings = new Pawn_WorkSettings(__instance);
                            if (__instance.Faction == Faction.OfPlayer)
                            {
                                __instance.workSettings.EnableAndInitialize();
                            }
                        }
                        if (__instance.timetable == null)
                        {
                            __instance.timetable = new Pawn_TimetableTracker(__instance);
                        }
                        if (__instance.outfits == null)
                        {
                            __instance.outfits = new Pawn_OutfitTracker(__instance);
                        }
                        if (__instance.drugs == null)
                        {
                            __instance.drugs = new Pawn_DrugPolicyTracker(__instance);
                        }
                        if (__instance.foodRestriction == null)
                        {
                            __instance.foodRestriction = new Pawn_FoodRestrictionTracker(__instance);
                        }
                        if (__instance.reading == null)
                        {
                            __instance.reading = new Pawn_ReadingTracker(__instance);
                        }
                    }

                    // Fix animation/drawing state
                    if (__instance.Drawer != null && __instance.Drawer.renderer != null)
                    {
                        __instance.Drawer.renderer.SetAllGraphicsDirty();
                        // Also force render tree rebuild if any
                        var renderTree = __instance.Drawer.renderer.renderTree;
                        if (renderTree != null)
                        {
                            renderTree.SetDirty();
                        }
                    }

                    // Trigger UI updates
                    PortraitsCache.SetDirty(__instance);
                    Find.ColonistBar.MarkColonistsDirty();
                    
                    Log.Message("[AndroidTiersContinuedPatch] Applied old save fix to surrogate: " + __instance.LabelShortCap);
                }
            }
        }
    }
}
