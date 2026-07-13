using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;
using RimWorld;

namespace AndroidTiersContinuedPatch
{
    public static class EndlessGrowthCompat
    {
        public static bool EndlessGrowthActive => ModsConfig.IsActive("Slime.EndlessGrowth");

        public static int GetMaxSkillLevel()
        {
            return EndlessGrowthActive ? 100 : 20;
        }

        public static float GetMaxSkillLevelFloat()
        {
            return EndlessGrowthActive ? 100f : 20f;
        }

        public static string FormatSkillSlash(string original)
        {
            if (original == null) return "/20";
            return original.Replace("20", GetMaxSkillLevel().ToString());
        }
    }

    [HarmonyPatch]
    public static class Patch_Dialog_SkillUp_DoWindowContents
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("MOARANDROIDS.Dialog_SkillUp");
            if (type == null)
            {
                Log.Warning("[AndroidTiersContinuedPatch] MOARANDROIDS.Dialog_SkillUp type not found. Endless Growth patch aborted.");
                return null;
            }
            return AccessTools.Method(type, "DoWindowContents", new[] { typeof(UnityEngine.Rect) });
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool patched = false;

            for (int i = 0; i < codes.Count; i++)
            {
                // Pattern 1: Slider max limit: list.Slider(..., 0f, 20f)
                if (i >= 2 && 
                    codes[i-2].opcode == OpCodes.Ldc_R4 && 
                    codes[i-1].opcode == OpCodes.Ldc_R4 && 
                    (float)codes[i-1].operand == 20f)
                {
                    if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand.ToString().Contains("Slider"))
                    {
                        codes[i-1].opcode = OpCodes.Call;
                        codes[i-1].operand = AccessTools.Method(typeof(EndlessGrowthCompat), nameof(EndlessGrowthCompat.GetMaxSkillLevelFloat));
                        patched = true;
                    }
                }

                // Pattern 2: Validation check: p + sr.levelInt <= 20
                if (i >= 1 && 
                    codes[i-1].opcode == OpCodes.Add && 
                    (codes[i].opcode == OpCodes.Ldc_I4_S || codes[i].opcode == OpCodes.Ldc_I4))
                {
                    int val = Convert.ToInt32(codes[i].operand);
                    if (val == 20)
                    {
                        codes[i].opcode = OpCodes.Call;
                        codes[i].operand = AccessTools.Method(typeof(EndlessGrowthCompat), nameof(EndlessGrowthCompat.GetMaxSkillLevel));
                        patched = true;
                    }
                }

                // Pattern 3: Label string format "/20"
                if (codes[i].opcode == OpCodes.Ldstr && codes[i].operand is string s && s.Contains("/20"))
                {
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(EndlessGrowthCompat), nameof(EndlessGrowthCompat.FormatSkillSlash))));
                    patched = true;
                    i++; // skip past the inserted instruction
                }
            }

            if (patched)
            {
                Log.Message("[AndroidTiersContinuedPatch] Successfully transpiled Dialog_SkillUp for Endless Growth compatibility.");
            }
            else
            {
                Log.Warning("[AndroidTiersContinuedPatch] Dialog_SkillUp transpiler did not modify any instructions.");
            }

            return codes;
        }
    }
}
