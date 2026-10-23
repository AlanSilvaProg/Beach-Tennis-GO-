using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum GameMode { Training, Tournament, Online }

// Main menu overlay (shown only while the game is in the menu state, i.e. not inside a match): a label with the current mode at the top, the round
// "Game mode" button and the mode selection window, which slides in from the left. The gameplay scene stays behind it.
// Everything lives in the scene (object "GameModeMenu" under the HUD canvas) so position, size and colours can be changed by hand.
// Only Training is implemented so far; Tournament and Online are in the window but still do nothing (ModeSelected is raised for them too,
// so a mode only needs a handler to come to life).
public class GameModeMenu : MonoBehaviour
{
    [Header("Menu")]
    [Tooltip("Everything of the menu; hidden while a match is on.")]
    public GameObject root;
    [Tooltip("Name of the current mode, at the top of the screen while the menu is up.")]
    public TMP_Text modeLabel;
    public Button openButton;
    [Tooltip("Picture of the round Game mode button in each language, in the order English, Portuguese, Spanish.")]
    public Sprite[] openButtonSprites = new Sprite[3];

    [Header("Selection window")]
    public RectTransform window;
    public Button trainingButton;
    [Tooltip("Picture of the Training button in each language (English, Portuguese, Spanish).")]
    public Sprite[] trainingSprites = new Sprite[3];
    public Button tournamentButton;
    [Tooltip("Picture of the Tournament button in each language (English, Portuguese, Spanish).")]
    public Sprite[] tournamentSprites = new Sprite[3];
    public Button onlineButton;
    [Tooltip("BACK: closes the window.")]
    public Button backButton;
    public TMP_Text backLabel;
    [Tooltip("How much of the window is on screen when it is open (0.5 = half of it; the other half stays off the left edge).")]
    [Range(0.3f, 1f)] public float visibleFraction = 0.5f;
    [Tooltip("Thickness of the window's frame (canvas units). The three buttons are centred in the space that is on screen and inside the frame.")]
    public float frameThickness = 30f;
    [Tooltip("Seconds the window takes to slide in / out.")]
    [Min(0.05f)] public float slideTime = 0.25f;
    [Tooltip("Size change of the selected mode's button.")]
    public float selectedScale = 1.1f;
    [Tooltip("The Game mode button breathes a little while the window is closed.")]
    [Range(0f, 0.1f)] public float pulse = 0.04f;
    [Tooltip("The mode name at the top (\"TRAINING MODE\") pulses gently.")]
    [Range(0f, 0.1f)] public float labelPulse = 0.035f;

    public GameMode Mode { get; private set; } = GameMode.Training;
    public bool WindowOpen { get; private set; }
    public event Action<GameMode> ModeSelected;

    float velocity;
    float currentX;
    Vector3 openButtonScale = Vector3.one;
    Vector3 modeLabelScale = Vector3.one;
    int shownLanguage = -1;

    void Awake()
    {
        if (modeLabel != null) modeLabelScale = modeLabel.transform.localScale;
        if (openButton != null)
        {
            openButtonScale = openButton.transform.localScale;
            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(ToggleWindow);
        }
        Wire(trainingButton, GameMode.Training);
        Wire(tournamentButton, GameMode.Tournament);
        Wire(onlineButton, GameMode.Online);
        if (backButton != null) { backButton.onClick.RemoveAllListeners(); backButton.onClick.AddListener(CloseWindow); }
        CenterButtons();
        Refresh();
        currentX = ClosedX;
        ApplyX();
    }

    // Horizontal centre (measured from the window's left edge) of the free space: the part of the window that is on screen, minus the frame
    // on its visible side. With the window half way out, that is the middle of the visible half, shifted away from the frame.
    float ButtonCentreX => WindowWidth * (1f - visibleFraction) + (WindowWidth * visibleFraction - frameThickness) * 0.5f;

    void CenterButtons()
    {
        foreach (var b in new[] { trainingButton, tournamentButton, onlineButton, backButton })
        {
            if (b == null) continue;
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, rt.anchorMin.y);
            rt.anchoredPosition = new Vector2(ButtonCentreX, rt.anchoredPosition.y);
        }
    }

    void OnValidate()
    {
        if (window != null) CenterButtons(); // keeps them centred while the size / visible part / frame are tweaked in the inspector
    }

    void Wire(Button b, GameMode mode)
    {
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => Select(mode));
    }

    float WindowWidth => window != null ? window.rect.width : 300f;
    float ClosedX => -WindowWidth - 60f;
    float OpenX => -WindowWidth * (1f - visibleFraction);

    public void ToggleWindow() { WindowOpen = !WindowOpen; }
    public void CloseWindow() { WindowOpen = false; }

    // The menu is only visible outside a match (and on the win screen, without the mode name); hiding it also closes the window.
    public void SetVisible(bool visible, bool showLabel = true)
    {
        if (root != null && root.activeSelf != visible) root.SetActive(visible);
        if (modeLabel != null && modeLabel.gameObject.activeSelf != showLabel) modeLabel.gameObject.SetActive(showLabel);
        if (!visible && (WindowOpen || currentX != ClosedX)) { WindowOpen = false; currentX = ClosedX; velocity = 0f; ApplyX(); }
    }

    public bool IsVisible => root != null && root.activeSelf;

    void Select(GameMode mode)
    {
        ModeSelected?.Invoke(mode);
        if (mode != GameMode.Training) return; // not implemented yet
        Mode = mode;
        Refresh();
        WindowOpen = false;
    }

    // Label text and the selected mode's button: highlighted and disabled (it is the mode being played).
    public void Refresh()
    {
        if (modeLabel != null) modeLabel.text = ModeName(Mode);
        Highlight(trainingButton, Mode == GameMode.Training);
        Highlight(tournamentButton, Mode == GameMode.Tournament);
        Highlight(onlineButton, Mode == GameMode.Online);
        if (trainingButton != null) trainingButton.interactable = Mode != GameMode.Training;
        if (tournamentButton != null) tournamentButton.interactable = Mode != GameMode.Tournament;
        if (onlineButton != null) onlineButton.interactable = Mode != GameMode.Online;
    }

    public static string ModeName(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.Tournament: return I18n.Tr("TOURNAMENT");
            case GameMode.Online: return I18n.Tr("ONLINE VS");
            default: return I18n.Tr("TRAINING MODE");
        }
    }

    void Highlight(Button b, bool selected)
    {
        if (b == null) return;
        b.transform.localScale = Vector3.one * (selected ? selectedScale : 1f);
        var img = b.GetComponent<Image>();
        if (img != null) img.color = selected ? Color.white : new Color(0.82f, 0.82f, 0.82f, 1f);
    }

    void Update()
    {
        ApplyLanguageSprite();
        if (modeLabel != null) modeLabel.transform.localScale = modeLabelScale * (1f + labelPulse * Mathf.Sin(Time.unscaledTime * 2.5f));
        if (window == null) return;
        float target = WindowOpen ? OpenX : ClosedX;
        if (Mathf.Abs(currentX - target) > 0.05f || Mathf.Abs(velocity) > 0.05f)
        {
            currentX = Mathf.SmoothDamp(currentX, target, ref velocity, slideTime, Mathf.Infinity, Time.unscaledDeltaTime);
            ApplyX();
        }
        window.gameObject.SetActive(WindowOpen || currentX > ClosedX + 1f); // nothing is drawn while the window is parked off screen

        if (openButton != null)
        {
            // The window covers the round button: it is hidden while the window is open or still sliding, and breathes a little otherwise.
            bool windowShown = WindowOpen || currentX > ClosedX + 1f;
            if (openButton.gameObject.activeSelf == windowShown) openButton.gameObject.SetActive(!windowShown);
            if (!windowShown && pulse > 0f) openButton.transform.localScale = openButtonScale * (1f + pulse * Mathf.Sin(Time.unscaledTime * 3f));
        }
    }

    // The buttons have their name written on them, so their pictures follow the language (which can be set after Awake).
    void ApplyLanguageSprite()
    {
        int lang = (int)I18n.Current;
        if (lang == shownLanguage) return;
        shownLanguage = lang;
        if (backLabel != null) backLabel.text = I18n.Tr("BACK");
        SetLanguageSprite(openButton, openButtonSprites, lang);
        SetLanguageSprite(trainingButton, trainingSprites, lang);
        SetLanguageSprite(tournamentButton, tournamentSprites, lang);
    }

    static void SetLanguageSprite(Button button, Sprite[] sprites, int lang)
    {
        if (button == null || sprites == null || lang >= sprites.Length || sprites[lang] == null || button.image == null) return;
        button.image.sprite = sprites[lang];
    }

    void ApplyX()
    {
        if (window == null) return;
        var p = window.anchoredPosition;
        window.anchoredPosition = new Vector2(currentX, p.y);
    }
}
