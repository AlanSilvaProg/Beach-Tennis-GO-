#if UNITY_EDITOR
using UnityEngine;

// Small drawing helpers for the OnDrawGizmos of the gameplay objects (editor only; nothing of this is in the build).
// What each object draws (switch Gizmos on in the Scene or Game view):
//   Court                 the playing area (lines of the court + net line), with the "out" tolerance of the CPU
//   Net                   the collision box of the net and its top edge
//   Racket                the area it can move in, the hit area (a cylinder: ground reach and height reach), the swept path of the last frame
//   Ball                  the collision sphere
//   GameManager           where the serve ball hangs for each side
//   AiRacketController    where it rests and the zone it aims its returns at (and the "short ball" line)
//   PlayerRacketController  where the racket rests
public static class GizmoShapes
{
    public static readonly Color Court = new Color(1f, 1f, 1f, 0.9f);
    public static readonly Color Net = new Color(1f, 0.3f, 0.25f, 1f);
    public static readonly Color PlayArea = new Color(0.2f, 0.85f, 1f, 0.9f);
    public static readonly Color HitArea = new Color(0.3f, 1f, 0.35f, 1f);
    public static readonly Color Serve = new Color(1f, 0.9f, 0.2f, 1f);
    public static readonly Color Aim = new Color(1f, 0.35f, 0.9f, 1f);
    public static readonly Color BallColor = new Color(0.9f, 1f, 0.2f, 1f);

    public static void Circle(Vector3 centre, float radius, Color color, int segments = 40)
    {
        Gizmos.color = color;
        Vector3 prev = centre + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector3 next = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    // A rectangle lying on the ground plane (y), given by its centre and half sizes.
    public static void Rect(Vector3 centre, float halfX, float halfZ, Color color)
    {
        Gizmos.color = color;
        var a = centre + new Vector3(-halfX, 0f, -halfZ);
        var b = centre + new Vector3(halfX, 0f, -halfZ);
        var c = centre + new Vector3(halfX, 0f, halfZ);
        var d = centre + new Vector3(-halfX, 0f, halfZ);
        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
    }

    // A cylinder standing on y = baseY: ground circle, top circle and four edges.
    public static void Cylinder(Vector3 baseCentre, float radius, float height, Color color)
    {
        Circle(baseCentre, radius, color);
        Circle(baseCentre + Vector3.up * height, radius, color);
        Gizmos.color = color;
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f;
            var p = baseCentre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(p, p + Vector3.up * height);
        }
    }

    public static void Cross(Vector3 at, float size, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawLine(at + new Vector3(-size, 0f, 0f), at + new Vector3(size, 0f, 0f));
        Gizmos.DrawLine(at + new Vector3(0f, 0f, -size), at + new Vector3(0f, 0f, size));
    }
}
#endif
