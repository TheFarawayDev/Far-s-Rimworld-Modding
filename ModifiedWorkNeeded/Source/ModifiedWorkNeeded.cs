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

        public override void ExposeData()
        {
            Scribe_Values.Look(ref workMultiplier, "workMultiplier", 0.25f);
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
            
            listingStandard.Label(string.Format("Work Amount Multiplier: {0}", settings.workMultiplier.ToStringPercent()));
            
            float newMultiplier = listingStandard.Slider(settings.workMultiplier, 0.01f, 10f);
            
            if (newMultiplier != settings.workMultiplier)
            {
                settings.workMultiplier = newMultiplier;
                ModifiedWorkNeededStartup.ApplyMultiplier(settings.workMultiplier);
            }

            if (listingStandard.ButtonText("Reset to Vanilla (100%)"))
            {
                settings.workMultiplier = 1f;
                ModifiedWorkNeededStartup.ApplyMultiplier(1f);
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

        static ModifiedWorkNeededStartup()
        {
            int recipeCount = 0;
            int statCount = 0;

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
            }

            Log.Message(string.Format("[Modified Work Needed] Cached {0} recipes and {1} thing stats. Default multiplier: {2}", recipeCount, statCount, ModifiedWorkNeededMod.settings.workMultiplier));
            
            ApplyMultiplier(ModifiedWorkNeededMod.settings.workMultiplier);
        }

        public static void ApplyMultiplier(float multiplier)
        {
            foreach (var kvp in originalRecipeWork)
            {
                kvp.Key.workAmount = kvp.Value * multiplier;
            }

            foreach (var kvp in originalStatWork)
            {
                kvp.Key.value = kvp.Value * multiplier;
            }
        }
    }
}
