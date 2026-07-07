using UnityEngine;
using Verse;
using HarmonyLib;

namespace InfiniteTurrets
{
    public class InfiniteTurretsMod : Mod
    {
        public static InfiniteTurretsSettings Settings;

        public InfiniteTurretsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<InfiniteTurretsSettings>();
            
            var harmony = new Harmony("idkman2021.infiniteturrets");
            harmony.PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.CheckboxLabeled("Infinite Durability", ref Settings.infiniteDurability, "If checked, turret barrels will never consume durability/fuel.");

            if (!Settings.infiniteDurability)
            {
                listingStandard.Label(string.Format("Durability Multiplier: {0:F2}x", Settings.durabilityMultiplier));
                Settings.durabilityMultiplier = listingStandard.Slider(Settings.durabilityMultiplier, 0.1f, 10.0f);
            }

            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Infinite Turrets";
        }
    }
}
