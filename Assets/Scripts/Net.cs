using UnityEngine;

// The beach tennis net (model includes the posts): size and how the ball reacts when it hits it.
public class Net : MonoBehaviour
{
    [Header("Dimensions (metres)")]
    [Tooltip("Height of the top of the net (and its posts) above the ground; the ball collides below this.")]
    public float height = 1.7f;
    public float width = 10.2f;
    public float thickness = 0.04f;
    [Tooltip("Visible height of the net band.")]
    public float bandHeight = 1.0f;

    [Header("Collision")]
    [Tooltip("Fraction of the ball's forward speed kept (and reversed) after hitting the net.")]
    [Range(0f, 1f)] public float bounceBack = 0.25f;
    [Tooltip("Fraction of the ball's sideways speed kept after hitting the net.")]
    [Range(0f, 1f)] public float sideDamping = 0.5f;

    [Header("Visuals")]
    [Tooltip("Net model; its pivot must be at the bottom centre.")]
    public Transform band;
    [Tooltip("Size of the net model at scale 1 (width, height, thickness); it is stretched to the dimensions above.")]
    public Vector3 modelSize = new Vector3(1f, 0.37f, 0.03f);

    public float TopY => transform.position.y + height;
    public float HalfWidth => width * 0.5f;

    void OnValidate() { Fit(); }

    public void Fit()
    {
        if (band != null)
        {
            band.localPosition = new Vector3(0f, height - bandHeight, 0f);
            band.localScale = new Vector3(width / modelSize.x, bandHeight / modelSize.y, thickness / modelSize.z);
        }
    }

#if UNITY_EDITOR
    // Gizmos: the box the ball collides with (red) and the top edge of the net.
    void OnDrawGizmos()
    {
        Vector3 size = new Vector3(width, height, Mathf.Max(thickness, 0.02f));
        Gizmos.color = GizmoShapes.Net;
        Gizmos.DrawWireCube(transform.position + Vector3.up * (height * 0.5f), size);
        Gizmos.DrawLine(transform.position + new Vector3(-HalfWidth, height, 0f), transform.position + new Vector3(HalfWidth, height, 0f));
    }
#endif
}
