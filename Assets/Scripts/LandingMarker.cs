using System.Collections.Generic;
using UnityEngine;

// Small contracting waves on the ground showing where the ball will come down (they start at the full size and shrink to the centre). Follows the ball's current
// trajectory (so it updates after every hit) and hides while the ball is held, grounded or hidden.
// Baseline style (Training): the waves mark where the ball (coming to the player) will bounce, and an arc draws the path it will take after the
// bounce, up to the player's line; once the ball has bounced the waves go away and the arc shows what is left of the path.
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

    [Header("Arc after the bounce (baseline style)")]
    [Tooltip("Material of the arc (shader BeachTennisGO/LandingArc). Empty = no arc.")]
    public Material arcMaterial;
    public float arcWidth = 0.12f;
    public Color arcColor = new Color(1f, 0.95f, 0.45f, 0.95f);
    [Range(0f, 1f)] public float arcEndAlpha = 0.35f;

    static readonly int Intensity = Shader.PropertyToID("_Intensity");

    Ball ball;
    Court court;
    MaterialPropertyBlock block;
    float shownFor;
    GameManager game;
    LineRenderer arc;
    readonly List<Vector3> path = new List<Vector3>(256);
    Vector3[] points = new Vector3[256];

    void Awake()
    {
        ball = GetComponent<Ball>();
        court = FindFirstObjectByType<Court>();
        if (marker != null) marker.transform.localScale = new Vector3(diameter, diameter, 1f);
        if (arcMaterial != null)
        {
            var go = new GameObject("LandingArc");
            go.transform.SetParent(transform, false);
            arc = go.AddComponent<LineRenderer>();
            arc.sharedMaterial = arcMaterial;
            arc.useWorldSpace = true;
            arc.alignment = LineAlignment.View;
            arc.numCapVertices = 4;
            arc.numCornerVertices = 2;
            arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            arc.receiveShadows = false;
            arc.enabled = false;
        }
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
        if (Baseline && !(v.z < 0f && game.Bounces == 0 && game.CurrentState == GameManager.State.Rally)) return false; // only the first bounce of a ball coming to the player
        // Time until the ball centre reaches the resting height: p.y + v.y t - g t^2 / 2 = y0.
        float y0 = court.groundY + ball.radius;
        float disc = v.y * v.y + 2f * ball.gravity * (ball.Position.y - y0);
        if (disc < 0f) return false;
        float t = (v.y + Mathf.Sqrt(disc)) / ball.gravity;
        hit = new Vector3(ball.Position.x + v.x * t, court.groundY, ball.Position.z + v.z * t);
        return true;
    }

    bool Baseline
    {
        get
        {
            if (game == null) game = FindFirstObjectByType<GameManager>();
            return game != null && game.Baseline;
        }
    }

    // The arc: the path after the bounce (before it) or what is left of the path (after it), up to the player's line.
    void UpdateArc()
    {
        if (arc == null) return;
        bool show = false;
        if (Baseline && !ball.Grounded && game.CurrentState == GameManager.State.Rally && ball.Velocity.z < 0f && ball.Velocity.sqrMagnitude > minSpeed * minSpeed)
        {
            path.Clear();
            var f = ball.Predict(ball.Position, ball.Velocity, court, game.Net, game.BaselineZ, path, game.Bounces);
            int from = game.Bounces == 0 ? f.bounceIndex : 0;
            int n = path.Count - from;
            if (!f.hitNet && (game.Bounces > 0 || f.bounced) && n >= 2)
            {
                if (points.Length < n) points = new Vector3[n * 2];
                for (int i = 0; i < n; i++) points[i] = path[from + i];
                arc.positionCount = n;
                arc.SetPositions(points);
                float w = arcWidth;
                arc.widthMultiplier = w;
                var a = new Color(arcColor.r, arcColor.g, arcColor.b, arcColor.a);
                var b = new Color(arcColor.r, arcColor.g, arcColor.b, arcColor.a * arcEndAlpha);
                arc.startColor = a; arc.endColor = b;
                show = true;
            }
        }
        arc.enabled = show;
    }

    void LateUpdate()
    {
        if (marker == null || court == null) return;
        UpdateArc();
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
        if (arc != null) arc.enabled = false;
    }
}
