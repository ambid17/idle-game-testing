using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Events;
using UnityEngine;

namespace Automation
{
    // Lives on the Control Center GameObject. Reconciles the live MiningAutomaton/StorageDrone/
    // FuelDrone GameObjects under it to match UpgradeManager's purchased counts - one function
    // covers both "spawn automatons at the Control Center" on scene load (per the design doc's
    // idle/offline behavior, and after SaveService restores levels) and spawning immediately on a
    // mid-session purchase.
    public class AutomationSpawner : MonoBehaviour
    {
        [SerializeField] private MiningAutomaton automatonPrefab;
        [SerializeField] private StorageDrone storageDronePrefab;
        [SerializeField] private FuelDrone fuelDronePrefab;
        [SerializeField] private Transform automatonSpawn;
        [SerializeField] private Transform depotDepositLocation;

        // Where idle drones hover (and sleep - see DroneSleepVisual). Storage drones also deposit
        // here; set just above the surface so a sleeping drone doesn't sink into the dirt.
        [SerializeField] private Transform storageDroneParking;
        [SerializeField] private Transform fuelDroneParking;

        private readonly List<MiningAutomaton> automatons = new();
        private readonly List<StorageDrone> storageDrones = new();
        private readonly List<FuelDrone> fuelDrones = new();

        // Control Center reveal (ControlCenterRevealController): automatons stay hidden and inert
        // from the moment the reveal is queued until its cinematic has walked the first one out.
        // A flag as well as a pass over the live list, since the reveal and the spawn both react
        // to the same purchase event in no guaranteed order.
        private bool holdingAutomatonsInside;

        // Null until the first automaton has been bought.
        public MiningAutomaton FirstAutomaton => automatons.Count > 0 ? automatons[0] : null;

        public void HoldAutomatonsInside()
        {
            holdingAutomatonsInside = true;
            foreach (var automaton in automatons) automaton.HoldInside();
        }

        public void ReleaseAutomatons()
        {
            holdingAutomatonsInside = false;
            foreach (var automaton in automatons) automaton.Release();
        }

        private void Awake()
        {
            if (automatonPrefab == null) Debug.LogError($"AutomationSpawner is missing automatonPrefab.");
            if (storageDronePrefab == null) Debug.LogError($"AutomationSpawner is missing storageDronePrefab.");
            if (fuelDronePrefab == null) Debug.LogError($"AutomationSpawner is missing fuelDronePrefab.");
            if (storageDroneParking == null) Debug.LogError($"AutomationSpawner is missing storageDroneParking.");
            if (fuelDroneParking == null) Debug.LogError($"AutomationSpawner is missing fuelDroneParking.");
        }

        private void Start()
        {

        }

        private void OnEnable()
        {
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Add<UpgradeLoadedEvent>(OnUpgradeLoaded);
            GameManager.EventService.Add<PrestigeCompletedEvent>(OnPrestigeCompleted);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Remove<UpgradeLoadedEvent>(OnUpgradeLoaded);
            GameManager.EventService.Remove<PrestigeCompletedEvent>(OnPrestigeCompleted);
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent evt)
        {
            var effect = evt.Definition.Effect;
            if (effect == Economy.UpgradeEffect.Automation_AutomatonCount || effect == Economy.UpgradeEffect.Automation_StorageDroneCount || effect == Economy.UpgradeEffect.Automation_FuelDroneCount)
            {
                ReconcileAll();
            }
        }

        private void OnUpgradeLoaded(UpgradeLoadedEvent evt)
        {
            var effect = evt.Definition.Effect;
            if (effect == Economy.UpgradeEffect.Automation_AutomatonCount || effect == Economy.UpgradeEffect.Automation_StorageDroneCount || effect == Economy.UpgradeEffect.Automation_FuelDroneCount)
            {
                ReconcileAll();
            }
        }

        // GameDesignDoc "# Prestige": PrestigeManager.ExecutePrestige clears purchased Market
        // levels but "keep tier" prestige perk baselines still apply (UpgradeManager.GetLevel), so
        // any kept automaton count needs to spawn immediately - nothing else fires
        // UpgradePurchasedEvent as part of a prestige reset.
        private void OnPrestigeCompleted(PrestigeCompletedEvent evt) => ReconcileAll();

        private void ReconcileAll()
        {
            var upgrades = Economy.UpgradeManager.Instance;
            Reconcile(automatons, automatonPrefab, upgrades.Automation_AutomatonCount, (instance, index) =>
            {
                instance.Configure(index, depotDepositLocation.position);
                if (holdingAutomatonsInside) instance.HoldInside();
            });
            Reconcile(storageDrones, storageDronePrefab, upgrades.Automation_StorageDroneCount, (instance, index) => instance.Configure(storageDroneParking.position, index));
            Reconcile(fuelDrones, fuelDronePrefab, upgrades.Automation_FuelDroneCount, (instance, _) => instance.Configure(fuelDroneParking.position));
        }

        private void Reconcile<T>(List<T> instances, T prefab, int targetCount, Action<T, int> configure) where T : Component
        {
            if (prefab == null) return;

            while (instances.Count < targetCount)
            {
                var instance = Instantiate(prefab, automatonSpawn.transform.position, Quaternion.identity);
                instance.gameObject.name = $"{prefab.name} {instances.Count + 1}";
                configure(instance, instances.Count + 1);
                instances.Add(instance);
            }

            while (instances.Count > targetCount)
            {
                int lastIndex = instances.Count - 1;
                var last = instances[lastIndex];
                instances.RemoveAt(lastIndex);
                if (last != null) Destroy(last.gameObject);
            }
        }
    }
}
