using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace DoMoreResearch
{
    [StaticConstructorOnStartup]
    public static class Startup
    {
        static Startup()
        {
            var harmony = new Harmony("com.thefarawaydev.domoreresearch");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Dynamic patching for the Research Window to seamlessly support overhauls like YART and ResearchPowl
            var researchTabDef = DefDatabase<MainButtonDef>.GetNamed("Research", false);
            if (researchTabDef != null && researchTabDef.tabWindowClass != null)
            {
                var doWindowContents = AccessTools.Method(researchTabDef.tabWindowClass, "DoWindowContents");
                if (doWindowContents != null)
                {
                    harmony.Patch(doWindowContents, postfix: new HarmonyMethod(typeof(ResearchWindow_Patch), nameof(ResearchWindow_Patch.DoWindowContents_Postfix)));
                }

                var preOpen = AccessTools.Method(researchTabDef.tabWindowClass, "PreOpen");
                if (preOpen != null)
                {
                    harmony.Patch(preOpen, postfix: new HarmonyMethod(typeof(ResearchWindow_Patch), nameof(ResearchWindow_Patch.PreOpen_Postfix)));
                }
            }
        }
    }

    public static class AutoResearcher
    {
        private static ResearchProjectDef _cachedProject;
        private static bool _cacheDirty = true;

        public static void ClearCache()
        {
            _cacheDirty = true;
            _cachedProject = null;
        }

        public static ResearchProjectDef GetNextBestResearch()
        {
            if (!_cacheDirty)
                return _cachedProject;

            _cachedProject = CalculateNextBestResearch();
            _cacheDirty = false;
            return _cachedProject;
        }

        private static ResearchProjectDef CalculateNextBestResearch()
        {
            var available = DefDatabase<ResearchProjectDef>.AllDefsListForReading
                .Where(x => !x.IsHidden && x.CanStartNow);

            if (!available.Any()) return null;

            // 1. Tab Focus Logic
            if (!string.IsNullOrEmpty(DoMoreResearchMod.Settings.focusedTab))
            {
                var focused = available.Where(x => x.tab != null && x.tab.defName == DoMoreResearchMod.Settings.focusedTab);
                if (focused.Any())
                {
                    available = focused;
                }
                else
                {
                    // Exhausted the focused tab
                    DoMoreResearchMod.Settings.focusedTab = null;
                    Messages.Message("Auto-Research: Focused tab exhausted. Returning to normal selection.", MessageTypeDefOf.NeutralEvent, false);
                }
            }

            var playerTech = Faction.OfPlayer.def.techLevel;

            // 2. Strict Era vs Standard Progression (with Tech Level penalties respected via CostApparent)
            if (DoMoreResearchMod.Settings.strictTechEraProgression)
            {
                var lowestAvailableTech = available.Min(x => x.techLevel);
                return available
                    .Where(x => x.techLevel == lowestAvailableTech)
                    .OrderBy(x => x.CostApparent)
                    .FirstOrDefault();
            }
            else
            {
                return available
                    .OrderByDescending(x => x.techLevel <= playerTech)
                    .ThenBy(x => x.CostApparent)
                    .FirstOrDefault();
            }
        }
    }

    // Invalidate Cache when a project finishes
    [HarmonyPatch(typeof(ResearchManager), "FinishProject")]
    public static class ResearchManager_FinishProject_Patch
    {
        public static void Postfix() => AutoResearcher.ClearCache();
    }

    // Safely assign research when the pawn looks for work at a bench
    [HarmonyPatch(typeof(WorkGiver_Researcher), "ShouldSkip")]
    public static class WorkGiver_Researcher_ShouldSkip_Patch
    {
        public static void Prefix()
        {
            var currentProjField = AccessTools.Field(typeof(ResearchManager), "currentProj");
            var currentProj = currentProjField?.GetValue(Find.ResearchManager) as ResearchProjectDef;

            if (currentProj == null && DoMoreResearchMod.Settings.enableAutoResearch && !IsQueueActive())
            {
                var next = AutoResearcher.GetNextBestResearch();
                if (next != null)
                {
                    Find.ResearchManager.SetCurrentProject(next);
                    
                    if (DoMoreResearchMod.Settings.smartNotifications)
                    {
                        if (next.CostApparent > 1000f)
                            Messages.Message($"Auto-selected research: {next.LabelCap}", MessageTypeDefOf.TaskCompletion, false);
                    }
                    else
                    {
                        Messages.Message($"Auto-selected research: {next.LabelCap}", MessageTypeDefOf.SilentInput, false);
                    }
                }
            }
        }

        private static bool IsQueueActive()
        {
            // YART Support
            try
            {
                var yartType = AccessTools.TypeByName("YART.Data.ResearchQueueManager");
                if (yartType != null)
                {
                    var instanceProp = AccessTools.Property(yartType, "Instance");
                    var instance = instanceProp?.GetValue(null);
                    if (instance != null)
                    {
                        var queuesField = AccessTools.Field(yartType, "queues");
                        var queuesDict = queuesField?.GetValue(instance) as System.Collections.IDictionary;
                        if (queuesDict != null)
                        {
                            foreach (System.Collections.DictionaryEntry entry in queuesDict)
                            {
                                var list = entry.Value as System.Collections.IList;
                                if (list != null && list.Count > 0)
                                    return true;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore exceptions for types that might not exist
            }

            // ResearchPal / ResearchPowl / ResearchTree (Fluffy) Support
            foreach (var typeName in new[] { "ResearchPal.Queue", "ResearchTree.Queue", "ResearchPowl.Queue" })
            {
                try
                {
                    var type = AccessTools.TypeByName(typeName);
                    if (type != null)
                    {
                        var listField = AccessTools.Field(type, "projects") ?? AccessTools.Field(type, "queue");
                        var list = listField?.GetValue(null) as System.Collections.IList;
                        if (list != null && list.Count > 0)
                            return true;
                    }
                }
                catch
                {
                    // Ignore exceptions for types that might not exist
                }
            }

            return false;
        }
    }

    public static class ResearchWindow_Patch
    {
        public static void PreOpen_Postfix()
        {
            if (!DoMoreResearchMod.Settings.firstTimeSettingsOpened)
            {
                DoMoreResearchMod.Settings.firstTimeSettingsOpened = true;
                DoMoreResearchMod.Settings.Write();
                Find.WindowStack.Add(new Dialog_ModSettings(LoadedModManager.GetMod<DoMoreResearchMod>()));
            }
        }

        public static void DoWindowContents_Postfix(Window __instance, Rect inRect)
        {
            // Add a small button in the top right of the research window for quick settings access
            Rect buttonRect = new Rect(inRect.width - 40f, 10f, 28f, 28f);
            if (Widgets.ButtonImage(buttonRect, TexButton.Rename)) // Use built-in texture
            {
                Find.WindowStack.Add(new Dialog_ModSettings(LoadedModManager.GetMod<DoMoreResearchMod>()));
            }
            TooltipHandler.TipRegion(buttonRect, "Auto-Research Settings");

            // Handle Shift-Clicking to focus on a tab
            // This is a simple approach: if user holds shift and clicks anywhere inside the window,
            // we capture the currently selected tab. Since different UI mods store the active tab differently,
            // we might have to fallback or check standard vanilla fields. 
            // In vanilla, it's MainTabWindow_Research.CurTab.
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Event.current.shift)
            {
                if (__instance is MainTabWindow_Research vanillaWindow)
                {
                    var tabField = AccessTools.Field(typeof(MainTabWindow_Research), "curTab");
                    if (tabField != null)
                    {
                        var curTab = tabField.GetValue(vanillaWindow) as ResearchTabDef;
                        if (curTab != null)
                        {
                            DoMoreResearchMod.Settings.focusedTab = curTab.defName;
                            AutoResearcher.ClearCache();
                            Messages.Message($"Auto-Research focus set to: {curTab.LabelCap}", MessageTypeDefOf.PositiveEvent, false);
                        }
                    }
                }
            }
        }
    }
}
