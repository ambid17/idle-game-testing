using UnityEngine;

namespace Automation
{
    // Anything a Fuel Drone can refuel - implemented by the player (Player.PlayerController) and
    // MiningAutomaton. Registered with FuelConsumerRegistry so drones can discover/claim targets
    // without each entity type knowing about the others directly. Mirrors IOreCarrier's role for
    // Storage Drones.
    public interface IFuelConsumer
    {
        Transform FuelTransform { get; }
        float FuelMissing { get; }
        float FuelMax { get; }
        void AddFuel(float amount);
    }
}
