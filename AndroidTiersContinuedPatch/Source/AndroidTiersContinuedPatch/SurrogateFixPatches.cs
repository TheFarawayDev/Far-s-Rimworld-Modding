using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AndroidTiersContinuedPatch
{
    // ========================================================================
    // Task 1: Missing Surrogates in the Colonist Bar
    // ========================================================================
    [HarmonyPatch(typeof(ColonistBar), "GetColonistsInOrder")]
    public static class ColonistBar_GetColonistsInOrder_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref List<Pawn> __result)
        {
            if (__result == null) 
                __result = new List<Pawn>();

            if (Find.Maps != null)
            {
                foreach (Map map in Find.Maps)
                {
                    if (map.mapPawns?.AllPawns == null) continue;
                    foreach (Pawn pawn in map.mapPawns.AllPawns)
                    {
                        if (IsDormantSurrogate(pawn) && !__result.Contains(pawn))
                        {
                            __result.Add(pawn);
                        }
                    }
                }
            }
        }

        private static bool IsDormantSurrogate(Pawn pawn)
        {
            // TODO: Replace this with your specific ABF comp logic.
            // For now, providing a safe placeholder checking the defName
            if (pawn.def.defName.Contains("Surrogate"))
            {
                var comp = pawn.TryGetComp<CompPowerTrader>();
                return comp == null || !comp.PowerOn;
            }
            return false;
        }
    }

    // ========================================================================
    // Task 2: Missing Attachment Animation (Yayo's Animation Conflict)
    // ========================================================================
    public static class YayosAnimationCompatibilityPatch
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(PawnRenderer __instance, Pawn ___pawn, Vector3 rootLoc, float angle, bool renderBody, Rot4 bodyFacing, RotDrawMode draw, PawnRenderFlags flags)
        {
            if (IsConnectingToSurrogate(___pawn))
            {
                DrawConnectionAnimation(___pawn, rootLoc, angle);
                return true; 
            }
            return true;
        }

        public static bool IsConnectingToSurrogate(Pawn pawn)
        {
            // TODO: Replace with your ABF logic to check if the connection animation is supposed to be playing.
            return false;
        }

        public static void DrawConnectionAnimation(Pawn pawn, Vector3 rootLoc, float angle)
        {
            // TODO: Render your custom matrix/mesh here.
        }
    }

    public static class YayosAnimationCompatibilityPatch_16
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(PawnRenderer __instance, PawnDrawParms parms)
        {
            if (YayosAnimationCompatibilityPatch.IsConnectingToSurrogate(parms.pawn))
            {
                Vector3 position = parms.matrix.GetColumn(3);
                float angle = 0f; // Can extract from matrix if needed
                YayosAnimationCompatibilityPatch.DrawConnectionAnimation(parms.pawn, position, angle);
            }
            return true;
        }
    }
}
