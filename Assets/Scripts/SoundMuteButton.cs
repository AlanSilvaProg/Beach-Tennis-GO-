using UnityEngine;
using UnityEngine.UI;

// Round sound on/off button (top-left corner). The choice is remembered between sessions. Muting sets AudioListener.volume to 0; the Poki ad
// breaks restore it through SoundMuteButton.Volume so an ad that ends never un-mutes a player who chose silence.
[RequireComponent(typeof(Button))]
public class SoundMuteButton : MonoBehaviour
{
    const string PrefKey = "muted";

    public Image icon;
    public Sprite soundOn;
    public Sprite soundOff;

    public static bool Muted { get; private set; }
    public static float Volume => Muted ? 0f : 1f;

    void Awake()
    {
        try { Muted = PlayerPrefs.GetInt(PrefKey, 0) == 1; } catch { }
        if (icon == null) icon = GetComponent<Image>();
        GetComponent<Button>().onClick.AddListener(Toggle);
        Apply();
    }

    void Toggle()
    {
        Muted = !Muted;
        try { PlayerPrefs.SetInt(PrefKey, Muted ? 1 : 0); PlayerPrefs.Save(); } catch { }
        Apply();
    }

    void Apply()
    {
        AudioListener.volume = Volume;
        if (icon != null) icon.sprite = Muted ? soundOff : soundOn;
    }
}
