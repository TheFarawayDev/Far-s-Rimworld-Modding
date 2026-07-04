using HarmonyLib;
using RimWorld;
using Verse;
using System.Reflection;

namespace WhyInfestations
{
    [StaticConstructorOnStartup]
    public static class WhyInfestationsMod
    {
        static WhyInfestationsMod()
        {
            var harmony = new Harmony("thefarawaydev.whyinfestations");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Message("[Why Infestations?] initialized. All infestations are now prevented.");
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_Infestation), "TryExecuteWorker")]
    public static class Patch_Infestation_Execute
    {
        public static bool Prefix(ref bool __result)
        {
            __result = false;
            return false; // Skips original method
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_Infestation), "CanFireNowSub")]
    public static class Patch_Infestation_CanFire
    {
        public static bool Prefix(ref bool __result)
        {
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_DeepDrillInfestation), "TryExecuteWorker")]
    public static class Patch_DeepDrillInfestation_Execute
    {
        public static bool Prefix(ref bool __result)
        {
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_DeepDrillInfestation), "CanFireNowSub")]
    public static class Patch_DeepDrillInfestation_CanFire
    {
        public static bool Prefix(ref bool __result)
        {
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_WastepackInfestation), "TryExecuteWorker")]
    public static class Patch_WastepackInfestation_Execute
    {
        public static bool Prefix(ref bool __result)
        {
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_WastepackInfestation), "CanFireNowSub")]
    public static class Patch_WastepackInfestation_CanFire
    {
        public static bool Prefix(ref bool __result)
        {
            __result = false;
            return false;
        }
    }
}
