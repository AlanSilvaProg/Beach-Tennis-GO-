using UnityEngine;
using UnityEngine.UI;

// Teaches the serve when the player is slow to do it: once `showAfter` seconds have passed in the player's serve without the ball being hit, a
// see-through "ghost" racket pulls back behind the ball and swings through it, with the tutorial hand holding it (the same hand as the menu's
// TapHint). It keeps looping, whatever the player does with the racket, until the serve is hit.
// Nothing is written on the screen: the ghost shows both who serves (it only appears when it is the player's serve) and how.
public class ServeHint : MonoBehaviour
{
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

    void Update()
    {
        if (game == null || ghost == null) return;
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
