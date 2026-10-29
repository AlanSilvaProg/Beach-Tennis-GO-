using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The controls tutorial: when the first training match of a session starts, the screen fades to black and shows only the controls.
//  - Computer: W A S D drawn as keys with the text MOVE above them, and a mouse with its left button marked and the text HIT.
//  - Phone: the joystick (with its knob going round) and the swing button, with the same two texts.
// The game stands still while it is open (GameManager.TutorialHold); any click, touch or key closes it. Everything is made at run time on its own
// overlay canvas, nothing to wire in the scene. Tools / Beach Tennis GO / Show tutorial (in play mode) opens it again.
public class TutorialOverlay : MonoBehaviour
{
    [Range(0f, 1f)] public float fadeToBlack = 0.88f;
    public float fadeTime = 0.35f;
    [Tooltip("Seconds before a click or a key can close it (so the click that started the match does not).")]
    public float minimumTime = 0.6f;
    public Color keyColor = new Color(0.97f, 0.97f, 0.95f);
    public Color highlight = new Color(1f, 0.82f, 0.25f);

    GameManager game;
    RectTransform root, content;
    Image dim;
    TMP_FontAsset font;
    Material fontMaterial;
    Sprite rounded, disc, ring;
    bool mobile, open, closing;
    float shown, openedAt;
    RectTransform knob, pulseButton;
    TextMeshProUGUI hint;
    Vector2 knobCentre;

    public bool IsOpen => open;

    public static TutorialOverlay Create(GameManager game, TMP_FontAsset font, Material fontMaterial)
    {
        var go = new GameObject("TutorialOverlay");
        var t = go.AddComponent<TutorialOverlay>();
        t.game = game; t.font = font; t.fontMaterial = fontMaterial;
        t.Build();
        return t;
    }

    void Build()
    {
        var canvasGo = new GameObject("Tutorial Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 97;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)canvasGo.transform;

        var dimGo = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        dimGo.transform.SetParent(root, false);
        var drt = (RectTransform)dimGo.transform;
        drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
        dim = dimGo.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0f);
        dim.raycastTarget = true; // the touch that closes it is not a swing

        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(root, false);
        content = (RectTransform)contentGo.transform;
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
        content.sizeDelta = Vector2.zero;

        rounded = MakeRounded(96, 30);
        disc = MakeSprite(128, r => Mathf.Clamp01((1f - r) * 14f));
        ring = MakeSprite(128, r => Mathf.Clamp01(1f - Mathf.Abs(r - 0.92f) / 0.08f));
        root.gameObject.SetActive(false);
    }

    // ---------- opening and closing ----------

    public void Show()
    {
        if (open) return;
        var controls = FindFirstObjectByType<PlayerControls>();
        mobile = controls != null ? controls.Mobile : Application.isMobilePlatform;
        open = true; closing = false; shown = 0f; openedAt = Time.unscaledTime;
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        BuildContent();
        game.SetTutorialHold(true);
    }

    void Close()
    {
        if (!open || closing) return;
        closing = true;
    }

    void Update()
    {
        if (!open) return;
        shown = Mathf.MoveTowards(shown, closing ? 0f : 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));
        dim.color = new Color(0f, 0f, 0f, fadeToBlack * Mathf.SmoothStep(0f, 1f, shown));
        content.GetComponent<RectTransform>().localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, Mathf.SmoothStep(0f, 1f, shown));
        SetContentAlpha(shown);
        Animate();

        if (closing && shown <= 0f)
        {
            open = false;
            root.gameObject.SetActive(false);
            game.SetTutorialHold(false);
            return;
        }
        bool pressed = Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
        if (pressed && !closing && Time.unscaledTime - openedAt >= minimumTime) Close();
    }

    CanvasGroup group;
    void SetContentAlpha(float a)
    {
        if (group == null && !content.TryGetComponent(out group)) group = content.gameObject.AddComponent<CanvasGroup>();
        group.alpha = a;
    }

    // The knob goes round and the button breathes, to show what they do.
    void Animate()
    {
        float t = Time.unscaledTime;
        if (knob != null)
        {
            float a = t * 2.2f;
            knob.anchoredPosition = game != null && game.Baseline
                ? knobCentre + new Vector2(Mathf.Sin(a) * 62f, 0f)                                  // sideways only
                : knobCentre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 62f;
        }
        if (pulseButton != null) pulseButton.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(t * 5f));
        if (hint != null) { var c = hint.color; c.a = (0.55f + 0.45f * Mathf.Sin(t * 3.2f)) * shown; hint.color = c; }
    }

    // ---------- the content ----------

    void BuildContent()
    {
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        knob = pulseButton = null;
        Vector2 size = root.rect.size;
        bool wide = size.x > size.y * 1.15f;
        // Two groups: MOVE and HIT. Side by side on a wide screen, one over the other on a tall one.
        Vector2 moveAt = wide ? new Vector2(-size.x * 0.22f, 20f) : new Vector2(0f, size.y * 0.2f);
        Vector2 hitAt = wide ? new Vector2(size.x * 0.22f, 20f) : new Vector2(0f, -size.y * 0.17f);
        if (mobile) { if (!wide) { moveAt = new Vector2(-size.x * 0.24f, 0f); hitAt = new Vector2(size.x * 0.24f, 0f); } }

        if (mobile) { BuildStick(moveAt); BuildButton(hitAt); }
        else { BuildKeys(moveAt); BuildMouse(hitAt); }

        hint = Text("Hint", content, mobile ? I18n.Tr("TAP TO START") : I18n.Tr("CLICK TO START"), 46, new Vector2(0f, -size.y * 0.42f), new Vector2(1200f, 80f));
        hint.color = new Color(1f, 1f, 1f, 0.8f);
    }

    // W / A S D keys with MOVE above.
    void BuildKeys(Vector2 at)
    {
        Text("MoveLabel", content, I18n.Tr("MOVE"), 100, at + new Vector2(0f, 250f), new Vector2(900f, 140f));
        float k = 130f, gap = 14f;
        if (game != null && game.Baseline)
        {
            // along the baseline the player only runs sideways: just A and D
            Key("A", at + new Vector2(-(k + gap) * 0.5f, 0f), k);
            Key("D", at + new Vector2((k + gap) * 0.5f, 0f), k);
            return;
        }
        Key("W", at + new Vector2(0f, k * 0.5f + gap * 0.5f + 20f), k);
        Key("A", at + new Vector2(-(k + gap), -(k * 0.5f + gap * 0.5f) + 20f), k);
        Key("S", at + new Vector2(0f, -(k * 0.5f + gap * 0.5f) + 20f), k);
        Key("D", at + new Vector2(k + gap, -(k * 0.5f + gap * 0.5f) + 20f), k);
    }

    void Key(string letter, Vector2 pos, float size)
    {
        var shadow = Img("KeyShadow", content, rounded, new Color(0.55f, 0.58f, 0.62f), pos + new Vector2(0f, -10f), new Vector2(size, size));
        shadow.type = Image.Type.Sliced;
        var face = Img("Key" + letter, content, rounded, keyColor, pos, new Vector2(size, size));
        face.type = Image.Type.Sliced;
        var t = Text("Letter", face.transform, letter, 84, Vector2.zero, new Vector2(size, size));
        t.color = new Color(0.12f, 0.2f, 0.32f);
    }

    // A mouse seen from above, the left button marked, with HIT above it.
    void BuildMouse(Vector2 at)
    {
        Text("HitLabel", content, I18n.Tr("HIT"), 100, at + new Vector2(0f, 250f), new Vector2(900f, 140f)).color = highlight;
        var body = Img("MouseBody", content, rounded, keyColor, at + new Vector2(0f, -10f), new Vector2(190f, 290f));
        body.type = Image.Type.Sliced;
        // left button (marked)
        var left = Img("LeftButton", content, rounded, highlight, at + new Vector2(-46f, 70f), new Vector2(86f, 120f));
        left.type = Image.Type.Sliced;
        pulseButton = left.rectTransform;
        // right button and the wheel
        var right = Img("RightButton", content, rounded, new Color(0.84f, 0.85f, 0.86f), at + new Vector2(46f, 70f), new Vector2(86f, 120f));
        right.type = Image.Type.Sliced;
        Img("Wheel", content, rounded, new Color(0.55f, 0.58f, 0.62f), at + new Vector2(0f, 82f), new Vector2(18f, 54f)).type = Image.Type.Sliced;
        // an arrow from the label to the button
        var arrow = Text("Arrow", content, "▼", 64, at + new Vector2(-46f, 185f), new Vector2(120f, 90f));
        arrow.color = highlight;
    }

    // The joystick with its knob going round.
    void BuildStick(Vector2 at)
    {
        Text("MoveLabel", content, I18n.Tr("MOVE"), 100, at + new Vector2(0f, 250f), new Vector2(900f, 140f));
        Img("StickBase", content, disc, new Color(1f, 1f, 1f, 0.2f), at, new Vector2(300f, 300f));
        Img("StickRing", content, ring, new Color(1f, 1f, 1f, 0.7f), at, new Vector2(300f, 300f));
        var k = Img("Knob", content, disc, new Color(1f, 1f, 1f, 0.8f), at, new Vector2(130f, 130f));
        knob = k.rectTransform;
        knobCentre = at;
    }

    // The swing button as it is in the game.
    void BuildButton(Vector2 at)
    {
        Text("HitLabel", content, I18n.Tr("HIT"), 100, at + new Vector2(0f, 250f), new Vector2(900f, 140f)).color = highlight;
        var b = Img("SwingButton", content, disc, new Color(1f, 0.85f, 0.25f, 0.9f), at, new Vector2(240f, 240f));
        Img("Ring", b.transform, ring, new Color(1f, 1f, 1f, 0.9f), Vector2.zero, new Vector2(240f, 240f));
        pulseButton = b.rectTransform;
        var icon = Resources.Load<Sprite>("hit-icon");
        if (icon != null) Img("Icon", b.transform, icon, Color.white, Vector2.zero, new Vector2(222f, 222f));
        else Text("Label", b.transform, I18n.Tr("HIT"), 62, Vector2.zero, new Vector2(240f, 120f)).color = Color.white;
    }

    // ---------- small helpers ----------

    Image Img(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI Text(string name, Transform parent, string text, float size, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        if (fontMaterial != null) t.fontSharedMaterial = fontMaterial;
        t.text = text;
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

    static Sprite MakeRounded(int n, int r)
    {
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

    static Sprite MakeSprite(int n, System.Func<float, float> alphaAtRadius)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alphaAtRadius(r)));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
