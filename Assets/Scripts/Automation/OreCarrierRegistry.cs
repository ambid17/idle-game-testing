using System.Collections.Generic;

namespace Automation
{
    // Tracks every IOreCarrier (player + mining automatons) so Storage Drones can find targets,
    // and lets a drone claim a carrier so two drones never converge on the same one at once.
    // Pure-logic singleton, not GameManager-registered, matching Wallet/Depot/UpgradeManager.
    public class OreCarrierRegistry : Singleton<OreCarrierRegistry>
    {
        private readonly List<IOreCarrier> carriers = new();
        private readonly Dictionary<IOreCarrier, StorageDrone> claimsByCarrier = new();

        public IReadOnlyList<IOreCarrier> Carriers => carriers;

        public void Register(IOreCarrier carrier)
        {
            if (carrier == null || carriers.Contains(carrier)) return;
            carriers.Add(carrier);
        }

        public void Unregister(IOreCarrier carrier)
        {
            if (carrier == null) return;
            carriers.Remove(carrier);
            claimsByCarrier.Remove(carrier);
        }

        public bool IsClaimed(IOreCarrier carrier) => carrier != null && claimsByCarrier.ContainsKey(carrier);

        // False if already claimed by a different drone; true if claimed successfully or already
        // held by this same drone.
        public bool TryClaim(StorageDrone drone, IOreCarrier carrier)
        {
            if (drone == null || carrier == null) return false;
            if (claimsByCarrier.TryGetValue(carrier, out var existing) && existing != drone) return false;

            claimsByCarrier[carrier] = drone;
            return true;
        }

        public void ReleaseClaim(StorageDrone drone)
        {
            if (drone == null) return;
            foreach (var key in new List<IOreCarrier>(claimsByCarrier.Keys))
            {
                if (claimsByCarrier[key] == drone) claimsByCarrier.Remove(key);
            }
        }
    }
}
