using Verse;

namespace InfiniteTurrets
{
    public class InfiniteTurretsSettings : ModSettings
    {
        public bool infiniteDurability = true;
        public float durabilityMultiplier = 1.0f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref infiniteDurability, "infiniteDurability", true);
            Scribe_Values.Look(ref durabilityMultiplier, "durabilityMultiplier", 1.0f);
            base.ExposeData();
        }
    }
}
