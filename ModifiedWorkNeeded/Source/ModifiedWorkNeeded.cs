using RimWorld;
using Verse;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace ModifiedWorkNeeded
{
    public class ModifiedWorkNeededSettings : ModSettings
    {
        public float workMultiplier = 0.25f;
        public float scannerMultiplier = 0.25f;
        public float orbitalScannerMultiplier = 0.25f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref workMultiplier, "workMultiplier", 0.25f);
            Scribe_Values.Look(ref scannerMultiplier, "scannerMultiplier", 0.25f);
            Scribe_Values.Look(ref orbitalScannerMultiplier, "orbitalScannerMultiplier", 0.25f);
            base.ExposeData();
        }
    }

    public class ModifiedWorkNeededMod : Mod
    {
        public static ModifiedWorkNeededSettings settings;

        public ModifiedWorkNeededMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<ModifiedWorkNeededSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            
            float oldWork = settings.workMultiplier;
            float oldScan = settings.scannerMultiplier;
            float oldOrbital = settings.orbitalScannerMultiplier;

            listingStandard.Label(string.Format("Work Amount Multiplier: {0}", settings.workMultiplier.ToStringPercent()));
            settings.workMultiplier = listingStandard.Slider(settings.workMultiplier, 0.0001f, 10f);

            listingStandard.Label(string.Format("Scanner Multiplier: {0}", settings.scannerMultiplier.ToStringPercent()));
            settings.scannerMultiplier = listingStandard.Slider(settings.scannerMultiplier, 0.0001f, 10f);

            listingStandard.Label(string.Format("Orbital Scanner Multiplier: {0}", settings.orbitalScannerMultiplier.ToStringPercent()));
            settings.orbitalScannerMultiplier = listingStandard.Slider(settings.orbitalScannerMultiplier, 0.0001f, 10f);

            if (oldWork != settings.workMultiplier || oldScan != settings.scannerMultiplier || oldOrbital != settings.orbitalScannerMultiplier)
            {
                ModifiedWorkNeededStartup.ApplyMultipliers(settings.workMultiplier, settings.scannerMultiplier, settings.orbitalScannerMultiplier);
            }

            if (listingStandard.ButtonText("Reset to Vanilla (100%)"))
            {
                settings.workMultiplier = 1f;
                settings.scannerMultiplier = 1f;
                settings.orbitalScannerMultiplier = 1f;
                ModifiedWorkNeededStartup.ApplyMultipliers(1f, 1f, 1f);
            }

            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Modified Work Needed";
        }
    }

    [StaticConstructorOnStartup]
    public static class ModifiedWorkNeededStartup
    {
        private static Dictionary<RecipeDef, float> originalRecipeWork = new Dictionary<RecipeDef, float>();
        private static Dictionary<StatModifier, float> originalStatWork = new Dictionary<StatModifier, float>();
        private static Dictionary<CompProperties_Scanner, float> originalScannerMtb = new Dictionary<CompProperties_Scanner, float>();
        private static Dictionary<CompProperties_Scanner, float> originalScannerGuaranteed = new Dictionary<CompProperties_Scanner, float>();
        private static Dictionary<CompProperties_Scanner, float> originalOrbitalMtb = new Dictionary<CompProperties_Scanner, float>();
        private static Dictionary<CompProperties_Scanner, float> originalOrbitalGuaranteed = new Dictionary<CompProperties_Scanner, float>();

        static ModifiedWorkNeededStartup()
        {
            int recipeCount = 0;
            int statCount = 0;
            int scannerCount = 0;
            int orbitalCount = 0;

            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (recipe.workAmount > 0)
                {
                    originalRecipeWork[recipe] = recipe.workAmount;
                    recipeCount++;
                }
            }

            foreach (ThingDef thing in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (thing.statBases != null)
                {
                    StatModifier workToMake = thing.statBases.FirstOrDefault(s => s.stat == StatDefOf.WorkToMake);
                    if (workToMake != null && workToMake.value > 0)
                    {
                        originalStatWork[workToMake] = workToMake.value;
                        statCount++;
                    }

                    StatModifier workToBuild = thing.statBases.FirstOrDefault(s => s.stat == StatDefOf.WorkToBuild);
                    if (workToBuild != null && workToBuild.value > 0)
                    {
                        originalStatWork[workToBuild] = workToBuild.value;
                        statCount++;
                    }
                }
                
                if (thing.comps != null)
                {
                    foreach (var comp in thing.comps)
                    {
                        CompProperties_Scanner scanner = comp as CompProperties_Scanner;
                        if (scanner != null)
                        {
                            string defNameLower = thing.defName.ToLower();
                            bool isOrbital = defNameLower.Contains("orbital") || defNameLower.Contains("longrange");
                            if (isOrbital)
                            {
                                if (scanner.scanFindMtbDays > 0)
                                {
                                    originalOrbitalMtb[scanner] = scanner.scanFindMtbDays;
                                    orbitalCount++;
                                }
                                if (scanner.scanFindGuaranteedDays > 0)
                                {
                                    originalOrbitalGuaranteed[scanner] = scanner.scanFindGuaranteedDays;
                                }
                            }
                            else
                            {
                                if (scanner.scanFindMtbDays > 0)
                                {
                                    originalScannerMtb[scanner] = scanner.scanFindMtbDays;
                                    scannerCount++;
                                }
                                if (scanner.scanFindGuaranteedDays > 0)
                                {
                                    originalScannerGuaranteed[scanner] = scanner.scanFindGuaranteedDays;
                                }
                            }
                        }
                    }
                }
            }

            Log.Message(string.Format("[Modified Work Needed] Cached {0} recipes, {1} thing stats, {2} scanners, {3} orbital scanners.", recipeCount, statCount, scannerCount, orbitalCount));
            
            ApplyMultipliers(ModifiedWorkNeededMod.settings.workMultiplier, ModifiedWorkNeededMod.settings.scannerMultiplier, ModifiedWorkNeededMod.settings.orbitalScannerMultiplier);
        }

        public static void ApplyMultipliers(float workMult, float scanMult, float orbitalMult)
        {
            foreach (var kvp in originalRecipeWork)
            {
                kvp.Key.workAmount = kvp.Value * workMult;
            }

            foreach (var kvp in originalStatWork)
            {
                kvp.Key.value = kvp.Value * workMult;
            }

            foreach (var kvp in originalScannerMtb)
            {
                kvp.Key.scanFindMtbDays = kvp.Value * scanMult;
            }

            foreach (var kvp in originalScannerGuaranteed)
            {
                kvp.Key.scanFindGuaranteedDays = kvp.Value * scanMult;
            }

            foreach (var kvp in originalOrbitalMtb)
            {
                kvp.Key.scanFindMtbDays = kvp.Value * orbitalMult;
            }

            foreach (var kvp in originalOrbitalGuaranteed)
            {
                kvp.Key.scanFindGuaranteedDays = kvp.Value * orbitalMult;
            }
        }
    }
}
