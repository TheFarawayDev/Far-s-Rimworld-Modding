using UnityEngine;
using Verse;
using HarmonyLib;
using RimWorld;

namespace FasterEarlyGame
{
    public class FasterEarlyGameMod : Mod
    {
        public static FasterEarlyGameSettings settings;

        public FasterEarlyGameMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<FasterEarlyGameSettings>();
            var harmony = new Harmony("thefarawaydev.fasterearlygame");
            harmony.PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            
            listingStandard.Label(string.Format("Research Speed Multiplier: {0:F2}x", settings.researchSpeedMultiplier));
            settings.researchSpeedMultiplier = listingStandard.Slider(settings.researchSpeedMultiplier, 0.1f, 10f);

            listingStandard.Label(string.Format("Mining Speed Multiplier: {0:F2}x", settings.miningSpeedMultiplier));
            settings.miningSpeedMultiplier = listingStandard.Slider(settings.miningSpeedMultiplier, 0.1f, 10f);

            listingStandard.Label(string.Format("Construction Speed Multiplier: {0:F2}x", settings.constructionSpeedMultiplier));
            settings.constructionSpeedMultiplier = listingStandard.Slider(settings.constructionSpeedMultiplier, 0.1f, 10f);

            listingStandard.Label(string.Format("Plant Work Speed Multiplier: {0:F2}x", settings.plantWorkSpeedMultiplier));
            settings.plantWorkSpeedMultiplier = listingStandard.Slider(settings.plantWorkSpeedMultiplier, 0.1f, 10f);

            listingStandard.Label(string.Format("Smoothing Speed Multiplier: {0:F2}x", settings.smoothingSpeedMultiplier));
            settings.smoothingSpeedMultiplier = listingStandard.Slider(settings.smoothingSpeedMultiplier, 0.1f, 10f);

            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Faster Early Game";
        }
    }

    [HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
    public static class StatWorker_GetValueUnfinalized_Patch
    {
        public static void Postfix(StatRequest req, bool applyPostProcess, StatDef ___stat, ref float __result)
        {
            if (req.Thing == null) return;
            Pawn pawn = req.Thing as Pawn;
            
            // Only apply to colonists to avoid buffing enemies/NPCs unnecessarily
            if (pawn == null || !pawn.IsColonist) return; 

            if (___stat == StatDefOf.ResearchSpeed)
                __result *= FasterEarlyGameMod.settings.researchSpeedMultiplier;
            else if (___stat == StatDefOf.MiningSpeed)
                __result *= FasterEarlyGameMod.settings.miningSpeedMultiplier;
            else if (___stat == StatDefOf.ConstructionSpeed)
                __result *= FasterEarlyGameMod.settings.constructionSpeedMultiplier;
            else if (___stat == StatDefOf.PlantWorkSpeed)
                __result *= FasterEarlyGameMod.settings.plantWorkSpeedMultiplier;
            else if (___stat == StatDefOf.SmoothingSpeed)
                __result *= FasterEarlyGameMod.settings.smoothingSpeedMultiplier;
        }
    }
}
