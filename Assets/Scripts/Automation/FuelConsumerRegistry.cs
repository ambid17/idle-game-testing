using System.Collections.Generic;

namespace Automation
{
    // Tracks every IFuelConsumer (player + mining automatons) so Fuel Drones can find targets,
    // and lets a drone claim a consumer so two drones never converge on the same one at once.
    // Pure-logic singleton, not GameManager-registered - mirrors OreCarrierRegistry exactly.
    public class FuelConsumerRegistry : Singleton<FuelConsumerRegistry>
    {
        private readonly List<IFuelConsumer> consumers = new();
        private readonly Dictionary<IFuelConsumer, FuelDrone> claimsByConsumer = new();

        public IReadOnlyList<IFuelConsumer> Consumers => consumers;

        public void Register(IFuelConsumer consumer)
        {
            if (consumer == null || consumers.Contains(consumer)) return;
            consumers.Add(consumer);
        }

        public void Unregister(IFuelConsumer consumer)
        {
            if (consumer == null) return;
            consumers.Remove(consumer);
            claimsByConsumer.Remove(consumer);
        }

        public bool IsClaimed(IFuelConsumer consumer) => consumer != null && claimsByConsumer.ContainsKey(consumer);

        // False if already claimed by a different drone; true if claimed successfully or already
        // held by this same drone.
        public bool TryClaim(FuelDrone drone, IFuelConsumer consumer)
        {
            if (drone == null || consumer == null) return false;
            if (claimsByConsumer.TryGetValue(consumer, out var existing) && existing != drone) return false;

            claimsByConsumer[consumer] = drone;
            return true;
        }

        public void ReleaseClaim(FuelDrone drone)
        {
            if (drone == null) return;
            foreach (var key in new List<IFuelConsumer>(claimsByConsumer.Keys))
            {
                if (claimsByConsumer[key] == drone) claimsByConsumer.Remove(key);
            }
        }
    }
}
