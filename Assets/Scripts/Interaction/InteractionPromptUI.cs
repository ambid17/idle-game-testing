using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interaction
{
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private TMP_Text promptLabel;

        public void Show(string text)
        {
            promptLabel.text = text;
            promptRoot.SetActive(true);
        }

        public void Hide() => promptRoot.SetActive(false);
    }
}
