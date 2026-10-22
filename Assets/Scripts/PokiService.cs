using System;
using System.Runtime.InteropServices;
using UnityEngine;

// Poki SDK integration (WebGL). The SDK script and the page-side logic live in the WebGL template; this class is the game's side:
//   GameplayStart()   the player started playing (first input of a match)
//   GameplayStop()    gameplay was interrupted (match over, ad, focus lost is handled by the page)
//   CommercialBreak() ad before heading back into gameplay (here: from the match-over screen into the next match)
// Everything is safe without the SDK (editor, other hosts, ad blocker): the break simply finishes at once, the game never waits for it.
// While an ad plays the game is muted and ignores input (`AdPlaying`); no SDK event is sent in that time.
// The object must be named "PokiService": the page calls back with SendMessage("PokiService", ...).
public class PokiService : MonoBehaviour
{
    public static bool AdPlaying { get; private set; }

    [Tooltip("Safety net for a break that never reports back (s, real time).")]
    public float breakTimeout = 90f;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void PokiGameplayStart();
    [DllImport("__Internal")] static extern void PokiGameplayStop();
    [DllImport("__Internal")] static extern int PokiCommercialBreak();
#else
    static void PokiGameplayStart() { }
    static void PokiGameplayStop() { }
    static int PokiCommercialBreak() { return 0; }
#endif

    bool playing;
    float breakStartedAt;
    float volumeBeforeAd = 1f;
    Action onBreakDone;

    // Static state survives a Play without domain reload.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { AdPlaying = false; } // not named Reset: MonoBehaviour has a magic Reset()

    public void GameplayStart()
    {
        if (AdPlaying || playing) return; // never twice in a row, never during an ad
        playing = true;
        PokiGameplayStart();
    }

    public void GameplayStop()
    {
        if (!playing) return;
        playing = false;
        PokiGameplayStop();
    }

    // `done` runs when the break is over (immediately when there is no ad). Always called, exactly once.
    public void CommercialBreak(Action done)
    {
        if (AdPlaying) return; // already waiting for one
        GameplayStop();
        onBreakDone = done;
        AdPlaying = true;
        breakStartedAt = Time.realtimeSinceStartup;
        if (PokiCommercialBreak() == 0) Finish();
    }

    // ---- called by the page (SendMessage) ----
    public void OnAdStarted()
    {
        if (!AdPlaying) return;
        volumeBeforeAd = AudioListener.volume > 0f ? AudioListener.volume : volumeBeforeAd;
        AudioListener.volume = 0f; // audio is muted while the ad plays
    }

    public void OnBreakFinished()
    {
        if (AdPlaying) Finish();
    }

    void Finish()
    {
        AudioListener.volume = volumeBeforeAd;
        AdPlaying = false;
        var done = onBreakDone;
        onBreakDone = null;
        done?.Invoke();
    }

    void Update()
    {
        if (AdPlaying && Time.realtimeSinceStartup - breakStartedAt > breakTimeout) Finish();
    }

    void OnDestroy()
    {
        if (AdPlaying) { AudioListener.volume = volumeBeforeAd; AdPlaying = false; }
    }
}
