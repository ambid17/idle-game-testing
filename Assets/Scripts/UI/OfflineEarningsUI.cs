using System.Collections.Generic;
using Economy;
using Events;
using MapGeneration;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // GameDesignDoc "idle": shown once on load when SaveService computes offline ore gained
    // (average ore/minute per mineral, from Mining Automatons only, times minutes away - see
    // Automation.IdleEarningsTracker/Persistence.SaveService). Follows DeathUI's full-screen-modal
    // pattern. Per the resolved design decisions, ore is only actually deposited into the Depot
    // once the player acknowledges via the Collect button, and the player's input is blocked while
    // this is open (InputBlocker).
    public class OfflineEarningsUI : MonoBehaviour
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private Transform rowContainer;
        [SerializeField] private OreRowUI rowPrefab;
        [SerializeField] private TMP_Text minutesAwayLabel;
        [SerializeField] private Button collectButton;

        private readonly List<OreRowUI> rows = new();
        private IReadOnlyDictionary<BlockTypeId, int> pendingOre;
        private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;

        private void Start()
        {
            if (collectButton != null) collectButton.onClick.AddListener(Collect);
            if (rendererRoot != null) rendererRoot.SetActive(false);
        }

        private void OnEnable() => GameManager.EventService.Add<OfflineEarningsReadyEvent>(Open);
        private void OnDisable() => GameManager.EventService.Remove<OfflineEarningsReadyEvent>(Open);

        private void Open(OfflineEarningsReadyEvent evt)
        {
            pendingOre = evt.OreGained;
            BuildRows(pendingOre);

            if (minutesAwayLabel != null) minutesAwayLabel.text = $"You were away for {evt.MinutesAway:0} minutes";

            InputBlocker.SetBlocked(true);
            if (rendererRoot != null) rendererRoot.SetActive(true);
        }

        private void BuildRows(IReadOnlyDictionary<BlockTypeId, int> oreGained)
        {
            ClearRows();
            if (rowPrefab == null || rowContainer == null)
            {
                Debug.LogError("OfflineEarningsUI.BuildRows: Missing rowPrefab or rowContainer. Cannot build ore rows.");
                return;
            }

            foreach (var kvp in oreGained)
            {
                if (kvp.Value <= 0) continue;

                var blockType = blockTypeDatabase.Get((byte)kvp.Key);
                if (blockType == null) continue;

                var row = Instantiate(rowPrefab, rowContainer);
                string displayName = string.IsNullOrEmpty(blockType.DisplayName) ? blockType.name : blockType.DisplayName;
                row.Bind(kvp.Key, displayName);
                row.SetCount(kvp.Value);
                rows.Add(row);
            }
        }

        private void ClearRows()
        {
            foreach (var row in rows)
            {
                if (row != null) Destroy(row.gameObject);
            }
            rows.Clear();
        }

        private void Collect()
        {
            if (pendingOre != null) Depot.Instance.Deposit(pendingOre);
            pendingOre = null;

            InputBlocker.SetBlocked(false);
            if (rendererRoot != null) rendererRoot.SetActive(false);
            GameManager.EventService.Dispatch<OfflineEarningsAcknowledgedEvent>();
        }
    }
}
