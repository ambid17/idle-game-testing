using Economy;
using UnityEngine;

namespace Automation
{
    // Anything a Storage Drone can drain ore from - implemented by the player (via PlayerInventory)
    // and MiningAutomaton. Registered with OreCarrierRegistry so drones can discover/claim targets
    // without each entity type knowing about the others directly.
    public interface IOreCarrier
    {
        Transform CarrierTransform { get; }
        OreInventory Inventory { get; }
    }
}
