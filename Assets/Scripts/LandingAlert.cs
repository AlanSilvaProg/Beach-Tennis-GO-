using UnityEngine;
using UnityEngine.UI;

// HUD alert for when the landing marker is outside the camera view: a blinking ball icon on the screen edge plus a red arrow
// pointing to where the player has to go. Nothing is shown while the marker on the ground is visible (or there is no landing).
// Lives in the scene under the HUD canvas ("LandingAlert"): move/resize/recolour the children by hand.
public class LandingAlert : MonoBehaviour
{
    public LandingMarker marker;
    public Camera cam;
    public CanvasGroup group;
    [Tooltip("Ball icon (ball2D).")]
    public RectTransform icon;
    [Tooltip("Red arrow; points along +X before rotating. Leave the sprite empty to use a generated triangle.")]
    public Image arrow;

    [Tooltip("Distance from the screen edge to the icon centre (canvas units).")]
    public float edgePadding = 70f;
    [Tooltip("Gap between the icon centre and the arrow centre (canvas units).")]
    public float arrowDistance = 48f;
    [Tooltip("The ground marker counts as visible while this far inside the screen (viewport fraction), so the alert does not flicker at the border.")]
    [Range(0f, 0.2f)] public float visibleMargin = 0.03f;
    public float blinkSpeed = 9f;
    [Range(0f, 1f)] public float minAlpha = 0.25f;

    Canvas canvas;
    Sprite generated;

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (arrow != null && arrow.sprite == null) arrow.sprite = generated = MakeArrowSprite();
        Hide();
    }

    void OnDestroy()
    {
        if (generated != null) { Destroy(generated.texture); Destroy(generated); }
    }

    void LateUpdate()
    {
        if (marker == null) marker = FindFirstObjectByType<LandingMarker>();
        if (cam == null) cam = Camera.main;
        if (marker == null || cam == null || canvas == null || !marker.TryGetLanding(out Vector3 hit)) { Hide(); return; }

        Vector3 vp = cam.WorldToViewportPoint(hit);
        float m = visibleMargin;
        if (vp.z > 0f && vp.x > m && vp.x < 1f - m && vp.y > m && vp.y < 1f - m) { Hide(); return; }

        // Direction from the screen centre to the landing point (flipped when it is behind the camera).
        Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
        if (vp.z < 0f) dir = -dir;
        Vector2 size = new Vector2(Screen.width, Screen.height);
        Vector2 d = Vector2.Scale(dir, size);
        if (d.sqrMagnitude < 1e-4f) d = Vector2.up;
        d.Normalize();

        // Intersect the ray from the centre with the padded screen rectangle.
        float pad = edgePadding * canvas.scaleFactor;
        Vector2 half = size * 0.5f - new Vector2(pad, pad);
        float s = Mathf.Min(Mathf.Abs(d.x) > 1e-4f ? half.x / Mathf.Abs(d.x) : float.MaxValue,
                            Mathf.Abs(d.y) > 1e-4f ? half.y / Mathf.Abs(d.y) : float.MaxValue);
        Vector2 pos = (size * 0.5f + d * s) / canvas.scaleFactor;

        icon.anchoredPosition = pos;
        if (arrow != null)
        {
            arrow.rectTransform.anchoredPosition = pos + d * arrowDistance;
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }
        group.alpha = Mathf.Lerp(minAlpha, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * blinkSpeed));
    }

    void Hide()
    {
        if (group != null) group.alpha = 0f;
    }

    // White triangle pointing along +X (tinted by the Image colour).
    static Sprite MakeArrowSprite()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = Mathf.Abs((y + 0.5f) / n - 0.5f);
                float edge = (1f - u) * 0.5f - v;     // inside while the half-width shrinks towards the tip at u = 1
                byte a = (byte)(Mathf.Clamp01(edge * n * 0.5f) * 255f);
                px[y * n + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
