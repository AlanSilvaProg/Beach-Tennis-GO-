using UnityEngine;

// Beach tennis court: playing area limits (used by the rules) and the sand/lines visuals.
public class Court : MonoBehaviour
{
    [Header("Dimensions (metres, from the centre)")]
    [Tooltip("Distance from the net to each baseline.")]
    public float halfLength = 8f;
    [Tooltip("Distance from the centre line to each sideline.")]
    public float halfWidth = 4.2f;
    public float groundY = 0f;

    [Header("Visuals")]
    public Transform area;
    public Transform lineNear, lineFar, lineLeft, lineRight, lineCentre;
    public float lineThickness = 0.08f;

    public bool Contains(Vector3 p)
    {
        return Mathf.Abs(p.x - transform.position.x) <= halfWidth && Mathf.Abs(p.z - transform.position.z) <= halfLength;
    }

    void OnValidate() { Fit(); }

    public void Fit()
    {
        FitRing();
        float y = groundY + 0.011f;
        if (area != null)
        {
            area.localPosition = new Vector3(0f, groundY + 0.005f, 0f);
            area.localScale = new Vector3(halfWidth * 2f, 0.01f, halfLength * 2f);
        }
        SetLine(lineNear, new Vector3(0f, y, -halfLength), new Vector3(halfWidth * 2f + lineThickness, 0.002f, lineThickness));
        SetLine(lineFar, new Vector3(0f, y, halfLength), new Vector3(halfWidth * 2f + lineThickness, 0.002f, lineThickness));
        SetLine(lineLeft, new Vector3(-halfWidth, y, 0f), new Vector3(lineThickness, 0.002f, halfLength * 2f));
        SetLine(lineRight, new Vector3(halfWidth, y, 0f), new Vector3(lineThickness, 0.002f, halfLength * 2f));
        SetLine(lineCentre, new Vector3(0f, y, 0f), new Vector3(halfWidth * 2f, 0.002f, lineThickness));
    }

    // Keeps the ad boards around the court in step with its dimensions.
    void FitRing()
    {
        var ring = FindFirstObjectByType<AdPanelRing>();
        if (ring != null && ring.court == this) ring.Fit();
    }

    static void SetLine(Transform t, Vector3 pos, Vector3 scale)
    {
        if (t == null) return;
        t.localPosition = pos;
        t.localScale = scale;
    }

#if UNITY_EDITOR
    // Gizmos: the playing area (what counts as "in") and the extra margin the CPU still plays as in; the net line.
    void OnDrawGizmos()
    {
        Vector3 c = transform.position + Vector3.up * (groundY + 0.02f);
        GizmoShapes.Rect(c, halfWidth, halfLength, GizmoShapes.Court);
        Gizmos.color = GizmoShapes.Court;
        Gizmos.DrawLine(c + new Vector3(-halfWidth, 0f, 0f), c + new Vector3(halfWidth, 0f, 0f)); // the net line
        var ai = FindFirstObjectByType<AiRacketController>();
        if (ai != null && ai.outTolerance > 0f)
            GizmoShapes.Rect(c, halfWidth + ai.outTolerance, halfLength + ai.outTolerance, new Color(1f, 1f, 1f, 0.3f));
    }
#endif
}
