using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

// Background music: one looping track (StreamingAssets/Music/music.mp3), very quiet, and quieter still while a match is on.
// It is an object in the scene ("MusicPlayer"): set both volumes there. It loads the file after the game is up, so it adds nothing to the
// first download. The "Music" switch in the settings mutes it (ChannelMute).
// The track is mono 32 kHz 64 kbps on purpose: the browser decodes the whole file into memory, and this keeps that around 23 MB.
[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    const string File = "Music/music.mp3";

    [Tooltip("Volume in the menu and on the win / lose screens (0..1).")]
    [Range(0f, 1f)] public float menuVolume = 0.10f;
    [Tooltip("Volume while a match is on (serve, rally, point pause).")]
    [Range(0f, 1f)] public float matchVolume = 0.04f;
    [Tooltip("Seconds the volume takes to move between the two.")]
    public float fadeTime = 1.5f;

    AudioSource source;
    GameManager game;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f; // plain 2D sound
        source.volume = 0f;       // fades in once the track is loaded
        ChannelMute.Attach(source, AudioChannel.Music);
    }

    void Start() { StartCoroutine(Load()); }

    // The track was made by a download (about 23 MB decoded): free it when the scene goes, the next scene loads it again.
    void OnDestroy() { if (source != null && source.clip != null) Destroy(source.clip); }

    IEnumerator Load()
    {
        string path = Application.streamingAssetsPath + "/" + File;
        string url = path.Contains("://") ? path : new Uri(path).AbsoluteUri;
        using (var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break; // no music: the game goes on without it
            var clip = DownloadHandlerAudioClip.GetContent(req);
            clip.name = "music";
            source.clip = clip;
            source.Play();
        }
    }

    void Update()
    {
        if (source == null || source.clip == null) return;
        if (game == null) game = FindFirstObjectByType<GameManager>();
        float target = game != null && game.MatchOn ? matchVolume : menuVolume;
        source.volume = Mathf.MoveTowards(source.volume, target, Mathf.Abs(menuVolume - matchVolume) / Mathf.Max(0.1f, fadeTime) * Time.unscaledDeltaTime);
    }
}
