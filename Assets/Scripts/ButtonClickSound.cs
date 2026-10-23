using UnityEngine;
using UnityEngine.UI;

// Plays a short click on every UI button (mute, game mode, mode selection, restart / retry). It creates itself once the scene is loaded and
// hooks the buttons that exist then, so a button only needs to be in the scene. Each click picks one of the clips in Resources/Click at random
// (never the same one twice in a row); drop more clips in that folder to add variations.
public class ButtonClickSound : MonoBehaviour
{
    const float Volume = 0.8f;

    AudioSource source;
    AudioClip[] clips;
    int last = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var clips = Resources.LoadAll<AudioClip>("Click");
        if (clips.Length == 0) return;
        var go = new GameObject("ButtonClickSound");
        var self = go.AddComponent<ButtonClickSound>();
        self.clips = clips;
        self.source = go.AddComponent<AudioSource>();
        self.source.playOnAwake = false;
        self.source.spatialBlend = 0f; // plain 2D sound
        ChannelMute.Attach(self.source, AudioChannel.Sfx);
        foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)) b.onClick.AddListener(self.Play);
    }

    void Play()
    {
        int i = Random.Range(0, clips.Length);
        if (clips.Length > 1 && i == last) i = (i + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
        last = i;
        source.PlayOneShot(clips[i], Volume);
    }
}
