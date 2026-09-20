using Events;
using MapGeneration;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ToastItemUI : MonoBehaviour
{
    private const float DisplaySeconds = 0.5f;
    private const float FadeOutSeconds = 0.4f;
    private const float FadeOutMoveDistance = 40f;

    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameLabel;
    private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 shownAnchoredPosition;

    void Awake()
    {
        if (iconImage == null) Debug.LogError("OreMinedToastUI.iconImage is not assigned.");
        if (nameLabel == null) Debug.LogError("OreMinedToastUI.nameLabel is not assigned.");

        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        shownAnchoredPosition = rectTransform.anchoredPosition;
    }

    public void Open(OreMinedEvent evt)
    {
        gameObject.SetActive(true);
        var blockType = blockTypeDatabase.Get((byte)evt.Id);
        if (blockType == null)
        {
            Debug.LogError($"OreMinedToastUI: no BlockType registered for {evt.Id}.");
            return;
        }

        iconImage.sprite = blockType.Icon;
        nameLabel.text = $"+{evt.Amount} {blockType.DisplayName}";

        StopAllCoroutines();
        rectTransform.anchoredPosition = shownAnchoredPosition;
        canvasGroup.alpha = 1f;
        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        yield return new WaitForSeconds(DisplaySeconds);

        float elapsed = 0f;
        while (elapsed < FadeOutSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / FadeOutSeconds);
            canvasGroup.alpha = 1f - t;
            rectTransform.anchoredPosition = shownAnchoredPosition + Vector2.up * (FadeOutMoveDistance * t);
            yield return null;
        }

        gameObject.SetActive(false);
    }
}
