using Events;
using TMPro;
using UnityEngine;

namespace Processing
{
    // World-space badge that floats above the Processing Center building, showing how many jobs
    // have auto-completed (and been auto-deposited) since the player last opened the Processing
    // panel - see ProcessingManager.UncollectedCompletions / ProcessingUI.Open. Driven by
    // ProcessingCompletionCountChangedEvent rather than polling, with an initial pull from
    // ProcessingManager.Instance in OnEnable so the badge is correct even if it enables after
    // completions already happened (e.g. right after a save load). Scene-placed sibling of the
    // building's title text, same shape as Economy.ChestWeightBarUI - rendererRoot is the only
    // thing toggled, this MonoBehaviour stays enabled throughout.
    public class ProcessingCompletionBadgeUI : MonoBehaviour
    {
        [SerializeField] private GameObject rendererRoot;
        [SerializeField] private TextMeshPro countText;

        private void Awake()
        {
            if (rendererRoot == null) Debug.LogError($"{nameof(ProcessingCompletionBadgeUI)} on {name} is missing its rendererRoot reference.");
            if (countText == null) Debug.LogError($"{nameof(ProcessingCompletionBadgeUI)} on {name} is missing its countText reference.");
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<ProcessingCompletionCountChangedEvent>(OnCountChanged);
            Refresh(ProcessingManager.Instance.UncollectedCompletions);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<ProcessingCompletionCountChangedEvent>(OnCountChanged);
        }

        private void OnCountChanged(ProcessingCompletionCountChangedEvent evt) => Refresh(evt.Count);

        private void Refresh(int count)
        {
            rendererRoot.SetActive(count > 0);
            countText.text = count.ToString();
        }
    }
}
