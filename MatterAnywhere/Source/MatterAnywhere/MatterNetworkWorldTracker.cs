using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace TheFarawayDev.MatterAnywhere
{
    public class MatterNetworkWorldTracker : WorldComponent
    {
        public Dictionary<string, int> globalResourceLedger = new Dictionary<string, int>();

        public static MatterNetworkWorldTracker Instance => Find.World?.GetComponent<MatterNetworkWorldTracker>();

        public MatterNetworkWorldTracker(World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref globalResourceLedger, "globalResourceLedger", LookMode.Value, LookMode.Value);
            if (globalResourceLedger == null)
            {
                globalResourceLedger = new Dictionary<string, int>();
            }
        }
    }
}
