using System.Collections.Generic;
using Economy;
using Events;
using Processing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Processing
{
    // The Processing Center's Exchange tab: every good the player has crafted on the left, and
    // the selected one's price graph, holdings and sell buttons on the right. Prices come from
    // Processing.GoodsMarket and move while the tab is open, so it redraws on every market tick.
    // Lives on the tab's content root, which ProcessingUI's TabGroupUI switches on and off - so
    // unlike the panel controllers it listens only while it's the visible tab. Selling goes out
    // as a SellGoodsRequestedEvent (handled by ProcessingUI) rather than touching the Depot here.
    public class GoodsExchangeUI : MonoBehaviour
    {
        [Header("Goods list")]
        [SerializeField] private Transform rowContainer;
        [SerializeField] private GoodsExchangeRowUI rowPrefab;

        [Header("Selected good")]
        [SerializeField] private Image detailIcon;
        [SerializeField] private TMP_Text detailNameLabel;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField] private TMP_Text changeLabel;
        [SerializeField] private TMP_Text eventLabel;
        [SerializeField] private LineGraphUI graph;
        [SerializeField] private TMP_Text highLabel;
        [SerializeField] private TMP_Text lowLabel;
        [SerializeField] private TMP_Text ownedLabel;

        [Header("Selling")]
        [SerializeField] private Button sellOneButton;
        [SerializeField] private Button sellHalfButton;
        [SerializeField] private TMP_Text sellHalfButtonLabel;
        [SerializeField] private Button sellAllButton;
        [SerializeField] private TMP_Text sellAllButtonLabel;

        private const string RiseColor = "#5CE65C";
        private const string FallColor = "#FF6B6B";

        // How much headroom the graph leaves above the window's high and below its low, as a
        // fraction of that range, so the line never rides the plot's edge.
        private const float GraphPadding = 0.1f;

        private readonly Dictionary<ProcessingRecipeId, GoodsExchangeRowUI> rows = new();
        private readonly List<float> graphValues = new();
        private readonly List<IReadOnlyList<float>> graphSeries = new();
        private ProcessingRecipeDefinition selected;

        private void Awake()
        {
            CheckNullRefs();
            graphSeries.Add(graphValues);

            foreach (var recipe in GameManager.ProcessingRecipeDatabase.Recipes)
            {
                var row = Instantiate(rowPrefab, rowContainer);
                row.Bind(recipe, Select);
                row.gameObject.name = $"GoodsExchangeRow_{recipe.name}";
                rows[recipe.Id] = row;
            }

            sellOneButton.onClick.AddListener(() => RequestSell(1));
            sellHalfButton.onClick.AddListener(() => RequestSell(HalfOf(OwnedCount())));
            sellAllButton.onClick.AddListener(() => RequestSell(OwnedCount()));
        }

        private void CheckNullRefs()
        {
            if (rowContainer == null) Debug.LogError("GoodsExchangeUI.rowContainer is not assigned.");
            if (rowPrefab == null) Debug.LogError("GoodsExchangeUI.rowPrefab is not assigned.");
            if (detailIcon == null) Debug.LogError("GoodsExchangeUI.detailIcon is not assigned.");
            if (detailNameLabel == null) Debug.LogError("GoodsExchangeUI.detailNameLabel is not assigned.");
            if (priceLabel == null) Debug.LogError("GoodsExchangeUI.priceLabel is not assigned.");
            if (changeLabel == null) Debug.LogError("GoodsExchangeUI.changeLabel is not assigned.");
            if (eventLabel == null) Debug.LogError("GoodsExchangeUI.eventLabel is not assigned.");
            if (graph == null) Debug.LogError("GoodsExchangeUI.graph is not assigned.");
            if (highLabel == null) Debug.LogError("GoodsExchangeUI.highLabel is not assigned.");
            if (lowLabel == null) Debug.LogError("GoodsExchangeUI.lowLabel is not assigned.");
            if (ownedLabel == null) Debug.LogError("GoodsExchangeUI.ownedLabel is not assigned.");
            if (sellOneButton == null) Debug.LogError("GoodsExchangeUI.sellOneButton is not assigned.");
            if (sellHalfButton == null) Debug.LogError("GoodsExchangeUI.sellHalfButton is not assigned.");
            if (sellHalfButtonLabel == null) Debug.LogError("GoodsExchangeUI.sellHalfButtonLabel is not assigned.");
            if (sellAllButton == null) Debug.LogError("GoodsExchangeUI.sellAllButton is not assigned.");
            if (sellAllButtonLabel == null) Debug.LogError("GoodsExchangeUI.sellAllButtonLabel is not assigned.");
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<GoodsMarketTickedEvent>(Refresh);
            GameManager.EventService.Add<DepotChangedEvent>(Refresh);

            // The graph plots into its RectTransform's size, which the layout groups above only
            // settle at the end of the frame the tab was switched on.
            Canvas.ForceUpdateCanvases();
            Refresh();
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<GoodsMarketTickedEvent>(Refresh);
            GameManager.EventService.Remove<DepotChangedEvent>(Refresh);
        }

        // "+12%" / "-8%" against the recipe's base value, coloured by direction. Shared with the
        // list rows so both read the same.
        public static string FormatChange(float multiplier)
        {
            float percent = (multiplier - 1f) * 100f;
            return $"<color={(percent >= 0f ? RiseColor : FallColor)}>{percent:+0;-0}%</color>";
        }

        private void Select(ProcessingRecipeDefinition recipe)
        {
            selected = recipe;
            Refresh();
        }

        private void Refresh()
        {
            // Rows stay hidden until the good has been crafted at least once, like the Depot's.
            foreach (var recipe in GameManager.ProcessingRecipeDatabase.Recipes)
            {
                bool discovered = Depot.Instance.IsGoodDiscovered(recipe.Id);
                rows[recipe.Id].gameObject.SetActive(discovered);
                if (discovered && selected == null) selected = recipe;
            }

            foreach (var kvp in rows)
            {
                kvp.Value.Refresh();
                kvp.Value.SetSelected(selected != null && kvp.Key == selected.Id);
            }

            // ProcessingUI only shows this tab once a good has been crafted, so there's always
            // something to select by the time it's visible.
            if (selected != null) RefreshDetail();
        }

        private void RefreshDetail()
        {
            double unitValue = Depot.Instance.GoodUnitValue(selected);
            int owned = OwnedCount();

            detailIcon.sprite = selected.Icon;
            detailNameLabel.text = string.IsNullOrEmpty(selected.DisplayName) ? selected.name : selected.DisplayName;
            priceLabel.text = $"${unitValue:0}";
            changeLabel.text = FormatChange(GoodsMarket.Instance.Multiplier(selected.Id));
            eventLabel.text = FormatEvent(GoodsMarket.Instance.ActiveEvent(selected.Id));
            ownedLabel.text = $"Owned: {owned}  (worth ${unitValue * owned:0})";

            int half = HalfOf(owned);
            sellOneButton.interactable = owned > 0;
            sellHalfButton.interactable = owned > 0;
            sellAllButton.interactable = owned > 0;
            sellHalfButtonLabel.text = $"Sell {half} (${unitValue * half:0})";
            sellAllButtonLabel.text = $"Sell All (${unitValue * owned:0})";

            RefreshGraph();
        }

        // History is stored as price multipliers; scaled here to what a unit would actually have
        // sold for, so the graph and its high/low labels read in dollars.
        private void RefreshGraph()
        {
            float dollarsPerMultiplier = (float)(selected.SaleValue * Depot.Instance.GoodValueMultiplier);
            float low = float.MaxValue;
            float high = float.MinValue;

            graphValues.Clear();
            foreach (float multiplier in GoodsMarket.Instance.History(selected.Id))
            {
                float value = multiplier * dollarsPerMultiplier;
                graphValues.Add(value);
                low = Mathf.Min(low, value);
                high = Mathf.Max(high, value);
            }

            float padding = Mathf.Max(0.01f, (high - low) * GraphPadding);
            graph.SetSeries(graphSeries, low - padding, high + padding);
            highLabel.text = $"High ${high:0}";
            lowLabel.text = $"Low ${low:0}";
        }

        private static string FormatEvent(GoodsMarketEventKind kind)
        {
            switch (kind)
            {
                case GoodsMarketEventKind.Boom: return $"<color={RiseColor}>In demand - prices surging</color>";
                case GoodsMarketEventKind.Crash: return $"<color={FallColor}>Oversupplied - prices slumping</color>";
                default: return string.Empty;
            }
        }

        private int OwnedCount()
        {
            Depot.Instance.StoredGoods.TryGetValue(selected.Id, out var count);
            return count;
        }

        // Rounds up, so "half" of a single unit still sells it.
        private static int HalfOf(int count) => (count + 1) / 2;

        private void RequestSell(int amount)
        {
            GameManager.EventService.Dispatch(new SellGoodsRequestedEvent(selected.Id, amount));
        }
    }
}
