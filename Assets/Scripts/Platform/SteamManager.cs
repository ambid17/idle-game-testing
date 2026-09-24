#if !DISABLESTEAMWORKS
using Steamworks;
#endif
using UnityEngine;

namespace Platform
{
    // Owns the Steamworks API lifetime: initializes it on Awake, pumps callbacks every frame, and
    // shuts it down on destroy. Lives on the GameManager GameObject and is accessed via
    // GameManager.SteamManager. If Steam isn't running (or the platform doesn't support
    // Steamworks), IsInitialized stays false and callers should skip any Steam-only features.
    public class SteamManager : MonoBehaviour
    {
        // TODO: replace with the real App ID once it's issued in Steamworks. 480 is Valve's public
        // "Spacewar" test app, which any Steam account can use during development.
        private const uint AppId = 5323550;
        private const uint PlaceholderAppId = 5323550;

        public bool IsInitialized { get; private set; }

#if !DISABLESTEAMWORKS
        private SteamAPIWarningMessageHook_t warningMessageHook;

        private void Awake()
        {
            if (!Packsize.Test())
            {
                Debug.LogError("Steamworks.NET: Packsize test failed - the wrong Steamworks.NET assembly is being used for this platform.");
                return;
            }

            if (!DllCheck.Test())
            {
                Debug.LogError("Steamworks.NET: DllCheck test failed - one or more Steamworks binaries seem to be the wrong version.");
                return;
            }

#if !UNITY_EDITOR
            // If the exe was launched outside of Steam, relaunch through Steam so the overlay,
            // ownership check, and cloud saves all work. Skipped for the placeholder ID so a
            // pre-release build doesn't relaunch as Spacewar.
            if (AppId != PlaceholderAppId && SteamAPI.RestartAppIfNecessary(new AppId_t(AppId)))
            {
                Application.Quit();
                return;
            }
#endif

            IsInitialized = SteamAPI.Init();
            if (!IsInitialized)
            {
                // Not an error: expected whenever the Steam client isn't running (e.g. in the Editor).
                Debug.LogWarning("SteamAPI.Init() failed - Steam client not running or steam_appid.txt missing. Steam features disabled.");
                return;
            }

            warningMessageHook = OnSteamApiWarning;
            SteamClient.SetWarningMessageHook(warningMessageHook);
        }

        private void Update()
        {
            if (IsInitialized)
            {
                SteamAPI.RunCallbacks();
            }
        }

        private void OnDestroy()
        {
            if (IsInitialized)
            {
                SteamAPI.Shutdown();
                IsInitialized = false;
            }
        }

        [AOT.MonoPInvokeCallback(typeof(SteamAPIWarningMessageHook_t))]
        private static void OnSteamApiWarning(int severity, System.Text.StringBuilder message)
        {
            Debug.LogWarning($"Steamworks: {message}");
        }
#endif
    }
}
