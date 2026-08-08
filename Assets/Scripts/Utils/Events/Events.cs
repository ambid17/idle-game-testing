using System.Collections.Generic;
using Automation;
using Economy;
using Interaction;
using MapGeneration;

namespace Events
{
    public class CurrencyUpdatedEvent { }
    public class PlayerDiedEvent { }
    public class PlayerRevivedEvent { }
    public class PlayerHpUpdatedEvent { }

    public class DidCraftUpgradeEvent { }

    public class ClosedCraftingUiEvent { }


    public class CurrencyRewardEvent: IEvent
    {
        public float MinutesAway;
        public float Award;

        public CurrencyRewardEvent(float minutesAway, float award)
        {
            MinutesAway = minutesAway;
            Award = award;
        }
    }

    public class SceneIsReadyEvent : IEvent
    {
        public SceneIsReadyEvent()
        {
        }
    }

    public class DollarsChangedEvent { }

    public class DepotChangedEvent { }

    public class InventoryChangedEvent { }

    public class InventoryOpenedEvent { }

    public class BuildingInteractedEvent : IEvent
    {
        public InteractableType Type;

        public BuildingInteractedEvent(InteractableType type)
        {
            Type = type;
        }
    }

    public class UpgradePurchasedEvent : IEvent
    {
        public UpgradeDefinition Definition;
        public int NewLevel;

        public UpgradePurchasedEvent(UpgradeDefinition definition, int newLevel)
        {
            Definition = definition;
            NewLevel = newLevel;
        }
    }

    public class PurchaseRequestedEvent : IEvent
    {
        public UpgradeDefinition Definition;

        public PurchaseRequestedEvent(UpgradeDefinition definition)
        {
            Definition = definition;
        }
    }

    public class SellRequestedEvent : IEvent
    {
        public BlockTypeId Id;
        public float Fraction;

        public SellRequestedEvent(BlockTypeId id, float fraction)
        {
            Id = id;
            Fraction = fraction;
        }
    }

    public class CellMinedEvent : IEvent
    {
        public int LayerIndex;
        public int X;
        public int Y;
        public BlockType Block;
        public bool ArtifactFound;

        public CellMinedEvent(int layerIndex, int x, int y, BlockType block, bool artifactFound)
        {
            LayerIndex = layerIndex;
            X = x;
            Y = y;
            Block = block;
            ArtifactFound = artifactFound;
        }
    }

    public class HazardTriggeredEvent : IEvent
    {
        public int LayerIndex;
        public int X;
        public int Y;
        public HazardBehavior Hazard;

        public HazardTriggeredEvent(int layerIndex, int x, int y, HazardBehavior hazard)
        {
            LayerIndex = layerIndex;
            X = x;
            Y = y;
            Hazard = hazard;
        }
    }

    // Automation (automationImplementation.md) events below.

    public class SetStorageDroneTargetModeRequestedEvent : IEvent
    {
        public TargetMode Mode;
        public SetStorageDroneTargetModeRequestedEvent(TargetMode mode) => Mode = mode;
    }

    public class SetFuelDroneTargetModeRequestedEvent : IEvent
    {
        public TargetMode Mode;
        public SetFuelDroneTargetModeRequestedEvent(TargetMode mode) => Mode = mode;
    }

    public class SetFuelSpendingCapRequestedEvent : IEvent
    {
        public float Percent;
        public SetFuelSpendingCapRequestedEvent(float percent) => Percent = percent;
    }

    public class AutomationSettingsChangedEvent { }

    // Dispatched by MiningAutomaton/StorageDrone whenever they deposit ore at the Depot - drives
    // notification toasts and the Control Center's per-automaton earnings graph.
    public class OreDepositedByAutomationEvent : IEvent
    {
        public string EntityDisplayName;
        public IReadOnlyDictionary<BlockTypeId, int> Deposited;
        // Index of the depositing automaton for the earnings graph, or -1 for storage drones
        // (which aren't graphed - only automaton output counts per the design doc's idle-earnings
        // scope).
        public int AutomatonIndex;

        public OreDepositedByAutomationEvent(string entityDisplayName, IReadOnlyDictionary<BlockTypeId, int> deposited, int automatonIndex = -1)
        {
            EntityDisplayName = entityDisplayName;
            Deposited = deposited;
            AutomatonIndex = automatonIndex;
        }
    }

    public class OfflineEarningsReadyEvent : IEvent
    {
        public IReadOnlyDictionary<BlockTypeId, int> OreGained;
        public float MinutesAway;

        public OfflineEarningsReadyEvent(IReadOnlyDictionary<BlockTypeId, int> oreGained, float minutesAway)
        {
            OreGained = oreGained;
            MinutesAway = minutesAway;
        }
    }

    public class OfflineEarningsAcknowledgedEvent { }

    // Prestige (GameDesignDoc "# Prestige") events below.

    public class PrestigePointsChangedEvent { }

    public class ArtifactsTurnedInEvent : IEvent
    {
        public int Count;
        public double PointsEarned;

        public ArtifactsTurnedInEvent(int count, double pointsEarned)
        {
            Count = count;
            PointsEarned = pointsEarned;
        }
    }

    public class TurnInArtifactsRequestedEvent { }

    public class PrestigeUpgradePurchasedEvent : IEvent
    {
        public PrestigeUpgradeDefinition Definition;
        public int NewLevel;

        public PrestigeUpgradePurchasedEvent(PrestigeUpgradeDefinition definition, int newLevel)
        {
            Definition = definition;
            NewLevel = newLevel;
        }
    }

    public class PrestigePurchaseRequestedEvent : IEvent
    {
        public PrestigeUpgradeDefinition Definition;

        public PrestigePurchaseRequestedEvent(PrestigeUpgradeDefinition definition)
        {
            Definition = definition;
        }
    }

    public class PrestigeConfirmationRequestedEvent { }

    public class PrestigeCompletedEvent : IEvent
    {
        public int NewSeed;

        public PrestigeCompletedEvent(int newSeed)
        {
            NewSeed = newSeed;
        }
    }
}
