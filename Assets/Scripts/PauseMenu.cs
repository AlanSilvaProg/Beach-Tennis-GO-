using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The pause button (top right, the mirror of the sound button at the top left; it is a grey, dead version of itself when the game cannot be
// paused: menu, result screens, ads, online matches) and the window it opens: the screen fades to black and two buttons, RESUME and EXIT GAME, are shown while
// the game stands still (GameManager.SetPaused stops the time and the sounds, the music goes on). On a computer Escape (or P) pauses and resumes too.
// Everything is made at run time on its own overlay canvas: nothing to wire in the scene. The picture of the button is Resources/pause-button.
public class PauseMenu : MonoBehaviour
{
    [Header("Button")]
    [Tooltip("Size of the pause button and where its centre is, from the top right corner. The default is the mirror of the sound button (HUD canvas units).")]
    public float buttonSize = 60f;
    public Vector2 buttonCentre = new Vector2(55f, 55f);

    [Header("Window")]
    [Range(0f, 1f)] public float fadeToBlack = 0.82f;
    [Tooltip("Seconds the fade takes.")]
    public float fadeTime = 0.25f;
    public Vector2 optionSize = new Vector2(620f, 150f);
    public Color resumeColor = new Color(0.2f, 0.66f, 0.3f);
    public Color exitColor = new Color(0.82f, 0.27f, 0.27f);

    GameManager game;
    RectTransform root, window, pauseButton;
    Button pauseButtonControl;
    SettingsWindow settings;
    Image dim;
    TextMeshProUGUI title, resumeLabel, exitLabel;
    float shown;          // 0..1: how far the window has faded in
    bool built;

    public static PauseMenu Create(GameManager game, TMP_FontAsset font, Material fontMaterial)
    {
        var go = new GameObject("PauseMenu");
        var menu = go.AddComponent<PauseMenu>();
        menu.game = game;
        menu.Build(font, fontMaterial);
        return menu;
    }

    void Build(TMP_FontAsset font, Material fontMaterial)
    {
        var canvasGo = new GameObject("Pause Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95; // over the HUD and the effects, under nothing
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)canvasGo.transform;

        // The button: in the HUD, next to the sound button (top left), so that both have the same size and the same distance from their corner.
        var sprite = Resources.Load<Sprite>("pause-button");
        var inactive = Resources.Load<Sprite>("pause-button-disabled");
        var mute = FindFirstObjectByType<SoundMuteButton>();
        Transform buttonParent = mute != null ? mute.transform.parent : root;
        var buttonGo = new GameObject("PauseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(buttonParent, false);
        pauseButton = (RectTransform)buttonGo.transform;
        pauseButton.anchorMin = pauseButton.anchorMax = new Vector2(1f, 1f);
        pauseButton.pivot = new Vector2(0.5f, 0.5f);
        pauseButton.sizeDelta = new Vector2(buttonSize, buttonSize);
        pauseButton.anchoredPosition = new Vector2(-buttonCentre.x, -buttonCentre.y);
        if (mute != null)
        {
            var mrt = (RectTransform)mute.transform; // the same size as the sound button, mirrored
            pauseButton.sizeDelta = mrt.sizeDelta;
            pauseButton.anchoredPosition = new Vector2(-mrt.anchoredPosition.x, mrt.anchoredPosition.y);
        }
        var bi = buttonGo.GetComponent<Image>();
        bi.sprite = sprite;
        bi.preserveAspect = true;
        pauseButtonControl = buttonGo.GetComponent<Button>();
        pauseButtonControl.targetGraphic = bi;
        pauseButtonControl.transition = Selectable.Transition.SpriteSwap;
        pauseButtonControl.spriteState = new SpriteState { disabledSprite = inactive != null ? inactive : sprite };
        pauseButtonControl.onClick.AddListener(() => { if (game != null) game.SetPaused(true); });

        // The window: the black fade over the whole screen, and the content in the middle.
        var windowGo = new GameObject("PauseWindow", typeof(RectTransform), typeof(Image));
        windowGo.transform.SetParent(root, false);
        window = (RectTransform)windowGo.transform;
        window.anchorMin = Vector2.zero; window.anchorMax = Vector2.one; window.offsetMin = window.offsetMax = Vector2.zero;
        dim = windowGo.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0f);
        dim.raycastTarget = true; // nothing under the window can be touched

        title = MakeText("Title", window, font, fontMaterial, 120, new Vector2(0f, 250f), new Vector2(1200f, 200f));
        var rounded = MakeRounded();
        resumeLabel = MakeOption("Resume", rounded, resumeColor, font, fontMaterial, new Vector2(0f, 20f), () => { if (game != null) game.SetPaused(false); });
        exitLabel = MakeOption("Exit", rounded, exitColor, font, fontMaterial, new Vector2(0f, -170f), () => { if (game != null) game.ExitToMenu(); });
        window.gameObject.SetActive(false);
        built = true;
    }

    TextMeshProUGUI MakeText(string name, Transform parent, TMP_FontAsset font, Material mat, float size, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        if (mat != null) t.fontSharedMaterial = mat;
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize = size;
        t.color = Color.white;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = box;
        rt.anchoredPosition = pos;
        return t;
    }

    TextMeshProUGUI MakeOption(string name, Sprite rounded, Color color, TMP_FontAsset font, Material mat, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        return MakeOption(name, window, rounded, color, font, mat, pos, onClick);
    }

    TextMeshProUGUI MakeOption(string name, Transform parent, Sprite rounded, Color color, TMP_FontAsset font, Material mat, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = optionSize;
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.sprite = rounded;
        img.type = Image.Type.Sliced;
        img.color = color;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);
        return MakeText("Label", go.transform, font, mat, 64, Vector2.zero, optionSize);
    }

    // A rounded rectangle that stretches: the corners are kept (9-slice).
    static Sprite MakeRounded()
    {
        const int n = 96, r = 36;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }

    void Update()
    {
        if (!built || game == null) return;

        // Escape / P: pause or resume (a computer).
        if (game.CanPause || game.Paused)
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) game.SetPaused(!game.Paused);

        // The button is always there: alive while the game can be paused, a grey dead copy of itself otherwise. The one place it gives way is
        // the menu and the result screens: the settings gear sits in the same corner then.
        if (settings == null) settings = FindFirstObjectByType<SettingsWindow>();
        bool gearHere = settings != null && settings.openButton != null && settings.openButton.gameObject.activeSelf;
        if (pauseButton.gameObject.activeSelf == gearHere) pauseButton.gameObject.SetActive(!gearHere);
        bool buttonOn = game.CanPause && !game.Paused;
        if (pauseButtonControl.interactable != buttonOn) pauseButtonControl.interactable = buttonOn;

        // The window fades with real time (the game's own time is stopped).
        float target = game.Paused ? 1f : 0f;
        shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));
        bool open = shown > 0f;
        if (window.gameObject.activeSelf != open)
        {
            window.gameObject.SetActive(open);
            if (open) { window.SetAsLastSibling(); RefreshTexts(); }
        }
        if (!open) return;
        dim.color = new Color(0f, 0f, 0f, fadeToBlack * Mathf.SmoothStep(0f, 1f, shown));
        float pop = Mathf.Lerp(0.85f, 1f, Mathf.SmoothStep(0f, 1f, shown));
        for (int i = 0; i < window.childCount; i++)
        {
            var c = window.GetChild(i);
            c.localScale = Vector3.one * pop;
        }
        SetAlpha(title, shown); SetAlpha(resumeLabel, shown); SetAlpha(exitLabel, shown);
        foreach (var g in window.GetComponentsInChildren<Graphic>()) if (g != dim && !(g is TMP_Text)) { var col = g.color; col.a = shown; g.color = col; }
    }

    static void SetAlpha(TMP_Text t, float a) { if (t != null) { var c = t.color; c.a = a; t.color = c; } }

    void RefreshTexts()
    {
        title.text = I18n.Tr("PAUSED");
        resumeLabel.text = I18n.Tr("RESUME");
        exitLabel.text = I18n.Tr("EXIT GAME");
    }
}
