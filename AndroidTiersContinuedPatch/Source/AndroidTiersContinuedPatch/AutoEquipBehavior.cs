using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;
using HarmonyLib;

namespace AndroidTiersContinuedPatch
{
    public class AndroidTiersContinuedPatchSettings : ModSettings
    {
        public bool autoEquipArmor = true;
        public bool autoEquipWeapons = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref autoEquipArmor, "autoEquipArmor", true);
            Scribe_Values.Look(ref autoEquipWeapons, "autoEquipWeapons", true);
        }
    }

    public class AndroidTiersContinuedPatchMod : Mod
    {
        public static AndroidTiersContinuedPatchSettings settings;

        public AndroidTiersContinuedPatchMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<AndroidTiersContinuedPatchSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("Enable Droid Auto-Equip Armor", ref settings.autoEquipArmor, "If true, droids will automatically equip the best armor they can find.");
            listing.CheckboxLabeled("Enable Droid Auto-Equip Weapons", ref settings.autoEquipWeapons, "If true, droids will automatically equip better weapons.");
            listing.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Android Tiers Continued Patch";
        }
    }

    [StaticConstructorOnStartup]
    public static class AutoEquipInjector
    {
        static AutoEquipInjector()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                InjectIntoThinkTree("AndroidLike");
                InjectIntoThinkTree("MechM7Like");
                InjectIntoThinkTree("HumanLike"); // For robots/droids that use the standard human brain
            });
        }

        private static void InjectIntoThinkTree(string defName)
        {
            ThinkTreeDef tree = DefDatabase<ThinkTreeDef>.GetNamedSilentFail(defName);
            if (tree == null || tree.thinkRoot == null) return;

            ThinkNode_Priority rootPriority = tree.thinkRoot as ThinkNode_Priority;
            if (rootPriority == null) return;

            // 1. Inject OptimizeWeapon
            if (!rootPriority.subNodes.Any(n => n is JobGiver_OptimizeWeapon))
            {
                JobGiver_OptimizeWeapon weaponNode = new JobGiver_OptimizeWeapon();
                rootPriority.subNodes.Insert(Math.Max(0, rootPriority.subNodes.Count - 2), weaponNode);
                Log.Message(string.Format("[AndroidTiersContinuedPatch] Injected JobGiver_OptimizeWeapon into {0}", defName));
            }

            // 2. Inject OptimizeApparelDroids (Only for Android/Mech specific trees to avoid double-dipping vanilla HumanLike)
            if (defName != "HumanLike" && !rootPriority.subNodes.Any(n => n is JobGiver_OptimizeApparelDroids))
            {
                JobGiver_OptimizeApparelDroids apparelNode = new JobGiver_OptimizeApparelDroids();
                rootPriority.subNodes.Insert(Math.Max(0, rootPriority.subNodes.Count - 2), apparelNode);
                Log.Message(string.Format("[AndroidTiersContinuedPatch] Injected JobGiver_OptimizeApparelDroids into {0}", defName));
            }
        }
    }

    public class JobGiver_OptimizeApparelDroids : JobGiver_OptimizeApparel
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (AndroidTiersContinuedPatchMod.settings == null || !AndroidTiersContinuedPatchMod.settings.autoEquipArmor)
                return null;

            // Only run for player faction
            if (pawn.Faction != Faction.OfPlayer)
                return null;

            // Do not interrupt important wait jobs like Bestowing Ceremony or other Lord duties
            if (pawn.CurJob != null && (pawn.CurJob.def == JobDefOf.Wait_MaintainPosture || pawn.CurJob.def.defName == "Wait_Downed" || pawn.CurJob.def.defName == "Wait_SafeTemperature"))
                return null;

            return base.TryGiveJob(pawn);
        }
    }

    public class JobGiver_OptimizeWeapon : ThinkNode_JobGiver
    {
        private static Dictionary<Pawn, int> nextOptimizeTicks = new Dictionary<Pawn, int>();

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (AndroidTiersContinuedPatchMod.settings == null || !AndroidTiersContinuedPatchMod.settings.autoEquipWeapons)
                return null;

            if (pawn.equipment == null) return null;
            if (pawn.Faction != Faction.OfPlayer) return null;
            if (pawn.Map == null) return null;

            // Ensure this logic only applies to synthetic/robotic pawns
            string defName = pawn.def.defName.ToLower();
            if (!defName.Contains("droid") && !defName.Contains("android") && !defName.Contains("mech") && !defName.Contains("robot"))
                return null;

            int currentTick = Find.TickManager.TicksGame;
            int nextTick;
            if (nextOptimizeTicks.TryGetValue(pawn, out nextTick))
            {
                if (currentTick < nextTick) return null;
            }

            // Check more frequently to seem responsive (every 500-800 ticks, approx 15-25 seconds)
            nextOptimizeTicks[pawn] = currentTick + Rand.Range(500, 800);

            if (pawn.Drafted || pawn.IsBurning() || pawn.Downed)
                return null;

            // Do not interrupt important wait jobs like Bestowing Ceremony or other Lord duties
            if (pawn.CurJob != null && (pawn.CurJob.def == JobDefOf.Wait_MaintainPosture || pawn.CurJob.def.defName == "Wait_Downed" || pawn.CurJob.def.defName == "Wait_SafeTemperature"))
                return null;


            ThingWithComps currentWeapon = pawn.equipment.Primary;
            float currentScore = GetWeaponScore(currentWeapon);

            Thing bestWeapon = null;
            float bestScore = currentScore;

            List<Thing> weapons = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon);
            foreach (Thing w in weapons)
            {
                if (w.IsForbidden(pawn)) continue;
                if (!pawn.CanReserveAndReach(w, PathEndMode.OnCell, Danger.Some)) continue;

                float score = GetWeaponScore(w);
                
                // If unarmed, grab any decent weapon
                if (currentWeapon == null)
                {
                    if (score > bestScore && score > 20f)
                    {
                        bestScore = score;
                        bestWeapon = w;
                    }
                }
                // If armed, only swap if significantly better (15% better and 50 value diff)
                else
                {
                    if (score > bestScore * 1.15f && score > currentScore + 50f)
                    {
                        bestScore = score;
                        bestWeapon = w;
                    }
                }
            }

            if (bestWeapon != null)
            {
                return JobMaker.MakeJob(JobDefOf.Equip, bestWeapon);
            }

            return null;
        }

        private float GetWeaponScore(Thing weapon)
        {
            if (weapon == null) return 0f;
            
            float score = weapon.MarketValue;
            
            // Prefer ranged weapons significantly over melee for droids
            if (weapon.def.IsRangedWeapon)
            {
                score *= 1.5f; 
            }
            // Penalty for extremely heavy/slow weapons unless they are high tier
            if (weapon.def.BaseMass > 15f)
            {
                score *= 0.8f;
            }

            return score;
        }
    }

    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), "ApparelScoreRaw")]
    public static class ApparelScoreRaw_DroidArmorPatch
    {
        public static void Postfix(Pawn pawn, Apparel ap, ref float __result)
        {
            if (AndroidTiersContinuedPatchMod.settings == null || !AndroidTiersContinuedPatchMod.settings.autoEquipArmor) return;

            // Only apply this restriction to droids/androids
            if (pawn.def.defName.Contains("Droid") || pawn.def.defName.Contains("droid") || pawn.def.defName.StartsWith("Android") || pawn.def.defName.StartsWith("android"))
            {
                // Check if it's actually armor (has a decent base sharp/blunt rating)
                float sharp = ap.def.statBases.GetStatValueFromList(StatDefOf.ArmorRating_Sharp, 0f);
                float blunt = ap.def.statBases.GetStatValueFromList(StatDefOf.ArmorRating_Blunt, 0f);
                
                // If it provides almost no armor (like normal shirts/pants/cowboy hats), score it extremely low
                if (sharp < 0.15f && blunt < 0.15f)
                {
                    __result = -1000f;
                }
            }
        }
    }
}
