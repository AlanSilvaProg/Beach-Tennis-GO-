using UnityEngine;

// Racket body: moves on a horizontal plane (never up/down), hit area and visual lean.
// Behaviour is added by a RacketController.
public class Racket : MonoBehaviour
{
    [Header("Model")]
    public Transform model;
    [Tooltip("Uniform scale applied to the racket model.")]
    public float modelScale = 1.6f;
    [Tooltip("Distance from the model pivot (handle) to the centre of the head, in unscaled model units.")]
    public float headCenter = 0.68f;
    [Tooltip("Yaw of the racket face: 0 = player side (faces +Z), 180 = opponent side (faces -Z).")]
    public float facingYaw = 0f;

    [Header("Play area")]
    [Tooltip("Fixed height of the racket head.")]
    public float height = 1.0f;
    [Tooltip("Closest the racket can get to the net.")]
    public float minDistance = 0.6f;
    [Tooltip("Furthest the racket can get from the net.")]
    public float maxDistance = 8.5f;
    public float maxX = 4.7f;
    [Tooltip("When off, maxDistance and maxX are ignored: the controller decides how far the racket can go (the player is limited only by the screen).")]
    public bool limitToArea = true;

    [Header("Hit area")]
    [Tooltip("Reach of the racket on the ground plane. Height is ignored: a ball inside this circle is always hit, at any height.")]
    public float hitRadius = 0.7f;

    [Header("Feel")]
    [Tooltip("How quickly the measured racket velocity follows the real one.")]
    public float velocitySmoothing = 25f;
    public float rollPerSpeed = 4f;
    public float maxRoll = 35f;
    public float pitchPerSpeed = 5f;
    public float maxPitch = 30f;
    public float restPitch = 0f;
    [Tooltip("Roll (degrees) when the racket is at the side limit; it leans outwards the further it is from the centre.")]
    public float edgeRoll = 40f;

    public Vector3 Velocity { get; private set; }
    public Vector3 Position => transform.position;
    // -1 for the player side (z < 0), +1 for the opponent side (z > 0).
    public float SideSign => Mathf.Cos(facingYaw * Mathf.Deg2Rad) >= 0f ? -1f : 1f;
    // Speed towards the net (m/s); negative when moving backwards.
    public float ForwardSpeed => -SideSign * Velocity.z;
    public float SideSpeed => Velocity.x;

    Vector3 lastPos;
    Vector3 smoothVel;

    void OnValidate() { ApplyModel(); }
    void Awake() { ApplyModel(); lastPos = transform.position; }

    void ApplyModel()
    {
        if (model == null) return;
        model.localScale = Vector3.one * modelScale;
        model.localPosition = new Vector3(0f, -headCenter * modelScale, 0f);
    }

    public Vector3 Clamp(Vector3 p)
    {
        float d = Mathf.Max(Mathf.Abs(p.z), minDistance);
        float x = p.x;
        if (limitToArea)
        {
            d = Mathf.Min(d, maxDistance);
            x = Mathf.Clamp(x, -maxX, maxX);
        }
        return new Vector3(x, height, SideSign * d);
    }

    public void MoveTo(Vector3 p)
    {
        transform.position = Clamp(p);
    }

    void LateUpdate()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 v = (transform.position - lastPos) / dt;
        smoothVel = Vector3.Lerp(smoothVel, v, 1f - Mathf.Exp(-dt * velocitySmoothing));
        Velocity = smoothVel;
        lastPos = transform.position;

        float roll = Mathf.Clamp(SideSpeed * rollPerSpeed * SideSign, -maxRoll, maxRoll)
                     + SideSign * Mathf.Clamp(transform.position.x / maxX, -1f, 1f) * edgeRoll;
        float pitch = Mathf.Clamp(ForwardSpeed * pitchPerSpeed, -maxPitch, maxPitch) + restPitch;
        transform.rotation = Quaternion.Euler(0f, facingYaw, 0f) * Quaternion.Euler(pitch, 0f, roll);
    }
}
