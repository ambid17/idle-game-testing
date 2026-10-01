using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One "count + ore icon" ingredient chip inside a ProcessingRecipeRowUI.
public class ProcessingIngredientRow : MonoBehaviour
{
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Image materialIcon;

    private static readonly Color ShortColor = new(1f, 0.42f, 0.42f);

    void Start()
    {
        if(countText == null) Debug.LogError("ProcessingIngredientRow.countText is not assigned.");
        if (materialIcon == null) Debug.LogError("ProcessingIngredientRow.materialIcon is not assigned.");
    }

    // `affordable` is false when the Depot holds less of this ore than one unit needs.
    public void Bind(int count, Sprite materialIcon, Sprite materialIconBackground, bool affordable)
    {
        countText.text = count.ToString();
        countText.color = affordable ? Color.white : ShortColor;
        this.materialIcon.SetIcon(materialIcon, materialIconBackground);
    }
}
