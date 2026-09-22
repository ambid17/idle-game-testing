using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProcessingIngredientRow : MonoBehaviour
{
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Image materialIcon;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(countText == null) Debug.LogError("ProcessingIngredientRow.countText is not assigned.");
        if (materialIcon == null) Debug.LogError("ProcessingIngredientRow.materialIcon is not assigned.");
    }

    // Update is called once per frame
    public void Bind(int count, Sprite materialIcon)
    {
        countText.text = count.ToString();
        this.materialIcon.sprite = materialIcon;
    }
}
