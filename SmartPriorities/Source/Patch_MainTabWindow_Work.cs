using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;

namespace SmartPriorities
{
    [HarmonyPatch(typeof(MainTabWindow_Work), "DoWindowContents")]
    public static class Patch_MainTabWindow_Work_DoWindowContents
    {
        public static void Postfix(MainTabWindow_Work __instance, Rect rect)
        {
            // The top of the work tab window has a margin.
            // "Manual priorities" checkbox is drawn around x=5, y=5.
            // We will draw our button to the right of it.
            
            float buttonWidth = 140f;
            float buttonHeight = 24f;
            
            // Calculate a safe rect. Manual priorities checkbox usually takes up some space.
            Rect btnRect = new Rect(200f, 5f, buttonWidth, buttonHeight);
            
            bool clicked = Widgets.ButtonText(btnRect, "Auto-Assign All", true, true, true);
            if (clicked)
            {
                // Assign for all free colonists on the current map
                Map map = Find.CurrentMap;
                if (map != null)
                {
                    int count = 0;
                    List<Pawn> pawns = new List<Pawn>(map.mapPawns.FreeColonists);
                    foreach (Pawn pawn in pawns)
                    {
                        SmartPriorities_Algorithm.AutoAssignPriorities(pawn);
                        count++;
                    }
                    Messages.Message(string.Format("Smart Priorities assigned for {0} colonists.", count), MessageTypeDefOf.PositiveEvent, false);
                }
            }
        }
    }
}
