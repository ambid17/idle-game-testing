using Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // One row of the Options > Controls tab: a static label plus a button showing the currently
    // bound key. Clicking is handled by KeybindsUI, which owns the capture logic for every row.
    public class KeybindRowUI : MonoBehaviour
    {
        [SerializeField] private GameAction action;
        [SerializeField] private Button rebindButton;
        [SerializeField] private TMP_Text keyText;

        public GameAction Action => action;
        public Button RebindButton => rebindButton;

        private void Start()
        {
            if (rebindButton == null) Debug.LogError($"KeybindRowUI({action}).rebindButton is not assigned.");
            if (keyText == null) Debug.LogError($"KeybindRowUI({action}).keyText is not assigned.");
        }

        public void SetKeyText(string text)
        {
            keyText.text = text;
        }
    }
}
