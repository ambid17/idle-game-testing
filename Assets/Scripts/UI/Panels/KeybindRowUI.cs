using Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // One row of the Options > Controls tab: a static label plus one button per control scheme
    // showing the currently bound key/button. Clicking is handled by KeybindsUI, which owns the
    // capture logic for every row.
    public class KeybindRowUI : MonoBehaviour
    {
        [SerializeField] private GameAction action;
        [SerializeField] private Button rebindButton;
        [SerializeField] private TMP_Text keyText;
        [SerializeField] private Button gamepadRebindButton;
        [SerializeField] private TMP_Text gamepadKeyText;

        public GameAction Action => action;
        public Button RebindButton => rebindButton;
        public Button GamepadRebindButton => gamepadRebindButton;

        private void Start()
        {
            if (rebindButton == null) Debug.LogError($"KeybindRowUI({action}).rebindButton is not assigned.");
            if (keyText == null) Debug.LogError($"KeybindRowUI({action}).keyText is not assigned.");
            if (gamepadRebindButton == null) Debug.LogError($"KeybindRowUI({action}).gamepadRebindButton is not assigned.");
            if (gamepadKeyText == null) Debug.LogError($"KeybindRowUI({action}).gamepadKeyText is not assigned.");
        }

        public Button GetButton(InputScheme scheme) => scheme == InputScheme.Gamepad ? gamepadRebindButton : rebindButton;

        public void SetKeyText(InputScheme scheme, string text)
        {
            (scheme == InputScheme.Gamepad ? gamepadKeyText : keyText).text = text;
        }
    }
}
