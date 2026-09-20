using Events;
using MapGeneration;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ToastItemUI : MonoBehaviour
{
    private const float DisplaySeconds = 1.5f;

    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameLabel;
    private BlockTypeDatabase blockTypeDatabase => GameManager.BlockTypeDatabase;

    void Start()
    {
        if (iconImage == null) Debug.LogError("OreMinedToastUI.iconImage is not assigned.");
        if (nameLabel == null) Debug.LogError("OreMinedToastUI.nameLabel is not assigned.");
    }

    void Update()
    {
        
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
        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        yield return new WaitForSeconds(DisplaySeconds);
        gameObject.SetActive(false);
    }
}
