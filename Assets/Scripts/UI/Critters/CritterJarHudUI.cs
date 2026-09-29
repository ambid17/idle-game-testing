using Critters;
using Events;
using TMPro;
using UnityEngine;

namespace UI
{
    // Small HUD badge: how many caught critters are riding along in the jar, waiting to be turned
    // in at the Critter Shop. Hidden while the jar is empty.
    public class CritterJarHudUI : MonoBehaviour
    {
        [SerializeField] private GameObject badgeRoot;
        [SerializeField] private TMP_Text countLabel;

        private void Start()
        {
            if (badgeRoot == null) Debug.LogError("CritterJarHudUI.badgeRoot is not assigned.");
            if (countLabel == null) Debug.LogError("CritterJarHudUI.countLabel is not assigned.");
            Refresh();
        }

        private void OnEnable() => GameManager.EventService.Add<CritterCollectionChangedEvent>(Refresh);
        private void OnDisable() => GameManager.EventService.Remove<CritterCollectionChangedEvent>(Refresh);

        private void Refresh()
        {
            int count = CritterCollection.Instance.Jar.Count;
            badgeRoot.SetActive(count > 0);
            countLabel.text = $"x{count}";
        }
    }
}
