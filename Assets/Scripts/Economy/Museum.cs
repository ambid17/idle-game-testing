using Events;
using Player;
using UnityEngine;

namespace Economy
{
    // Museum per GameDesignDoc "Map Layout > buildings > museum" / "# Prestige": turning artifacts
    // in credits Prestige points, spent on the permanent perk tree. Mirrors Depot.Sell's shape
    // (read a multiplier off the relevant upgrade manager, credit currency, dispatch a changed
    // event) - business logic lives here, MuseumUI just wires the button/event like DepotUI does
    // for Depot.Sell.
    public class Museum : Singleton<Museum>
    {
        public double TurnInArtifacts(PlayerInventory playerInventory)
        {
            if (playerInventory == null)
            {
                Debug.LogError("Museum.TurnInArtifacts: playerInventory is null.");
                return 0;
            }

            int count = playerInventory.WithdrawAllArtifacts();
            if (count <= 0) return 0;

            double pointsEarned = count * PrestigeUpgradeManager.Instance.PrestigePointsPerArtifactMultiplier;
            PrestigePoints.Instance.Add(pointsEarned);
            GameManager.EventService.Dispatch(new ArtifactsTurnedInEvent(count, pointsEarned));
            return pointsEarned;
        }
    }
}
