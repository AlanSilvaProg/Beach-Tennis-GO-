using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The player's controls.
//  - Computer (mouse and keyboard): nothing is drawn. W A S D (or the arrows) run and a click anywhere on the screen (or Space / J) swings.
//  - Phone (touch): a joystick on the left half that runs the character and a swing button on the right half.
//    The joystick is "floating": it appears where the thumb lands and the knob follows the thumb. A touch anywhere in the right half is a swing,
//    so there is no need to aim at the button.
// A computer that turns out to have a touch screen switches to the phone layout at the first touch.
// Debug: ForceMobile shows the phone layout (always visible, also in the menu) with the mouse acting as a finger. In the editor it is set by
// the menu Tools / Beach Tennis GO / Debug mobile controls.
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

    // Show (and use) the phone layout whatever the device. In the editor it follows the menu item.
    public static bool ForceMobile
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(DebugPref, false);
#else
            return false;
#endif
        }
    }
    public const string DebugPref = "BeachTennisGO.DebugMobileControls";
    public bool Mobile => mobile;

    // -1..1 on both axes: x to the right, y towards the net.
    public Vector2 Move { get; private set; }
    public bool Visible => visible;

    // Where the swing button is on the screen (pixels), when it is drawn (a phone).
    public bool TryGetSwingButtonScreen(out Vector2 screen)
    {
        screen = default;
        if (button == null || !button.gameObject.activeInHierarchy) return false;
        screen = RectTransformUtility.WorldToScreenPoint(null, button.position);
        return true;
    }

    bool visible, swingQueued, mobile;
    RectTransform root, stickBase, knob, button, stickPad, swingPad;
    Image baseImage, knobImage, buttonImage, buttonRing;
    TextMeshProUGUI label;
    Sprite disc, ring;
    bool touched;
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
        if (!swingInput) { swingQueued = false; return false; }
        bool s = swingQueued || (visible && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J)));
        swingQueued = false;
        return s;
    }

    // The racket swings by itself (the player only moves), so there is no swing button and a click or a tap does not hit. Turn it on to bring the button back.
    public bool swingInput = false;

    public void SetVisible(bool v)
    {
        if (ForceMobile) v = true;
        if (visible == v) return;
        visible = v;
        // The controls are never switched off (that would drop a finger that is still on the stick: it would have to touch again): they are only
        // hidden, and while they are, the stick is ignored. A finger that stays down goes on moving the character afterwards.
        ApplyVisibility();
        if (!v) swingQueued = false;
    }

    CanvasGroup group;
    void ApplyVisibility()
    {
        if (root == null) return;
        if (group == null && !root.TryGetComponent(out group)) group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = visible ? 1f : 0f;
        // Hidden controls must not catch touches or clicks (the menu's own taps).
        if (stickPad != null) stickPad.GetComponent<Image>().raycastTarget = visible;
        if (swingPad != null) swingPad.GetComponent<Image>().raycastTarget = visible;
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

        // Touch areas (invisible): they catch the thumbs / the click anywhere inside them. Their size depends on the layout (ApplyLayout).
        stickPad = MakeArea("StickArea");
        stickPad.gameObject.AddComponent<StickPad>().owner = this;
        swingPad = MakeArea("SwingArea");
        swingPad.gameObject.AddComponent<SwingPad>().owner = this;

        // Every picture is centred on its parent (anchors in the middle), so the stick's ring, disc and knob and the button's ring share one centre.
        stickBase = MakeImage("StickBase", root, disc, new Vector2(0f, 0f), new Vector2(stickRadius * 2.3f, stickRadius * 2.3f), new Color(1f, 1f, 1f, 0.16f), out baseImage);
        MakeImage("Ring", stickBase, ring, new Vector2(0.5f, 0.5f), new Vector2(stickRadius * 2.3f, stickRadius * 2.3f), new Color(1f, 1f, 1f, 0.5f), out var _r);
        knob = MakeImage("Knob", stickBase, disc, new Vector2(0.5f, 0.5f), new Vector2(stickRadius * 1.15f, stickRadius * 1.15f), new Color(1f, 1f, 1f, 0.55f), out knobImage);

        button = MakeImage("SwingButton", root, disc, new Vector2(1f, 0f), new Vector2(buttonSize, buttonSize), new Color(1f, 0.8f, 0.2f, 0.82f), out buttonImage);
        button.anchoredPosition = new Vector2(-buttonPlace.x, buttonPlace.y);
        MakeImage("Ring", button, ring, new Vector2(0.5f, 0.5f), new Vector2(buttonSize, buttonSize), new Color(1f, 1f, 1f, 0.75f), out buttonRing);

        // The picture of the button: a racket with the trail of its swing (the text is only there if the picture is missing).
        var hitIcon = Resources.Load<Sprite>("hit-icon");
        if (hitIcon != null)
        {
            MakeImage("Icon", button, hitIcon, new Vector2(0.5f, 0.5f), new Vector2(buttonSize * 0.92f, buttonSize * 0.92f), Color.white, out var _icon);
            ((RectTransform)button.Find("Icon")).anchoredPosition = Vector2.zero;
        }
        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(button, false);
        label = textGo.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        if (fontMaterial != null) label.fontSharedMaterial = fontMaterial;
        label.text = hitIcon != null ? "" : I18n.Tr("HIT");
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 52;
        label.color = Color.white;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;

        ShowStickAt(restingPlace, false);
        mobile = Application.isMobilePlatform || ForceMobile;
        ApplyLayout();
        ApplyVisibility();
    }

    // The phone layout draws the stick and the button and splits the screen in two; the computer layout draws nothing and any click is a swing.
    void ApplyLayout()
    {
        stickBase.gameObject.SetActive(mobile);
        button.gameObject.SetActive(mobile && swingInput);
        stickPad.gameObject.SetActive(mobile);
        swingPad.gameObject.SetActive(swingInput);
        SetArea(swingPad, mobile ? new Vector2(1f - swingArea.x, 0f) : Vector2.zero, mobile ? new Vector2(1f, swingArea.y) : Vector2.one);
        // without a swing button the stick can be started anywhere on the lower screen
        SetArea(stickPad, Vector2.zero, new Vector2(swingInput ? stickArea.x : 1f, stickArea.y));
        if (!mobile) { stickPointer = int.MinValue; stickValue = Vector2.zero; }
    }

    static void SetArea(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    RectTransform MakeArea(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);
        var rt = (RectTransform)go.transform;
        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
        return rt;
    }

    RectTransform MakeImage(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 size, Color color, out Image image)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
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
        stickPointer = e.pointerId; // a new touch always takes the stick over (a finger that was lost is not waited for)
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

    bool HeldPointerAlive()
    {
        if (stickPointer == -1) return Input.GetMouseButton(0);      // the mouse (left button)
        if (stickPointer < 0) return true;                           // other mouse buttons: not tracked
        for (int i = 0; i < Input.touchCount; i++) if (Input.GetTouch(i).fingerId == stickPointer) return true;
        return false;
    }

    void SwingDown() { if (visible) { swingQueued = true; pressGlow = 1f; } }

    void Update()
    {
        // A touch on a device that was taken for a computer: from now on it is a phone.
        if (Input.touchCount > 0) touched = true;
        bool wantMobile = Application.isMobilePlatform || ForceMobile || touched;
        if (wantMobile != mobile) { mobile = wantMobile; ApplyLayout(); }

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

        // The finger that holds the stick is still down? (If its release was lost, the stick lets go by itself.)
        if (stickPointer != int.MinValue && !HeldPointerAlive())
        {
            stickPointer = int.MinValue;
            stickValue = Vector2.zero;
            ShowStickAt(restingPlace, false);
        }

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J)) pressGlow = 1f;
        pressGlow = Mathf.MoveTowards(pressGlow, 0f, Time.unscaledDeltaTime * 4f);
        if (buttonImage != null)
        {
            buttonImage.color = Color.Lerp(new Color(1f, 0.8f, 0.2f, 0.82f), new Color(1f, 0.92f, 0.45f, 1f), pressGlow);
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
