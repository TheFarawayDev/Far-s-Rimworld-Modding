using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace QuarryCo
{
    public class JobDriver_Quarry : JobDriver
    {
        private const int BaseWorkTicks = 3000;
        private const float MiningXpPerTick = 0.11f;

        private Building_Quarry Quarry { get { return (Building_Quarry)TargetThingA; } }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => !Quarry.IsActive);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil mine = ToilMaker.MakeToil("MakeNewToils");
            mine.tickAction = delegate
            {
                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Mining, MiningXpPerTick);
                }
            };
            mine.defaultCompleteMode = ToilCompleteMode.Delay;
            mine.defaultDuration = BaseWorkTicks;
            mine.WithProgressBarToilDelay(TargetIndex.A);
            mine.activeSkill = () => SkillDefOf.Mining;
            yield return mine;

            Toil finish = ToilMaker.MakeToil("MakeNewToils");
            finish.initAction = delegate
            {
                Quarry.SpawnRandomResource(pawn);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
