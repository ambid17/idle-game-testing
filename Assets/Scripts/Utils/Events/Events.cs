using System.Collections.Generic;
using Automation;
using Economy;
using Interaction;
using MapGeneration;
using Player;
using Processing;
using Tutorial;
using UnityEngine;

namespace Events
{
    public class CurrencyUpdatedEvent { }

    public class PlayerDiedEvent : IEvent
    {
        public DeathReason Reason;

        public PlayerDiedEvent(DeathReason reason)
        {
            Reason = reason;
        }
    }

    public class PlayerRevivedEvent { }
    public class PlayerHpUpdatedEvent { }

    // Dispatched by Player.PlayerDeathEffect once its explosion beat finishes playing - DeathUI
    // listens for this (not PlayerDiedEvent) to decide when to actually reveal the death screen,
    // so the reveal happens after the explosion instead of instantly when HP/fuel hits zero.
    public class PlayerDeathMenuRequestedEvent { }

    // Dispatched by PlayerHealth.TakeDamage whenever a hit actually reduces CurrentHp (not when a
    // shield charge absorbs it instead - that already has its own HudNotificationEvent). Drives
    // UI.DamageScreenEffectUI's full-screen flash.
    public class PlayerDamagedEvent : IEvent
    {
        public float Amount;

        public PlayerDamagedEvent(float amount)
        {
            Amount = amount;
        }
    }

    // Dispatched by PlayerHealth whenever a shield charge (PrestigeUpgradeEffect.ShieldChargeCount)
    // is consumed or regenerated.
    public class ShieldChargeChangedEvent : IEvent
    {
        public int Current;
        public int Max;

        public ShieldChargeChangedEvent(int current, int max)
        {
            Current = current;
            Max = max;
        }
    }

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

    // Unified notification system (UI.Notifications.NotificationManager): every UI notification -
    // toast, warning, deposit report - dispatches one of these rather than driving its own popup.
    // Urgency picks the queue/screen position (TimeSensitive: top-middle, holds then fades, e.g.
    // low fuel or a full inventory; Queued: bottom-right, rises while fading, e.g. an ore-mined or
    // automaton-deposit report). The two queues are independent so a Queued backlog can never delay
    // a TimeSensitive warning. Icon is optional - null picks the text-only display prefab, a
    // non-null Sprite picks the icon+text one.
    public enum NotificationUrgency { TimeSensitive, Queued }

    public class NotificationEvent : IEvent
    {
        public string Message;
        public NotificationUrgency Urgency;
        public Sprite Icon;

        public NotificationEvent(string message, NotificationUrgency urgency, Sprite icon = null)
        {
            Message = message;
            Urgency = urgency;
            Icon = icon;
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
        public InteractableType InteractableType;
        public InteractionType InteractionType;

        public PlayerInteractedEvent(InteractableType interactableType, InteractionType interactionType)
        {
            InteractableType = interactableType;
            InteractionType = interactionType;
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

    // Fired whenever ProcessingManager.UncollectedCompletions changes - a completion badge above
    // the Processing Center listens for this rather than polling. Count is 0 right after the
    // player opens the Processing panel (ProcessingUI.Open clears it).
    public class ProcessingCompletionCountChangedEvent : IEvent
    {
        public int Count;

        public ProcessingCompletionCountChangedEvent(int count)
        {
            Count = count;
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

    public class CustomBlockTriggeredEvent : IEvent
    {
        public int LayerIndex;
        public int X;
        public int Y;
        public CustomBehavior Hazard;

        public CustomBlockTriggeredEvent(int layerIndex, int x, int y, CustomBehavior hazard)
        {
            LayerIndex = layerIndex;
            X = x;
            Y = y;
            Hazard = hazard;
        }
    }

    // Dispatched by MapGenerationService.MineCell alongside HazardTriggeredEvent, but for a mined
    // cell whose Category is PowerUp rather than Hazard - reuses the same HazardBehavior enum
    // (TreasureChest/SightPotion), which already anticipates this dual use (see BlockType.cs).
    public class PowerUpTriggeredEvent : IEvent
    {
        public int LayerIndex;
        public int X;
        public int Y;
        public CustomBehavior Behavior;

        public PowerUpTriggeredEvent(int layerIndex, int x, int y, CustomBehavior behavior)
        {
            LayerIndex = layerIndex;
            X = x;
            Y = y;
            Behavior = behavior;
        }
    }

    // Dispatched by MapGeneration.FallingRockHazardEffect after its jiggle warning: once per cell
    // it passes through while falling (small Radius - "touches the player mid-fall") and once
    // more, with a wider Radius, when it lands. Player.HazardDamageHandler listens for this (not
    // HazardTriggeredEvent) so the damage window lands at contact/impact, not at the moment the
    // rock's support was mined.
    public class FallingRockImpactEvent : IEvent
    {
        public int LayerIndex;
        public int X;
        public int Y;
        public float Radius;

        public FallingRockImpactEvent(int layerIndex, int x, int y, float radius)
        {
            LayerIndex = layerIndex;
            X = x;
            Y = y;
            Radius = radius;
        }
    }

    // Dispatched by MapGeneration.ExplosiveHazardEffect once its jiggle/flash telegraph finishes -
    // the actual moment the blast radius gets destroyed and the player takes damage, not the
    // instant the block was mined (see HazardTriggeredEvent). Player.HazardDamageHandler and
    // HazardEffectResolver both listen for this instead of HazardTriggeredEvent for the Explosive
    // case specifically, so the delay is felt by both damage and destruction together.
    public class ExplosiveDetonatedEvent : IEvent
    {
        public int LayerIndex;
        public int X;
        public int Y;

        public ExplosiveDetonatedEvent(int layerIndex, int x, int y)
        {
            LayerIndex = layerIndex;
            X = x;
            Y = y;
        }
    }

    // Dispatched repeatedly by MapGeneration.GasCloudHazardEffect on a tick interval while
    // the player overlaps its current radius - lets the cloud be a lingering damage-over-time
    // effect rather than a single hit at trigger time. Per-tick damage amount is Player.
    // HazardDamageHandler's own concern (mirrors how it already owns explosiveDamage/
    // fallingRockDamage/lavaDamage), not something the effect dictates.
    public class GasCloudDamageTickEvent : IEvent
    {
        public int LayerIndex;
        public int X;
        public int Y;
        public float Radius;

        public GasCloudDamageTickEvent(int layerIndex, int x, int y, float radius)
        {
            LayerIndex = layerIndex;
            X = x;
            Y = y;
            Radius = radius;
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
