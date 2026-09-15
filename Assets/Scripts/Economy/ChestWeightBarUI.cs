using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Economy
{
    // World-space HUD bar that floats above a spawned Chest (see Chest.cs), reusing the same
    // Image.fillAmount convention as HUDUI/InventoryUI's weight meters.
    public class ChestWeightBarUI : MonoBehaviour
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private Image weightFillBar;
        [SerializeField] private TMP_Text weightLabel;

        public void SetWeight(float currentWeight, float maxWeight)
        {
            if (rendererRoot != null) rendererRoot.SetActive(true);
            if (weightFillBar != null)
            {
                weightFillBar.fillAmount = maxWeight > 0f ? Mathf.Clamp01(currentWeight / maxWeight) : 0f;
            }
            if (weightLabel != null) weightLabel.text = $"{currentWeight:0}/{maxWeight:0}";
        }
    }
}
