using System;
using UnityEngine;

// The game's side of the Poki SDK, on top of Poki's official wrapper (PokiUnitySDK.cs + Plugins/WebGL/PokiSDKBridge.jslib, from the Poki Unity
// package; on Poki the page and the SDK calls come from their loader, built from the template's index.json).
//   GameplayStart()   the player started playing (first input of a match, every return to gameplay)
//   GameplayStop()    gameplay was interrupted (match over, ad, window lost focus)
//   CommercialBreak() ad before heading back into gameplay (every start of a match: Poki decides whether an ad really plays)
//   RewardedBreak()   opt-in rewarded ad
// Rules kept here, on top of the wrapper: the gameplay events never repeat and never fire during an ad or while the page is hidden; the game is
// muted and ignores input during an ad (`AdPlaying`); an ad that never reports back is cut after `breakTimeout`.
// Everything is safe without the SDK (editor, other hosts, ad blocker): a break simply finishes at once.
public class PokiService : MonoBehaviour
{
    // Poki id of this game. The SDK calls used here do not take it; it is the key for Poki's per-game services (AUDS) if they are added later.
    public const string GameId = "761a602c-88ba-435a-8df8-d5fdbe420364";

    public static bool AdPlaying { get; private set; }

    [Tooltip("Safety net for a break that never reports back (s, real time).")]
    public float breakTimeout = 90f;

    bool wantPlaying;      // the game wants gameplay to be on
    bool hidden;           // the page / window lost focus
    bool sentPlaying;      // what Poki was told last
    bool loadingFinishedSent;
    float breakStartedAt;
    float volumeBeforeAd = 1f;
    Action onBreakDone;
    Action<bool> onRewardedDone;

    // Static state survives a Play without domain reload.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { AdPlaying = false; } // not named Reset: MonoBehaviour has a magic Reset()

    void Awake()
    {
        var sdk = PokiUnitySDK.Instance;
        sdk.gameLoadingStart();
        try { sdk.init(); } catch (PokiException) { } // already initialising (e.g. a second scene load)
    }

    void Start()
    {
        // Everything of the first scene is up: this is "done loading".
        if (!loadingFinishedSent)
        {
            loadingFinishedSent = true;
            PokiUnitySDK.Instance.gameLoadingFinished();
        }
    }

    // ---- gameplay events: derived from "the game wants to play" + "the page is visible" + "no ad", sent only when the result changes ----
    public void GameplayStart() { wantPlaying = true; Sync(); }
    public void GameplayStop() { wantPlaying = false; Sync(); }

    void OnApplicationFocus(bool focus) { hidden = !focus; Sync(); }
    void OnApplicationPause(bool paused) { hidden = paused; Sync(); }

    void Sync()
    {
        bool target = wantPlaying && !hidden && !AdPlaying;
        if (target == sentPlaying) return;
        sentPlaying = target;
        if (target) PokiUnitySDK.Instance.gameplayStart(); else PokiUnitySDK.Instance.gameplayStop();
    }

    // ---- ads ----
    // `done` runs when the break is over (immediately when there is no ad). Always called, exactly once.
    public void CommercialBreak(Action done)
    {
        if (AdPlaying) return; // already waiting for one
        GameplayStop();
        BeginAd();
#if UNITY_EDITOR
        EndAd();
        done?.Invoke();
#else
        onBreakDone = done;
        var sdk = PokiUnitySDK.Instance;
        sdk.commercialBreakCallBack = OnCommercialBreakFinished;
        sdk.commercialBreak(); // finishes at once (through the callback) when the SDK is not ready or an ad blocker is on
#endif
    }

    // Rewarded ad: `done(true)` only when the player watched it to the end. Without an ad (not in Poki, ad blocker, error) it is `done(false)`:
    // no reward, no message. In the editor the reward is given at once so the feature can be tested. Called exactly once.
    public void RewardedBreak(Action<bool> done)
    {
        if (AdPlaying) return; // never two at the same time (also stops a double click from paying twice)
        GameplayStop();
        BeginAd();
#if UNITY_EDITOR
        EndAd();
        done?.Invoke(true);
#else
        onRewardedDone = done;
        var sdk = PokiUnitySDK.Instance;
        sdk.rewardedBreakCallBack = OnRewardedBreakFinished;
        sdk.rewardedBreak();
#endif
    }

    void BeginAd()
    {
        AdPlaying = true;
        breakStartedAt = Time.realtimeSinceStartup;
        volumeBeforeAd = AudioListener.volume > 0f ? AudioListener.volume : volumeBeforeAd;
        AudioListener.volume = 0f; // muted for the whole break (disable input: see AdPlaying)
    }

    void EndAd()
    {
        AudioListener.volume = SoundMuteButton.Muted ? 0f : volumeBeforeAd;
        AdPlaying = false;
        Sync(); // the page may have been hidden during the ad
    }

    void OnCommercialBreakFinished()
    {
        if (!AdPlaying || onBreakDone == null) return;
        var done = onBreakDone;
        onBreakDone = null;
        EndAd();
        done();
    }

    void OnRewardedBreakFinished(bool withReward)
    {
        if (!AdPlaying || onRewardedDone == null) return;
        var done = onRewardedDone;
        onRewardedDone = null;
        EndAd();
        done(withReward);
    }

    void Update()
    {
        if (!AdPlaying || Time.realtimeSinceStartup - breakStartedAt <= breakTimeout) return;
        // An ad that never reports back must not freeze the game.
        if (onRewardedDone != null) OnRewardedBreakFinished(false);
        else if (onBreakDone != null) OnCommercialBreakFinished();
        else EndAd();
    }

    void OnDestroy()
    {
        if (AdPlaying) { AudioListener.volume = SoundMuteButton.Muted ? 0f : volumeBeforeAd; AdPlaying = false; }
    }
}
