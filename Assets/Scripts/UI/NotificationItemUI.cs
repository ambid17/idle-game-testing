using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // One toast in the stacked notification queue (NotificationQueueUI). Self-contained 3s
    // auto-dismiss timer per the design doc's notification requirement; the close button dismisses
    // early. Fully self-managing - destroys its own GameObject on dismissal, so the owning
    // container needs no explicit removal bookkeeping.
    public class NotificationItemUI : MonoBehaviour
    {
        private const float AutoDismissSeconds = 3f;

        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private Button closeButton;

        private float timer;

        public void Bind(string message)
        {
            if (messageLabel != null) messageLabel.text = message;
            if (closeButton != null) closeButton.onClick.AddListener(Dismiss);
            timer = 0f;
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= AutoDismissSeconds) Dismiss();
        }

        private void Dismiss()
        {
            Destroy(gameObject);
        }
    }
}
