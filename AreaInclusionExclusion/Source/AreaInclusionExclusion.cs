using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Verse;
using Verse.Sound;
using RimWorld;
using HarmonyLib;
using UnityEngine;

namespace AreaInclusionExclusion
{
    // ==========================================
    // 1. DATA CONTAINER FOR PERSISTENT STATE
    // ==========================================
    public class AreaInclusionExclusionData : IExposable
    {
        public List<Area> included = new List<Area>();
        public List<Area> excluded = new List<Area>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref included, "includedAreas", LookMode.Reference);
            Scribe_Collections.Look(ref excluded, "excludedAreas", LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (included == null) included = new List<Area>();
                if (excluded == null) excluded = new List<Area>();
                included.RemoveAll(x => x == null);
                excluded.RemoveAll(x => x == null);
            }
        }

        public void CleanNulls()
        {
            if (included != null) included.RemoveAll(x => x == null);
            if (excluded != null) excluded.RemoveAll(x => x == null);
        }
    }

    // ==========================================
    // 2. MANAGER MAPPING PAWN SETTINGS TO DATA
    // ==========================================
    public static class AreaInclusionExclusionManager
    {
        private static ConditionalWeakTable<Pawn_PlayerSettings, AreaInclusionExclusionData> dataTable =
            new ConditionalWeakTable<Pawn_PlayerSettings, AreaInclusionExclusionData>();

        public static AreaInclusionExclusionData GetOrCreateData(Pawn_PlayerSettings settings)
        {
            if (settings == null) return null;
            AreaInclusionExclusionData data;
            if (!dataTable.TryGetValue(settings, out data))
            {
                data = new AreaInclusionExclusionData();
                dataTable.Add(settings, data);
            }
            return data;
        }
    }

    // ==========================================
    // 3. HARMONY INITIALIZATION
    // ==========================================
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            try
            {
                var harmony = new Harmony("thefarawaydev.areainclusionexclusion");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Message("[AreaInclusionExclusion] Initialized Harmony patches successfully.");
            }
            catch (Exception ex)
            {
                Log.Error("[AreaInclusionExclusion] Error during Harmony initialization: " + ex);
            }
        }
    }

    // ==========================================
    // 4. HARMONY PATCHES
    // ==========================================

    // Patch 1: Save/Load data inside Pawn_PlayerSettings.ExposeData
    [HarmonyPatch(typeof(Pawn_PlayerSettings), "ExposeData")]
    public static class Patch_Pawn_PlayerSettings_ExposeData
    {
        public static void Postfix(Pawn_PlayerSettings __instance)
        {
            if (__instance == null) return;
            AreaInclusionExclusionData data = AreaInclusionExclusionManager.GetOrCreateData(__instance);
            if (data != null)
            {
                data.ExposeData();
            }
        }
    }

    // Patch 2: Clean up deleted areas
    [HarmonyPatch(typeof(Pawn_PlayerSettings), "Notify_AreaRemoved")]
    public static class Patch_Pawn_PlayerSettings_Notify_AreaRemoved
    {
        public static void Postfix(Pawn_PlayerSettings __instance, Area area)
        {
            if (__instance == null || area == null) return;
            AreaInclusionExclusionData data = AreaInclusionExclusionManager.GetOrCreateData(__instance);
            if (data != null)
            {
                if (data.included.Contains(area))
                {
                    data.included.Remove(area);
                }
                if (data.excluded.Contains(area))
                {
                    data.excluded.Remove(area);
                }
            }
        }
    }

    // Patch 3: Overriding InAllowedArea checks
    [HarmonyPatch(typeof(ForbidUtility), "InAllowedArea", new Type[] { typeof(IntVec3), typeof(Pawn) })]
    public static class Patch_ForbidUtility_InAllowedArea
    {
        public static bool Prefix(IntVec3 c, Pawn forPawn, ref bool __result)
        {
            if (forPawn == null || forPawn.playerSettings == null)
            {
                return true;
            }

            AreaInclusionExclusionData data = AreaInclusionExclusionManager.GetOrCreateData(forPawn.playerSettings);
            if (data == null)
            {
                return true;
            }

            data.CleanNulls();

            if (data.included.Count == 0 && data.excluded.Count == 0)
            {
                // Fallback to vanilla logic
                return true;
            }

            // Exclusions always take priority: if cell c is in any excluded area, forbid it.
            for (int i = 0; i < data.excluded.Count; i++)
            {
                Area area = data.excluded[i];
                if (area != null && area[c])
                {
                    __result = false;
                    return false;
                }
            }

            // Inclusions: cell c must be in at least one of the included areas.
            if (data.included.Count > 0)
            {
                bool inAnyIncluded = false;
                for (int i = 0; i < data.included.Count; i++)
                {
                    Area area = data.included[i];
                    if (area != null && area[c])
                    {
                        inAnyIncluded = true;
                        break;
                    }
                }
                __result = inAnyIncluded;
                return false;
            }

            // If there are only exclusions, then the base is the vanilla AreaRestriction.
            Area vanillaArea = forPawn.playerSettings.AreaRestrictionInPawnCurrentMap;
            if (vanillaArea == null)
            {
                __result = true; // Unrestricted (and not excluded)
            }
            else
            {
                __result = vanillaArea[c];
            }
            return false;
        }
    }

    // Patch 4: Custom UI rendering in AreaAllowedGUI
    [HarmonyPatch(typeof(AreaAllowedGUI), "DoAllowedAreaSelectors")]
    public static class Patch_AreaAllowedGUI_DoAllowedAreaSelectors
    {
        public static bool Prefix(Rect rect, Pawn p)
        {
            if (p == null || p.Map == null || p.playerSettings == null) return true;

            List<Area> allAreas = p.Map.areaManager.AllAreas;
            int count = allAreas.Count + 1; // +1 for Unrestricted
            float btnWidth = rect.width / (float)count;

            // Draw "Unrestricted" button
            Rect unRestRect = new Rect(rect.x, rect.y, btnWidth, rect.height).ContractedBy(1f);
            DrawAreaButton(unRestRect, p, null);

            // Draw all other areas
            for (int i = 0; i < allAreas.Count; i++)
            {
                Rect areaRect = new Rect(rect.x + (i + 1) * btnWidth, rect.y, btnWidth, rect.height).ContractedBy(1f);
                DrawAreaButton(areaRect, p, allAreas[i]);
            }

            return false; // Skip vanilla drawing
        }

        private static void DrawAreaButton(Rect rect, Pawn pawn, Area area)
        {
            AreaInclusionExclusionData data = AreaInclusionExclusionManager.GetOrCreateData(pawn.playerSettings);
            if (data == null) return;

            data.CleanNulls();

            bool isIncluded = false;
            bool isExcluded = false;

            if (area == null) // Unrestricted
            {
                if (data.included.Count == 0)
                {
                    isIncluded = true;
                }
            }
            else
            {
                if (data.included.Contains(area))
                {
                    isIncluded = true;
                }
                else if (data.excluded.Contains(area))
                {
                    isExcluded = true;
                }
                else if (data.included.Count == 0 && pawn.playerSettings.AreaRestrictionInPawnCurrentMap == area)
                {
                    isIncluded = true;
                }
            }

            // Determine background color
            Color bgColor = Color.grey;
            if (area != null)
            {
                bgColor = area.Color;
            }
            else
            {
                bgColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            }

            // Adjust opacity and color based on state
            Color drawColor = bgColor;
            if (isIncluded)
            {
                drawColor.a = 0.8f;
            }
            else if (isExcluded)
            {
                drawColor = Color.red;
                drawColor.a = 0.8f;
            }
            else
            {
                drawColor.a = 0.2f;
            }

            // Draw button background solid
            Widgets.DrawBoxSolid(rect, drawColor);

            // Draw mouse hover highlight
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            // Draw text
            string labelText = (area != null) ? area.Label : "Unrestricted".Translate().ToString();
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            GUI.color = (isIncluded || isExcluded) ? Color.white : new Color(0.8f, 0.8f, 0.8f, 0.6f);
            Widgets.Label(rect, labelText);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // Draw border highlights
            if (isExcluded)
            {
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 2f), Color.red);
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y + rect.height - 2f, rect.width, 2f), Color.red);
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 2f, rect.height), Color.red);
                Widgets.DrawBoxSolid(new Rect(rect.x + rect.width - 2f, rect.y, 2f, rect.height), Color.red);
            }
            else if (isIncluded && (data.included.Count > 0 || (area != null && data.excluded.Count > 0)))
            {
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 2f), Color.green);
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y + rect.height - 2f, rect.width, 2f), Color.green);
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 2f, rect.height), Color.green);
                Widgets.DrawBoxSolid(new Rect(rect.x + rect.width - 2f, rect.y, 2f, rect.height), Color.green);
            }

            // Mouse button clicking logic
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 0) // Left click: Toggle inclusion
                {
                    if (area == null) // Selecting unrestricted
                    {
                        data.included.Clear();
                        data.excluded.Clear();
                        pawn.playerSettings.AreaRestrictionInPawnCurrentMap = null;
                    }
                    else
                    {
                        if (data.excluded.Contains(area))
                        {
                            data.excluded.Remove(area);
                        }

                        if (data.included.Contains(area))
                        {
                            data.included.Remove(area);
                            if (data.included.Count == 0 && data.excluded.Count == 0)
                            {
                                pawn.playerSettings.AreaRestrictionInPawnCurrentMap = null;
                            }
                        }
                        else
                        {
                            data.included.Add(area);
                            pawn.playerSettings.AreaRestrictionInPawnCurrentMap = area;
                        }
                    }
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    Event.current.Use();
                }
                else if (Event.current.button == 1) // Right click: Toggle exclusion
                {
                    if (area != null)
                    {
                        if (data.included.Contains(area))
                        {
                            data.included.Remove(area);
                        }

                        if (data.excluded.Contains(area))
                        {
                            data.excluded.Remove(area);
                            if (data.included.Count == 0 && data.excluded.Count == 0)
                            {
                                pawn.playerSettings.AreaRestrictionInPawnCurrentMap = null;
                            }
                        }
                        else
                        {
                            data.excluded.Add(area);
                            if (pawn.playerSettings.AreaRestrictionInPawnCurrentMap == area)
                            {
                                pawn.playerSettings.AreaRestrictionInPawnCurrentMap = null;
                            }
                        }
                    }
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    Event.current.Use();
                }
            }
        }
    }
}
