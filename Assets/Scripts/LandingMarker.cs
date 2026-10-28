using UnityEngine;

// Small contracting waves on the ground showing where the ball will come down (they start at the full size and shrink to the centre). Follows the ball's current
// trajectory (so it updates after every hit) and hides while the ball is held, grounded or hidden.
[RequireComponent(typeof(Ball))]
public class LandingMarker : MonoBehaviour
{
    [Tooltip("Ground quad with the LandingPulse material.")]
    public Renderer marker;
    public float diameter = 1.6f;
    [Tooltip("Height above the ground (must clear the court lines and the shadow).")]
    public float lift = 0.02f;
    [Tooltip("The marker fades in over this many seconds once the ball has been hit (0 = instantly).")]
    public float appearTime = 0.1f;
    [Tooltip("Ignored below this speed (the ball hanging for the serve).")]
    public float minSpeed = 0.5f;

    static readonly int Intensity = Shader.PropertyToID("_Intensity");

    Ball ball;
    Court court;
    MaterialPropertyBlock block;
    float shownFor;

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

    // Where the ball will come down on its current trajectory; false while it is held, grounded or too slow.
    public bool TryGetLanding(out Vector3 hit)
    {
        hit = default;
        if (court == null) court = FindFirstObjectByType<Court>();
        if (court == null || ball == null) return false;
        Vector3 v = ball.Velocity;
        if (ball.Grounded || v.sqrMagnitude <= minSpeed * minSpeed) return false;
        // Time until the ball centre reaches the resting height: p.y + v.y t - g t^2 / 2 = y0.
        float y0 = court.groundY + ball.radius;
        float disc = v.y * v.y + 2f * ball.gravity * (ball.Position.y - y0);
        if (disc < 0f) return false;
        float t = (v.y + Mathf.Sqrt(disc)) / ball.gravity;
        hit = new Vector3(ball.Position.x + v.x * t, court.groundY, ball.Position.z + v.z * t);
        return true;
    }

    void LateUpdate()
    {
        if (marker == null || court == null) return;
        bool flying = TryGetLanding(out Vector3 hit);

        marker.enabled = flying;
        if (!flying) { shownFor = 0f; return; }
        shownFor += Time.deltaTime;

        marker.transform.SetPositionAndRotation(new Vector3(hit.x, hit.y + lift, hit.z), Quaternion.Euler(90f, 0f, 0f));
        if (block == null) block = new MaterialPropertyBlock();
        marker.GetPropertyBlock(block);
        block.SetFloat(Intensity, appearTime <= 0f ? 1f : Mathf.Clamp01(shownFor / appearTime));
        marker.SetPropertyBlock(block);
    }

    void OnDisable()
    {
        if (marker != null) marker.enabled = false;
    }
}
