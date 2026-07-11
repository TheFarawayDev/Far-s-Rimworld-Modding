using UnityEngine;
using Verse;
using RimWorld;

namespace DoMoreResearch
{
    public class DoMoreResearchSettings : ModSettings
    {
        public bool enableAutoResearch = true;
        public bool smartNotifications = true;
        public bool strictTechEraProgression = false;
        public string focusedTab = null;
        public bool firstTimeSettingsOpened = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableAutoResearch, "enableAutoResearch", true);
            Scribe_Values.Look(ref smartNotifications, "smartNotifications", true);
            Scribe_Values.Look(ref strictTechEraProgression, "strictTechEraProgression", false);
            Scribe_Values.Look(ref focusedTab, "focusedTab", null);
            Scribe_Values.Look(ref firstTimeSettingsOpened, "firstTimeSettingsOpened", false);
        }
    }

    public class DoMoreResearchMod : Mod
    {
        public static DoMoreResearchSettings Settings;

        public DoMoreResearchMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<DoMoreResearchSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled("Enable Auto-Research", ref Settings.enableAutoResearch, "Master kill switch for auto-research selection.");
            listing.CheckboxLabeled("Smart Notifications", ref Settings.smartNotifications, "Only notify if the auto-selected tech costs more than 1000 points. Keeps low-tier tech selection silent.");
            listing.CheckboxLabeled("Strict Tech Era Progression", ref Settings.strictTechEraProgression, "Force the mod to finish ALL available tech in the current or lower era before selecting higher era tech.");

            listing.Gap();
            string focusText = string.IsNullOrEmpty(Settings.focusedTab) ? "None" : Settings.focusedTab;
            listing.Label($"Current Tab Focus: {focusText}", tooltip: "Shift-click a tab in the Research window to focus it.");
            
            if (!string.IsNullOrEmpty(Settings.focusedTab) && listing.ButtonText("Clear Focus"))
            {
                Settings.focusedTab = null;
                AutoResearcher.ClearCache();
            }

            listing.Gap();
            listing.Label("--- Testing & Debugging ---");
            if (listing.ButtonText("Test Notification (Important Tech)"))
            {
                Messages.Message("Auto-selected research: [TEST] Spacer Tech", MessageTypeDefOf.TaskCompletion, false);
            }
            if (listing.ButtonText("Test Notification (Minor Tech)"))
            {
                if (Settings.smartNotifications)
                {
                    Messages.Message("Auto-selected research: [TEST] Tribal Tech", MessageTypeDefOf.SilentInput, false);
                }
                else
                {
                    Messages.Message("Auto-selected research: [TEST] Tribal Tech", MessageTypeDefOf.TaskCompletion, false);
                }
            }

            listing.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Do More Research";
        }
        
        public override void WriteSettings()
        {
            base.WriteSettings();
            AutoResearcher.ClearCache();
        }
    }
}
