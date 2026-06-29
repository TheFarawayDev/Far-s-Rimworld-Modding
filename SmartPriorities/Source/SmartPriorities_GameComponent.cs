using System.Collections.Generic;
using Verse;

namespace SmartPriorities
{
    public class SmartPriorities_GameComponent : GameComponent
    {
        public Dictionary<Pawn, string> assignedLearningJobs = new Dictionary<Pawn, string>();

        public SmartPriorities_GameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref assignedLearningJobs, "assignedLearningJobs", LookMode.Reference, LookMode.Value, ref pawnKeys, ref jobValues);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (assignedLearningJobs == null)
                {
                    assignedLearningJobs = new Dictionary<Pawn, string>();
                }
                
                // Clean up dead or despawned pawns to prevent save bloat
                List<Pawn> toRemove = new List<Pawn>();
                foreach (var kvp in assignedLearningJobs)
                {
                    if (kvp.Key == null || kvp.Key.Dead || kvp.Key.Destroyed)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
                foreach (Pawn p in toRemove)
                {
                    assignedLearningJobs.Remove(p);
                }
            }
        }

        private List<Pawn> pawnKeys = new List<Pawn>();
        private List<string> jobValues = new List<string>();
    }
}
