using UnityEngine;

// Keeps the whole court (both baselines and sidelines) in view for any screen shape.
// Pulls back a little while serving and moves in once the rally starts.
[RequireComponent(typeof(Camera))]
public class CameraRig : MonoBehaviour
{
    public Court court;
    [Tooltip("Point the camera always looks at.")]
    public Vector3 lookAt = new Vector3(0f, 0.6f, 1f);
    [Tooltip("Look-down angle in degrees.")]
    public float pitch = 32f;
    public float fieldOfView = 50f;
    [Tooltip("Extra court area (m) that must stay visible around the lines.")]
    public float margin = 0.15f;
    [Tooltip("Fraction of the screen kept free at the edges.")]
    [Range(0f, 0.3f)] public float viewportPadding = 0.02f;
    public float minDistance = 6f;
    public float fitStep = 0.2f;
    [Tooltip("How much further back the camera sits while serving.")]
    public float serveDistanceFactor = 1.1f;
    public float smoothTime = 0.6f;

    Camera cam;
    Vector3 playPos, servePos, velocity;
    bool serving = true;
    float lastAspect;

    void Awake()
    {
        cam = GetComponent<Camera>();
        Refit();
        transform.position = servePos;
        transform.LookAt(lookAt);
    }

    public void SetServing(bool value) { serving = value; }

    void LateUpdate()
    {
        if (!Mathf.Approximately(lastAspect, cam.aspect)) Refit();
        transform.position = Vector3.SmoothDamp(transform.position, serving ? servePos : playPos, ref velocity, smoothTime);
        transform.LookAt(lookAt);
    }

    void Refit()
    {
        lastAspect = cam.aspect;
        cam.fieldOfView = fieldOfView;
        Vector3 dir = new Vector3(0f, Mathf.Sin(pitch * Mathf.Deg2Rad), -Mathf.Cos(pitch * Mathf.Deg2Rad));
        Vector3 oldPos = transform.position;
        float hw = court.halfWidth + margin, hl = court.halfLength + margin;
        Vector3[] corners =
        {
            new Vector3(-hw, 0f, -hl), new Vector3(hw, 0f, -hl), new Vector3(-hw, 0f, hl), new Vector3(hw, 0f, hl)
        };
        float d = minDistance;
        for (int i = 0; i < 400; i++, d += fitStep)
        {
            transform.position = lookAt + dir * d;
            transform.LookAt(lookAt);
            bool ok = true;
            foreach (var c in corners)
            {
                Vector3 v = cam.WorldToViewportPoint(c);
                if (v.z <= 0f || v.x < viewportPadding || v.x > 1f - viewportPadding
                    || v.y < viewportPadding || v.y > 1f - viewportPadding) { ok = false; break; }
            }
            if (ok) break;
        }
        playPos = lookAt + dir * d;
        servePos = lookAt + dir * (d * serveDistanceFactor);
        transform.position = oldPos;
    }
}
