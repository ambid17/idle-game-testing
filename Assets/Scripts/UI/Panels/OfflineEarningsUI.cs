using System.Collections;
using System.Collections.Generic;
using Audio;
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
    // this is open (InputBlocker). Inherits ModalBase so TutorialModalUI waits for it to close
    // (and Escape collects).
    // Juice: the rows pop in one after another and count up from zero; Collect bursts sparkles out
    // of each row and the button before the panel closes.
    public class OfflineEarningsUI : ModalBase
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private Transform rowContainer;
        [SerializeField] private OreRowUI rowPrefab;
        [SerializeField] private TMP_Text minutesAwayLabel;
        [SerializeField] private Button collectButton;
        [SerializeField] private float rowStaggerSeconds = 0.08f;
        [SerializeField] private float countUpSeconds = 0.9f;
        [Tooltip("Real seconds the collect sparkles play before the panel closes.")]
        [SerializeField] private float collectSeconds = 0.35f;

        private readonly List<OreRowUI> rows = new();
        private readonly List<int> rowCounts = new();
        private bool collecting;
        private IReadOnlyDictionary<BlockTypeId, int> pendingOre;
        private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;

        private void Start()
        {
            if (collectButton != null) collectButton.onClick.AddListener(Collect);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            GameManager.EventService.Add<OfflineEarningsReadyEvent>(Open);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            GameManager.EventService.Remove<OfflineEarningsReadyEvent>(Open);
            collecting = false;
        }

        private void Open(OfflineEarningsReadyEvent evt)
        {
            pendingOre = evt.OreGained;
            BuildRows(pendingOre);

            if (minutesAwayLabel != null) minutesAwayLabel.text = $"You were away for {evt.MinutesAway:0} minutes";

            InputBlocker.SetBlocked(true);
            rendererRoot.SetActive(true);
            SetOpened();
            StartCoroutine(RevealRows());
        }

        // Each row pops in after the one before and its count/value roll up from zero.
        private IEnumerator RevealRows()
        {
            foreach (var row in rows) row.transform.localScale = Vector3.zero;

            float total = countUpSeconds + rowStaggerSeconds * rows.Count;
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    float local = t - i * rowStaggerSeconds;
                    if (local < 0f) continue;
                    float pop = Mathf.Clamp01(local / 0.2f);
                    rows[i].transform.localScale = Vector3.one * Effects.Easing.OutBack(pop);
                    float count = Mathf.Clamp01(local / countUpSeconds);
                    rows[i].SetCount(Mathf.RoundToInt(rowCounts[i] * Effects.Easing.OutCubic(count)), instant: true);
                }
                yield return null;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].transform.localScale = Vector3.one;
                rows[i].SetCount(rowCounts[i], instant: true);
            }
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
                row.Bind(blockType);
                row.SetCount(0, instant: true);
                rows.Add(row);
                rowCounts.Add(kvp.Value);
            }
        }

        private void ClearRows()
        {
            foreach (var row in rows)
            {
                if (row != null) Destroy(row.gameObject);
            }
            rows.Clear();
            rowCounts.Clear();
        }

        public override void Close() => Collect();

        // The ore is banked at once; the panel lingers for collectSeconds while sparkles burst
        // out of the rows and the button.
        private void Collect()
        {
            if (!IsOpen || collecting) return;
            if (pendingOre != null) Depot.Instance.Deposit(pendingOre);
            pendingOre = null;
            StopAllCoroutines();
            StartCoroutine(CollectFlourish());
        }

        private IEnumerator CollectFlourish()
        {
            collecting = true;
            GameManager.AudioService.Play(SoundId.Deposit);
            var root = (RectTransform)rendererRoot.transform;
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].SetCount(rowCounts[i], instant: true);
                var rowRect = (RectTransform)rows[i].transform;
                rowRect.localScale = Vector3.one;
                UiFx.SparkleBurst(root, root.InverseTransformPoint(rowRect.position), 6, 20f, 260f, 34f, UiFx.Gold);
            }
            var button = (RectTransform)collectButton.transform;
            UiFx.GlowFlash(root, root.InverseTransformPoint(button.position), button.rect.width * 1.4f, new Color(1f, 0.85f, 0.3f, 0.6f));
            UiFx.SparkleBurst(root, root.InverseTransformPoint(button.position), 16, 30f, 420f, 44f, UiFx.Gold);

            yield return new WaitForSecondsRealtime(collectSeconds);

            collecting = false;
            InputBlocker.SetBlocked(false);
            rendererRoot.SetActive(false);
            SetClosed();
        }
    }
}
