using System;
using System.Collections;
using System.Collections.Generic;
using Audio;
using Economy;
using Events;
using Museum;
using Persistence;
using Player;
using UI;
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

    // Saved as an int - append-only. Release is never stored as the ending: the world it ends in
    // is gone, so the save stays at the Vault with the choice un-made (see ReleaseWitnessed).
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
        // The Release ending has been played through at least once.
        public bool ReleaseWitnessed;
        // Reseal: the Bound has said his thanks in the void cave / the player has gone home from it.
        public bool VoidCaveSpeechHeard;
        public bool VoidCaveLeft;
    }

    // The Seals story's state (GameDesignDoc "# Story & Endgame: The Seals"): which stage the
    // player has dug down to, which Keystones they pried loose, and how it ended. Nothing here
    // resets on prestige. Opens a dialog with the Bound the first time each layer is reached, shakes
    // the mine with ambient tremors that grow with the stage and Resonance count, hands the
    // curator / critter keeper / Resonance prompt their stage lines, and starts the two endings:
    // ReleaseCinematic (the world is destroyed; the save stays at the Vault) and VoidCave (Reseal).
    // Child of the GameManager object, accessed via GameManager.StoryManager.
    public class StoryManager : MonoBehaviour
    {
        // First layer index of each stage after Commission - the layers each drill tier unlocks.
        private static readonly int[] StageFirstLayers = { 3, 6, 9 };
        private const int TotalSeals = 4;
        private const string EpilogueConversation = "Story.Epilogue";
        private const string RewindConversation = "Story.Rewind";
        private const string WhisperConversation = "Story.Whisper";
        private const string VoiceTag = "{voice}";
        private const string CuratorTag = "{curator}";
        private const string KeystonesTag = "{keystones}";

        // Set by ReleaseCinematic right before it reloads the scene, so the Bound speaks once the
        // player is back in front of the Seal.
        public static bool RewoundThisSession;

        [SerializeField] private StoryContent content;
        [SerializeField] private ReleaseCinematic releaseCinematic;
        [SerializeField] private VoidCave voidCave;

        [Header("Keystones")]
        [Tooltip("Artifact payout for taking each Keystone, top chamber first.")]
        [SerializeField] private int[] keystoneRewards = { 15, 30, 60 };
        [SerializeField, Min(0)] private int resealBaseCost = 20;
        [Tooltip("Reseal costs this many artifacts per artifact paid out by the Keystones taken.")]
        [SerializeField, Min(0f)] private float resealCostPerTakenArtifact = 2f;

        [Header("Post-game")]
        [SerializeField, Range(0f, 1f)] private float resealDamageTakenMultiplier = 0.75f;

        [Header("Character lines")]
        [Tooltip("Chance a greeting / Talk comes from the story pool rather than the character's ordinary lines.")]
        [SerializeField, Range(0f, 1f)] private float storyLineChance = 0.6f;

        [Header("Whispers")]
        [Tooltip("Seconds between whatever set a whisper off and the Bound's dialog opening.")]
        [SerializeField, Min(0f)] private float whisperDelay = 0.6f;

        [Header("Tremors")]
        [Tooltip("Seconds between tremors at the lowest and highest intensity.")]
        [SerializeField, Min(1f)] private float tremorIntervalCalm = 240f;
        [SerializeField, Min(1f)] private float tremorIntervalRestless = 70f;
        [SerializeField, Min(0f)] private float tremorForceCalm = 0.08f;
        [SerializeField, Min(0f)] private float tremorForceRestless = 0.3f;
        [SerializeField, Min(0f)] private float tremorSeconds = 0.7f;
        [Tooltip("Resonances that count toward tremor intensity.")]
        [SerializeField, Min(1)] private int tremorResonanceCap = 6;

        private readonly HashSet<int> takenKeystones = new();
        private readonly HashSet<int> examinedChambers = new();
        private readonly Queue<string> pendingWhispers = new();
        private readonly Queue<(string Id, List<DialogLine> Lines)> pendingConversations = new();
        private int deepestLayerIndex = -1;
        private float whisperTimer;
        private float tremorTimer;
        private Image flash;

        public StoryContent Content => content;
        public StoryEnding Ending { get; private set; }
        public bool RetranslationSeen { get; private set; }
        public bool ReleaseWitnessed { get; private set; }
        public bool VoidCaveSpeechHeard { get; private set; }
        public bool VoidCaveLeft { get; private set; }
        // Reseal chosen and the player hasn't gone home from the Bound's cave yet.
        public bool IsInVoidCave => Ending == StoryEnding.Reseal && !VoidCaveLeft;
        public bool IsEndingPlaying { get; private set; }
        public float TremorForce => tremorForceRestless;
        public float TremorSeconds => tremorSeconds;

        public int KeystoneCount => keystoneRewards.Length;
        // ExaminedChambers index for the Vault - one past the last Keystone chamber.
        public int VaultChamberIndex => keystoneRewards.Length;
        public int KeystonesTaken => takenKeystones.Count;
        public int SealsHolding => TotalSeals - takenKeystones.Count;

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

        // Reseal: nothing below is pushing back - applied in PlayerHealth.TakeDamage.
        public float DamageTakenMultiplier => Ending == StoryEnding.Reseal ? resealDamageTakenMultiplier : 1f;

        private void Awake()
        {
            if (content == null) Debug.LogError("StoryManager.content is not assigned.");
            if (releaseCinematic == null) Debug.LogError("StoryManager.releaseCinematic is not assigned.");
            if (voidCave == null) Debug.LogError("StoryManager.voidCave is not assigned.");
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
            SetFlash(Color.white, 0f);

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
            if (!SaveService.Instance.HasLoadedData || InputBlocker.IsBlocked || PrestigeCinematic.IsPlaying || IsEndingPlaying) return;

            UpdateWhispers();

            float intensity = TremorIntensity;
            if (intensity <= 0f) return;

            tremorTimer -= Time.deltaTime;
            if (tremorTimer > 0f) return;

            tremorTimer = Mathf.Lerp(tremorIntervalCalm, tremorIntervalRestless, intensity) * UnityEngine.Random.Range(0.7f, 1.3f);
            GameManager.AudioService.Play(SoundId.RockRumble);
            GameManager.CameraShake.Shake(Vector2.up * Mathf.Lerp(tremorForceCalm, tremorForceRestless, intensity), tremorSeconds);
        }

        // 0 = still (a fresh mine, or once the Seal is mended); 1 = the Vault stage with several Resonances behind it.
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

            if (pendingWhispers.Count == 0) whisperTimer = whisperDelay;
            pendingWhispers.Enqueue(line);
        }

        // Opens a conversation once nothing else has the screen, like a whisper.
        public void QueueConversation(string conversationId, List<DialogLine> lines)
        {
            if (lines.Count == 0) return;

            if (pendingConversations.Count == 0 && pendingWhispers.Count == 0) whisperTimer = whisperDelay;
            pendingConversations.Enqueue((conversationId, lines));
        }

        // The Bound speaks in the dialog box, like the curator - but only once nothing else has
        // the screen, so it never talks over a shop panel or the choice it is reacting to.
        private void UpdateWhispers()
        {
            if ((pendingWhispers.Count == 0 && pendingConversations.Count == 0) || ModalTracker.IsAnyModalOpen) return;

            whisperTimer -= Time.deltaTime;
            if (whisperTimer > 0f) return;

            if (pendingConversations.Count > 0)
            {
                var (id, queued) = pendingConversations.Dequeue();
                whisperTimer = whisperDelay;
                GameManager.EventService.Dispatch(new DialogRequestedEvent(id, queued));
                return;
            }

            var lines = new List<DialogLine>();
            while (pendingWhispers.Count > 0) AddVoicePages(lines, pendingWhispers.Dequeue());
            GameManager.EventService.Dispatch(new DialogRequestedEvent(WhisperConversation, lines));
        }

        private void AddVoicePages(List<DialogLine> lines, string entry)
        {
            string color = ColorUtility.ToHtmlStringRGB(content.VoiceColor);
            int first = lines.Count;
            AddPages(lines, RetranslationSeen || Ending != StoryEnding.None ? content.VoiceName : content.UnknownVoiceName, content.VoicePortrait, entry, SoundId.BoundVoice);
            for (int i = first; i < lines.Count; i++) lines[i].Text = $"<color=#{color}><i>{lines[i].Text}</i></color>";
        }

        // ---- Character lines ----

        // Null when the character should use one of their ordinary lines instead.
        public string CuratorGreeting() => PickStoryLine(Ending == StoryEnding.Reseal
            ? content.CuratorGreetingsAfterReseal
            : content.StageLinesFor(content.CuratorGreetings, SpokenStage));

        public string CuratorChatter() => Ending != StoryEnding.None ? null : PickStoryLine(content.StageLinesFor(content.CuratorChatter, SpokenStage));

        public string ShopkeeperChatter() => PickStoryLine(Ending == StoryEnding.Reseal
            ? content.ShopkeeperChatterAfterReseal
            : content.StageLinesFor(content.ShopkeeperChatter, SpokenStage));

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
            AddVoicePages(lines, text.Voice);
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
        public int ResealCost => ResealCostFor(0);

        // The Reseal price once this Keystone is taken too - the Keystone prompt warns with it.
        public int ResealCostIfTaken(int keystoneIndex) =>
            IsKeystoneTaken(keystoneIndex) ? ResealCost : ResealCostFor(keystoneRewards[keystoneIndex]);

        private int ResealCostFor(int extraTakenArtifacts)
        {
            int taken = extraTakenArtifacts;
            foreach (int index in takenKeystones) taken += keystoneRewards[index];
            return resealBaseCost + Mathf.RoundToInt(taken * resealCostPerTakenArtifact);
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

            if (ending == StoryEnding.Release) releaseCinematic.Play();
            else voidCave.PlayReseal();
        }

        // Held by ReleaseCinematic / VoidCave for as long as their sequence runs: no whispers or
        // tremors, and the chambers ignore Interact.
        public void SetEndingPlaying(bool playing) => IsEndingPlaying = playing;

        // Release: the save written here - still at the Vault, the choice un-made - is the one the
        // player continues from, and nothing after it is saved.
        public void BeginRelease()
        {
            ReleaseWitnessed = true;
            GameManager.EventService.Dispatch<StoryProgressChangedEvent>();
            SaveService.Instance.Save();
            SaveService.Instance.SavingSuspended = true;
        }

        // Reseal: the Seal takes back what was taken and the story is over.
        public void CompleteReseal()
        {
            Wallet.Instance.TrySpendArtifacts(ResealCost);
            Ending = StoryEnding.Reseal;
            GameManager.EventService.Dispatch<StoryProgressChangedEvent>();
        }

        public void MarkVoidCaveSpeechHeard() => VoidCaveSpeechHeard = true;

        // The player is home (by the cave's portal, or by any other way out of it): the epilogue
        // plays once the screen is free.
        public void MarkVoidCaveLeft()
        {
            if (VoidCaveLeft) return;

            VoidCaveLeft = true;
            GameManager.EventService.Dispatch<StoryProgressChangedEvent>();
            QueueConversation(EpilogueConversation, BuildLines(content.ResealHomecoming, content.ResealKeystoneOutcomes));
            SaveService.Instance.Save();
        }

        // Entries are narration unless they start with {voice} or {curator}; a {keystones} entry is
        // replaced by the keystoneLines entry for the number of Keystones taken.
        public List<DialogLine> BuildLines(string[] entries, string[] keystoneLines = null)
        {
            var curator = MuseumCuratorController.Instance.Dialog;

            var lines = new List<DialogLine>();
            foreach (string entry in entries)
            {
                if (entry == KeystonesTag) AddPages(lines, string.Empty, null, KeystoneLine(keystoneLines));
                else if (entry.StartsWith(VoiceTag)) AddVoicePages(lines, entry.Substring(VoiceTag.Length));
                else if (entry.StartsWith(CuratorTag)) AddPages(lines, curator.SpeakerName, curator.Portrait, entry.Substring(CuratorTag.Length));
                else AddPages(lines, string.Empty, null, entry);
            }
            return lines;
        }

        public string KeystoneLine(string[] keystoneLines) =>
            keystoneLines == null || keystoneLines.Length == 0 ? string.Empty : keystoneLines[Mathf.Min(KeystonesTaken, keystoneLines.Length - 1)];

        private static void AddPages(List<DialogLine> lines, string speaker, Sprite portrait, string entry, SoundId voice = SoundId.DialogBlip)
        {
            if (string.IsNullOrEmpty(entry)) return;

            foreach (var page in entry.Split(StoryContent.PageSeparator))
            {
                var text = page.Trim();
                if (text.Length > 0) lines.Add(new DialogLine(speaker, portrait, text, voice));
            }
        }

        // A full-screen colour over everything, for the endings. Unscaled time: it must still
        // fade if something froze the clock.
        public IEnumerator Flash(Color color, float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetFlash(color, Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            SetFlash(color, to);
        }

        public void SetFlash(Color color, float alpha)
        {
            flash.color = new Color(color.r, color.g, color.b, alpha);
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
                ReleaseWitnessed = ReleaseWitnessed,
                VoidCaveSpeechHeard = VoidCaveSpeechHeard,
                VoidCaveLeft = VoidCaveLeft,
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
            ReleaseWitnessed = false;
            VoidCaveSpeechHeard = false;
            VoidCaveLeft = false;

            if (data != null)
            {
                deepestLayerIndex = Mathf.Max(deepestLayerIndex, data.DeepestLayerIndex);
                RetranslationSeen = data.RetranslationSeen;
                // Saves from when Release was a stored ending: it is un-made, like every Release now.
                Ending = data.Ending == StoryEnding.Reseal ? StoryEnding.Reseal : StoryEnding.None;
                ReleaseWitnessed = data.ReleaseWitnessed || data.Ending == StoryEnding.Release;
                VoidCaveSpeechHeard = data.VoidCaveSpeechHeard;
                VoidCaveLeft = data.VoidCaveLeft;
                foreach (int index in data.TakenKeystones)
                {
                    if (index >= 0 && index < keystoneRewards.Length) takenKeystones.Add(index);
                }
                foreach (int index in data.ExaminedChambers) examinedChambers.Add(index);
            }

            if (RewoundThisSession)
            {
                RewoundThisSession = false;
                if (Ending == StoryEnding.None) QueueConversation(RewindConversation, BuildLines(content.RewindLines));
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
