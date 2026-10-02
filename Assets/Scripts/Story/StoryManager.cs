using System;
using System.Collections;
using System.Collections.Generic;
using Audio;
using Economy;
using Events;
using Museum;
using Persistence;
using Player;
using UnityEngine;
using UnityEngine.UI;

namespace Story
{
    // How far the Seals story has come - derived from the deepest layer the player has ever mined
    // on, so each stage opens with the drill tier that gates its layers. Also indexes StoryContent's
    // per-stage pools.
    public enum StoryStage
    {
        Commission = 0,
        Warnings = 1,
        Retranslation = 2,
        Vault = 3,
    }

    // Saved as an int - append-only.
    public enum StoryEnding
    {
        None = 0,
        Release = 1,
        Reseal = 2,
    }

    // Save-file shape for StoryManager (a field of Persistence.GameSaveData).
    [Serializable]
    public class StorySaveData
    {
        public int DeepestLayerIndex = -1;
        public List<int> TakenKeystones = new();
        // Chamber indices whose mural has been read - StoryManager.VaultChamberIndex for the Vault.
        public List<int> ExaminedChambers = new();
        public bool RetranslationSeen;
        public StoryEnding Ending;
    }

    // The Seals story's state (GameDesignDoc "# Story & Endgame: The Seals"): which stage the
    // player has dug down to, which Keystones they pried loose, and how it ended. Nothing here
    // resets on prestige. Whispers the Bound's line the first time each layer is reached, shakes
    // the mine with ambient tremors that grow with the stage and Resonance count, hands the
    // curator / critter keeper / Resonance prompt their stage lines, and runs the ending sequence.
    // Child of the GameManager object, accessed via GameManager.StoryManager.
    public class StoryManager : MonoBehaviour
    {
        // First layer index of each stage after Commission - the layers each drill tier unlocks.
        private static readonly int[] StageFirstLayers = { 3, 6, 9 };
        private const int TotalSeals = 4;
        private const string EpilogueConversation = "Story.Epilogue";
        private const string VoiceTag = "{voice}";
        private const string CuratorTag = "{curator}";
        private const string KeystonesTag = "{keystones}";

        [SerializeField] private StoryContent content;

        [Header("Keystones")]
        [Tooltip("Artifact payout for taking each Keystone, top chamber first.")]
        [SerializeField] private int[] keystoneRewards = { 15, 30, 60 };
        [SerializeField, Min(0)] private int resealBaseCost = 20;
        [Tooltip("Reseal costs this many artifacts per artifact paid out by the Keystones taken.")]
        [SerializeField, Min(0f)] private float resealCostPerTakenArtifact = 2f;

        [Header("Post-game")]
        [SerializeField, Min(1f)] private float releaseSaleValueMultiplier = 1.25f;
        [SerializeField, Range(0f, 1f)] private float resealDamageTakenMultiplier = 0.75f;

        [Header("Character lines")]
        [Tooltip("Chance a greeting / Talk comes from the story pool rather than the character's ordinary lines.")]
        [SerializeField, Range(0f, 1f)] private float storyLineChance = 0.6f;

        [Header("Whispers")]
        [Tooltip("How much longer than an ordinary warning toast a whisper stays on screen.")]
        [SerializeField, Min(0.1f)] private float whisperDurationMultiplier = 2f;

        [Header("Tremors")]
        [Tooltip("Seconds between tremors at the lowest and highest intensity.")]
        [SerializeField, Min(1f)] private float tremorIntervalCalm = 240f;
        [SerializeField, Min(1f)] private float tremorIntervalRestless = 70f;
        [SerializeField, Min(0f)] private float tremorForceCalm = 0.08f;
        [SerializeField, Min(0f)] private float tremorForceRestless = 0.3f;
        [SerializeField, Min(0f)] private float tremorSeconds = 0.7f;
        [Tooltip("Resonances that count toward tremor intensity.")]
        [SerializeField, Min(1)] private int tremorResonanceCap = 6;

        [Header("Ending sequence")]
        [SerializeField, Min(0f)] private float endingRumbleSeconds = 1.2f;
        [SerializeField, Min(0f)] private float endingFlashInSeconds = 0.5f;
        [SerializeField, Min(0f)] private float endingFlashHoldSeconds = 0.6f;
        [SerializeField, Min(0f)] private float endingFlashOutSeconds = 0.9f;

        private readonly HashSet<int> takenKeystones = new();
        private readonly HashSet<int> examinedChambers = new();
        private int deepestLayerIndex = -1;
        private float tremorTimer;
        private Image flash;

        public StoryContent Content => content;
        public StoryEnding Ending { get; private set; }
        public bool RetranslationSeen { get; private set; }
        public bool IsEndingPlaying { get; private set; }

        public int KeystoneCount => keystoneRewards.Length;
        // ExaminedChambers index for the Vault - one past the last Keystone chamber.
        public int VaultChamberIndex => keystoneRewards.Length;
        public int KeystonesTaken => takenKeystones.Count;
        public int SealsHolding => Ending == StoryEnding.Release ? 0 : TotalSeals - takenKeystones.Count;

        public StoryStage Stage
        {
            get
            {
                int stage = 0;
                while (stage < StageFirstLayers.Length && deepestLayerIndex >= StageFirstLayers[stage]) stage++;
                return (StoryStage)stage;
            }
        }

        // The stage the characters talk from: nobody speaks of Seals until the curator has
        // actually told the player what they are.
        private StoryStage SpokenStage
        {
            get
            {
                var stage = Stage;
                return stage >= StoryStage.Retranslation && !RetranslationSeen ? StoryStage.Warnings : stage;
            }
        }

        // Release: the light has somewhere to go - folded into PrestigeUpgradeManager.Prestige_IncomeMultiplier.
        public float SaleValueMultiplier => Ending == StoryEnding.Release ? releaseSaleValueMultiplier : 1f;
        // Reseal: nothing below is pushing back - applied in PlayerHealth.TakeDamage.
        public float DamageTakenMultiplier => Ending == StoryEnding.Reseal ? resealDamageTakenMultiplier : 1f;

        private void Awake()
        {
            if (content == null) Debug.LogError("StoryManager.content is not assigned.");
        }

        private void Start()
        {
            var canvasObject = new GameObject("Story Flash Canvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            flash = new GameObject("Flash").AddComponent<Image>();
            flash.transform.SetParent(canvasObject.transform, false);
            flash.raycastTarget = false;
            var flashRect = flash.rectTransform;
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            SetFlash(0f);

            tremorTimer = tremorIntervalCalm;
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<BlockMinedEvent>(OnBlockMined);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<BlockMinedEvent>(OnBlockMined);
        }

        private void Update()
        {
            float intensity = TremorIntensity;
            if (intensity <= 0f) return;
            if (!SaveService.Instance.HasLoadedData || InputBlocker.IsBlocked || PrestigeCinematic.IsPlaying || IsEndingPlaying) return;

            tremorTimer -= Time.deltaTime;
            if (tremorTimer > 0f) return;

            tremorTimer = Mathf.Lerp(tremorIntervalCalm, tremorIntervalRestless, intensity) * UnityEngine.Random.Range(0.7f, 1.3f);
            GameManager.AudioService.Play(SoundId.RockRumble);
            GameManager.CameraShake.Shake(Vector2.up * Mathf.Lerp(tremorForceCalm, tremorForceRestless, intensity), tremorSeconds);
        }

        // 0 = still (a fresh mine, or after either ending); 1 = the Vault stage with several Resonances behind it.
        private float TremorIntensity
        {
            get
            {
                if (Ending != StoryEnding.None) return 0f;
                float resonances = Mathf.Min(PrestigeManager.Instance.PrestigeCount, tremorResonanceCap) / (float)tremorResonanceCap;
                return Mathf.Clamp01(((int)Stage + resonances) / (StageFirstLayers.Length + 1f));
            }
        }

        // ---- Depth ----

        private void OnBlockMined(BlockMinedEvent e)
        {
            if (e.LayerIndex <= deepestLayerIndex) return;

            deepestLayerIndex = e.LayerIndex;
            if (Ending == StoryEnding.None && e.LayerIndex < content.LayerWhispers.Length) Whisper(content.LayerWhispers[e.LayerIndex]);
            GameManager.EventService.Dispatch<StoryProgressChangedEvent>();
        }

        private void Whisper(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            string color = ColorUtility.ToHtmlStringRGB(content.VoiceColor);
            GameManager.EventService.Dispatch(new NotificationEvent($"<color=#{color}><i>\"{line}\"</i></color>", NotificationUrgency.TimeSensitive, durationMultiplier: whisperDurationMultiplier));
        }

        // ---- Character lines ----

        // Null when the character should use one of their ordinary lines instead.
        public string CuratorGreeting() => PickStoryLine(Ending switch
        {
            StoryEnding.Release => content.CuratorGreetingsAfterRelease,
            StoryEnding.Reseal => content.CuratorGreetingsAfterReseal,
            _ => content.StageLinesFor(content.CuratorGreetings, SpokenStage),
        });

        public string CuratorChatter() => Ending != StoryEnding.None ? null : PickStoryLine(content.StageLinesFor(content.CuratorChatter, SpokenStage));

        public string ShopkeeperChatter() => PickStoryLine(Ending switch
        {
            StoryEnding.Release => content.ShopkeeperChatterAfterRelease,
            StoryEnding.Reseal => content.ShopkeeperChatterAfterReseal,
            _ => content.StageLinesFor(content.ShopkeeperChatter, SpokenStage),
        });

        private string PickStoryLine(string[] pool)
        {
            if (pool == null || pool.Length == 0 || UnityEngine.Random.value >= storyLineChance) return null;
            return string.Format(StoryContent.PickRandom(pool), SealsHolding);
        }

        public bool ShouldPlayRetranslation => Stage >= StoryStage.Retranslation && !RetranslationSeen && Ending == StoryEnding.None;

        public void MarkRetranslationSeen()
        {
            RetranslationSeen = true;
            GameManager.EventService.Dispatch<StoryProgressChangedEvent>();
        }

        public string ResonancePrompt()
        {
            if (Ending != StoryEnding.None) return content.ResonancePromptAfterEnding;
            if (PrestigeManager.Instance.PrestigeCount == 0) return content.FirstResonancePrompt;
            return string.Format(content.ResonancePrompts[(int)SpokenStage], SealsHolding);
        }

        // ---- Seal Chambers ----

        public bool HasExamined(int chamberIndex) => examinedChambers.Contains(chamberIndex);
        public void MarkExamined(int chamberIndex) => examinedChambers.Add(chamberIndex);

        // The mural read-out for a chamber's first visit: narration, then the Bound if it has anything to say.
        public List<DialogLine> BuildChamberLines(int chamberIndex)
        {
            var text = chamberIndex == VaultChamberIndex ? content.Vault : content.Chambers[chamberIndex];
            var lines = new List<DialogLine>();
            AddPages(lines, string.Empty, null, text.Narration);
            AddPages(lines, RetranslationSeen ? content.VoiceName : content.UnknownVoiceName, null, text.Voice);
            return lines;
        }

        public bool IsKeystoneTaken(int keystoneIndex) => takenKeystones.Contains(keystoneIndex);
        public int KeystoneReward(int keystoneIndex) => keystoneRewards[keystoneIndex];

        public void TakeKeystone(int keystoneIndex)
        {
            if (Ending != StoryEnding.None || !takenKeystones.Add(keystoneIndex)) return;

            int reward = keystoneRewards[keystoneIndex];
            Wallet.Instance.AddArtifacts(reward);
            GameManager.AudioService.Play(SoundId.ArtifactFound);
            GameManager.CameraShake.Shake(Vector2.up * tremorForceRestless, tremorSeconds);
            GameManager.EventService.Dispatch(new NotificationEvent($"Keystone taken. +{reward} <color=purple>Artifacts</color>", NotificationUrgency.Queued));
            // The first chamber sits above where the whispers begin - it stays silent there too.
            if (Stage >= StoryStage.Warnings) Whisper(StoryContent.PickRandom(content.KeystoneTakenWhispers));
            GameManager.EventService.Dispatch<StoryProgressChangedEvent>();
        }

        // What mending the last Seal takes back: a base price plus a multiple of every Keystone payout taken.
        public int ResealCost
        {
            get
            {
                int taken = 0;
                foreach (int index in takenKeystones) taken += keystoneRewards[index];
                return resealBaseCost + Mathf.RoundToInt(taken * resealCostPerTakenArtifact);
            }
        }

        public bool CanAffordReseal => Wallet.Instance.ArtifactCount >= ResealCost;

        // ---- Ending ----

        public void ChooseEnding(StoryEnding ending)
        {
            if (ending == StoryEnding.None || Ending != StoryEnding.None || IsEndingPlaying) return;
            if (ending == StoryEnding.Reseal && !CanAffordReseal)
            {
                GameManager.EventService.Dispatch(new NotificationEvent(
                    string.Format(content.ResealTooExpensive, ResealCost, Wallet.Instance.ArtifactCount), NotificationUrgency.TimeSensitive));
                return;
            }
            StartCoroutine(EndingSequence(ending));
        }

        // The mine shakes, the screen goes white, the choice is applied under the flash, and the
        // epilogue plays once it clears.
        private IEnumerator EndingSequence(StoryEnding ending)
        {
            IsEndingPlaying = true;
            InputBlocker.SetBlocked(true);

            GameManager.AudioService.Play(SoundId.RockRumble);
            GameManager.CameraShake.Shake(Vector2.up * tremorForceRestless * 1.5f, endingRumbleSeconds);
            yield return new WaitForSeconds(endingRumbleSeconds);

            GameManager.AudioService.Play(SoundId.Prestige);
            yield return Flash(0f, 1f, endingFlashInSeconds);

            if (ending == StoryEnding.Reseal) Wallet.Instance.TrySpendArtifacts(ResealCost);
            Ending = ending;
            GameManager.EventService.Dispatch<StoryProgressChangedEvent>();
            SaveService.Instance.Save();

            yield return new WaitForSecondsRealtime(endingFlashHoldSeconds);
            yield return Flash(1f, 0f, endingFlashOutSeconds);

            InputBlocker.SetBlocked(false);
            IsEndingPlaying = false;
            GameManager.EventService.Dispatch(new DialogRequestedEvent(EpilogueConversation, BuildEpilogue(ending)));
        }

        private List<DialogLine> BuildEpilogue(StoryEnding ending)
        {
            bool release = ending == StoryEnding.Release;
            var keystoneLines = release ? content.ReleaseKeystoneLines : content.ResealKeystoneLines;
            var curator = MuseumCuratorController.Instance.Dialog;

            var lines = new List<DialogLine>();
            foreach (string entry in release ? content.ReleaseEpilogue : content.ResealEpilogue)
            {
                if (entry == KeystonesTag) AddPages(lines, string.Empty, null, keystoneLines[Mathf.Min(KeystonesTaken, keystoneLines.Length - 1)]);
                else if (entry.StartsWith(VoiceTag)) AddPages(lines, content.VoiceName, null, entry.Substring(VoiceTag.Length));
                else if (entry.StartsWith(CuratorTag)) AddPages(lines, curator.SpeakerName, curator.Portrait, entry.Substring(CuratorTag.Length));
                else AddPages(lines, string.Empty, null, entry);
            }
            return lines;
        }

        private static void AddPages(List<DialogLine> lines, string speaker, Sprite portrait, string entry)
        {
            if (string.IsNullOrEmpty(entry)) return;

            foreach (var page in entry.Split(StoryContent.PageSeparator))
            {
                var text = page.Trim();
                if (text.Length > 0) lines.Add(new DialogLine(speaker, portrait, text));
            }
        }

        // Unscaled time: it must still fade if something froze the clock.
        private IEnumerator Flash(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetFlash(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            SetFlash(to);
        }

        private void SetFlash(float alpha)
        {
            flash.color = new Color(1f, 1f, 1f, alpha);
            flash.gameObject.SetActive(alpha > 0f);
        }

        // ---- Persistence ----

        public StorySaveData ToSaveData()
        {
            var data = new StorySaveData
            {
                DeepestLayerIndex = deepestLayerIndex,
                RetranslationSeen = RetranslationSeen,
                Ending = Ending,
            };
            data.TakenKeystones.AddRange(takenKeystones);
            data.ExaminedChambers.AddRange(examinedChambers);
            return data;
        }

        // lifetimeDeepestLayerIndex seeds the stage on saves from before the story existed.
        public void RestoreFromSaveData(StorySaveData data, int lifetimeDeepestLayerIndex)
        {
            takenKeystones.Clear();
            examinedChambers.Clear();
            deepestLayerIndex = lifetimeDeepestLayerIndex;
            RetranslationSeen = false;
            Ending = StoryEnding.None;

            if (data != null)
            {
                deepestLayerIndex = Mathf.Max(deepestLayerIndex, data.DeepestLayerIndex);
                RetranslationSeen = data.RetranslationSeen;
                Ending = data.Ending;
                foreach (int index in data.TakenKeystones)
                {
                    if (index >= 0 && index < keystoneRewards.Length) takenKeystones.Add(index);
                }
                foreach (int index in data.ExaminedChambers) examinedChambers.Add(index);
            }

            GameManager.EventService.Dispatch<StoryProgressChangedEvent>();
        }

        // ---- Dev ----

        [ContextMenu("Dev: Advance Stage")]
        private void DevAdvanceStage()
        {
            int stage = (int)Stage;
            if (stage >= StageFirstLayers.Length) return;

            deepestLayerIndex = StageFirstLayers[stage];
            Debug.Log($"StoryManager: stage is now {Stage}.");
            GameManager.EventService.Dispatch<StoryProgressChangedEvent>();
        }

        [ContextMenu("Dev: Reset Story")]
        private void DevResetStory()
        {
            RestoreFromSaveData(null, -1);
            Debug.Log("StoryManager: story progress reset.");
        }
    }
}
