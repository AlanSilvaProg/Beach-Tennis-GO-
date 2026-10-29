using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The controls of a computer, very small, always on the screen during a match: W A S D drawn as little keys with MOVE (the racket hits by itself, so there
// is nothing else to press). They sit at the top right, just under the button of the settings (the pause button in a match). Not shown on a phone (it has its own stick
// and button). Made at run time inside the HUD canvas, next to the sound button, so the sizes are those of the other HUD buttons.
public class ControlsLegend : MonoBehaviour
{
    [Header("Place")]
    [Tooltip("Distance of the legend's top right corner from the top right corner of the screen (HUD units). The settings button is 60 wide and centred 55 from the corner.")]
    public Vector2 corner = new Vector2(31f, 99f);

    [Header("Look")]
    public float keySize = 17f;
    public float keyGap = 2f;
    public float fontSize = 13f;
    public Color keyColor = new Color(0.97f, 0.97f, 0.95f, 1f);
    [Tooltip("A dark rounded panel behind the legend: on the sky the little keys and words are hard to read without it.")]
    public Color panelColor = new Color(0.05f, 0.09f, 0.17f, 0.78f);
    public Vector2 panelPadding = new Vector2(6f, 6f);
    public Color highlight = new Color(1f, 0.82f, 0.25f, 1f);
    [Range(0f, 1f)] public float alpha = 1f;

    GameManager game;
    PlayerControls controls;
    RectTransform root;
    CanvasGroup group;
    Sprite rounded;
    TMP_FontAsset font;
    Material fontMaterial;
    float shown;

    public static ControlsLegend Create(GameManager game, TMP_FontAsset font, Material fontMaterial)
    {
        var go = new GameObject("ControlsLegend", typeof(RectTransform));
        var legend = go.AddComponent<ControlsLegend>();
        legend.game = game; legend.font = font; legend.fontMaterial = fontMaterial;
        legend.Build();
        return legend;
    }

    void Build()
    {
        // Inside the HUD (where the sound button is); if it is not found, on a canvas of its own.
        var mute = FindFirstObjectByType<SoundMuteButton>();
        Transform parent;
        if (mute != null) parent = mute.transform.parent;
        else
        {
            var canvasGo = new GameObject("Legend Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.matchWidthOrHeight = 0.5f;
            parent = canvasGo.transform;
        }
        transform.SetParent(parent, false);
        root = (RectTransform)transform;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = new Vector2(-corner.x, -corner.y);
        root.sizeDelta = Vector2.zero;
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        rounded = MakeRounded(32, 10);
        float k = keySize, g = keyGap;
        float clusterW = 3f * k + 2f * g, clusterH = 2f * k + g;

        // The panel behind everything (drawn first): from the left of the words to the right of the pictures, with a little room all round.
        float labelRoom = 40f;
        float panelW = clusterW + 6f + labelRoom + panelPadding.x * 2f - 2f;
        float panelH = clusterH + panelPadding.y * 2f - 2f;
        var panel = Img("Panel", root, rounded, panelColor, new Vector2(panelPadding.x, panelPadding.y), new Vector2(panelW, panelH), new Vector2(1f, 1f));
        panel.type = Image.Type.Sliced;

        // MOVE: W over A S D (the cluster's right edge is the legend's right edge)
        Key("W", new Vector2(-(k + g), 0f), k);
        Key("A", new Vector2(-(2f * k + 2f * g), -(k + g)), k);
        Key("S", new Vector2(-(k + g), -(k + g)), k);
        Key("D", new Vector2(0f, -(k + g)), k);
        Label(I18n.Tr("MOVE"), new Vector2(-clusterW - 6f, -clusterH * 0.5f));
    }

    void Key(string letter, Vector2 pos, float size)
    {
        var face = Img("Key" + letter, root, rounded, keyColor, pos, new Vector2(size, size), new Vector2(1f, 1f));
        face.type = Image.Type.Sliced;
        var t = Text(letter, face.transform, fontSize * 0.8f, Vector2.zero, new Vector2(size, size));
        t.color = new Color(0.12f, 0.2f, 0.32f, 1f);
    }

    // A word to the left of a picture, right aligned.
    void Label(string text, Vector2 pos)
    {
        var t = Text(text, root, fontSize, pos, new Vector2(80f, 20f), true);
        t.alignment = TextAlignmentOptions.MidlineRight;
        t.color = Color.white;
    }

    Image Img(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, Vector2 size, Vector2 anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI Text(string text, Transform parent, float size, Vector2 pos, Vector2 box, bool topRightAnchored = false)
    {
        var go = new GameObject("Text", typeof(RectTransform));
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
        if (topRightAnchored) { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0.5f); }
        else { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); }
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

    void Update()
    {
        if (game == null || group == null) return;
        if (controls == null) controls = FindFirstObjectByType<PlayerControls>();
        bool phone = controls != null ? controls.Mobile : Application.isMobilePlatform;
        bool show = !phone && game.MatchOn && !game.TutorialHold && !game.Paused && !PokiService.AdPlaying;
        shown = Mathf.MoveTowards(shown, show ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        group.alpha = alpha * shown;
    }
}
