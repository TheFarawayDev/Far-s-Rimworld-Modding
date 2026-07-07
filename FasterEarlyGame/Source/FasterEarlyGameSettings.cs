using UnityEngine;
using Verse;

namespace FasterEarlyGame
{
    public class FasterEarlyGameSettings : ModSettings
    {
        public float researchSpeedMultiplier = 2f;
        public float miningSpeedMultiplier = 1.5f;
        public float constructionSpeedMultiplier = 1.5f;
        public float plantWorkSpeedMultiplier = 1.5f;
        public float smoothingSpeedMultiplier = 1.5f;
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref researchSpeedMultiplier, "researchSpeedMultiplier", 2f);
            Scribe_Values.Look(ref miningSpeedMultiplier, "miningSpeedMultiplier", 1.5f);
            Scribe_Values.Look(ref constructionSpeedMultiplier, "constructionSpeedMultiplier", 1.5f);
            Scribe_Values.Look(ref plantWorkSpeedMultiplier, "plantWorkSpeedMultiplier", 1.5f);
            Scribe_Values.Look(ref smoothingSpeedMultiplier, "smoothingSpeedMultiplier", 1.5f);
        }
    }
}
