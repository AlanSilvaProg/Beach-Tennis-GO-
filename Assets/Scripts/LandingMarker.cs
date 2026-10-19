using UnityEngine;

// Soft radial pulse on the ground showing where the ball will come down. Follows the ball's current
// trajectory (so it updates after every hit) and hides while the ball is held, grounded or hidden.
[RequireComponent(typeof(Ball))]
public class LandingMarker : MonoBehaviour
{
    [Tooltip("Ground quad with the LandingPulse material.")]
    public Renderer marker;
    public float diameter = 1.6f;
    [Tooltip("Height above the ground (must clear the court lines and the shadow).")]
    public float lift = 0.02f;
    [Tooltip("Marker fades in over this many seconds of flight time remaining (0 = always full).")]
    public float fadeInTime = 0.6f;
    [Tooltip("Ignored below this speed (the ball hanging for the serve).")]
    public float minSpeed = 0.5f;

    static readonly int Intensity = Shader.PropertyToID("_Intensity");

    Ball ball;
    Court court;
    MaterialPropertyBlock block;

    void Awake()
    {
        ball = GetComponent<Ball>();
        court = FindFirstObjectByType<Court>();
        if (marker != null) marker.transform.localScale = new Vector3(diameter, diameter, 1f);
    }

    void OnValidate()
    {
        if (marker != null) marker.transform.localScale = new Vector3(diameter, diameter, 1f);
    }

    void LateUpdate()
    {
        if (marker == null || court == null) return;
        Vector3 v = ball.Velocity;
        bool flying = !ball.Grounded && v.sqrMagnitude > minSpeed * minSpeed;
        float t = 0f;
        Vector3 hit = default;
        if (flying)
        {
            // Time until the ball centre reaches the resting height: p.y + v.y t - g t^2 / 2 = y0.
            float y0 = court.groundY + ball.radius;
            float disc = v.y * v.y + 2f * ball.gravity * (ball.Position.y - y0);
            flying = disc >= 0f;
            if (flying)
            {
                t = (v.y + Mathf.Sqrt(disc)) / ball.gravity;
                hit = new Vector3(ball.Position.x + v.x * t, 0f, ball.Position.z + v.z * t);
            }
        }

        marker.enabled = flying;
        if (!flying) return;

        marker.transform.SetPositionAndRotation(new Vector3(hit.x, court.groundY + lift, hit.z), Quaternion.Euler(90f, 0f, 0f));
        if (block == null) block = new MaterialPropertyBlock();
        marker.GetPropertyBlock(block);
        block.SetFloat(Intensity, fadeInTime <= 0f ? 1f : Mathf.Clamp01(1f - t / fadeInTime * 0.5f));
        marker.SetPropertyBlock(block);
    }

    void OnDisable()
    {
        if (marker != null) marker.enabled = false;
    }
}
