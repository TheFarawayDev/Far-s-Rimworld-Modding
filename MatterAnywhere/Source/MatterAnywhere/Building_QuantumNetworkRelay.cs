using SK_Matter_Network;
using Verse;

namespace TheFarawayDev.MatterAnywhere
{
    public class Building_QuantumNetworkRelay : NetworkBuilding
    {
        // Inherits all network connection behavior from SK_Matter_Network.NetworkBuilding
    }

    public class CompProperties_NetworkRelay : CompProperties
    {
        public CompProperties_NetworkRelay()
        {
            this.compClass = typeof(CompNetworkRelay);
        }
    }
}
