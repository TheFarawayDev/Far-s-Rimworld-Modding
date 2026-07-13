using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;
using Verse.AI;
using RimWorld;

namespace AndroidTiersContinuedPatch
{
    [HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
    public static class Patch_Pawn_JobTracker_DetermineNextJob
    {
        private static readonly Dictionary<int, int> lastScanTick = new Dictionary<int, int>();

        public static void Postfix(Pawn_JobTracker __instance, ref ThinkResult __result, Pawn ___pawn)
        {
            if (___pawn == null || !___pawn.Spawned || ___pawn.Dead || ___pawn.Faction != Faction.OfPlayer)
                return;

            // Only run if the toggle is enabled in settings
            if (AndroidTiersPatchMod.settings == null || !AndroidTiersPatchMod.settings.autoEquipEnabled)
                return;

            // Only apply to androids
            if (!IsAndroid(___pawn))
                return;

            // Only scan if the pawn is currently idle (result is invalid, has no job, or is a low-priority job)
            bool isIdle = !__result.IsValid || 
                          __result.Job == null || 
                          __result.Job.def == JobDefOf.Wait || 
                          __result.Job.def == JobDefOf.Wait_Wander || 
                          __result.Job.def == JobDefOf.GotoWander;
            if (!isIdle)
                return;

            // Throttling: only scan once every 1000 ticks per pawn
            int currentTick = Find.TickManager.TicksGame;
            int pawnId = ___pawn.thingIDNumber;
            if (lastScanTick.TryGetValue(pawnId, out int lastTick) && currentTick - lastTick < 1000)
                return;

            lastScanTick[pawnId] = currentTick;

            // Try to find a better weapon
            Job equipJob = TryGetWeaponUpgradeJob(___pawn);
            if (equipJob != null)
            {
                __result = new ThinkResult(equipJob, null, null, false);
                return;
            }

            // Try to find a better apparel/armor
            Job wearJob = TryGetApparelUpgradeJob(___pawn);
            if (wearJob != null)
            {
                __result = new ThinkResult(wearJob, null, null, false);
                return;
            }
        }

        private static bool IsAndroid(Pawn pawn)
        {
            if (pawn?.def == null) return false;
            string defName = pawn.def.defName;
            return defName.StartsWith("Android") || 
                   defName == "M7Mech" || 
                   defName == "M8Mech" || 
                   defName == "AT_HellUnit";
        }

        private static Job TryGetWeaponUpgradeJob(Pawn pawn)
        {
            if (pawn.equipment == null) return null;

            // Determine if pawn prefers Melee or Ranged
            bool prefersMelee = false;
            if (pawn.story?.traits?.HasTrait(TraitDefOf.Brawler) ?? false)
            {
                prefersMelee = true;
            }
            else
            {
                int meleeSkill = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                int shootingSkill = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                if (meleeSkill > shootingSkill + 3)
                {
                    prefersMelee = true;
                }
            }

            // Current weapon score
            float currentScore = 0f;
            Thing currentWeapon = pawn.equipment.Primary;
            if (currentWeapon != null)
            {
                currentScore = GetWeaponScore(currentWeapon, prefersMelee);
            }

            // Find best weapon on the map
            Thing bestWeapon = null;
            float bestScore = currentScore;

            var listerThings = pawn.Map?.listerThings;
            if (listerThings == null) return null;

            var weapons = listerThings.ThingsInGroup(ThingRequestGroup.Weapon);
            if (weapons == null) return null;

            foreach (var weapon in weapons)
            {
                if (weapon.def.IsWeapon && 
                    weapon.Spawned &&
                    !weapon.IsForbidden(pawn) && 
                    pawn.CanReserveAndReach(weapon, PathEndMode.Touch, Danger.Some))
                {
                    float score = GetWeaponScore(weapon, prefersMelee);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestWeapon = weapon;
                    }
                }
            }

            // Only equip if the new weapon is at least 15% better (avoids minor back-and-forth)
            if (bestWeapon != null && (currentWeapon == null || bestScore > currentScore * 1.15f))
            {
                if (EquipmentUtility.CanEquip(bestWeapon, pawn, out string reason))
                {
                    return JobMaker.MakeJob(JobDefOf.Equip, bestWeapon);
                }
            }

            return null;
        }

        private static float GetWeaponScore(Thing weapon, bool prefersMelee)
        {
            float score = weapon.MarketValue; // Base score is Market Value

            // Quality modifier
            if (QualityUtility.TryGetQuality(weapon, out QualityCategory qc))
            {
                switch (qc)
                {
                    case QualityCategory.Awful: score *= 0.5f; break;
                    case QualityCategory.Poor: score *= 0.75f; break;
                    case QualityCategory.Normal: score *= 1.0f; break;
                    case QualityCategory.Good: score *= 1.2f; break;
                    case QualityCategory.Excellent: score *= 1.5f; break;
                    case QualityCategory.Masterwork: score *= 2.0f; break;
                    case QualityCategory.Legendary: score *= 3.0f; break;
                }
            }

            // Hitpoints modifier
            if (weapon.def.useHitPoints && weapon.MaxHitPoints > 0)
            {
                score *= (float)weapon.HitPoints / weapon.MaxHitPoints;
            }

            // Preference modifier
            bool isMelee = weapon.def.IsMeleeWeapon;
            if (isMelee == prefersMelee)
            {
                score *= 1.5f;
            }
            else
            {
                score *= 0.5f;
            }

            return score;
        }

        private static Job TryGetApparelUpgradeJob(Pawn pawn)
        {
            if (pawn.apparel == null) return null;

            var listerThings = pawn.Map?.listerThings;
            if (listerThings == null) return null;

            var apparels = listerThings.ThingsInGroup(ThingRequestGroup.Apparel);
            if (apparels == null) return null;

            Thing bestApparel = null;
            float bestAdvantage = 0f;

            foreach (var apparel in apparels)
            {
                if (apparel.def.IsApparel && 
                    apparel.Spawned && 
                    !apparel.IsForbidden(pawn) && 
                    pawn.CanReserveAndReach(apparel, PathEndMode.Touch, Danger.Some))
                {
                    if (!ApparelUtility.HasPartsToWear(pawn, apparel.def)) continue;

                    float advantage = GetApparelAdvantage(apparel, pawn);
                    if (advantage > bestAdvantage && advantage > 10f)
                    {
                        bestAdvantage = advantage;
                        bestApparel = apparel;
                    }
                }
            }

            if (bestApparel != null)
            {
                return JobMaker.MakeJob(JobDefOf.Wear, bestApparel);
            }

            return null;
        }

        private static float GetApparelAdvantage(Thing newApparel, Pawn pawn)
        {
            float newScore = GetApparelScore(newApparel);

            float currentScore = 0f;
            var currentWorn = pawn.apparel.WornApparel;
            for (int i = 0; i < currentWorn.Count; i++)
            {
                var worn = currentWorn[i];
                if (ApparelUtility.CanWearTogether(newApparel.def, worn.def, pawn.RaceProps.body))
                    continue;

                currentScore += GetApparelScore(worn);
            }

            return newScore - currentScore;
        }

        private static float GetApparelScore(Thing apparel)
        {
            float sharp = apparel.GetStatValue(StatDefOf.ArmorRating_Sharp);
            float blunt = apparel.GetStatValue(StatDefOf.ArmorRating_Blunt);
            float heat = apparel.GetStatValue(StatDefOf.ArmorRating_Heat);
            
            float score = (sharp * 100f) + (blunt * 50f) + (heat * 25f);
            if (score <= 0f)
            {
                score = apparel.MarketValue * 0.1f;
            }

            if (QualityUtility.TryGetQuality(apparel, out QualityCategory qc))
            {
                switch (qc)
                {
                    case QualityCategory.Awful: score *= 0.5f; break;
                    case QualityCategory.Poor: score *= 0.75f; break;
                    case QualityCategory.Normal: score *= 1.0f; break;
                    case QualityCategory.Good: score *= 1.2f; break;
                    case QualityCategory.Excellent: score *= 1.5f; break;
                    case QualityCategory.Masterwork: score *= 2.0f; break;
                    case QualityCategory.Legendary: score *= 3.0f; break;
                }
            }

            if (apparel.def.useHitPoints && apparel.MaxHitPoints > 0)
            {
                score *= (float)apparel.HitPoints / apparel.MaxHitPoints;
            }

            return score;
        }
    }
}
