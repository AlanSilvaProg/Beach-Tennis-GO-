using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Teaches the serve when the player is slow to do it: once `showAfter` seconds have passed in the player's serve without the ball being hit, a
// see-through "ghost" racket pulls back behind the ball and swings through it, with the tutorial hand holding it (the same hand as the menu's
// TapHint). It keeps looping, whatever the player does with the racket, until the serve is hit.
// Nothing is written on the screen: the ghost shows both who serves (it only appears when it is the player's serve) and how.
// The character serves with the swing button (pressed on a phone, a click anywhere on a computer): when the player is slow to serve, the same hand taps on the button
// (a phone: a ring pulses round it) or on the screen with the word CLICK (a computer), with a ring spreading at every tap, until the serve is hit.
public class ServeHint : MonoBehaviour
{
    [Header("Character serve (swing button)")]
    [Tooltip("Seconds of the player's serve before the hand shows how.")]
    public float buttonShowAfter = 2.5f;
    [Tooltip("Seconds of one tap of the hand.")]
    public float tapLoop = 1.1f;
    [Tooltip("How far the hand moves away from the spot and back (canvas units, at the hand's scale of 1).")]
    public Vector2 tapTravel = new Vector2(50.5f, -44.2f);
    [Tooltip("Where the hand taps on a computer (fractions of the screen, from the bottom left).")]
    public Vector2 clickAt = new Vector2(0.5f, 0.3f);

    [Header("Scene")]
    public GameManager game;
    [Tooltip("The tutorial hand (a UI Image).")]
    public RectTransform hand;
    public Image handImage;
    [Tooltip("See-through material of the ghost racket (shader BeachTennisGO/GhostRacket with the racket's texture).")]
    public Material ghostMaterial;
    [Tooltip("Where the fingertip is, from the middle of the hand picture (canvas units).")]
    public Vector2 fingertip = new Vector2(-1f, 47.3f);
    public float handScale = 0.425f;

    [Header("When")]
    [Tooltip("The very first serve of the session shows the ghost at once.")]
    public bool showAtFirstServe = true;
    [Tooltip("In the other serves: seconds of the player's serve (the ball still hanging) before the ghost shows up. Moving the racket does not hide it.")]
    public float showAfter = 2f;

    [Header("The demonstration")]
    [Tooltip("Seconds of one demonstration (pull back, swing, rest).")]
    public float loopTime = 2.6f;
    [Tooltip("How far behind the ball the racket is pulled back (m).")]
    public float pullBack = 1.1f;
    [Tooltip("How far past the ball the swing goes on (m).")]
    public float followThrough = 1.6f;
    [Range(0f, 1f)] public float ghostAlpha = 0.7f;
    [Tooltip("Size of the ghost racket compared with the real one (1 = the same size).")]
    [Min(0.1f)] public float ghostScale = 1f;

    Transform ghost;
    Renderer[] ghostRenderers;
    RectTransform area;
    float waited;        // seconds of this serve so far
    bool firstThis;      // this serve is the first one of the session
    float shownFor;      // seconds the demonstration has been going
    float visibility;    // 0..1, fades in and out
    Vector3 lastGhostPos;
    bool wasServing;
    bool firstServeDone;  // the first serve of the session has already been shown

    void Awake()
    {
        area = (RectTransform)transform.parent;
        if (hand != null) hand.gameObject.SetActive(false);
    }

    void Start() { BuildGhost(); }

    void OnDestroy() { if (ghost != null) Destroy(ghost.gameObject); }

    // The ghost is a copy of the player's racket model with the see-through material.
    void BuildGhost()
    {
        if (game == null || game.player == null || game.player.Racket == null || game.player.Racket.model == null) return;
        var racket = game.player.Racket;
        var root = new GameObject("GhostRacket");
        // Not a child of the HUD: that is a UI object, and the canvas scale would blow the racket up. The ghost lives in the world.
        ghost = root.transform;
        var model = Instantiate(racket.model.gameObject, ghost);
        model.name = "Model";
        model.transform.localPosition = racket.model.localPosition;
        model.transform.localRotation = racket.model.localRotation;
        model.transform.localScale = racket.model.localScale;
        ghostRenderers = model.GetComponentsInChildren<Renderer>();
        foreach (var r in ghostRenderers)
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = ghostMaterial;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
        ghost.gameObject.SetActive(false);
    }

    // ---------- the character's serve: tap the button / click ----------
    RectTransform tapRing, buttonRing;
    Image tapRingImage, buttonRingImage;
    TextMeshProUGUI clickLabel;
    float buttonWaited, buttonShownFor, buttonVisibility;
    PlayerControls controlsRef;
    Sprite ringSprite;

    void BuildButtonParts()
    {
        if (tapRing != null || area == null) return;
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - Mathf.Abs(r - 0.9f) / 0.1f)));
            }
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        tapRing = MakePart("TapRing", out tapRingImage);
        buttonRing = MakePart("ButtonRing", out buttonRingImage);
        tapRingImage.sprite = buttonRingImage.sprite = ringSprite;

        var go = new GameObject("ClickLabel", typeof(RectTransform));
        go.transform.SetParent(area, false);
        clickLabel = go.AddComponent<TextMeshProUGUI>();
        if (game != null && game.hud != null && game.hud.winLine1 != null)
        {
            clickLabel.font = game.hud.winLine1.font;
            clickLabel.fontSharedMaterial = game.hud.winLine1.fontSharedMaterial;
        }
        clickLabel.alignment = TextAlignmentOptions.Center;
        clickLabel.fontSize = 56;
        clickLabel.color = Color.white;
        clickLabel.raycastTarget = false;
        clickLabel.textWrappingMode = TextWrappingModes.NoWrap;
        var lrt = clickLabel.rectTransform;
        lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.sizeDelta = new Vector2(500f, 80f);
        go.SetActive(false);
        if (hand != null) hand.SetAsLastSibling();
    }

    RectTransform MakePart(string name, out Image image)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(area, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        image = go.GetComponent<Image>();
        image.raycastTarget = false;
        go.SetActive(false);
        return rt;
    }

    Vector2 ToLocal(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local);
        return local;
    }

    void UpdateButtonServeHint()
    {
        if (ghost != null && ghost.gameObject.activeSelf) ghost.gameObject.SetActive(false);
        BuildButtonParts();
        bool serving = game.PlayerServing && !PokiService.AdPlaying && !game.TutorialHold;
        if (!serving)
        {
            buttonWaited = 0f; buttonShownFor = 0f;
            buttonVisibility = Mathf.MoveTowards(buttonVisibility, 0f, Time.unscaledDeltaTime * 5f);
        }
        else
        {
            buttonWaited += Time.unscaledDeltaTime;
            bool show = buttonWaited >= buttonShowAfter;
            buttonVisibility = Mathf.MoveTowards(buttonVisibility, show ? 1f : 0f, Time.unscaledDeltaTime * (show ? 3f : 6f));
            if (show) buttonShownFor += Time.unscaledDeltaTime;
        }
        ApplyButtonServe();
    }

    void ApplyButtonServe()
    {
        bool visible = buttonVisibility > 0.001f;
        if (hand != null && hand.gameObject.activeSelf != visible) hand.gameObject.SetActive(visible);
        if (tapRing == null) return;
        if (!visible) { tapRing.gameObject.SetActive(false); buttonRing.gameObject.SetActive(false); clickLabel.gameObject.SetActive(false); return; }

        // Where it taps: the swing button on a phone, the middle of the lower screen on a computer.
        if (controlsRef == null) controlsRef = FindFirstObjectByType<PlayerControls>();
        Vector2 screen = default;
        bool onButton = controlsRef != null && controlsRef.TryGetSwingButtonScreen(out screen);
        if (!onButton) screen = new Vector2(Screen.width * clickAt.x, Screen.height * clickAt.y);
        Vector2 spot = ToLocal(screen);

        float k = (buttonShownFor % tapLoop) / tapLoop;          // 0..1 through one tap
        float away = Mathf.SmoothStep(0f, 1f, k < 0.5f ? k * 2f : (1f - k) * 2f);   // 0 on the spot, 1 at the far end
        Vector2 handPos = spot + tapTravel * handScale * away;   // it comes down onto the spot (away = 0) and lifts off again
        float alpha = buttonVisibility;

        if (hand != null)
        {
            hand.localScale = Vector3.one * handScale;
            hand.anchoredPosition = handPos - fingertip * handScale;
            hand.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-12f, -28.9f, away));
            var c = handImage.color; c.a = alpha; handImage.color = c;
        }

        // a ring spreads from the spot at every tap (when the hand arrives)
        float burst = Mathf.Clamp01(k / 0.6f);
        bool ringOn = k < 0.6f;
        tapRing.gameObject.SetActive(ringOn);
        if (ringOn)
        {
            tapRing.anchoredPosition = spot;
            tapRing.sizeDelta = Vector2.one * Mathf.Lerp(70f, 210f, burst);
            tapRingImage.color = new Color(1f, 0.9f, 0.4f, (1f - burst) * alpha);
        }
        // a phone: a steady ring round the button says what it is
        buttonRing.gameObject.SetActive(onButton);
        if (onButton)
        {
            buttonRing.anchoredPosition = spot;
            float s = 1f + 0.07f * Mathf.Sin(buttonShownFor * 6f);
            buttonRing.sizeDelta = Vector2.one * 150f * s;
            buttonRingImage.color = new Color(1f, 0.9f, 0.4f, 0.9f * alpha);
        }
        // a computer: the word CLICK under the spot
        clickLabel.gameObject.SetActive(!onButton);
        if (!onButton)
        {
            clickLabel.text = I18n.Tr("CLICK");
            clickLabel.rectTransform.anchoredPosition = spot + new Vector2(0f, -110f);
            var lc = clickLabel.color; lc.a = alpha; clickLabel.color = lc;
        }
    }

    void Update()
    {
        if (game == null) return;
        // The character serves with its button: the hand shows the button instead of a swipe.
        bool buttonServe = game.player is PlayerRacketController pc && pc.UsesButton;
        if (buttonServe) { UpdateButtonServeHint(); return; }
        if (ghost == null) return;
        bool serving = game.PlayerServing && !PokiService.AdPlaying;
        if (!serving)
        {
            waited = 0f; shownFor = 0f; wasServing = false;
            visibility = Mathf.MoveTowards(visibility, 0f, Time.unscaledDeltaTime * 4f);
            Apply();
            return;
        }
        if (!wasServing)
        {
            wasServing = true; waited = 0f; shownFor = 0f;
            firstThis = showAtFirstServe && !firstServeDone; // the first serve: the ghost is there from the start
            firstServeDone = true;
        }

        waited += Time.unscaledDeltaTime;
        bool show = firstThis || waited >= showAfter;
        visibility = Mathf.MoveTowards(visibility, show ? 1f : 0f, Time.unscaledDeltaTime * (show ? 3f : 6f));
        if (show) shownFor += Time.unscaledDeltaTime;
        Apply();
    }

    void Apply()
    {
        bool visible = visibility > 0.001f;
        if (ghost.gameObject.activeSelf != visible) ghost.gameObject.SetActive(visible);
        if (hand != null && hand.gameObject.activeSelf != visible) hand.gameObject.SetActive(visible);
        if (!visible) return;

        // Path of the racket in this demonstration, in the world: from where the player starts, back behind the ball, then through it.
        var racket = game.player.Racket;
        Vector3 ballPos = game.Ball.Position;
        float side = racket.SideSign;                       // -1: the player is at z < 0
        Vector3 start = new Vector3(ballPos.x, racket.height, game.player.restPosition.z);
        Vector3 back = new Vector3(ballPos.x, racket.height, ballPos.z + side * pullBack);
        Vector3 end = new Vector3(ballPos.x, racket.height, ballPos.z - side * followThrough);

        float k = (shownFor % loopTime) / loopTime;          // 0..1 through the demonstration
        Vector3 pos;
        float fade = 1f;
        if (k < 0.32f) pos = Vector3.Lerp(start, back, Mathf.SmoothStep(0f, 1f, k / 0.32f));            // pull back, slowly
        else if (k < 0.42f) pos = back;                                                                   // a short pause
        else if (k < 0.58f) pos = Vector3.Lerp(back, end, Mathf.Pow((k - 0.42f) / 0.16f, 1.6f));         // the swing, fast
        else { pos = end; fade = 1f - Mathf.InverseLerp(0.7f, 0.95f, k); }                                  // let go and fade out
        if (k < 0.08f) fade = Mathf.InverseLerp(0f, 0.08f, k);                                              // fade in

        // Lean like the real racket does when it moves forward / back.
        Vector3 velocity = (pos - lastGhostPos) / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        lastGhostPos = pos;
        float forwardSpeed = -side * velocity.z;
        float pitch = Mathf.Clamp(forwardSpeed * racket.pitchPerSpeed, -racket.maxPitch, racket.maxPitch) + racket.restPitch;
        ghost.position = pos;
        ghost.localScale = Vector3.one * ghostScale;
        ghost.rotation = Quaternion.Euler(0f, racket.facingYaw, 0f) * Quaternion.Euler(pitch, 0f, 0f);

        float alpha = visibility * fade;
        ghostMaterial.SetFloat("_Alpha", ghostAlpha * alpha);

        // The hand holds the racket: its fingertip is on the racket, wherever that is on the screen.
        var cam = Camera.main;
        if (cam != null && hand != null)
        {
            Vector3 screen = cam.WorldToScreenPoint(pos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local);
            hand.localScale = Vector3.one * handScale;
            hand.anchoredPosition = local - fingertip * handScale;
            hand.localEulerAngles = new Vector3(0f, 0f, -12f);
            var c = handImage.color; c.a = alpha; handImage.color = c;
        }
    }
}
