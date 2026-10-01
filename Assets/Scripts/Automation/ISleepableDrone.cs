namespace Automation
{
    // Implemented by drones that can run out of work (StorageDrone, FuelDrone). DroneSleepVisual
    // reads IsIdle to play the sleep animation.
    public interface ISleepableDrone
    {
        // True only once the drone has no target AND has come to rest at its idle anchor -
        // a drone still flying home isn't "asleep" yet.
        bool IsIdle { get; }
    }
}
