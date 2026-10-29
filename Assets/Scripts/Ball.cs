using UnityEngine;

// Ball with lightweight custom physics (no Rigidbody): gravity, net collision and ground contact.
public class Ball : MonoBehaviour
{
    [Header("Physics")]
    public float radius = 0.15f;
    public float gravity = 9.81f;
    [Tooltip("The ball is out of play when it flies this far from the court centre.")]
    public float maxDistance = 40f;

    [Header("Bounce")]
    [Tooltip("The ball bounces on the sand instead of stopping (set by the GameManager in an offline match).")]
    public bool bouncy;
    [Tooltip("Vertical speed kept after a bounce.")]
    [Range(0.2f, 0.95f)] public float restitution = 0.6f;
    [Tooltip("Horizontal speed kept after a bounce.")]
    [Range(0.3f, 1f)] public float friction = 0.7f;
    [Tooltip("A bounce whose impact is slower than this (m/s) is the last one: the ball stops.")]
    public float settleSpeed = 1.2f;

    [Header("Visual")]
    [Tooltip("Child holding the ball mesh; scaled to the radius.")]
    public Transform body;
    public Transform shadow;
    public float shadowSize = 0.4f;

    public Vector3 Velocity;
    public Vector3 Position;
    public bool Grounded { get; private set; }

    void OnValidate()
    {
        if (body != null) body.localScale = Vector3.one * radius * 2f;
    }

    public void Place(Vector3 pos, Vector3 vel)
    {
        Position = pos;
        Velocity = vel;
        Grounded = false;
        Sync(0f);
        var trail = GetComponent<BallTrail>();
        if (trail != null) trail.Clear();
    }

    // Sets where the ball is and how it moves without clearing its trail (the other player's match sends this about 20 times a second).
    public void SetState(Vector3 pos, Vector3 vel)
    {
        Position = pos;
        Velocity = vel;
        Grounded = false;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
        if (shadow != null) shadow.gameObject.SetActive(visible);
    }

    // Pushes the simulated position to the transform and keeps the shadow on the ground.
    public void Sync(float groundY)
    {
        transform.position = Position;
        if (shadow == null) return;
        shadow.position = new Vector3(Position.x, groundY + 0.015f, Position.z);
        shadow.localScale = new Vector3(shadowSize, 0.001f, shadowSize);
    }

    // Advances one physics step. Returns true on the step where the ball touches the ground.
    public bool Step(float dt, Court court, Net net, out Vector3 prev)
    {
        prev = Position;
        if (Grounded) return false;

        Velocity.y -= gravity * dt;
        Position += Velocity * dt;

        if (prev.z * Position.z < 0f && Mathf.Abs(Position.x) < net.HalfWidth && Position.y < net.TopY + radius)
        {
            Position.z = prev.z;
            Velocity.z = -Velocity.z * net.bounceBack;
            Velocity.x *= net.sideDamping;
        }

        if (Position.y - radius <= court.groundY)
        {
            Position.y = court.groundY + radius;
            if (bouncy && Velocity.y < -settleSpeed)
            {
                Velocity = new Vector3(Velocity.x * friction, -Velocity.y * restitution, Velocity.z * friction);
                return true;   // a bounce: the ball is still in play
            }
            Velocity = Vector3.zero;
            Grounded = true;
            return true;
        }
        return false;
    }

    // Velocity that sends the ball from p so that it reaches (landX, targetY, landZ) in time t.
    // Lengthens the flight (higher arc) until the ball clears the net.
    public Vector3 SolveHit(Vector3 p, float landX, float landZ, float targetY, float t, Net net)
    {
        Vector3 v = Vector3.zero;
        for (int i = 0; i < 16; i++)
        {
            v.x = (landX - p.x) / t;
            v.z = (landZ - p.z) / t;
            v.y = (targetY - p.y) / t + 0.5f * gravity * t;

            float tn = Mathf.Abs(v.z) > 0.01f ? -p.z / v.z : -1f;
            if (tn <= 0f) break;
            float yn = p.y + v.y * tn - 0.5f * gravity * tn * tn;
            if (yn > net.TopY + radius + 0.25f) break;
            t *= 1.08f;
        }
        return v;
    }

    // Predicts where a ball with position p and velocity v comes down through height y.
    // If it reaches the ground first, returns false and the landing point.
    public bool PredictAtHeight(Vector3 p, Vector3 v, float y, Court court, out Vector3 hit)
    {
        const float dt = 1f / 120f;
        hit = p;
        for (int i = 0; i < 960; i++)
        {
            Vector3 prev = p;
            v.y -= gravity * dt;
            p += v * dt;
            if (v.y < 0f && prev.y >= y && p.y <= y)
            {
                hit = Vector3.Lerp(prev, p, (prev.y - y) / (prev.y - p.y));
                return true;
            }
            if (p.y - radius <= court.groundY) { hit = p; return false; }
        }
        return false;
    }

#if UNITY_EDITOR
    // Gizmos: the collision sphere (where the ball is while the game runs, where the transform is otherwise).
    void OnDrawGizmos()
    {
        Gizmos.color = GizmoShapes.BallColor;
        Gizmos.DrawWireSphere(Application.isPlaying ? Position : transform.position, radius);
    }
#endif
}
