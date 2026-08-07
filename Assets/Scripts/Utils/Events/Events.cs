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
}
