using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace SmartPriorities
{
    public static class SmartPriorities_Algorithm
    {
        public static void AutoAssignPriorities(Pawn pawn)
        {
            if (pawn == null || pawn.workSettings == null || pawn.skills == null || pawn.Dead) return;

            if (!Current.Game.playSettings.useWorkPriorities)
            {
                Current.Game.playSettings.useWorkPriorities = true;
                foreach (Pawn p in Find.CurrentMap.mapPawns.FreeColonists)
                {
                    if (p.workSettings != null && !p.workSettings.EverWork)
                    {
                        p.workSettings.EnableAndInitialize();
                    }
                }
            }

            var sortedWorkTypes = DefDatabase<WorkTypeDef>.AllDefs.OrderByDescending(w => w.naturalPriority).ToList();

            // 1. Calculate Ranks upfront to find "Dedicated Laborers"
            int bestRankAchieved = 999;
            Dictionary<string, int> pawnRanks = new Dictionary<string, int>();

            foreach (WorkTypeDef workType in sortedWorkTypes)
            {
                if (pawn.WorkTypeIsDisabled(workType) || workType.relevantSkills == null || workType.relevantSkills.Count == 0) continue;
                
                Passion myPassion;
                int effectiveSkill;
                int rank = GetPawnRank(pawn, workType, pawn.Map, out myPassion, out effectiveSkill);
                
                pawnRanks[workType.defName] = rank;
                if (rank < bestRankAchieved) bestRankAchieved = rank;
            }

            // If they are never Rank 1 or 2 in ANY job, they are a dedicated laborer
            bool isDedicatedLaborer = (bestRankAchieved > 2);

            // 2. Find Learning Jobs
            List<WorkTypeDef> learningJobs = new List<WorkTypeDef>();
            var gameComp = Current.Game.GetComponent<SmartPriorities_GameComponent>();
            
            string savedJobs = null;
            if (gameComp != null && gameComp.assignedLearningJobs.TryGetValue(pawn, out savedJobs))
            {
                string[] defNames = savedJobs.Split(',');
                foreach (string defName in defNames)
                {
                    WorkTypeDef def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(defName);
                    if (def != null) learningJobs.Add(def);
                }
            }
            else
            {
                List<WorkTypeDef> potentialLearningJobs = new List<WorkTypeDef>();
                foreach (WorkTypeDef wt in sortedWorkTypes)
                {
                    if (pawn.WorkTypeIsDisabled(wt) || wt.relevantSkills == null || wt.relevantSkills.Count == 0) continue;
                    
                    Passion passion;
                    int skill = GetEffectiveSkill(pawn, wt, out passion);
                    
                    // Focus on tasks they are bad at (skill < 4)
                    if (skill < 4)
                    {
                        // Weight passions heavily if they have any
                        if (passion != Passion.None)
                        {
                            potentialLearningJobs.Add(wt);
                            potentialLearningJobs.Add(wt);
                            potentialLearningJobs.Add(wt);
                        }
                        else
                        {
                            potentialLearningJobs.Add(wt);
                        }
                    }
                }

                // Randomly pick up to 3 distinct jobs
                potentialLearningJobs.Shuffle();
                foreach (WorkTypeDef wt in potentialLearningJobs)
                {
                    if (!learningJobs.Contains(wt))
                    {
                        learningJobs.Add(wt);
                        if (learningJobs.Count >= 3) break;
                    }
                }
                
                if (gameComp != null && learningJobs.Count > 0)
                {
                    List<string> defNames = new List<string>();
                    foreach (var wt in learningJobs) defNames.Add(wt.defName);
                    gameComp.assignedLearningJobs[pawn] = string.Join(",", defNames.ToArray());
                }
            }

            // 3. Dynamic Mechanoid Offloading Logic (Supports all mods)
            Dictionary<string, int> offloadLevels = new Dictionary<string, int>();
            if (pawn.Map != null)
            {
                int colonistCount = pawn.Map.mapPawns.FreeColonistsCount;
                if (colonistCount <= 0) colonistCount = 1;

                Dictionary<string, int> mechCountsByWorkType = new Dictionary<string, int>();
                
                foreach (Pawn p in pawn.Map.mapPawns.PawnsInFaction(Faction.OfPlayer))
                {
                    if (p.RaceProps != null && p.RaceProps.IsMechanoid && !p.Dead)
                    {
                        if (p.RaceProps.mechEnabledWorkTypes != null)
                        {
                            foreach (WorkTypeDef wt in p.RaceProps.mechEnabledWorkTypes)
                            {
                                if (!mechCountsByWorkType.ContainsKey(wt.defName))
                                {
                                    mechCountsByWorkType[wt.defName] = 0;
                                }
                                mechCountsByWorkType[wt.defName]++;
                            }
                        }
                    }
                }

                foreach (var kvp in mechCountsByWorkType)
                {
                    offloadLevels[kvp.Key] = GetOffloadLevel(kvp.Value, colonistCount);
                }
            }

            // 4. Assign Priorities
            int priority1Count = 0;
            foreach (WorkTypeDef workType in sortedWorkTypes)
            {
                if (pawn.WorkTypeIsDisabled(workType))
                {
                    pawn.workSettings.SetPriority(workType, 0);
                    continue;
                }

                int rank;
                int priority = CalculateIdealPriority(pawn, workType, isDedicatedLaborer, pawnRanks, offloadLevels, out rank);

                // Apply Learning Job boost
                if (learningJobs.Contains(workType) && priority == 0)
                {
                    priority = 3; 
                }

                // Overload Prevention: Capping Priority 1 assignments
                if (priority == 1)
                {
                    // Exclude critical tasks from the cap
                    if (workType.defName != "Firefighter" && workType.defName != "Patient" && workType.defName != "PatientBedRest" && workType.defName != "BasicWorker")
                    {
                        priority1Count++;
                        if (priority1Count > 3)
                        {
                            priority = 2; // Demote to Priority 2
                        }
                    }
                }

                pawn.workSettings.SetPriority(workType, priority);
            }
        }

        private static int GetEffectiveSkill(Pawn pawn, WorkTypeDef workType, out Passion highestPassion)
        {
            int max = 0;
            highestPassion = Passion.None;
            if (workType.relevantSkills == null || workType.relevantSkills.Count == 0) return 0;

            foreach (SkillDef skillDef in workType.relevantSkills)
            {
                SkillRecord record = pawn.skills.GetSkill(skillDef);
                if (record != null)
                {
                    if (record.Level > max) max = record.Level;
                    if (record.passion > highestPassion) highestPassion = record.passion;
                }
            }

            if (pawn.story != null && pawn.story.traits != null)
            {
                bool isLongTask = workType.defName == "Crafting" || workType.defName == "Art" || workType.defName == "Research" || workType.defName == "Smithing" || workType.defName == "Tailoring";
                
                foreach (var trait in pawn.story.traits.allTraits)
                {
                    if (isLongTask)
                    {
                        if (trait.def.defName == "Industriousness") max += trait.Degree * 2; 
                        if (trait.def.defName == "Neurotic") max += trait.Degree * 2; 
                    }

                    if (workType.defName == "Hunting")
                    {
                        if (trait.def.defName == "Brawler") max -= 10;
                        if (trait.def.defName == "ShootingAccuracy") max += trait.Degree * 3; 
                    }

                    if (workType.defName == "Cooking")
                    {
                        if (trait.def.defName == "Gourmand") max += 3;
                    }
                }
            }

            if (max < 0) max = 0;
            return max;
        }

        private static int GetPawnRank(Pawn pawn, WorkTypeDef workType, Map map, out Passion myPassion, out int myEffectiveSkill)
        {
            myEffectiveSkill = GetEffectiveSkill(pawn, workType, out myPassion);
            if (map == null) return 1;

            int rank = 1;
            foreach (Pawn other in map.mapPawns.FreeColonists)
            {
                if (other == pawn || other.Dead || other.workSettings == null || other.WorkTypeIsDisabled(workType)) continue;

                Passion theirPassion;
                int theirSkill = GetEffectiveSkill(other, workType, out theirPassion);

                if (theirSkill > myEffectiveSkill) rank++;
                else if (theirSkill == myEffectiveSkill && theirPassion > myPassion) rank++;
                else if (theirSkill == myEffectiveSkill && theirPassion == myPassion && other.thingIDNumber > pawn.thingIDNumber) rank++;
            }
            return rank;
        }

        private static int GetOffloadLevel(int mechCount, int colonistCount)
        {
            float ratio = (float)mechCount / colonistCount;
            if (ratio >= 0.66f) return 3; // Full offload
            if (ratio >= 0.33f) return 2; // Major offload
            if (ratio > 0.15f) return 1;  // Minor offload
            return 0; // No offload
        }

        private static int CalculateIdealPriority(Pawn pawn, WorkTypeDef workType, bool isDedicatedLaborer, Dictionary<string, int> precalcRanks, Dictionary<string, int> offloadLevels, out int rank)
        {
            int basePriority = CalculateBasePriority(pawn, workType, isDedicatedLaborer, precalcRanks, out rank);
            
            int offload = 0;
            if (basePriority == 0 || offloadLevels == null || !offloadLevels.TryGetValue(workType.defName, out offload) || offload == 0)
            {
                return basePriority;
            }

            if (offload == 3) 
            {
                return 0;
            }
            else if (offload == 2) 
            {
                if (rank == 1) return Math.Max(4, basePriority); 
                return 0;
            }
            else if (offload == 1) 
            {
                if (rank == 1) return basePriority; 
                if (rank == 2) return Math.Max(4, basePriority); 
                return 0; 
            }
            
            return basePriority;
        }

        private static int CalculateBasePriority(Pawn pawn, WorkTypeDef workType, bool isDedicatedLaborer, Dictionary<string, int> precalcRanks, out int rank)
        {
            rank = 1;
            if (workType.defName == "Firefighter" || workType.defName == "Patient" || workType.defName == "BasicWorker" || workType.defName == "PatientBedRest")
            {
                return 1;
            }

            if (workType.relevantSkills == null || workType.relevantSkills.Count == 0)
            {
                if (workType.defName == "Hauling" || workType.defName == "Cleaning")
                {
                    if (isDedicatedLaborer) return 1;
                    else return 4;
                }
                return 3;
            }

            Passion myPassion;
            int effectiveSkill = GetEffectiveSkill(pawn, workType, out myPassion);
            
            if (precalcRanks != null && precalcRanks.TryGetValue(workType.defName, out rank))
            {
                // We already have the rank
            }
            else
            {
                rank = GetPawnRank(pawn, workType, pawn.Map, out myPassion, out effectiveSkill);
            }

            bool isDangerousJob = workType.defName == "Doctor" || workType.defName == "Cooking" || workType.defName == "Warden";
            if (isDangerousJob)
            {
                if (effectiveSkill < 4 && myPassion == Passion.None)
                {
                    if (rank == 1) return 2; 
                    return 0; 
                }
                if (rank > 2) return 0; 
            }

            if (effectiveSkill < 2 && myPassion == Passion.None)
            {
                if (rank == 1) return 3;
                
                if (workType.defName == "Mining") return 4;

                return 0;
            }

            if (rank == 1)
            {
                if (myPassion != Passion.None || effectiveSkill >= 8) return 1;
                else return 2;
            }
            else if (rank == 2)
            {
                if (myPassion == Passion.Major || effectiveSkill >= 12) return 2;
                if (myPassion == Passion.Minor || effectiveSkill >= 8) return 3;
                return 4;
            }
            else 
            {
                bool isGenericLabor = workType.defName == "Growing" || workType.defName == "Mining" || workType.defName == "PlantCutting" || workType.defName == "Construction";
                if (isGenericLabor)
                {
                    if (effectiveSkill >= 8) return 3;
                    return 4;
                }
                else
                {
                    if (effectiveSkill >= 12) return 3;
                    return 0; 
                }
            }
        }
    }
}
