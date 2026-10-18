using UnityEngine;

// Stylised ball trail: a wide coloured ribbon with a bright thin core. It stretches, brightens and shifts
// colour as the ball speeds up, and stays off while the ball hangs still (serve) or is hidden.
[RequireComponent(typeof(Ball))]
public class BallTrail : MonoBehaviour
{
    [Header("Renderers")]
    public TrailRenderer outer;
    public TrailRenderer core;

    [Header("Shape")]
    [Tooltip("How long the trail lasts at slow and at fast speed (seconds).")]
    public Vector2 time = new Vector2(0.25f, 0.5f);
    [Tooltip("Width of the outer ribbon at slow and at fast speed (m).")]
    public Vector2 width = new Vector2(0.22f, 0.34f);
    [Tooltip("Core width as a fraction of the outer width.")]
    [Range(0.05f, 1f)] public float coreWidthScale = 0.35f;
    [Tooltip("Width along the trail: 0 = ball, 1 = tail tip.")]
    public AnimationCurve widthCurve = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(0.35f, 0.7f), new Keyframe(1f, 0f));

    [Header("Colours")]
    public Color slowColor = new Color(1f, 0.78f, 0.15f, 0.85f);
    public Color fastColor = new Color(1f, 0.25f, 0.4f, 0.95f);
    public Color coreColor = new Color(1f, 1f, 0.9f, 0.95f);
    [Tooltip("Colour of the tail tip (alpha 0 = fades out).")]
    public Color tailColor = new Color(1f, 0.4f, 0.1f, 0f);

    [Header("Speed range (m/s) mapped to slow..fast")]
    public float minSpeed = 3f;
    public float maxSpeed = 22f;
    [Tooltip("The trail only draws above this speed.")]
    public float emitAbove = 0.5f;

    Ball ball;

    void Awake()
    {
        ball = GetComponent<Ball>();
        ApplyStatic();
    }

    void OnValidate() { ApplyStatic(); }

    void ApplyStatic()
    {
        foreach (var t in new[] { outer, core })
        {
            if (t == null) continue;
            t.widthCurve = widthCurve;
            t.numCapVertices = 6;
            t.numCornerVertices = 4;
            t.minVertexDistance = 0.05f;
            t.alignment = LineAlignment.View;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.receiveShadows = false;
            t.autodestruct = false;
        }
    }

    // Wipes the old streak (call after teleporting the ball).
    public void Clear()
    {
        if (outer != null) outer.Clear();
        if (core != null) core.Clear();
    }

    void LateUpdate()
    {
        if (ball == null) ball = GetComponent<Ball>();
        float speed = ball.Velocity.magnitude;
        bool emit = speed > emitAbove;
        float k = Mathf.InverseLerp(minSpeed, maxSpeed, speed);

        if (outer != null)
        {
            outer.emitting = emit;
            outer.time = Mathf.Lerp(time.x, time.y, k);
            outer.widthMultiplier = Mathf.Lerp(width.x, width.y, k);
            outer.startColor = Color.Lerp(slowColor, fastColor, k);
            outer.endColor = tailColor;
        }
        if (core != null)
        {
            core.emitting = emit;
            core.time = Mathf.Lerp(time.x, time.y, k) * 0.7f;
            core.widthMultiplier = Mathf.Lerp(width.x, width.y, k) * coreWidthScale;
            core.startColor = coreColor;
            Color end = coreColor; end.a = 0f;
            core.endColor = end;
        }
    }
}
