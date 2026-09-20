using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Events;
using MapGeneration;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // HUD toast driven by OreMinedEvent - shows the icon+name of whatever Ore-category block the
    // player just personally mined (PlayerMining.CollectMinedBlock). Single-slot and restarts its
    // dismiss timer on every new event, same behavior as HudToastUI, rather than queuing - mining
    // fires this often enough that a strict per-mine queue would just pile up.
    public class OreMinedToastUI : MonoBehaviour
    {
        [SerializeField] ToastItemUI toastItemUI;

        List<ToastItemUI> toastItemPool = new List<ToastItemUI>();

        private void Start()
        {
            if (toastItemUI == null) Debug.LogError("OreMinedToastUI.toastItemUI is not assigned.");
            var firstToast = Instantiate(toastItemUI, transform);
            toastItemPool.Add(firstToast);
            firstToast.gameObject.SetActive(false);
        }

        private void OnEnable() => GameManager.EventService.Add<OreMinedEvent>(Open);
        private void OnDisable() => GameManager.EventService.Remove<OreMinedEvent>(Open);

        private void Open(OreMinedEvent evt)
        {
            var firstAvailableToast = toastItemPool.FirstOrDefault(t => t.gameObject.activeSelf == false);
            if(firstAvailableToast == null)
            {
                firstAvailableToast = Instantiate(toastItemUI, transform);
                toastItemPool.Add(firstAvailableToast);
            }
            firstAvailableToast.Open(evt);

        }
    }
}
