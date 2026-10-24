using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Full-screen settings window (opened by the gear button): points to win, sets to win, and a mute switch for voice, music and sound effects.
// Everything lives in the scene (object "SettingsWindow" under the HUD canvas); this script only wires the buttons and the texts.
// Points / sets count from the next match on.
public class SettingsWindow : MonoBehaviour
{
    [Header("Window")]
    public GameObject window;
    [Tooltip("The gear button; visible only when the game says so (menu and result screens), never under the open window.")]
    public Button openButton;
    public Button closeButton;

    [Tooltip("The block with the options: as wide as the screen allows, up to Max Content Width, centred.")]
    public RectTransform content;
    public float maxContentWidth = 760f;
    public float sideMargin = 30f;

    [Header("Texts")]
    public TMP_Text title;
    public TMP_Text closeLabel;

    [Header("Match rules")]
    public TMP_Text pointsLabel;
    public TMP_Text pointsValue;
    public Button pointsLess, pointsMore;
    public TMP_Text setsLabel;
    public TMP_Text setsValue;
    public Button setsLess, setsMore;

    [Header("Sound")]
    public TMP_Text voiceLabel;
    public Button voiceButton;
    public TMP_Text voiceState;
    public TMP_Text musicLabel;
    public Button musicButton;
    public TMP_Text musicState;
    public TMP_Text sfxLabel;
    public Button sfxButton;
    public TMP_Text sfxState;

    [Header("Look of the switches")]
    public Color onColor = new Color(0.2f, 0.66f, 0.3f);
    public Color offColor = new Color(0.45f, 0.5f, 0.58f);

    public bool IsOpen => window != null && window.activeSelf;

    void Awake()
    {
        Hook(openButton, Open);
        Hook(closeButton, Close);
        Hook(pointsLess, () => { GameSettings.StepPoints(-1); Refresh(); PokiEvents.Track("settings", "change", "setting", "points_to_win", "value", GameSettings.PointsToWin); });
        Hook(pointsMore, () => { GameSettings.StepPoints(+1); Refresh(); PokiEvents.Track("settings", "change", "setting", "points_to_win", "value", GameSettings.PointsToWin); });
        Hook(setsLess, () => { GameSettings.StepSets(-1); Refresh(); PokiEvents.Track("settings", "change", "setting", "sets_to_win", "value", GameSettings.SetsToWin); });
        Hook(setsMore, () => { GameSettings.StepSets(+1); Refresh(); PokiEvents.Track("settings", "change", "setting", "sets_to_win", "value", GameSettings.SetsToWin); });
        Hook(voiceButton, () => Toggle(AudioChannel.Voice));
        Hook(musicButton, () => Toggle(AudioChannel.Music));
        Hook(sfxButton, () => Toggle(AudioChannel.Sfx));
        if (window != null) window.SetActive(false);
    }

    static void Hook(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(action);
    }

    void Update() { FitContent(); }

    void FitContent()
    {
        if (content == null || !IsOpen) return;
        float available = ((RectTransform)content.parent).rect.width - 2f * sideMargin;
        float width = Mathf.Max(200f, Mathf.Min(available, maxContentWidth));
        content.anchorMin = content.anchorMax = new Vector2(0.5f, content.anchorMin.y);
        if (!Mathf.Approximately(content.sizeDelta.x, width)) content.sizeDelta = new Vector2(width, content.sizeDelta.y);
    }

    public void Open() { if (window == null) return; PokiEvents.Track("settings", "open"); window.SetActive(true); window.transform.SetAsLastSibling(); FitContent(); Refresh(); }
    public void Close() { if (window != null && window.activeSelf) PokiEvents.Track("settings", "close"); if (window != null) window.SetActive(false); }

    // The gear is hidden while the window covers the screen.
    public void SetButtonVisible(bool visible)
    {
        if (openButton == null) return;
        visible &= !IsOpen;
        if (openButton.gameObject.activeSelf != visible) openButton.gameObject.SetActive(visible);
    }

    void Toggle(AudioChannel channel)
    {
        GameSettings.SetMuted(channel, !GameSettings.IsMuted(channel));
        Refresh();
        PokiEvents.Track("settings", "change", "setting", channel.ToString().ToLowerInvariant() + "_muted", "value", GameSettings.IsMuted(channel));
    }

    // Texts follow the language (it can be set after Awake), values follow the settings.
    void Refresh()
    {
        Set(title, I18n.Tr("SETTINGS"));
        Set(closeLabel, I18n.Tr("BACK"));
        Set(pointsLabel, I18n.Tr("Points to win"));
        Set(pointsValue, GameSettings.PointsToWin.ToString());
        Set(setsLabel, I18n.Tr("Sets to win"));
        Set(setsValue, GameSettings.SetsToWin.ToString());
        Set(voiceLabel, I18n.Tr("Voice"));
        Set(musicLabel, I18n.Tr("Music"));
        Set(sfxLabel, I18n.Tr("Sound effects"));
        Show(voiceButton, voiceState, AudioChannel.Voice);
        Show(musicButton, musicState, AudioChannel.Music);
        Show(sfxButton, sfxState, AudioChannel.Sfx);
        if (pointsLess != null) pointsLess.interactable = GameSettings.PointsToWin > GameSettings.PointsOptions[0];
        if (pointsMore != null) pointsMore.interactable = GameSettings.PointsToWin < GameSettings.PointsOptions[GameSettings.PointsOptions.Length - 1];
        if (setsLess != null) setsLess.interactable = GameSettings.SetsToWin > GameSettings.SetsOptions[0];
        if (setsMore != null) setsMore.interactable = GameSettings.SetsToWin < GameSettings.SetsOptions[GameSettings.SetsOptions.Length - 1];
    }

    static void Set(TMP_Text t, string value) { if (t != null) t.text = value; }

    // The switch says whether the channel is heard (ON) or muted (OFF).
    void Show(Button button, TMP_Text state, AudioChannel channel)
    {
        bool heard = !GameSettings.IsMuted(channel);
        Set(state, heard ? I18n.Tr("ON") : I18n.Tr("OFF"));
        if (button == null) return;
        var colors = button.colors;
        colors.normalColor = heard ? onColor : offColor;
        colors.highlightedColor = Color.Lerp(colors.normalColor, Color.white, 0.2f);
        colors.pressedColor = Color.Lerp(colors.normalColor, Color.black, 0.25f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
    }
}
