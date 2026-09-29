using System.Collections;
using Events;
using MapGeneration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // HUD card showing the result of an Analyzer scan (Player.PlayerAnalyzer -> BlockAnalyzedEvent):
    // the block's icon, a category-tinted title, and the readout text. Holds for a time scaled by
    // text length, then fades. A new scan replaces whatever is showing and restarts the timer.
    // Non-blocking - the player keeps moving/mining while it's up.
    public class AnalyzerReadoutUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup rendererRoot;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private float minHoldSeconds = 3f;
        [Tooltip("Extra hold time per character of body text, so longer lore stays readable.")]
        [SerializeField] private float holdSecondsPerCharacter = 0.04f;
        [SerializeField] private float fadeSeconds = 0.4f;

        [Header("Title colors")]
        [SerializeField] private Color oreColor = new(0.49f, 0.99f, 0.49f);
        [SerializeField] private Color hazardColor = new(1f, 0.42f, 0.3f);
        [SerializeField] private Color powerUpColor = new(0.4f, 0.8f, 1f);
        [SerializeField] private Color artifactColor = new(0.8f, 0.55f, 1f);

        private Coroutine showRoutine;

        private void Start()
        {
            if (rendererRoot == null) Debug.LogError($"{nameof(AnalyzerReadoutUI)} is missing rendererRoot.");
            if (iconImage == null) Debug.LogError($"{nameof(AnalyzerReadoutUI)} is missing iconImage.");
            if (titleLabel == null) Debug.LogError($"{nameof(AnalyzerReadoutUI)} is missing titleLabel.");
            if (bodyLabel == null) Debug.LogError($"{nameof(AnalyzerReadoutUI)} is missing bodyLabel.");

            rendererRoot.gameObject.SetActive(false);
        }

        private void OnEnable() => GameManager.EventService.Add<BlockAnalyzedEvent>(OnBlockAnalyzed);
        private void OnDisable() => GameManager.EventService.Remove<BlockAnalyzedEvent>(OnBlockAnalyzed);

        private void OnBlockAnalyzed(BlockAnalyzedEvent evt)
        {
            var block = evt.BlockType;
            iconImage.sprite = block.Icon;
            iconImage.gameObject.SetActive(block.Icon != null);
            titleLabel.text = $"<size=70%>{CategoryLabel(block.Category)}</size>\n{block.DisplayName}";
            titleLabel.color = CategoryColor(block.Category);
            bodyLabel.text = evt.Body;

            if (showRoutine != null) StopCoroutine(showRoutine);
            showRoutine = StartCoroutine(Show(minHoldSeconds + evt.Body.Length * holdSecondsPerCharacter));
        }

        private IEnumerator Show(float holdSeconds)
        {
            rendererRoot.alpha = 1f;
            rendererRoot.gameObject.SetActive(true);

            yield return new WaitForSeconds(holdSeconds);

            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.deltaTime;
                rendererRoot.alpha = 1f - Mathf.Clamp01(elapsed / fadeSeconds);
                yield return null;
            }

            rendererRoot.gameObject.SetActive(false);
            showRoutine = null;
        }

        private static string CategoryLabel(BlockCategory category) => category switch
        {
            BlockCategory.Ore => "MINERAL",
            BlockCategory.Hazard => "HAZARD",
            BlockCategory.PowerUp => "POWER-UP",
            BlockCategory.Artifact => "ARTIFACT",
            _ => category.ToString().ToUpperInvariant(),
        };

        private Color CategoryColor(BlockCategory category) => category switch
        {
            BlockCategory.Ore => oreColor,
            BlockCategory.Hazard => hazardColor,
            BlockCategory.PowerUp => powerUpColor,
            BlockCategory.Artifact => artifactColor,
            _ => Color.white,
        };
    }
}
