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
}
