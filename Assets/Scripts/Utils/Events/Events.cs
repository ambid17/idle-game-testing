using System.Collections.Generic;
using Automation;
using Economy;
using Interaction;
using MapGeneration;
using Processing;
using Tutorial;
using UnityEngine;

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

    public class DollarsChangedEvent { }

    public class ArtifactCountChangedEvent { }

    public class DepotChangedEvent { }

    public class InventoryChangedEvent { }

    // Generic HUD toast - drives UI.HudToastUI's popup. Dispatch this wherever a brief,
    // non-blocking message should surface to the player (e.g. PlayerMining when a dig is blocked
    // by a full inventory, PlayerController when fuel drops below half).
    public class HudNotificationEvent : IEvent
    {
        public string Message;

        public HudNotificationEvent(string message)
        {
            Message = message;
        }
    }

    // Dispatched by PlayerInventory.HandleDeath with the ore that was just withdrawn on death, so
    // Economy.ChestSpawner can drop it into a chest instead of it just being discarded.
    public class PlayerInventoryDroppedEvent : IEvent
    {
        public IReadOnlyDictionary<BlockTypeId, int> OreCounts;

        public PlayerInventoryDroppedEvent(IReadOnlyDictionary<BlockTypeId, int> oreCounts)
        {
            OreCounts = oreCounts;
        }
    }

    public class UICloseEvent { }

    // Dispatched by PlayerController on Escape when UI.ModalTracker.IsAnyModalOpen - closes just
    // the open modal (nested inside a panel, or a standalone tutorial popup) without touching
    // the panel underneath it. See UICloseEvent for the panel-level version.
    public class ModalCloseRequestedEvent { }

    public class PlayerInteractedEvent : IEvent
    {
        public InteractableType Type;

        public PlayerInteractedEvent(InteractableType type)
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

    public class UpgradeLoadedEvent : IEvent
    {
        public UpgradeDefinition Definition;
        public int NewLevel;

        public UpgradeLoadedEvent(UpgradeDefinition definition, int newLevel)
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

    // Processing Center (Assets/Docs/processingImplementation.md) events below.

    public class ProcessingStartRequestedEvent : IEvent
    {
        public int SlotIndex;
        public ProcessingRecipeDefinition Recipe;
        public int Quantity;

        public ProcessingStartRequestedEvent(int slotIndex, ProcessingRecipeDefinition recipe, int quantity)
        {
            SlotIndex = slotIndex;
            Recipe = recipe;
            Quantity = quantity;
        }
    }

    public class ProcessingCancelRequestedEvent : IEvent
    {
        public int SlotIndex;

        public ProcessingCancelRequestedEvent(int slotIndex)
        {
            SlotIndex = slotIndex;
        }
    }

    public class ProcessingJobStartedEvent : IEvent
    {
        public int SlotIndex;
        public ProcessingRecipeDefinition Recipe;
        public int Quantity;

        public ProcessingJobStartedEvent(int slotIndex, ProcessingRecipeDefinition recipe, int quantity)
        {
            SlotIndex = slotIndex;
            Recipe = recipe;
            Quantity = quantity;
        }
    }

    public class ProcessingJobCompletedEvent : IEvent
    {
        public int SlotIndex;
        public ProcessingRecipeDefinition Recipe;
        public int Quantity;

        public ProcessingJobCompletedEvent(int slotIndex, ProcessingRecipeDefinition recipe, int quantity)
        {
            SlotIndex = slotIndex;
            Recipe = recipe;
            Quantity = quantity;
        }
    }

    public class ProcessingJobCancelledEvent : IEvent
    {
        public int SlotIndex;

        public ProcessingJobCancelledEvent(int slotIndex)
        {
            SlotIndex = slotIndex;
        }
    }

    public class SellGoodsRequestedEvent : IEvent
    {
        public ProcessingRecipeId Id;
        public float Fraction;

        public SellGoodsRequestedEvent(ProcessingRecipeId id, float fraction)
        {
            Id = id;
            Fraction = fraction;
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

    // Dispatched by MiningAutomaton/StorageDrone (via AutomationDepositService) whenever they
    // deposit ore at the Depot - drives notification toasts and the Control Center's Miner
    // Dashboard ore/min table.
    public class OreDepositedByAutomationEvent : IEvent
    {
        public string EntityDisplayName;
        public IReadOnlyDictionary<BlockTypeId, int> Deposited;

        public OreDepositedByAutomationEvent(string entityDisplayName, IReadOnlyDictionary<BlockTypeId, int> deposited)
        {
            EntityDisplayName = entityDisplayName;
            Deposited = deposited;
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

    // Dispatched by PlayerController on Escape, but only when nothing else was already blocking
    // input - see PlayerController.Update. PauseMenuUI is the sole listener.
    public class PauseMenuOpenRequestedEvent { }

    // Tutorial popup system (Tutorial.TutorialManager). WorldPosition is null for a screen-space
    // overlay tutorial (UI.Panels.TutorialModalUI) or set for a world-anchored one
    // (UI.Panels.WorldTutorialPopupUI) - each display component ignores events that aren't theirs.
    public class ShowTutorialEvent : IEvent
    {
        public TutorialEntry Entry;
        public Vector3? WorldPosition;

        public ShowTutorialEvent(TutorialEntry entry, Vector3? worldPosition = null)
        {
            Entry = entry;
            WorldPosition = worldPosition;
        }
    }

    public class LoadCompletedEvent { }

    // Dispatched by PlayerController on backquote, only in the Editor or a Development Build (see
    // UI.DevPanelUI). Mirrors PauseMenuOpenRequestedEvent's shape.
    public class DevPanelOpenRequestedEvent { }
}
