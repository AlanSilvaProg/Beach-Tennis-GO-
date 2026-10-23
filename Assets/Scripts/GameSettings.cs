using System;
using UnityEngine;

public enum AudioChannel { Voice, Music, Sfx }

// Player settings, kept between sessions (PlayerPrefs, always inside try/catch: they can be unavailable, e.g. in a private window).
// Points / sets are read when a match starts; the mutes are applied at once by ChannelMute.
public static class GameSettings
{
    public static readonly int[] PointsOptions = { 5, 7, 11, 15, 21 };
    public static readonly int[] SetsOptions = { 1, 2, 3 };

    static int pointsIndex = 2;  // 11 points
    static int setsIndex;        // 1 set
    static bool voiceMuted, musicMuted, sfxMuted;
    static bool loaded;

    public static event Action Changed;

    public static int PointsToWin { get { Load(); return PointsOptions[pointsIndex]; } }
    public static int SetsToWin { get { Load(); return SetsOptions[setsIndex]; } }

    public static bool IsMuted(AudioChannel channel)
    {
        Load();
        switch (channel)
        {
            case AudioChannel.Voice: return voiceMuted;
            case AudioChannel.Music: return musicMuted;
            default: return sfxMuted;
        }
    }

    public static void SetMuted(AudioChannel channel, bool muted)
    {
        Load();
        switch (channel)
        {
            case AudioChannel.Voice: voiceMuted = muted; break;
            case AudioChannel.Music: musicMuted = muted; break;
            default: sfxMuted = muted; break;
        }
        Save();
    }

    // direction: +1 / -1 along the list of options (stops at the ends).
    public static void StepPoints(int direction) { Load(); pointsIndex = Mathf.Clamp(pointsIndex + direction, 0, PointsOptions.Length - 1); Save(); }
    public static void StepSets(int direction) { Load(); setsIndex = Mathf.Clamp(setsIndex + direction, 0, SetsOptions.Length - 1); Save(); }

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            pointsIndex = Mathf.Clamp(PlayerPrefs.GetInt("pointsIndex", pointsIndex), 0, PointsOptions.Length - 1);
            setsIndex = Mathf.Clamp(PlayerPrefs.GetInt("setsIndex", setsIndex), 0, SetsOptions.Length - 1);
            voiceMuted = PlayerPrefs.GetInt("voiceMuted", 0) == 1;
            musicMuted = PlayerPrefs.GetInt("musicMuted", 0) == 1;
            sfxMuted = PlayerPrefs.GetInt("sfxMuted", 0) == 1;
        }
        catch { }
    }

    static void Save()
    {
        try
        {
            PlayerPrefs.SetInt("pointsIndex", pointsIndex);
            PlayerPrefs.SetInt("setsIndex", setsIndex);
            PlayerPrefs.SetInt("voiceMuted", voiceMuted ? 1 : 0);
            PlayerPrefs.SetInt("musicMuted", musicMuted ? 1 : 0);
            PlayerPrefs.SetInt("sfxMuted", sfxMuted ? 1 : 0);
            PlayerPrefs.Save();
        }
        catch { }
        Changed?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { loaded = false; Changed = null; } // static state survives a Play without domain reload
}
