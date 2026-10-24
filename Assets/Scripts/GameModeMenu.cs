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
    [Tooltip("Black veil behind the window: dims everything and blocks every click behind it (tapping it closes the window).")]
    public Image fade;
    [Range(0f, 1f)] public float fadeAlpha = 0.65f;
    [Tooltip("Sorting order of the canvas that holds the veil and the window. It is a canvas of its own, above the HUD, so nothing of the HUD can be drawn over them.")]
    public int modalSortingOrder = 200;
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
    CanvasGroup windowGroup;
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
        MoveToModalCanvas();
        if (fade != null)
        {
            var fadeButton = fade.GetComponent<Button>();
            if (fadeButton != null) { fadeButton.onClick.RemoveAllListeners(); fadeButton.onClick.AddListener(CloseWindow); }
            fade.gameObject.SetActive(false);
        }
        if (window != null)
        {
            windowGroup = window.GetComponent<CanvasGroup>();
            if (windowGroup == null) windowGroup = window.gameObject.AddComponent<CanvasGroup>();
        }
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
        foreach (var b in new[] { trainingButton, tournamentButton, onlineButton })
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

    // The veil and the window go to a canvas of their own that is drawn after the whole HUD (a higher sorting order), so no text or button of the
    // HUD, e.g. the win / lose screens, can end up in front of them. It copies the HUD canvas's scaling, so the layout is the same.
    void MoveToModalCanvas()
    {
        if (fade == null && window == null) return;
        var hud = (fade != null ? fade.canvas : window.GetComponentInParent<Canvas>()).rootCanvas;
        var go = new GameObject("ModalCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = Mathf.Max(modalSortingOrder, hud.sortingOrder + 1);
        var scaler = go.GetComponent<CanvasScaler>();
        var source = hud.GetComponent<CanvasScaler>();
        if (source != null)
        {
            scaler.uiScaleMode = source.uiScaleMode;
            scaler.referenceResolution = source.referenceResolution;
            scaler.screenMatchMode = source.screenMatchMode;
            scaler.matchWidthOrHeight = source.matchWidthOrHeight;
        }
        if (fade != null) fade.transform.SetParent(go.transform, false);
        if (window != null) window.SetParent(go.transform, false);
        if (fade != null) fade.transform.SetAsFirstSibling(); // the veil under the window
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

    public void ToggleWindow() { SetWindow(!WindowOpen); }
    public void CloseWindow() { SetWindow(false); }

    void SetWindow(bool open)
    {
        if (WindowOpen == open) return;
        WindowOpen = open;
        PokiEvents.Track("mode_menu", open ? "open" : "close");
    }

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
        PokiEvents.Track("mode", "select", "mode", mode.ToString(), "available", mode == GameMode.Training);
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

        bool windowShown = WindowOpen || currentX > ClosedX + 1f;
        if (fade != null)
        {
            // The veil follows the window: it darkens while the window slides in and lets go while it slides out.
            float progress = Mathf.Clamp01((currentX - ClosedX) / Mathf.Max(1f, OpenX - ClosedX));
            if (fade.gameObject.activeSelf != windowShown) fade.gameObject.SetActive(windowShown);
            var c = fade.color; c.a = fadeAlpha * progress; fade.color = c;
            fade.raycastTarget = WindowOpen; // while the window slides out the veil is only a picture: clicks already reach what is behind it
        }

        if (windowGroup != null) windowGroup.blocksRaycasts = WindowOpen; // same for the window itself

        // The round button stays active under the veil; it breathes a little while the window is closed.
        if (openButton != null)
        {
            if (!windowShown && pulse > 0f) openButton.transform.localScale = openButtonScale * (1f + pulse * Mathf.Sin(Time.unscaledTime * 3f));
            else if (windowShown) openButton.transform.localScale = openButtonScale;
        }
    }

    // The buttons have their name written on them, so their pictures follow the language (which can be set after Awake).
    void ApplyLanguageSprite()
    {
        int lang = (int)I18n.Current;
        if (lang == shownLanguage) return;
        shownLanguage = lang;
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
