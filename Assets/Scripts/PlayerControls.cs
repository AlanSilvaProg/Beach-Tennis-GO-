using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The player's controls on the screen: a joystick (left side) that runs the character and a swing button (right side).
//  - The joystick is "floating": it appears where the thumb lands in the left part of the screen and the knob follows the thumb.
//  - The swing is a button at the bottom right; a touch anywhere in the right part of the screen counts as pressing it, so there is no need to aim.
//  - On a computer the arrows / WASD run and Space (or J) swings. Both ways work at once.
// Everything is made at run time on its own overlay canvas, below the game's own HUD (so the buttons of the HUD stay on top), nothing to wire in the scene.
public class PlayerControls : MonoBehaviour
{
    [Header("Joystick")]
    [Tooltip("How far the knob can travel from the centre of the stick (canvas units).")]
    public float stickRadius = 95f;
    [Tooltip("Below this (0-1) the stick counts as released.")]
    [Range(0f, 0.4f)] public float deadZone = 0.12f;
    [Tooltip("Where the stick waits when nobody touches it (canvas units from the bottom left).")]
    public Vector2 restingPlace = new Vector2(250f, 230f);
    [Tooltip("Share of the screen width, from the left, in which a touch starts the stick, and share of the height, from the bottom.")]
    public Vector2 stickArea = new Vector2(0.5f, 0.72f);

    [Header("Swing button")]
    public float buttonSize = 210f;
    [Tooltip("Where the button is (canvas units from the bottom right).")]
    public Vector2 buttonPlace = new Vector2(250f, 230f);
    [Tooltip("Share of the screen width, from the right, in which a touch counts as the swing button, and share of the height, from the bottom.")]
    public Vector2 swingArea = new Vector2(0.45f, 0.72f);

    // -1..1 on both axes: x to the right, y towards the net.
    public Vector2 Move { get; private set; }
    public bool Visible => visible;

    bool visible, swingQueued;
    RectTransform root, stickBase, knob, button;
    Image baseImage, knobImage, buttonImage, buttonRing;
    TextMeshProUGUI label;
    Sprite disc, ring;
    int stickPointer = int.MinValue;
    Vector2 stickCentre, stickValue;
    float pressGlow;

    public static PlayerControls Create(TMP_FontAsset font, Material fontMaterial)
    {
        var go = new GameObject("PlayerControls");
        var pc = go.AddComponent<PlayerControls>();
        pc.Build(font, fontMaterial);
        return pc;
    }

    // The swing was asked for since the last time this was read.
    public bool ConsumeSwing()
    {
        bool s = swingQueued || (visible && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J)));
        swingQueued = false;
        return s;
    }

    public void SetVisible(bool v)
    {
        if (visible == v) return;
        visible = v;
        if (root != null) root.gameObject.SetActive(v);
        if (!v) { stickPointer = int.MinValue; stickValue = Vector2.zero; Move = Vector2.zero; swingQueued = false; }
    }

    void Build(TMP_FontAsset font, Material fontMaterial)
    {
        var canvasGo = new GameObject("Controls Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        root = (RectTransform)canvasGo.transform;

        disc = MakeSprite(128, (r) => Mathf.Clamp01((1f - r) * 14f));
        ring = MakeSprite(128, (r) => Mathf.Clamp01(1f - Mathf.Abs(r - 0.92f) / 0.08f));

        // Touch areas (invisible): they catch the thumbs anywhere inside them.
        var stickPad = MakeArea("StickArea", new Vector2(0f, 0f), new Vector2(stickArea.x, stickArea.y));
        stickPad.gameObject.AddComponent<StickPad>().owner = this;
        var swingPad = MakeArea("SwingArea", new Vector2(1f - swingArea.x, 0f), new Vector2(1f, swingArea.y));
        swingPad.gameObject.AddComponent<SwingPad>().owner = this;

        stickBase = MakeImage("StickBase", disc, new Vector2(0f, 0f), new Vector2(stickRadius * 2.3f, stickRadius * 2.3f), new Color(1f, 1f, 1f, 0.16f), out baseImage);
        var baseRing = MakeImage("Ring", ring, Vector2.zero, new Vector2(stickRadius * 2.3f, stickRadius * 2.3f), new Color(1f, 1f, 1f, 0.5f), out var _r);
        baseRing.SetParent(stickBase, false);
        baseRing.anchoredPosition = Vector2.zero;
        knob = MakeImage("Knob", disc, new Vector2(0f, 0f), new Vector2(stickRadius * 1.15f, stickRadius * 1.15f), new Color(1f, 1f, 1f, 0.55f), out knobImage);
        knob.SetParent(stickBase, false);

        button = MakeImage("SwingButton", disc, new Vector2(1f, 0f), new Vector2(buttonSize, buttonSize), new Color(1f, 0.85f, 0.25f, 0.55f), out buttonImage);
        button.anchoredPosition = new Vector2(-buttonPlace.x, buttonPlace.y);
        var br = MakeImage("Ring", ring, Vector2.zero, new Vector2(buttonSize, buttonSize), new Color(1f, 1f, 1f, 0.75f), out buttonRing);
        br.SetParent(button, false);
        br.anchoredPosition = Vector2.zero;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(button, false);
        label = textGo.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        if (fontMaterial != null) label.fontSharedMaterial = fontMaterial;
        label.text = I18n.Tr("HIT");
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 52;
        label.color = Color.white;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;

        ShowStickAt(restingPlace, false);
        root.gameObject.SetActive(false);
    }

    RectTransform MakeArea(string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
        return rt;
    }

    RectTransform MakeImage(string name, Sprite sprite, Vector2 anchor, Vector2 size, Color color, out Image image)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return rt;
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

    // The base of the stick, centred at p (canvas units from the bottom left).
    void ShowStickAt(Vector2 p, bool active)
    {
        stickBase.anchoredPosition = p;
        stickCentre = p;
        knob.anchoredPosition = Vector2.zero;
        var c = baseImage.color; c.a = active ? 0.3f : 0.16f; baseImage.color = c;
    }

    // Pointer position in canvas units from the bottom left.
    Vector2 ToCanvas(Vector2 screen)
    {
        Vector2 size = root.rect.size;
        return new Vector2(screen.x / Screen.width * size.x, screen.y / Screen.height * size.y);
    }

    void StickDown(PointerEventData e)
    {
        if (stickPointer != int.MinValue) return;
        stickPointer = e.pointerId;
        Vector2 p = ToCanvas(e.position);
        p.x = Mathf.Max(p.x, stickRadius + 20f);
        p.y = Mathf.Max(p.y, stickRadius + 20f);
        ShowStickAt(p, true);
        StickDrag(e);
    }

    void StickDrag(PointerEventData e)
    {
        if (e.pointerId != stickPointer) return;
        Vector2 d = ToCanvas(e.position) - stickCentre;
        if (d.magnitude > stickRadius) d = d.normalized * stickRadius;
        knob.anchoredPosition = d;
        stickValue = d / stickRadius;
    }

    void StickUp(PointerEventData e)
    {
        if (e.pointerId != stickPointer) return;
        stickPointer = int.MinValue;
        stickValue = Vector2.zero;
        ShowStickAt(restingPlace, false);
    }

    void SwingDown() { if (visible) { swingQueued = true; pressGlow = 1f; } }

    void Update()
    {
        // The stick (when it is held) or the keyboard.
        Vector2 v = stickValue;
        if (v.sqrMagnitude < 1e-4f)
        {
            v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (v.sqrMagnitude > 1f) v.Normalize();
        }
        else
        {
            float m = v.magnitude;
            v = m < deadZone ? Vector2.zero : v / m * Mathf.Clamp01((m - deadZone) / (1f - deadZone));
        }
        Move = visible ? v : Vector2.zero;

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J)) pressGlow = 1f;
        pressGlow = Mathf.MoveTowards(pressGlow, 0f, Time.unscaledDeltaTime * 4f);
        if (buttonImage != null)
        {
            buttonImage.color = Color.Lerp(new Color(1f, 0.85f, 0.25f, 0.55f), new Color(1f, 0.95f, 0.5f, 0.95f), pressGlow);
            button.localScale = Vector3.one * (1f - 0.1f * pressGlow);
        }
    }

    // The touch areas.
    class StickPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public PlayerControls owner;
        public void OnPointerDown(PointerEventData e) { owner.StickDown(e); }
        public void OnDrag(PointerEventData e) { owner.StickDrag(e); }
        public void OnPointerUp(PointerEventData e) { owner.StickUp(e); }
    }

    class SwingPad : MonoBehaviour, IPointerDownHandler
    {
        public PlayerControls owner;
        public void OnPointerDown(PointerEventData e) { owner.SwingDown(); }
    }
}
