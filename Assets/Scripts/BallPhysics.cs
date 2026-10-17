using UnityEngine;

// Lightweight custom ball physics (no Rigidbody): gravity, table bounce, net.
public static class BallPhysics
{
    public const float Gravity = 9.81f;
    public const float Radius = 0.04f;
    public const float TableY = 0.76f;
    public const float HalfLength = 1.37f;
    public const float HalfWidth = 0.7625f;
    public const float NetHeight = 0.1525f;
    public const float NetHalfWidth = 0.9150f;
    public const float Restitution = 0.88f;
    public const float TableFriction = 0.97f;

    public static bool OnTable(Vector3 p)
    {
        return Mathf.Abs(p.x) <= HalfWidth && Mathf.Abs(p.z) <= HalfLength;
    }

    // Velocity that sends a ball from p to first bounce at landing (x, z) in time t.
    // Raises the arc (longer flight time) until it clears the net.
    public static Vector3 SolveHit(Vector3 p, float landX, float landZ, float t)
    {
        Vector3 v = Vector3.zero;
        float landY = TableY + Radius;
        for (int i = 0; i < 12; i++)
        {
            v.x = (landX - p.x) / t;
            v.z = (landZ - p.z) / t;
            v.y = (landY - p.y) / t + 0.5f * Gravity * t;

            float tn = Mathf.Abs(v.z) > 0.01f ? -p.z / v.z : -1f;
            if (tn <= 0f) break;
            float yn = p.y + v.y * tn - 0.5f * Gravity * tn * tn;
            if (yn > TableY + NetHeight + Radius + 0.04f) break;
            t *= 1.08f;
        }
        return v;
    }

    // Predicts where the ball crosses the plane z = planeZ (ignores net). False if it never does.
    public static bool Predict(Vector3 p, Vector3 v, float planeZ, out Vector3 hit)
    {
        const float dt = 1f / 120f;
        hit = p;
        for (int i = 0; i < 480; i++)
        {
            Vector3 prev = p;
            v.y -= Gravity * dt;
            p += v * dt;
            if (v.y < 0f && p.y - Radius < TableY && OnTable(p) && prev.y - Radius >= TableY - 0.05f)
            {
                p.y = TableY + Radius;
                v.y = -v.y * Restitution;
                v.x *= TableFriction;
                v.z *= TableFriction;
            }
            if ((prev.z - planeZ) * (p.z - planeZ) <= 0f && !Mathf.Approximately(prev.z, p.z))
            {
                float k = (planeZ - prev.z) / (p.z - prev.z);
                hit = Vector3.Lerp(prev, p, k);
                return true;
            }
            if (p.y < 0.2f) return false;
        }
        return false;
    }
}
