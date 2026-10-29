using UnityEngine;
using UnityEngine.UI;

// Teaches the serve when the player is slow to do it: once `showAfter` seconds have passed in the player's serve without the ball being hit, a
// see-through "ghost" racket pulls back behind the ball and swings through it, with the tutorial hand holding it (the same hand as the menu's
// TapHint). It keeps looping, whatever the player does with the racket, until the serve is hit.
// Nothing is written on the screen: the ghost shows both who serves (it only appears when it is the player's serve) and how.
// Baseline style (Training): the serve is a press, a move of the pointer and a release. The same hand shows it: it presses on the screen (a ring), drags
// up and to one side (a trail follows) and lets go (a ring spreads), alternating the side each time; it shows up when the player is slow to serve.
public class ServeHint : MonoBehaviour
{
    [Header("Baseline style")]
    [Tooltip("Seconds of the player's serve before the hand shows how (the controls tutorial has already been seen at the first serve).")]
    public float baselineShowAfter = 2.5f;
    [Tooltip("Seconds of one demonstration (press, drag, release, rest).")]
    public float baselineLoop = 2.8f;
    [Tooltip("Where the hand presses (fractions of the screen, from the bottom left) and how far it drags (up, sideways) as a fraction of the screen's height.")]
    public Vector2 pressAt = new Vector2(0.5f, 0.3f);
    public Vector2 dragBy = new Vector2(0.2f, 0.26f);

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

    // ---------- baseline style: press, drag, release ----------
    RectTransform pressRing, releaseRing, trail;
    Image pressImage, releaseImage, trailImage;
    float baselineWaited, baselineShown, baselineVisibility;
    int demoCount;
    int lastDemo = -1;
    Sprite ringSprite;

    void BuildBaselineParts()
    {
        if (pressRing != null || area == null) return;
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
        pressRing = MakePart("DemoPress", out pressImage);
        releaseRing = MakePart("DemoRelease", out releaseImage);
        trail = MakePart("DemoTrail", out trailImage);
        trailImage.sprite = null;                 // a plain bar
        pressImage.sprite = releaseImage.sprite = ringSprite;
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

    void UpdateBaselineHint()
    {
        if (ghost != null && ghost.gameObject.activeSelf) ghost.gameObject.SetActive(false);
        BuildBaselineParts();
        bool serving = game.PlayerServing && !PokiService.AdPlaying && !game.TutorialHold;
        if (!serving)
        {
            baselineWaited = 0f; baselineShown = 0f;
            baselineVisibility = Mathf.MoveTowards(baselineVisibility, 0f, Time.unscaledDeltaTime * 5f);
        }
        else
        {
            baselineWaited += Time.unscaledDeltaTime;
            bool show = baselineWaited >= baselineShowAfter;
            baselineVisibility = Mathf.MoveTowards(baselineVisibility, show ? 1f : 0f, Time.unscaledDeltaTime * (show ? 3f : 6f));
            if (show) baselineShown += Time.unscaledDeltaTime;
        }
        ApplyBaseline();
    }

    void ApplyBaseline()
    {
        bool visible = baselineVisibility > 0.001f;
        if (hand != null && hand.gameObject.activeSelf != visible) hand.gameObject.SetActive(visible);
        if (pressRing == null) return;
        if (!visible) { pressRing.gameObject.SetActive(false); releaseRing.gameObject.SetActive(false); trail.gameObject.SetActive(false); return; }

        int demo = Mathf.FloorToInt(baselineShown / baselineLoop);
        float k = (baselineShown % baselineLoop) / baselineLoop;
        float side = demo % 2 == 0 ? -1f : 1f;                    // one time to the left, the next to the right
        Vector2 a = new Vector2(Screen.width * pressAt.x, Screen.height * pressAt.y);
        Vector2 b = a + new Vector2(side * Screen.height * dragBy.x, Screen.height * dragBy.y);
        Vector2 la = ToLocal(a), lb = ToLocal(b);

        // 0-0.18 the hand comes in and presses; 0.18-0.62 it drags; 0.62-0.78 it lets go; then it fades.
        float drag = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.62f, k));
        Vector2 pos = Vector2.Lerp(la, lb, drag);
        float fade = k < 0.1f ? k / 0.1f : (k > 0.82f ? 1f - Mathf.InverseLerp(0.82f, 1f, k) : 1f);
        float alpha = baselineVisibility * fade;

        // the ring where it pressed (stays while the finger is down)
        bool down = k >= 0.12f && k < 0.66f;
        pressRing.gameObject.SetActive(down);
        if (down)
        {
            pressRing.anchoredPosition = la;
            float s = 1f + 0.08f * Mathf.Sin(baselineShown * 9f);
            pressRing.sizeDelta = new Vector2(120f, 120f) * s;
            pressImage.color = new Color(1f, 1f, 1f, 0.85f * alpha);
        }
        // the trail from the press to the finger
        Vector2 d = pos - la;
        bool showTrail = down && d.magnitude > 8f;
        trail.gameObject.SetActive(showTrail);
        if (showTrail)
        {
            trail.sizeDelta = new Vector2(d.magnitude, 10f);
            trail.anchoredPosition = la + d * 0.5f;
            trail.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            trailImage.color = new Color(1f, 0.9f, 0.4f, 0.8f * alpha);
        }
        // the ring that spreads where it let go
        float rel = Mathf.InverseLerp(0.62f, 0.8f, k);
        bool burst = k >= 0.62f && k < 0.8f;
        releaseRing.gameObject.SetActive(burst);
        if (burst)
        {
            releaseRing.anchoredPosition = lb;
            releaseRing.sizeDelta = Vector2.one * Mathf.Lerp(90f, 230f, rel);
            releaseImage.color = new Color(1f, 0.9f, 0.4f, (1f - rel) * alpha);
        }

        if (hand != null)
        {
            hand.localScale = Vector3.one * handScale;
            hand.anchoredPosition = pos - fingertip * handScale;
            hand.localEulerAngles = new Vector3(0f, 0f, -12f);
            var c = handImage.color; c.a = alpha; handImage.color = c;
        }
    }

    void Update()
    {
        if (game == null) return;
        if (game.Baseline) { UpdateBaselineHint(); return; }
        if (ghost == null) return;
        // The character serves with its button: there is no swipe to teach.
        bool buttonServe = game.player is PlayerRacketController pc && pc.UsesButton;
        bool serving = game.PlayerServing && !PokiService.AdPlaying && !buttonServe;
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
