using System;
using System.Reflection;
using Verse;
using HarmonyLib;

namespace SmartPriorities
{
    public class SmartPriorities_Mod : Mod
    {
        public static SmartPriorities_Mod Instance;

        public SmartPriorities_Mod(ModContentPack content) : base(content)
        {
            Instance = this;
            
            // Initialize Harmony
            var harmony = new Harmony("thefarawaydev.smartpriorities");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            
            Log.Message("[SmartPriorities] Successfully loaded and applied Harmony patches.");
        }
    }
}
