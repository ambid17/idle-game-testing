using System.Collections.Generic;
using Critters;
using Economy;
using Events;
using Museum;
using RunModifiers;
using TMPro;
using UI.Reuseable;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Dev Panel tab: skip Market/Museum progression. Maxing Automation_AutomatonCount/
    // StorageDroneCount/FuelDroneCount here also spawns the corresponding units for free, since
    // AutomationSpawner already reconciles entity counts off UpgradeLoadedEvent - no separate
    // "spawn" cheat is needed.
    public class DevPanelProgressionTab : MonoBehaviour
    {
        [SerializeField] private Transform upgradeRowContainer;
        [SerializeField] private DevProgressionUpgradeRowUI upgradeRowPrefab;
        [SerializeField] private Transform prestigeUpgradeRowContainer;
        [SerializeField] private DevProgressionUpgradeRowUI prestigeUpgradeRowPrefab;
        [SerializeField] private Button maxAllUpgradesButton;
        [SerializeField] private Button removeAllUpgradesButton;
        [SerializeField] private Button maxAllPrestigeUpgradesButton;
        [SerializeField] private Button removeAllPrestigeUpgradesButton;
        [SerializeField] private Button forcePrestigeButton;
        [SerializeField] private Button turnInAllCrittersButton;
        [SerializeField] private Button findAllRunesButton;
        // One button per run modifier: prestiges straight into a run with that modifier.
        [SerializeField] private Transform runModifierButtonContainer;
        [SerializeField] private Button runModifierButtonTemplate;
        // Shared description tooltip for those buttons - lives outside the scroll mask so it isn't clipped.
        [SerializeField] private RectTransform runModifierTooltip;
        [SerializeField] private TMP_Text runModifierTooltipText;

        private readonly List<DevProgressionUpgradeRowUI> upgradeRows = new();
        private readonly List<DevProgressionUpgradeRowUI> prestigeUpgradeRows = new();

        private void Start()
        {
            if (upgradeRowContainer == null) Debug.LogError("DevPanelProgressionTab.upgradeRowContainer is not assigned.");
            if (upgradeRowPrefab == null) Debug.LogError("DevPanelProgressionTab.upgradeRowPrefab is not assigned.");
            if (prestigeUpgradeRowContainer == null) Debug.LogError("DevPanelProgressionTab.prestigeUpgradeRowContainer is not assigned.");
            if (prestigeUpgradeRowPrefab == null) Debug.LogError("DevPanelProgressionTab.prestigeUpgradeRowPrefab is not assigned.");
            if (maxAllUpgradesButton == null) Debug.LogError("DevPanelProgressionTab.maxAllUpgradesButton is not assigned.");
            if (removeAllUpgradesButton == null) Debug.LogError("DevPanelProgressionTab.removeAllUpgradesButton is not assigned.");
            if (maxAllPrestigeUpgradesButton == null) Debug.LogError("DevPanelProgressionTab.maxAllPrestigeUpgradesButton is not assigned.");
            if (removeAllPrestigeUpgradesButton == null) Debug.LogError("DevPanelProgressionTab.removeAllPrestigeUpgradesButton is not assigned.");
            if (forcePrestigeButton == null) Debug.LogError("DevPanelProgressionTab.forcePrestigeButton is not assigned.");
            if (turnInAllCrittersButton == null) Debug.LogError("DevPanelProgressionTab.turnInAllCrittersButton is not assigned.");
            if (findAllRunesButton == null) Debug.LogError("DevPanelProgressionTab.findAllRunesButton is not assigned.");
            if (runModifierButtonContainer == null) Debug.LogError("DevPanelProgressionTab.runModifierButtonContainer is not assigned.");
            if (runModifierButtonTemplate == null) Debug.LogError("DevPanelProgressionTab.runModifierButtonTemplate is not assigned.");
            if (runModifierTooltip == null) Debug.LogError("DevPanelProgressionTab.runModifierTooltip is not assigned.");
            if (runModifierTooltipText == null) Debug.LogError("DevPanelProgressionTab.runModifierTooltipText is not assigned.");

            BuildUpgradeRows();
            BuildPrestigeUpgradeRows();
            BuildRunModifierButtons();

            if (maxAllUpgradesButton != null) maxAllUpgradesButton.onClick.AddListener(OnMaxAllUpgradesClicked);
            if (removeAllUpgradesButton != null) removeAllUpgradesButton.onClick.AddListener(OnRemoveAllUpgradesClicked);
            if (maxAllPrestigeUpgradesButton != null) maxAllPrestigeUpgradesButton.onClick.AddListener(OnMaxAllPrestigeUpgradesClicked);
            if (removeAllPrestigeUpgradesButton != null) removeAllPrestigeUpgradesButton.onClick.AddListener(OnRemoveAllPrestigeUpgradesClicked);
            if (forcePrestigeButton != null) forcePrestigeButton.onClick.AddListener(ForcePrestige);
            if (turnInAllCrittersButton != null) turnInAllCrittersButton.onClick.AddListener(TurnInAllCritters);
            if (findAllRunesButton != null) findAllRunesButton.onClick.AddListener(FindAllRunes);
        }

        private void BuildUpgradeRows()
        {
            if (upgradeRowContainer == null || upgradeRowPrefab == null) return;

            foreach (var def in GameManager.UpgradeDatabase.Upgrades)
            {
                if (def == null) continue;
                var row = Instantiate(upgradeRowPrefab, upgradeRowContainer);
                row.Bind(def.DisplayName,
                    () => UpgradeManager.Instance.GetPurchasedLevel(def),
                    () => UpgradeManager.Instance.SetLevelFromSave(def.DisplayName, def.MaxLevel),
                    () => OnIncrementUpgradeClicked(def),
                    () => OnDecrementUpgradeClicked(def));
                upgradeRows.Add(row);
            }
        }

        private void BuildPrestigeUpgradeRows()
        {
            if (prestigeUpgradeRowContainer == null || prestigeUpgradeRowPrefab == null) return;

            foreach (var def in GameManager.PrestigeUpgradeDatabase.Upgrades)
            {
                if (def == null) continue;
                var row = Instantiate(prestigeUpgradeRowPrefab, prestigeUpgradeRowContainer);
                row.Bind(def.DisplayName,
                    () => PrestigeUpgradeManager.Instance.GetPurchasedLevel(def),
                    () => PrestigeUpgradeManager.Instance.SetLevel(def.DisplayName, def.MaxLevel),
                    () => OnIncrementPrestigeUpgradeClicked(def),
                    () => OnDecrementPrestigeUpgradeClicked(def));
                prestigeUpgradeRows.Add(row);
            }
        }

        private void BuildRunModifierButtons()
        {
            if (runModifierButtonContainer == null || runModifierButtonTemplate == null) return;

            runModifierButtonTemplate.gameObject.SetActive(false);
            if (runModifierTooltip != null) runModifierTooltip.gameObject.SetActive(false);
            foreach (var def in GameManager.RunModifierDatabase.Modifiers)
            {
                if (def == null) continue;
                var button = Instantiate(runModifierButtonTemplate, runModifierButtonContainer);
                button.gameObject.name = $"RunModifierButton_{def.Id}";
                button.GetComponentInChildren<TMP_Text>().text = $"{(def.Kind == RunModifierKind.Blessing ? "<color=#7CFC00>B</color>" : "<color=#FF6060>G</color>")} {def.DisplayName}";
                button.onClick.AddListener(() => PrestigeWithModifier(def));
                var buttonRect = (RectTransform)button.transform;
                button.gameObject.AddComponent<HoverCallbackTrigger>().Bind(hovered => OnRunModifierHoverChanged(def, buttonRect, hovered));
                button.gameObject.SetActive(true);
            }
        }

        // Rolls the same parameters a click would right now, so {oreA}/{layer}/{amount} show real values.
        private void OnRunModifierHoverChanged(RunModifierDefinition def, RectTransform buttonRect, bool hovered)
        {
            if (runModifierTooltip == null || runModifierTooltipText == null) return;

            runModifierTooltip.gameObject.SetActive(hovered);
            if (!hovered) return;

            var state = RunModifierOfferRoller.RollFor(def, GameManager.LayerConfigProvider, PrestigeManager.Instance.NextSeed);
            runModifierTooltipText.text = $"<b>{def.DisplayName}</b> ({def.Kind})\n{GameManager.RunModifierService.Describe(def, state)}";

            // Tooltip pivot is bottom-center, so this sits it just above the hovered button.
            var corners = new Vector3[4];
            buttonRect.GetWorldCorners(corners);
            runModifierTooltip.position = (corners[1] + corners[2]) * 0.5f;
            runModifierTooltip.SetAsLastSibling();
        }

        // Skips the pick-1-of-3 screen and forces the chosen modifier, with parameters (ores,
        // target layer, contract amount) rolled from the next seed like a real offer would be.
        private void PrestigeWithModifier(RunModifierDefinition def)
        {
            var prestige = PrestigeManager.Instance;
            var state = RunModifierOfferRoller.RollFor(def, GameManager.LayerConfigProvider, prestige.NextSeed);
            prestige.ExecutePrestige(state);
            GameManager.EventService.Dispatch(new NotificationEvent($"Dev prestige: {def.DisplayName} - {GameManager.RunModifierService.Describe(def, state)}", NotificationUrgency.Queued));
        }

        private void OnIncrementUpgradeClicked(UpgradeDefinition def)
        {
            int newLevel = Mathf.Min(def.MaxLevel, UpgradeManager.Instance.GetPurchasedLevel(def) + 1);
            UpgradeManager.Instance.SetLevelFromSave(def.DisplayName, newLevel);
        }

        private void OnDecrementUpgradeClicked(UpgradeDefinition def)
        {
            int newLevel = Mathf.Max(0, UpgradeManager.Instance.GetPurchasedLevel(def) - 1);
            UpgradeManager.Instance.SetLevelFromSave(def.DisplayName, newLevel);
        }

        private void OnIncrementPrestigeUpgradeClicked(PrestigeUpgradeDefinition def)
        {
            int newLevel = Mathf.Min(def.MaxLevel, PrestigeUpgradeManager.Instance.GetPurchasedLevel(def) + 1);
            PrestigeUpgradeManager.Instance.SetLevel(def.DisplayName, newLevel);
        }

        private void OnDecrementPrestigeUpgradeClicked(PrestigeUpgradeDefinition def)
        {
            int newLevel = Mathf.Max(0, PrestigeUpgradeManager.Instance.GetPurchasedLevel(def) - 1);
            PrestigeUpgradeManager.Instance.SetLevel(def.DisplayName, newLevel);
        }

        // Skips the pick-1-of-3 screen - takes the first offer.
        private void ForcePrestige()
        {
            var offers = PrestigeManager.Instance.PendingOffers();
            PrestigeManager.Instance.ExecutePrestige(offers.Count > 0 ? offers[0] : null);
        }

        // Turns in one of every species (plus whatever's already in the jar) - discovers them all
        // and so unlocks every automaton hat.
        private void TurnInAllCritters()
        {
            CritterCollection.Instance.DevAddAllSpeciesToJar();
            var evt = CritterCollection.Instance.TurnInJar();
            GameManager.EventService.Dispatch(new NotificationEvent($"Dev turn-in: {evt.Count} critters, {evt.NewHats.Count} hats unlocked", NotificationUrgency.Queued));
        }

        // Marks every not-yet-donated rune as found, so the next Museum turn-in runs the curator's
        // full read-out (and unlocks every accessory) through the normal path.
        private void FindAllRunes()
        {
            RuneCollection.Instance.DevFindAllRunes();
            GameManager.EventService.Dispatch(new NotificationEvent($"Dev: {RuneCollection.Instance.FoundCount} runes found - turn them in at the Museum", NotificationUrgency.Queued));
        }

        private void OnMaxAllUpgradesClicked()
        {
            foreach (var def in GameManager.UpgradeDatabase.Upgrades)
            {
                if (def != null) UpgradeManager.Instance.SetLevelFromSave(def.DisplayName, def.MaxLevel);
            }
            RefreshRows(upgradeRows);
        }

        private void OnRemoveAllUpgradesClicked()
        {
            UpgradeManager.Instance.ResetAllLevels();
            RefreshRows(upgradeRows);
        }

        private void OnMaxAllPrestigeUpgradesClicked()
        {
            foreach (var def in GameManager.PrestigeUpgradeDatabase.Upgrades)
            {
                if (def != null) PrestigeUpgradeManager.Instance.SetLevel(def.DisplayName, def.MaxLevel);
            }
            RefreshRows(prestigeUpgradeRows);
        }

        private void OnRemoveAllPrestigeUpgradesClicked()
        {
            PrestigeUpgradeManager.Instance.ResetAllLevels();
            RefreshRows(prestigeUpgradeRows);
        }

        private static void RefreshRows(List<DevProgressionUpgradeRowUI> rows)
        {
            foreach (var row in rows)
            {
                if (row != null) row.Refresh();
            }
        }
    }
}
