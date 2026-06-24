using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using UnityEngine;

namespace AndroidTiersContinuedPatch
{
    [HarmonyPatch]
    public static class HideEnemyAndroidGizmosPatch
    {
        public static bool Prepare()
        {
            return AccessTools.TypeByName("MOARANDROIDS.CompAndroidState") != null;
        }

        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("MOARANDROIDS.CompAndroidState"), "CompGetGizmosExtra");
        }

        [HarmonyPostfix]
        public static void Postfix(ThingComp __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance == null) return;
            Pawn pawn = __instance.parent as Pawn;
            if (pawn != null)
            {
                // If the android is not ours, not our prisoner, and not our slave, hide the gizmos.
                if (pawn.Faction != Faction.OfPlayer && !pawn.IsPrisonerOfColony && !pawn.IsSlaveOfColony)
                {
                    __result = Enumerable.Empty<Gizmo>();
                }
            }
        }
    }

    [HarmonyPatch]
    public static class HackAnyEnemyAndroidPatch
    {
        public static bool Prepare()
        {
            return AccessTools.TypeByName("MOARANDROIDS.Designator_SurrogateToHack") != null;
        }

        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("MOARANDROIDS.Designator_SurrogateToHack"), "CanDesignateThing");
        }

        [HarmonyPostfix]
        public static void Postfix(object __instance, Thing t, ref AcceptanceReport __result)
        {
            if (__result.Accepted) return;

            Pawn cp = t as Pawn;
            if (cp != null && cp.Faction != Faction.OfPlayer)
            {
                bool isAndroid = cp.AllComps.Any(c => c.GetType().Name == "CompAndroidState");
                bool hasSkyMind = cp.AllComps.Any(c => c.GetType().Name == "CompSkyMind");
                
                if (isAndroid && hasSkyMind)
                {
                    __result = true;
                    Traverse.Create(__instance).Field("target").SetValue(cp);
                }
            }
        }
    }

    [HarmonyPatch]
    public static class FixTempHackingEndingPatch
    {
        public static bool Prepare()
        {
            return AccessTools.TypeByName("MOARANDROIDS.CompSkyMind") != null;
        }

        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("MOARANDROIDS.CompSkyMind"), "tempHackingEnding");
        }

        [HarmonyPrefix]
        public static bool Prefix(ThingComp __instance)
        {
            try
            {
                var traverse = Traverse.Create(__instance);
                int hacked = traverse.Field("hacked").GetValue<int>();
                if (hacked != 3) return false;

                Pawn cp = (Pawn)__instance.parent;
                var parentCAS = traverse.Field("parentCAS").GetValue<ThingComp>();

                // 1. Safe stopControlledSurrogate invocation
                try
                {
                    if (parentCAS != null)
                    {
                        var surrogateController = Traverse.Create(parentCAS).Field("surrogateController").GetValue<Pawn>();
                        if (surrogateController != null)
                        {
                            var utilsType = AccessTools.TypeByName("MOARANDROIDS.Utils");
                            if (utilsType != null)
                            {
                                var method = AccessTools.Method(utilsType, "getCachedCSO");
                                if (method != null)
                                {
                                    var cso = method.Invoke(null, new object[] { surrogateController });
                                    if (cso != null)
                                    {
                                        var stopMethod = AccessTools.Method(cso.GetType(), "stopControlledSurrogate");
                                        if (stopMethod != null)
                                        {
                                            var pars = stopMethod.GetParameters();
                                            object[] args = new object[pars.Length];
                                            args[0] = cp;
                                            if (pars.Length > 1) args[1] = true;  // externalController
                                            if (pars.Length > 2) args[2] = false; // preventNoHost
                                            if (pars.Length > 3) args[3] = false; // noPrisonedSurrogateConversion
                                            if (pars.Length > 4) args[4] = true;  // downedViaDisconnect
                                            for (int i = 5; i < pars.Length; i++)
                                            {
                                                if (pars[i].ParameterType == typeof(bool)) args[i] = false;
                                                else args[i] = null;
                                            }
                                            stopMethod.Invoke(cso, args);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Error("[AndroidTiersContinuedPatch] Safe stopControlledSurrogate failed: " + ex.ToString());
                }

                // 2. Reset faction back to original
                Faction hackOrigFaction = traverse.Field("hackOrigFaction").GetValue<Faction>();
                if (hackOrigFaction != null && cp.Faction != hackOrigFaction)
                {
                    cp.SetFaction(hackOrigFaction, null);
                }

                // 3. Reset prison/slave guest status if needed
                bool hackWasPrisoned = traverse.Field("hackWasPrisoned").GetValue<bool>();
                if (hackWasPrisoned && cp.guest != null)
                {
                    if (cp.IsSlave)
                        cp.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);
                    else
                        cp.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);

                    if (cp.workSettings == null)
                    {
                        cp.workSettings = new Pawn_WorkSettings(cp);
                        cp.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
                    }
                }

                // 4. Reset jobs and mindState
                if (cp.jobs != null)
                {
                    cp.jobs.StopAll();
                    cp.jobs.ClearQueuedJobs();
                }

                if (cp.mindState != null)
                    cp.mindState.Reset(true, false);

                PawnComponentsUtility.AddAndRemoveDynamicComponents(cp, false);

                // 5. Safe SkyMind Disconnection
                try
                {
                    var utilsTypeForGC = AccessTools.TypeByName("MOARANDROIDS.Utils");
                    if (utilsTypeForGC != null)
                    {
                        var fieldInfo = AccessTools.Field(utilsTypeForGC, "GCATPP");
                        var gcatpp = fieldInfo != null ? fieldInfo.GetValue(null) : null;
                        if (gcatpp != null)
                        {
                            var discMethod = AccessTools.Method(gcatpp.GetType(), "disconnectUser");
                            if (discMethod != null)
                            {
                                var pars = discMethod.GetParameters();
                                object[] args = new object[pars.Length];
                                args[0] = cp;
                                if (pars.Length > 1) args[1] = false; // notify
                                if (pars.Length > 2) args[2] = true;  // force
                                for (int i = 3; i < pars.Length; i++)
                                {
                                    if (pars[i].ParameterType == typeof(bool)) args[i] = false;
                                    else args[i] = null;
                                }
                                discMethod.Invoke(gcatpp, args);
                            }
                        }
                    }
                    traverse.Field("autoconn").SetValue(false);
                    traverse.Field("connected").SetValue(false);
                    if (Find.ColonistBar != null)
                    {
                        Find.ColonistBar.MarkColonistsDirty();
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Error("[AndroidTiersContinuedPatch] Safe disconnectUser failed: " + ex.ToString());
                }

                // 6. Reset hacked state and clear fields
                var propHacked = AccessTools.Property(__instance.GetType(), "Hacked");
                if (propHacked != null)
                {
                    propHacked.SetValue(__instance, -1, null);
                }
                else
                {
                    traverse.Field("hacked").SetValue(-1);
                    var utilsType = AccessTools.TypeByName("MOARANDROIDS.Utils");
                    if (utilsType != null)
                    {
                        var fieldInfo = AccessTools.Field(utilsType, "GCATPP");
                        var gcatpp = fieldInfo != null ? fieldInfo.GetValue(null) : null;
                        if (gcatpp != null)
                        {
                            Traverse.Create(gcatpp).Method("popVirusedThing", new object[] { cp }).GetValue();
                        }
                    }
                }

                traverse.Field("hackEndGT").SetValue(-1);
                traverse.Field("hackWasPrisoned").SetValue(false);

                if (cp.Map != null)
                {
                    cp.Map.attackTargetsCache.UpdateTarget(cp);

                    var allPawns = cp.Map.mapPawns.AllPawnsSpawned.ToList();
                    foreach (var p in allPawns)
                    {
                        if (p.CurJob != null && p.CurJob.targetA != null && p.CurJob.targetA.Thing == cp)
                        {
                            p.jobs.EndCurrentJob(JobCondition.InterruptForced, true, true);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error("[AndroidTiersContinuedPatch] Safe tempHackingEnding failed: " + ex.ToString());
            }

            return false; // Skip original method
        }
    }

    [HarmonyPatch]
    public static class HackAnyEnemyAndroidFixPatch
    {
        public static bool Prepare()
        {
            return AccessTools.TypeByName("MOARANDROIDS.Designator_SurrogateToHack") != null;
        }

        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("MOARANDROIDS.Designator_SurrogateToHack"), "FinalizeDesignationSucceeded");
        }

        [HarmonyPrefix]
        public static bool Prefix(Designator __instance)
        {
            try
            {
                var traverse = Traverse.Create(__instance);
                Pawn target = traverse.Field("target").GetValue<Pawn>();
                int hackType = traverse.Field("hackType").GetValue<int>();

                if (target == null) return false;

                var utilsType = AccessTools.TypeByName("MOARANDROIDS.Utils");
                ThingComp csm = (ThingComp)AccessTools.Method(utilsType, "getCachedCSM").Invoke(null, new object[] { target });
                ThingComp cas = (ThingComp)AccessTools.Method(utilsType, "getCachedCAS").Invoke(null, new object[] { target });
                
                string surrogateName = target.LabelShortCap;
                ThingComp cso = null;

                if (cas != null)
                {
                    Pawn externalController = Traverse.Create(cas).Field("externalController").GetValue<Pawn>();
                    if (externalController != null)
                    {
                        surrogateName = externalController.LabelShortCap;
                        cso = (ThingComp)AccessTools.Method(utilsType, "getCachedCSO").Invoke(null, new object[] { externalController });
                    }
                }

                Lord clord = target.GetLord();
                
                var gcatppField = AccessTools.Field(utilsType, "GCATPP");
                object gcatpp = gcatppField.GetValue(null);
                int nbp = Traverse.Create(gcatpp).Method("getNbHackingPoints").GetValue<int>();
                int nbpToConsume = 0;

                var settingsType = AccessTools.TypeByName("MOARANDROIDS.Settings");
                switch (hackType)
                {
                    case 1:
                        nbpToConsume = (int)AccessTools.Field(settingsType, "costPlayerVirus").GetValue(null);
                        break;
                    case 2:
                        nbpToConsume = (int)AccessTools.Field(settingsType, "costPlayerExplosiveVirus").GetValue(null);
                        break;
                    case 3:
                        nbpToConsume = (int)AccessTools.Field(settingsType, "costPlayerHackTemp").GetValue(null);
                        break;
                    case 4:
                        nbpToConsume = (int)AccessTools.Field(settingsType, "costPlayerHack").GetValue(null);
                        break;
                }

                if (nbpToConsume > nbp)
                {
                    Messages.Message("ATPP_CannotHackNotEnoughtHackingPoints".Translate(), MessageTypeDefOf.NegativeEvent);
                    return false;
                }

                // Safely handle Goodwill without crashing on 1.4/1.5 MissingMethodException
                if (target.Faction != null && target.Faction != Faction.OfPlayer && !target.Faction.HostileTo(Faction.OfPlayer))
                {
                    try
                    {
                        var method = target.Faction.GetType().GetMethod("TryAffectGoodwillWith", new[] { typeof(Faction), typeof(int), typeof(bool), typeof(bool), typeof(HistoryEventDef), typeof(Pawn) });
                        if (method != null)
                        {
                            method.Invoke(target.Faction, new object[] { Faction.OfPlayer, -1 * Rand.Range(5, 36), true, true, null, null });
                        }
                    }
                    catch { } // Ignore if it fails
                }

                switch (hackType)
                {
                    case 1:
                    case 2:
                        if (csm != null)
                        {
                            var propHacked = AccessTools.Property(csm.GetType(), "Hacked");
                            if (propHacked != null) propHacked.SetValue(csm, hackType, null);
                            else Traverse.Create(csm).Field("hacked").SetValue(hackType);
                        }
                        
                        target.SetFactionDirect(Faction.OfAncients);
                        
                        target.mindState.Reset(true, false);
                        target.mindState.duty = null;
                        if (target.jobs != null)
                        {
                            target.jobs.StopAll();
                            target.jobs.ClearQueuedJobs();
                        }
                        target.ClearAllReservations();
                        if (target.drafter != null) target.drafter.Drafted = false;

                        IntVec3 fallbackLocation;
                        RCellFinder.TryFindRandomSpotJustOutsideColony(target.PositionHeld, target.Map, out fallbackLocation);

                        var lordJobType = AccessTools.TypeByName("RimWorld.LordJob_AssistColony");
                        if (lordJobType != null)
                        {
                            var lordJob = Activator.CreateInstance(lordJobType, new object[] { Faction.OfAncients, fallbackLocation });
                            Lord lord = LordMaker.MakeNewLord(Faction.OfAncients, (LordJob)lordJob, Current.Game.CurrentMap, null);
                            if (clord != null && clord.ownedPawns.Contains(target))
                            {
                                clord.Notify_PawnLost(target, PawnLostCondition.Incapped, null);
                            }
                            lord.AddPawn(target);
                        }

                        if (hackType == 2 && csm != null)
                        {
                            int nbSec = (int)AccessTools.Field(settingsType, "nbSecExplosiveVirusTakeToExplode").GetValue(null);
                            Traverse.Create(csm).Field("infectedExplodeGT").SetValue(Find.TickManager.TicksGame + (nbSec * 60));
                        }
                        break;
                    case 3:
                    case 4:
                        bool wasPrisonner = target.IsPrisoner;
                        Faction prevFaction = target.Faction;
                        target.SetFaction(Faction.OfPlayer);

                        // Prevent EnableAndInitialize crash for animals
                        if (target.workSettings == null && target.RaceProps.Humanlike)
                        {
                            target.workSettings = new Pawn_WorkSettings(target);
                            target.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
                        }

                        if (clord != null && clord.ownedPawns.Contains(target))
                        {
                            clord.Notify_PawnLost(target, PawnLostCondition.ChangedFaction, null);
                        }

                        if (cso != null)
                        {
                            Traverse.Create(cso).Method("disconnectControlledSurrogate", new object[] { null }).GetValue();
                        }

                        if (hackType == 4 && cas != null)
                        {
                            Traverse.Create(cas).Field("externalController").SetValue(null);
                        }

                        if (target.Map != null)
                            target.Map.attackTargetsCache.UpdateTarget(target);
                        
                        PawnComponentsUtility.AddAndRemoveDynamicComponents(target, false);
                        
                        // Clear their hostile jobs so they don't keep attacking us!
                        if (target.jobs != null)
                        {
                            target.jobs.StopAll();
                            target.jobs.ClearQueuedJobs();
                        }
                        if (target.mindState != null)
                        {
                            target.mindState.Reset(true, false);
                            target.mindState.duty = null;
                        }

                        if (hackType == 3 && csm != null)
                        {
                            var propHacked = AccessTools.Property(csm.GetType(), "Hacked");
                            if (propHacked != null) propHacked.SetValue(csm, hackType, null);
                            else Traverse.Create(csm).Field("hacked").SetValue(hackType);

                            Traverse.Create(csm).Field("hackOrigFaction").SetValue(prevFaction);
                            Traverse.Create(csm).Field("hackWasPrisoned").SetValue(wasPrisonner);
                            int tempSec = (int)AccessTools.Field(settingsType, "nbSecDurationTempHack").GetValue(null);
                            Traverse.Create(csm).Field("hackEndGT").SetValue(Find.TickManager.TicksGame + (tempSec * 60));
                        }
                        else if (hackType == 4 && csm != null)
                        {
                            int infected = Traverse.Create(csm).Field("infected").GetValue<int>();
                            if (infected != -1)
                            {
                                Traverse.Create(csm).Field("infected").SetValue(-1);
                                Traverse.Create(csm).Field("infectedExplodeGT").SetValue(-1);
                            }
                            
                            var propHacked = AccessTools.Property(csm.GetType(), "Hacked");
                            int hacked = propHacked != null ? (int)propHacked.GetValue(csm, null) : Traverse.Create(csm).Field("hacked").GetValue<int>();
                            
                            if (hacked != -1)
                            {
                                if (propHacked != null) propHacked.SetValue(csm, -1, null);
                                else Traverse.Create(csm).Field("hacked").SetValue(-1);
                                
                                Traverse.Create(csm).Field("hackEndGT").SetValue(-1);
                                Traverse.Create(csm).Field("hackWasPrisoned").SetValue(false);
                            }

                            Traverse.Create(csm).Method("disconnectUsers").GetValue();
                        }
                        break;
                }

                Traverse.Create(gcatpp).Method("decHackingPoints", new object[] { nbpToConsume }).GetValue();

                var soundType = AccessTools.TypeByName("MOARANDROIDS.SoundDefOfAT");
                if (soundType != null)
                {
                    SoundDef soundHacked = (SoundDef)AccessTools.Field(soundType, "ATPP_SoundSurrogateHacked").GetValue(null);
                    if (soundHacked != null) soundHacked.PlayOneShotOnCamera(null);

                    SoundDef soundConn = (SoundDef)AccessTools.Field(soundType, "ATPP_SoundSurrogateConnection").GetValue(null);
                    if (soundConn != null) soundConn.PlayOneShotOnCamera(null);
                }

                Messages.Message("ATPP_SurrogateHackOK".Translate(surrogateName), target, MessageTypeDefOf.PositiveEvent);

                Designator des = __instance as Designator;
                if (des != null)
                {
                    var cmapField = AccessTools.Field(des.GetType(), "cmap");
                    var posField = AccessTools.Field(des.GetType(), "pos");
                    if (cmapField != null && posField != null)
                    {
                        Map cmap = cmapField.GetValue(des) as Map;
                        IntVec3 pos = (IntVec3)posField.GetValue(des);
                        if (cmap != null && pos != null)
                        {
                            FleckMaker.ThrowDustPuffThick(pos.ToVector3Shifted(), cmap, 4.0f, Color.red);
                        }
                    }
                }

                Find.DesignatorManager.Deselect();
            }
            catch (System.Exception ex)
            {
                Log.Error("[AndroidTiersContinuedPatch] FinalizeDesignationSucceeded failed safely: " + ex.ToString());
            }

            return false; // Skip original execution
        }
    }
}
