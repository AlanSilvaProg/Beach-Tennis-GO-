using UnityEngine;

// Keeps an AudioSource muted while its channel (voice / music / sound effects) is muted in the settings.
// For the music that is coming: put this on the music's AudioSource (or call ChannelMute.Attach(source, AudioChannel.Music)).
[RequireComponent(typeof(AudioSource))]
public class ChannelMute : MonoBehaviour
{
    public AudioChannel channel;

    AudioSource source;

    public static ChannelMute Attach(AudioSource source, AudioChannel channel)
    {
        if (source == null) return null;
        var existing = source.GetComponent<ChannelMute>();
        var cm = existing != null ? existing : source.gameObject.AddComponent<ChannelMute>();
        cm.channel = channel;
        cm.Apply();
        return cm;
    }

    void OnEnable() { GameSettings.Changed += Apply; Apply(); }
    void OnDisable() { GameSettings.Changed -= Apply; }

    void Apply()
    {
        if (source == null) source = GetComponent<AudioSource>();
        source.mute = GameSettings.IsMuted(channel);
    }
}
