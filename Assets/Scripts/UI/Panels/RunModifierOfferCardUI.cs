using System;
using RunModifiers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // One offered run modifier in RunModifierPickUI: name, Blessing/Gamble tag and description,
    // tinted by kind. Clicking it picks that modifier.
    public class RunModifierOfferCardUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text kindLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private Color blessingColor = new(0.16f, 0.36f, 0.3f, 0.95f);
        [SerializeField] private Color gambleColor = new(0.42f, 0.2f, 0.12f, 0.95f);

        public void Bind(RunModifierDefinition def, RunModifierState state, Action<RunModifierState> onPicked)
        {
            if (button == null || background == null || nameLabel == null || kindLabel == null || descriptionLabel == null)
            {
                Debug.LogError($"RunModifierOfferCardUI on {gameObject.name} is missing a reference.");
                return;
            }

            bool isBlessing = def.Kind == RunModifierKind.Blessing;
            nameLabel.text = def.DisplayName;
            kindLabel.text = isBlessing ? "Blessing" : "Gamble";
            descriptionLabel.text = GameManager.RunModifierService.Describe(def, state);
            background.color = isBlessing ? blessingColor : gambleColor;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onPicked(state));
        }
    }
}
