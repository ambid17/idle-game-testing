using System;
using RunModifiers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // One offered run modifier in RunModifierPickUI: name, Blessing/Gamble tag and description,
    // framed in the kind's colour (the kind label sits on the card sprite's header band).
    // Clicking it picks that modifier.
    public class RunModifierOfferCardUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text kindLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private Sprite blessingSprite;
        [SerializeField] private Sprite blessingHighlightedSprite;
        [SerializeField] private Sprite gambleSprite;
        [SerializeField] private Sprite gambleHighlightedSprite;

        public void Bind(RunModifierDefinition def, RunModifierState state, Action<RunModifierState> onPicked)
        {
            if (button == null || background == null || nameLabel == null || kindLabel == null || descriptionLabel == null
                || blessingSprite == null || blessingHighlightedSprite == null || gambleSprite == null || gambleHighlightedSprite == null)
            {
                Debug.LogError($"RunModifierOfferCardUI on {gameObject.name} is missing a reference.");
                return;
            }

            bool isBlessing = def.Kind == RunModifierKind.Blessing;
            nameLabel.text = def.DisplayName;
            kindLabel.text = isBlessing ? "Blessing" : "Gamble";
            descriptionLabel.text = GameManager.RunModifierService.Describe(def, state);

            // The button is a SpriteSwap, so hover/selection keeps the kind's colour.
            var highlighted = isBlessing ? blessingHighlightedSprite : gambleHighlightedSprite;
            background.sprite = isBlessing ? blessingSprite : gambleSprite;
            var spriteState = button.spriteState;
            spriteState.highlightedSprite = highlighted;
            spriteState.selectedSprite = highlighted;
            spriteState.pressedSprite = background.sprite;
            button.spriteState = spriteState;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onPicked(state));
        }
    }
}
