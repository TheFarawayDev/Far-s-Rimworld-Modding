using UnityEngine;
using Verse;

namespace QuarryCo
{
    public class QuarryCoSettings : ModSettings
    {
        public float WorkersPerTile = 1.0f;
        public bool AutoHaul = true;
        public int MinMiningSkill = 0;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref WorkersPerTile, "workersPerTile", 1.0f);
            Scribe_Values.Look(ref AutoHaul, "autoHaul", true);
            Scribe_Values.Look(ref MinMiningSkill, "minMiningSkill", 0);
        }
    }

    public class QuarryCoMod : Mod
    {
        public static QuarryCoSettings Settings;

        public QuarryCoMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<QuarryCoSettings>();
        }

        public override string SettingsCategory() => "Quarry Co.";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label("Workers per tile: " + Settings.WorkersPerTile.ToString("F1") + "x");
            listing.Gap(4f);
            Settings.WorkersPerTile = Mathf.Round(listing.Slider(Settings.WorkersPerTile, 0.1f, 3.0f) * 10f) / 10f;
            listing.Gap(4f);
            listing.Label("Small quarry (3x3): " + Mathf.Max(1, Mathf.RoundToInt(9 * Settings.WorkersPerTile)) + " workers");
            listing.Label("Medium quarry (5x5): " + Mathf.Max(1, Mathf.RoundToInt(25 * Settings.WorkersPerTile)) + " workers");
            listing.Label("Large quarry (7x7): " + Mathf.Max(1, Mathf.RoundToInt(49 * Settings.WorkersPerTile)) + " workers");
            listing.Label("Grand quarry (9x9): " + Mathf.Max(1, Mathf.RoundToInt(81 * Settings.WorkersPerTile)) + " workers");
            listing.Gap(12f);
            listing.CheckboxLabeled("Auto-haul quarried resources", ref Settings.AutoHaul, "Spawned resources are unforbidden and marked for hauling.");
            listing.Gap(12f);
            listing.Label("Minimum mining skill: " + Settings.MinMiningSkill);
            listing.Gap(4f);
            Settings.MinMiningSkill = Mathf.RoundToInt(listing.Slider(Settings.MinMiningSkill, 0f, 20f));
            listing.Gap(4f);
            listing.Label("Set to 0 to allow any pawn assigned to Quarrying.");
            listing.End();
        }
    }
}
