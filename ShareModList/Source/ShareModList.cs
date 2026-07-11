using System;
using System.Text;
using Verse;
using UnityEngine;
using RimWorld;
using System.Linq;

namespace ShareModList
{
    public class ShareModListMod : Mod
    {
        public ShareModListMod(ModContentPack content) : base(content)
        {
        }

        public override string SettingsCategory()
        {
            return "Share Mod List";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            
            listingStandard.Label("Click the button below to copy your active mod list to the clipboard. You can then paste it anywhere to share.");
            listingStandard.Gap();

            if (listingStandard.ButtonText("Copy Mod List to Clipboard"))
            {
                StringBuilder sb = new StringBuilder();
                var activeMods = LoadedModManager.RunningModsListForReading;
                
                sb.AppendLine("Active Mods:");
                foreach (var mod in activeMods)
                {
                    sb.AppendLine(string.Format("- {0} ({1})", mod.Name, mod.PackageIdPlayerFacing));
                }
                
                GUIUtility.systemCopyBuffer = sb.ToString();
                Messages.Message("Mod list copied to clipboard!", MessageTypeDefOf.TaskCompletion, false);
            }

            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }
    }
}
