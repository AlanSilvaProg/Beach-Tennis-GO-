using UnityEngine;

// Follows the match from behind the player's baseline. Nothing is scaled: the view is just closer.
//  - Distance: `distanceFactor` x the distance at which the whole court fits (below 1 = closer, rackets look bigger),
//    adjusted for the real screen shape: very wide screens zoom in a little, narrow ones (phones in portrait) back off a little.
//  - Centre of the view: `lookAt`.
//  - Sideways: slides with the player's racket (`followX`), up to `maxPan` metres.
//  - Pull-back: if the racket would leave the screen (e.g. going back for the serve), the camera backs off just
//    enough to keep it visible, but never further than `maxPullback` x the base distance.
//
// Runs in the editor too: changing any value here (or moving the court or the racket) updates the Game view
// straight away, so what you see is what the match starts with.
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class CameraRig : MonoBehaviour
{
    public Court court;
    [Tooltip("Racket the camera keeps in view and follows sideways. Found automatically when left empty.")]
    public Racket playerRacket;

    [Header("View")]
    [Tooltip("Centre of the view: the camera always looks at this point (before the sideways pan).")]
    public Vector3 lookAt = new Vector3(0f, 0.6f, 0.5f);
    [Tooltip("Look-down angle in degrees.")]
    public float pitch = 32f;
    public float fieldOfView = 50f;

    [Header("Distance")]
    [Tooltip("Screen shape (width / height) used to work out the whole-court distance. The camera distance does not change with the real screen shape, so portrait and landscape get the same position.")]
    public float referenceAspect = 1.6f;
    [Tooltip("Camera distance as a fraction of the distance that shows the whole court at the reference aspect. 1 = whole court, lower = closer.")]
    [Range(0.4f, 1.5f)] public float distanceFactor = 0.72f;
    [Tooltip("Extra court area (m) that must be visible when working out the whole-court distance.")]
    public float margin = 0.15f;
    [Tooltip("Fraction of the screen kept free at the edges for the whole-court distance.")]
    [Range(0f, 0.3f)] public float viewportPadding = 0.02f;
    public float minDistance = 3f;

    [Header("Screen shape (width / height of the real screen)")]
    [Tooltip("From this shape on the camera starts to zoom in, so a very wide screen does not show a small court in the middle of a lot of sand.")]
    public float wideStartAspect = 1.9f;
    [Tooltip("Shape at which the full wide zoom is reached.")]
    public float wideFullAspect = 2.5f;
    [Tooltip("Distance multiplier at the full wide shape (below 1 = closer).")]
    [Range(0.5f, 1f)] public float wideZoom = 0.8f;
    [Tooltip("On screens narrower than the reference shape the camera backs off to show more of the court width: this is the share of the distance the whole court would need (0 = never, 1 = fit it completely).")]
    [Range(0f, 1f)] public float narrowStrength = 0.6f;
    [Tooltip("Furthest the camera may back off for a narrow screen, as a multiple of the normal distance.")]
    [Range(1f, 2f)] public float narrowMaxFactor = 1.4f;

    [Header("Sideways follow")]
    [Tooltip("How much of the racket's sideways position the camera copies (0 = fixed, 1 = locked to the racket).")]
    [Range(0f, 1f)] public float followX = 0.6f;
    [Tooltip("Furthest the camera slides sideways from the centre (m).")]
    public float maxPan = 2f;

    [Header("Keep the racket in view")]
    [Tooltip("Screen border (fraction) the racket must stay inside; the camera backs off to respect it.")]
    public Vector2 racketPadding = new Vector2(0.08f, 0.1f);
    [Tooltip("Furthest the camera may back off, as a multiple of the base distance.")]
    [Range(1f, 2f)] public float maxPullback = 1.3f;
    [Tooltip("Extra distance (multiple of the base) kept while serving, on top of what the racket needs.")]
    [Range(1f, 1.5f)] public float serveDistanceFactor = 1f;
    [Tooltip("How long the camera takes to catch up with its target (s). Lower = faster.")]
    [Min(0.01f)] public float moveSmoothTime = 0.2f;

    [Header("Editor preview")]
    [Tooltip("Edit mode only: preview the serve view (true) or the rally view (false).")]
    public bool previewServing = true;

    const int PullbackSteps = 12;

    Camera cam;
    Vector3 velocity;
    float fitDistance;
    float aspectFactor = 1f;
    float lastAspect;
    bool serving = true;
    Vector3 dir;

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        FindRacket();
        Snap();
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled || Application.isPlaying) return;
        cam = GetComponent<Camera>();
        Snap();
    }

    // Puts the camera straight at its target (used at start and in the editor, where there is no smoothing).
    void Snap()
    {
        if (cam == null || court == null) return;
        Refit();
        // Only write when something changed, so the editor does not mark the scene dirty on every repaint.
        Quaternion rot = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 pos = Target();
        if (transform.rotation != rot) transform.rotation = rot;
        if ((transform.position - pos).sqrMagnitude > 1e-8f) transform.position = pos;
        velocity = Vector3.zero;
    }

    public void SetServing(bool value) { serving = value; }

    void FindRacket()
    {
        if (playerRacket != null) return;
        var controller = FindFirstObjectByType<PlayerRacketController>();
        if (controller != null) playerRacket = controller.Racket;
    }

    void LateUpdate()
    {
        if (court == null) return;
        if (!Application.isPlaying) { Snap(); return; }
        if (Mathf.Abs(cam.aspect - lastAspect) > 0.001f) Refit(); // the window was resized / the phone turned
        transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        transform.position = Vector3.SmoothDamp(transform.position, Target(), ref velocity, moveSmoothTime);
    }

    float BaseDistance => Mathf.Max(minDistance, fitDistance * distanceFactor * aspectFactor);

    // Where the camera wants to be: panned to the racket, then backed off until the racket fits.
    Vector3 Target()
    {
        float pan = 0f;
        if (playerRacket != null) pan = Mathf.Clamp(playerRacket.Position.x * followX, -maxPan, maxPan);
        Vector3 centre = lookAt + new Vector3(pan, 0f, 0f);

        bool serve = Application.isPlaying ? serving : previewServing;
        float lo = BaseDistance * (serve ? serveDistanceFactor : 1f);
        float hi = Mathf.Max(lo, BaseDistance * maxPullback);
        float d = hi;
        for (int i = 0; i <= PullbackSteps; i++)
        {
            float candidate = Mathf.Lerp(lo, hi, i / (float)PullbackSteps);
            if (RacketVisible(centre + dir * candidate)) { d = candidate; break; }
        }
        return centre + dir * d;
    }

    // Is the racket (its hit circle) inside the padded screen if the camera sits at `pos`?
    bool RacketVisible(Vector3 pos)
    {
        if (playerRacket == null) return true;
        Vector3 c = playerRacket.Position;
        float r = playerRacket.hitRadius;
        return Inside(new Vector3(c.x, c.y, c.z - r), pos) && Inside(new Vector3(c.x - r, c.y, c.z), pos)
            && Inside(new Vector3(c.x + r, c.y, c.z), pos) && Inside(new Vector3(c.x, c.y, c.z + r), pos);
    }

    bool Inside(Vector3 world, Vector3 camPos)
    {
        if (!Project(world, camPos, cam.aspect, out Vector2 vp)) return false;
        return vp.x >= racketPadding.x && vp.x <= 1f - racketPadding.x && vp.y >= racketPadding.y && vp.y <= 1f - racketPadding.y;
    }

    // Viewport position of a world point for a camera at `camPos` (rotation is always the same pitch, no yaw).
    bool Project(Vector3 world, Vector3 camPos, float aspect, out Vector2 vp)
    {
        Vector3 local = Quaternion.Inverse(Quaternion.Euler(pitch, 0f, 0f)) * (world - camPos);
        float tanV = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
        vp = Vector2.zero;
        if (local.z <= 0.01f) return false;
        vp = new Vector2(0.5f + 0.5f * local.x / (local.z * tanV * aspect), 0.5f + 0.5f * local.y / (local.z * tanV));
        return true;
    }

    // Distance at which the whole court is visible at the given screen shape.
    float FitDistance(float aspect)
    {
        float hw = court.halfWidth + margin, hl = court.halfLength + margin;
        Vector3[] corners =
        {
            new Vector3(-hw, 0f, -hl), new Vector3(hw, 0f, -hl), new Vector3(-hw, 0f, hl), new Vector3(hw, 0f, hl)
        };
        Vector3 centre = new Vector3(0f, lookAt.y, lookAt.z);
        float d = minDistance;
        for (int i = 0; i < 400; i++, d += 0.2f)
        {
            bool ok = true;
            foreach (var c in corners)
            {
                if (!Project(c, centre + dir * d, aspect, out Vector2 v)
                    || v.x < viewportPadding || v.x > 1f - viewportPadding || v.y < viewportPadding || v.y > 1f - viewportPadding) { ok = false; break; }
            }
            if (ok) break;
        }
        return d;
    }

    // The whole-court distance at the reference screen shape (the same position on every screen), then the correction for the real shape.
    void Refit()
    {
        cam.fieldOfView = fieldOfView;
        dir = new Vector3(0f, Mathf.Sin(pitch * Mathf.Deg2Rad), -Mathf.Cos(pitch * Mathf.Deg2Rad));
        fitDistance = FitDistance(referenceAspect);

        float aspect = cam.aspect;
        lastAspect = aspect;
        float factor = 1f;
        if (aspect < referenceAspect)
        {
            // Narrower than the reference: the whole court would need more distance; take part of that, up to a limit.
            float ratio = FitDistance(aspect) / fitDistance;
            factor = Mathf.Min(narrowMaxFactor, 1f + Mathf.Max(0f, ratio - 1f) * narrowStrength);
        }
        else if (aspect > wideStartAspect)
        {
            factor = Mathf.Lerp(1f, wideZoom, Mathf.InverseLerp(wideStartAspect, Mathf.Max(wideStartAspect + 0.01f, wideFullAspect), aspect));
        }
        aspectFactor = factor;
    }
}
