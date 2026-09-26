using System.Collections;
using System.Collections.Generic;
using Events;
using MapGeneration;
using Settings;
using UnityEngine;

namespace Audio
{
    // Plays every sound effect and the background music. Lives on the GameManager GameObject and
    // is accessed via GameManager.AudioService. Most sounds are driven from here by listening to
    // existing gameplay events (see OnEnable), so systems don't need to know audio exists - only
    // continuous/edge-triggered things with no event (mining hits, the jetpack loop, warnings)
    // call Play/SetLoopActive directly.
    //
    // Volume: Master is AudioListener.volume (SettingsService); SFX/Music are applied per-source
    // here from SettingsService, and ApplyVolumes() is called whenever those settings change.
    //
    // SFX are plain 2D sources. World-positioned sounds (hazards) go through PlayAt, which fades
    // them out with distance from the listener and pans them left/right, so an automaton setting
    // off a hazard far away doesn't sound like it went off in the player's ear.
    public class AudioService : MonoBehaviour
    {
        [SerializeField] private SoundLibrary library;
        [Tooltip("Where the player hears from - the main camera.")]
        [SerializeField] private Transform listener;
        [Tooltip("How many sound effects can overlap before the oldest gets cut off.")]
        [SerializeField] private int sfxVoiceCount = 16;
        [Tooltip("PlayAt sounds within this world distance of the listener play at full volume...")]
        [SerializeField] private float fullVolumeDistance = 8f;
        [Tooltip("...and fade to silent by this distance.")]
        [SerializeField] private float silentDistance = 20f;
        [Range(0f, 1f)]
        [SerializeField] private float maxStereoPan = 0.6f;
        [SerializeField] private float musicFadeSeconds = 1.5f;

        private readonly List<AudioSource> sfxVoices = new();
        private readonly Dictionary<SoundId, AudioSource> loopSources = new();
        private readonly Dictionary<SoundId, float> lastPlayedAt = new();
        private Transform sourceRoot;
        private AudioSource musicSource;
        private Coroutine musicFade;
        private float musicFadeFactor = 1f;
        private int nextVoice;
        private int lastShieldCharges = -1;
        // Load restores (queued prestige upgrades, offline processing completions...) replay the
        // same events live gameplay does - event-driven sounds stay muted until loading finishes.
        private bool hasLoaded;

        private static float SfxVolume => SettingsService.Instance.SFXVolume;
        private static float MusicVolume => SettingsService.Instance.MusicVolume;

        private void Awake()
        {
            if (library == null) Debug.LogError($"{nameof(AudioService)} on {name} is missing its SoundLibrary reference.");
            if (listener == null) Debug.LogError($"{nameof(AudioService)} on {name} is missing its listener reference.");
            library.Validate();

            sourceRoot = new GameObject("AudioSources").transform;
            sourceRoot.SetParent(transform, false);
            for (int i = 0; i < sfxVoiceCount; i++)
            {
                sfxVoices.Add(CreateSource($"SFX {i}"));
            }
            musicSource = CreateSource("Music");
            musicSource.loop = true;
        }

        private void Start()
        {
            if (library.Music != null) PlayMusic(library.Music);
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<LoadCompletedEvent>(OnLoadCompleted);
            GameManager.EventService.Add<BlockMinedEvent>(OnBlockMined);
            GameManager.EventService.Add<CustomBlockTriggeredEvent>(OnCustomBlockTriggered);
            GameManager.EventService.Add<ExplosiveDetonatedEvent>(OnExplosiveDetonated);
            GameManager.EventService.Add<FallingRockImpactEvent>(OnFallingRockImpact);
            GameManager.EventService.Add<PlayerDamagedEvent>(OnPlayerDamaged);
            GameManager.EventService.Add<ShieldChargeChangedEvent>(OnShieldChargeChanged);
            GameManager.EventService.Add<PlayerDiedEvent>(OnPlayerDied);
            GameManager.EventService.Add<PlayerRevivedEvent>(OnPlayerRevived);
            GameManager.EventService.Add<SellRequestedEvent>(OnSellRequested);
            GameManager.EventService.Add<SellGoodsRequestedEvent>(OnSellGoodsRequested);
            GameManager.EventService.Add<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Add<PrestigeUpgradeQueuedEvent>(OnPrestigeUpgradeQueued);
            GameManager.EventService.Add<PrestigeCompletedEvent>(OnPrestigeCompleted);
            GameManager.EventService.Add<ProcessingJobStartedEvent>(OnProcessingJobStarted);
            GameManager.EventService.Add<ProcessingJobCompletedEvent>(OnProcessingJobCompleted);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<LoadCompletedEvent>(OnLoadCompleted);
            GameManager.EventService.Remove<BlockMinedEvent>(OnBlockMined);
            GameManager.EventService.Remove<CustomBlockTriggeredEvent>(OnCustomBlockTriggered);
            GameManager.EventService.Remove<ExplosiveDetonatedEvent>(OnExplosiveDetonated);
            GameManager.EventService.Remove<FallingRockImpactEvent>(OnFallingRockImpact);
            GameManager.EventService.Remove<PlayerDamagedEvent>(OnPlayerDamaged);
            GameManager.EventService.Remove<ShieldChargeChangedEvent>(OnShieldChargeChanged);
            GameManager.EventService.Remove<PlayerDiedEvent>(OnPlayerDied);
            GameManager.EventService.Remove<PlayerRevivedEvent>(OnPlayerRevived);
            GameManager.EventService.Remove<SellRequestedEvent>(OnSellRequested);
            GameManager.EventService.Remove<SellGoodsRequestedEvent>(OnSellGoodsRequested);
            GameManager.EventService.Remove<UpgradePurchasedEvent>(OnUpgradePurchased);
            GameManager.EventService.Remove<PrestigeUpgradeQueuedEvent>(OnPrestigeUpgradeQueued);
            GameManager.EventService.Remove<PrestigeCompletedEvent>(OnPrestigeCompleted);
            GameManager.EventService.Remove<ProcessingJobStartedEvent>(OnProcessingJobStarted);
            GameManager.EventService.Remove<ProcessingJobCompletedEvent>(OnProcessingJobCompleted);
        }

        #region Public API

        // Non-positional one-shot (UI, the player's own actions).
        public void Play(SoundId id, float volumeScale = 1f)
        {
            PlayInternal(id, volumeScale, 0f);
        }

        // One-shot at a world position - quieter/panned with distance from the listener, and
        // skipped entirely beyond silentDistance.
        public void PlayAt(SoundId id, Vector3 worldPosition)
        {
            Vector2 offset = worldPosition - listener.position;
            float distance = offset.magnitude;
            float attenuation = 1f - Mathf.InverseLerp(fullVolumeDistance, silentDistance, distance);
            if (attenuation <= 0f) return;

            float pan = Mathf.Clamp(offset.x / silentDistance, -1f, 1f) * maxStereoPan;
            PlayInternal(id, attenuation, pan);
        }

        public void PlayAtCell(SoundId id, int layerIndex, int x, int y)
        {
            PlayAt(id, GameManager.MapGenerationService.CellToWorldCenter(layerIndex, x, y));
        }

        // For sustained sounds (jetpack thrust) - safe to call every frame with the current state;
        // it only starts/stops the loop when that state actually changes. Uses the entry's first clip.
        public void SetLoopActive(SoundId id, bool active)
        {
            loopSources.TryGetValue(id, out var source);
            if (!active)
            {
                if (source != null && source.isPlaying) source.Stop();
                return;
            }

            if (source != null && source.isPlaying) return;
            if (!library.TryGet(id, out var entry)) return;

            if (source == null)
            {
                source = CreateSource($"Loop {id}");
                source.loop = true;
                loopSources[id] = source;
            }
            source.clip = entry.Clips[0];
            source.volume = entry.Volume * SfxVolume;
            source.Play();
        }

        // Crossfades from whatever is playing. Unscaled time, so it still fades while paused.
        public void PlayMusic(AudioClip clip)
        {
            if (musicSource.clip == clip && musicSource.isPlaying) return;
            if (musicFade != null) StopCoroutine(musicFade);
            musicFade = StartCoroutine(CrossfadeMusic(clip));
        }

        // Called by SettingsService when the SFX/Music sliders change. One-shots already playing
        // keep their volume; loops and music update immediately.
        public void ApplyVolumes()
        {
            musicSource.volume = MusicVolume * musicFadeFactor;
            foreach (var kvp in loopSources)
            {
                if (library.TryGet(kvp.Key, out var entry)) kvp.Value.volume = entry.Volume * SfxVolume;
            }
        }

        #endregion

        private void PlayInternal(SoundId id, float volumeScale, float pan)
        {
            if (!library.TryGet(id, out var entry)) return;

            float now = Time.unscaledTime;
            if (lastPlayedAt.TryGetValue(id, out float last) && now - last < entry.MinInterval) return;
            lastPlayedAt[id] = now;

            var clip = entry.Clips[Random.Range(0, entry.Clips.Length)];
            var voice = NextVoice();
            voice.clip = clip;
            voice.volume = entry.Volume * volumeScale * SfxVolume;
            voice.pitch = 1f + Random.Range(-entry.PitchVariance, entry.PitchVariance);
            voice.panStereo = pan;
            voice.Play();
        }

        // First idle voice after the last one used; if all are busy, cuts off the oldest.
        private AudioSource NextVoice()
        {
            for (int i = 0; i < sfxVoices.Count; i++)
            {
                int index = (nextVoice + i) % sfxVoices.Count;
                if (!sfxVoices[index].isPlaying)
                {
                    nextVoice = (index + 1) % sfxVoices.Count;
                    return sfxVoices[index];
                }
            }

            var oldest = sfxVoices[nextVoice];
            nextVoice = (nextVoice + 1) % sfxVoices.Count;
            return oldest;
        }

        private AudioSource CreateSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(sourceRoot, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private IEnumerator CrossfadeMusic(AudioClip clip)
        {
            if (musicSource.isPlaying)
            {
                yield return FadeMusic(musicFadeFactor, 0f);
                musicSource.Stop();
            }

            musicSource.clip = clip;
            if (clip == null) yield break;

            musicSource.Play();
            yield return FadeMusic(0f, 1f);
            musicFade = null;
        }

        private IEnumerator FadeMusic(float from, float to)
        {
            for (float t = 0f; t < musicFadeSeconds; t += Time.unscaledDeltaTime)
            {
                musicFadeFactor = Mathf.Lerp(from, to, t / musicFadeSeconds);
                musicSource.volume = MusicVolume * musicFadeFactor;
                yield return null;
            }
            musicFadeFactor = to;
            musicSource.volume = MusicVolume * musicFadeFactor;
        }

        #region Event -> sound mapping

        private void OnLoadCompleted() => hasLoaded = true;

        private void PlayEvent(SoundId id)
        {
            if (hasLoaded) Play(id);
        }

        private void PlayEventAtCell(SoundId id, int layerIndex, int x, int y)
        {
            if (hasLoaded) PlayAtCell(id, layerIndex, x, y);
        }

        // Player mining only (see BlockMinedEvent). Hazards get their sound from
        // CustomBlockTriggeredEvent instead, which also covers automatons setting them off.
        private void OnBlockMined(BlockMinedEvent e)
        {
            switch (e.BlockType.Category)
            {
                case BlockCategory.Dirt: PlayEvent(SoundId.MineDirt); break;
                case BlockCategory.Ore: PlayEvent(SoundId.MineOre); break;
                case BlockCategory.Artifact: PlayEvent(SoundId.ArtifactFound); break;
                case BlockCategory.PowerUp: PlayEvent(SoundId.PowerUpCollected); break;
            }
        }

        private void OnCustomBlockTriggered(CustomBlockTriggeredEvent e)
        {
            switch (e.Hazard)
            {
                case CustomBehavior.Explosive: PlayEventAtCell(SoundId.ExplosiveFuse, e.LayerIndex, e.X, e.Y); break;
                case CustomBehavior.FallingRock: PlayEventAtCell(SoundId.RockRumble, e.LayerIndex, e.X, e.Y); break;
                case CustomBehavior.GasPocket: PlayEventAtCell(SoundId.GasRelease, e.LayerIndex, e.X, e.Y); break;
                case CustomBehavior.Lava: PlayEventAtCell(SoundId.LavaSizzle, e.LayerIndex, e.X, e.Y); break;
            }
        }

        private void OnExplosiveDetonated(ExplosiveDetonatedEvent e) => PlayEventAtCell(SoundId.Explosion, e.LayerIndex, e.X, e.Y);

        private void OnFallingRockImpact(FallingRockImpactEvent e)
        {
            if (e.IsLanding) PlayEventAtCell(SoundId.RockLand, e.LayerIndex, e.X, e.Y);
        }

        private void OnPlayerDamaged(PlayerDamagedEvent e) => PlayEvent(SoundId.PlayerHurt);

        // Fires on both consume and regen - only a drop means a hit was absorbed.
        private void OnShieldChargeChanged(ShieldChargeChangedEvent e)
        {
            if (lastShieldCharges >= 0 && e.Current < lastShieldCharges) PlayEvent(SoundId.ShieldBlock);
            lastShieldCharges = e.Current;
        }

        private void OnPlayerDied(PlayerDiedEvent e)
        {
            SetLoopActive(SoundId.Jetpack, false);
            PlayEvent(SoundId.PlayerDeath);
        }

        private void OnPlayerRevived() => PlayEvent(SoundId.PlayerRevive);
        private void OnSellRequested(SellRequestedEvent e) => PlayEvent(SoundId.Sell);
        private void OnSellGoodsRequested(SellGoodsRequestedEvent e) => PlayEvent(SoundId.Sell);
        private void OnUpgradePurchased(UpgradePurchasedEvent e) => PlayEvent(SoundId.UpgradePurchased);
        private void OnPrestigeUpgradeQueued(PrestigeUpgradeQueuedEvent e) => PlayEvent(SoundId.PrestigeUpgradeQueued);
        private void OnPrestigeCompleted(PrestigeCompletedEvent e) => PlayEvent(SoundId.Prestige);
        private void OnProcessingJobStarted(ProcessingJobStartedEvent e) => PlayEvent(SoundId.ProcessingStarted);
        private void OnProcessingJobCompleted(ProcessingJobCompletedEvent e) => PlayEvent(SoundId.ProcessingCompleted);

        #endregion
    }
}
