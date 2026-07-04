using System;
using UnityEngine;
using Verse;

namespace AndroidTiersContinuedPatch
{
    public class AndroidTiersPatchSettings : ModSettings
    {
        public float productionMultiplier = 1.0f;
        public float batteryConsumptionMultiplier = 1.0f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref productionMultiplier, "productionMultiplier", 1.0f);
            Scribe_Values.Look(ref batteryConsumptionMultiplier, "batteryConsumptionMultiplier", 1.0f);
        }
    }

    public class AndroidTiersPatchMod : Mod
    {
        public static AndroidTiersPatchSettings settings;

        public AndroidTiersPatchMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<AndroidTiersPatchSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            // Production Multiplier Slider
            listingStandard.Label(string.Format("Ani-Droid Production Multiplier: {0}x", settings.productionMultiplier.ToString("F2")));
            float newProd = (float)Math.Round(listingStandard.Slider(settings.productionMultiplier, 0.25f, 10.0f) * 4f) / 4f;
            if (newProd != settings.productionMultiplier)
            {
                settings.productionMultiplier = newProd;
            }
            listingStandard.Gap();

            // Battery Consumption Multiplier Slider
            listingStandard.Label(string.Format("Ani-Droid Battery Consumption Multiplier: {0}x", settings.batteryConsumptionMultiplier.ToString("F2")));
            float newBat = (float)Math.Round(listingStandard.Slider(settings.batteryConsumptionMultiplier, 0.25f, 10.0f) * 4f) / 4f;
            if (newBat != settings.batteryConsumptionMultiplier)
            {
                settings.batteryConsumptionMultiplier = newBat;
            }

            listingStandard.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            // Re-apply dynamically when settings are closed
            AnimalDietPatch.ApplySettings();
        }

        public override string SettingsCategory()
        {
            return "Android Tiers Continued Patch";
        }
    }
}
