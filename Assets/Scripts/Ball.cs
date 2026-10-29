using System.Collections.Generic;
using UnityEngine;

// Ball with lightweight custom physics (no Rigidbody): gravity, net collision and ground contact.
public class Ball : MonoBehaviour
{
    [Header("Physics")]
    public float radius = 0.15f;
    public float gravity = 9.81f;
    [Tooltip("The ball is out of play when it flies this far from the court centre.")]
    public float maxDistance = 40f;

    [Header("Bounce (baseline style)")]
    [Tooltip("The ball bounces on the sand instead of stopping (set by the GameManager in the baseline style).")]
    public bool bouncy;
    [Tooltip("Vertical speed kept after a bounce.")]
    [Range(0.2f, 0.95f)] public float restitution = 0.55f;
    [Tooltip("Horizontal speed kept after a bounce.")]
    [Range(0.5f, 1f)] public float friction = 0.88f;
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
                return true;
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


    // Launch velocity for the baseline style: the ball leaves p, bounces once on the ground and then reaches the receiver's racket line
    // (z = arriveZ) near the top of its bounce, at aimX. The first flight takes t1 (simulated seconds) and is lengthened (higher arc) until it
    // clears the net. Aiming beyond the lines makes the ball bounce out.
    public Vector3 SolveBounceShot(Vector3 p, float aimX, float arriveZ, float t1, Net net, float groundY)
    {
        float g = gravity, y0 = groundY + radius;
        Vector3 v = Vector3.zero;
        for (int i = 0; i < 16; i++)
        {
            float vy = (y0 - p.y) / t1 + 0.5f * g * t1;
            float impact = Mathf.Max(0.5f, -(vy - g * t1));
            float vy2 = restitution * impact;
            float t2 = Mathf.Max(0.1f, 0.9f * vy2 / g);
            float k = friction * t2 / t1;
            float bx = (aimX + k * p.x) / (1f + k), bz = (arriveZ + k * p.z) / (1f + k);
            v = new Vector3((bx - p.x) / t1, vy, (bz - p.z) / t1);

            float tn = Mathf.Abs(v.z) > 0.01f ? -p.z / v.z : -1f;
            if (tn <= 0f || tn >= t1) break;
            float yn = p.y + v.y * tn - 0.5f * g * tn * tn;
            if (yn > net.TopY + radius + 0.25f) break;
            t1 *= 1.08f;
        }
        return v;
    }

    // Launch velocity that makes the ball touch the ground at (bx, bz) after t1 simulated seconds (the first bounce of the baseline style). The flight
    // is lengthened (higher arc) until it clears the net.
    public Vector3 SolveBounceAt(Vector3 p, float bx, float bz, float t1, Net net, float groundY)
    {
        float g = gravity, y0 = groundY + radius;
        Vector3 v = Vector3.zero;
        for (int i = 0; i < 16; i++)
        {
            float vy = (y0 - p.y) / t1 + 0.5f * g * t1;
            v = new Vector3((bx - p.x) / t1, vy, (bz - p.z) / t1);
            float tn = Mathf.Abs(v.z) > 0.01f ? -p.z / v.z : -1f;
            if (tn <= 0f || tn >= t1) break;
            float yn = p.y + v.y * tn - 0.5f * g * tn * tn;
            if (yn > net.TopY + radius + 0.25f) break;
            t1 *= 1.08f;
        }
        return v;
    }

    public struct Flight
    {
        public bool bounced;      // it touched the ground
        public Vector3 bounce;    // where it first touched it
        public int bounceIndex;   // index of that point in the path
        public bool reached;      // it got to the stop line after bouncing
        public Vector3 end;       // where it was when it got there (or where the prediction stopped)
        public bool hitNet;
    }

    // Walks a ball forward with the physics of Step (gravity, net, bounces) until it passes |z| = stopZ on the side it is heading to.
    // `path` (optional) gets a point every few steps; `bounceIndex` tells which one is the first bounce.
    public Flight Predict(Vector3 p, Vector3 v, Court court, Net net, float stopZ, List<Vector3> path = null, int bouncesSoFar = 0)
    {
        const float dt = 1f / 120f;
        var f = new Flight { end = p };
        float dir = v.z < 0f ? -1f : 1f;
        int bounces = bouncesSoFar;
        if (path != null) path.Add(p);
        for (int i = 0; i < 720; i++)
        {
            Vector3 prev = p;
            v.y -= gravity * dt;
            p += v * dt;
            if (prev.z * p.z < 0f && Mathf.Abs(p.x) < net.HalfWidth && p.y < net.TopY + radius) { f.hitNet = true; f.end = p; return f; }
            bool hit = false;
            if (p.y - radius <= court.groundY)
            {
                p.y = court.groundY + radius;
                hit = true;
                if (bounces == bouncesSoFar) { f.bounced = true; f.bounce = p; f.bounceIndex = path != null ? path.Count : 0; if (path != null) path.Add(p); }
                bounces++;
                if (!bouncy || v.y > -settleSpeed || bounces > 3) { f.end = p; return f; }
                v = new Vector3(v.x * friction, -v.y * restitution, v.z * friction);
            }
            if (path != null && (i % 3 == 0 || hit) && !hit) path.Add(p);
            if (bounces > 0 && dir * p.z >= stopZ)
            {
                f.reached = true; f.end = p;
                if (path != null) path.Add(p);
                return f;
            }
        }
        f.end = p;
        return f;
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
