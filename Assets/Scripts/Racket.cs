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
    [Tooltip("Not used to move the racket any more (only the net, Min Distance, limits it); kept for the gizmos and the fall pose.")]
    public float maxDistance = 8.5f;
    public float maxX = 4.7f;
    [Tooltip("When off, maxDistance and maxX are ignored and the controller decides how far the racket can go.")]
    public bool limitToArea = true;

    [Header("Hit area")]
    [Tooltip("Reach of the racket on the ground plane: a ball inside this circle (and below the height reach) is hit.")]
    public float hitRadius = 0.7f;
    [Tooltip("Vertical reach above the racket head (m). A ball higher than this passes over the racket; the landing marker shows where to meet it.")]
    public float hitHeight = 0.9f;

    [Header("Feel")]
    [Tooltip("How quickly the measured racket velocity follows the real one.")]
    public float velocitySmoothing = 25f;

    [Header("Swing detection")]
    [Tooltip("Force of a swing is the fastest the racket moved in this window before contact (s).")]
    public float powerWindow = 0.12f;
    [Tooltip("Direction of a swing is the average movement over this short window just before contact (s).")]
    public float directionWindow = 0.05f;
    public float rollPerSpeed = 4f;
    public float maxRoll = 35f;
    public float pitchPerSpeed = 5f;
    public float maxPitch = 30f;
    public float restPitch = 0f;
    [Tooltip("Roll (degrees) when the racket is at the side limit; it leans outwards the further it is from the centre.")]
    public float edgeRoll = 40f;

    [Header("Leaning towards the ball")]
    [Tooltip("While the ball is far away, or going away, the racket leans with the side of the court it is on (Edge Roll). Once the ball is coming and gets this close (m), it leans towards the ball instead.")]
    public float ballLeanNear = 3f;
    [Tooltip("The lean towards the ball starts to take over at this distance (m) from the racket.")]
    public float ballLeanFar = 6f;
    [Tooltip("Sideways distance (m) between the ball and the racket at which the lean towards the ball is at its full Edge Roll.")]
    public float ballLeanReach = 1.2f;
    [Tooltip("How quickly the lean follows its target (higher = snappier).")]
    public float leanSmoothing = 12f;

    public Vector3 Velocity { get; private set; }
    public Vector3 Position => transform.position;
    // Where the racket was at the end of the previous frame. PreviousPosition -> Position is the path swept this frame, so a very fast
    // swipe cannot jump over the ball between two frames.
    public Vector3 PreviousPosition => lastPos;
    // The point of that path where the racket met the ball (set by the GameManager just before the shot is computed).
    public Vector3 HitCenter { get; set; }
    // Highest ball height (world y) the racket can still hit.
    public float MaxHitY => height + hitHeight;
    // -1 for the player side (z < 0), +1 for the opponent side (z > 0).
    public float SideSign => Mathf.Cos(facingYaw * Mathf.Deg2Rad) >= 0f ? -1f : 1f;
    // Speed towards the net (m/s); negative when moving backwards.
    public float ForwardSpeed => -SideSign * Velocity.z;
    public float SideSpeed => Velocity.x;
    // Same two components, taken from SwingVelocity (more accurate for a fast swipe).
    public float SwingForwardSpeed => -SideSign * SwingVelocity.z;
    public float SwingSideSpeed => SwingVelocity.x;

    // The end-of-match poses: the winner flips and hops from side to side, the loser drops to the ground. ClearPose (SnapSweep, i.e. every
    // ResetPosition) gives the racket back to its controller.
    enum Pose { None, Celebrate, Fall }
    [Header("End of match poses")]
    [Tooltip("Celebration: height of the somersault (m) and how long it takes (s).")]
    public float flipHeight = 1.1f;
    public float flipTime = 0.85f;
    [Tooltip("Celebration: the little hops after the somersault: height (m), duration of each (s), side swing (m) and tilt (degrees).")]
    public float hopHeight = 0.3f;
    public float hopTime = 0.38f;
    public float hopSwing = 0.8f;
    public float hopTilt = 22f;
    [Tooltip("Fall: seconds to reach the ground and the height of the racket head when it lies there.")]
    public float fallTime = 0.5f;
    public float lieHeight = 0.14f;
    Pose pose;
    float poseTime;
    Vector3 poseOrigin;

    public bool Posing => pose != Pose.None;
    public void Celebrate() { pose = Pose.Celebrate; poseTime = 0f; poseOrigin = transform.position; }
    public void Fall() { pose = Pose.Fall; poseTime = 0f; poseOrigin = transform.position; }
    public void ClearPose()
    {
        if (pose == Pose.None) return;
        pose = Pose.None;
        transform.position = Clamp(poseOrigin); // back where it stood, at its normal height
        lastPos = transform.position;
    }

    void ApplyPose(float dt)
    {
        poseTime += dt;
        Vector3 p = poseOrigin;
        float pitch = restPitch, roll = 0f, yaw = 0f;
        if (pose == Pose.Celebrate)
        {
            if (poseTime < flipTime)
            {
                float u = poseTime / flipTime;
                p.y = height + flipHeight * 4f * u * (1f - u);
                pitch = 360f * Mathf.SmoothStep(0f, 1f, u);
            }
            else
            {
                float k = (poseTime - flipTime) / Mathf.Max(0.05f, hopTime);
                float frac = k - Mathf.Floor(k);
                p.y = height + hopHeight * 4f * frac * (1f - frac);
                float limit = limitToArea ? maxX : 100f;
                p.x = Mathf.Clamp(poseOrigin.x + hopSwing * Mathf.Sin(k * Mathf.PI), -limit, limit);
                roll = hopTilt * Mathf.Cos(k * Mathf.PI);
            }
        }
        else
        {
            float u = Mathf.Clamp01(poseTime / Mathf.Max(0.05f, fallTime));
            float ease = u * u;
            float after = poseTime - fallTime;
            p.y = Mathf.Lerp(height, lieHeight, ease) + (after > 0f ? 0.1f * Mathf.Abs(Mathf.Sin(after * 11f)) * Mathf.Exp(-after * 7f) : 0f);
            pitch = Mathf.Lerp(restPitch, -88f, ease);   // lies on its back, the face up
            roll = Mathf.Lerp(0f, 12f, ease);
            yaw = Mathf.Lerp(0f, 28f, ease);
        }
        transform.position = p;
        transform.rotation = Quaternion.Euler(0f, facingYaw + yaw, 0f) * Quaternion.Euler(pitch, 0f, roll);
        lastPos = p;
        Velocity = smoothVel = Vector3.zero;
    }

    Vector3 lastPos;
    Vector3 smoothVel;
    float leanRoll;  // the smoothed side lean (degrees)
    Ball ball;

    // Recent unsmoothed velocities (newest last), used to read a fast swipe accurately.
    struct Sample { public float time; public Vector3 v; }
    readonly Sample[] history = new Sample[48];
    int historyCount, historyHead;

    // Velocity of the current swing: direction from the last few frames, speed from the fastest moment of the
    // swing along that direction. Unlike Velocity it does not lag behind a sudden fast movement, and a backswing
    // before the swing is ignored. Falls back to Velocity when the racket is not moving.
    public Vector3 SwingVelocity
    {
        get
        {
            float now = Time.time;
            Vector3 mean = Vector3.zero;
            int n = 0;
            for (int i = 0; i < historyCount; i++)
            {
                Sample s = history[(historyHead - 1 - i + history.Length) % history.Length];
                float age = now - s.time;
                if (age > Mathf.Max(powerWindow, directionWindow)) break;
                if (age <= directionWindow) { mean += s.v; n++; }
            }
            if (n == 0 || mean.sqrMagnitude < 1e-6f) return Velocity;
            Vector3 dir = (mean / n).normalized;
            float peak = 0f;
            for (int i = 0; i < historyCount; i++)
            {
                Sample s = history[(historyHead - 1 - i + history.Length) % history.Length];
                if (now - s.time > powerWindow) break;
                peak = Mathf.Max(peak, Vector3.Dot(s.v, dir));
            }
            return dir * peak;
        }
    }

    void OnValidate() { ApplyModel(); }
    void Awake() { ApplyModel(); lastPos = transform.position; }

    // A quick squash and stretch of the model when it hits the ball (strength 0-1; the stronger the shot the bigger the punch).
    public void Punch(float strength) { punch = Mathf.Clamp01(strength); punchTime = 0f; }
    float punch, punchTime = 99f;
    const float PunchDuration = 0.22f;

    void ApplyPunch()
    {
        if (model == null || punchTime > PunchDuration + 0.1f) return;
        punchTime += Time.deltaTime;
        float k = Mathf.Clamp01(punchTime / PunchDuration);
        float s = 1f + punch * 0.35f * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.4f);
        model.localScale = Vector3.one * modelScale * s;
        model.localPosition = new Vector3(0f, -headCenter * modelScale * s, 0f); // keeps the head where it was
    }

    void ApplyModel()
    {
        if (model == null) return;
        model.localScale = Vector3.one * modelScale;
        model.localPosition = new Vector3(0f, -headCenter * modelScale, 0f);
    }

    // 0 = ignore the ball (it is away, going away, or not in play), 1 = lean fully towards it (it is coming and close).
    float BallLeanWeight()
    {
        if (ball == null) ball = FindFirstObjectByType<Ball>();
        if (ball == null || !ball.gameObject.activeInHierarchy) return 0f;
        if (ball.Velocity.z * SideSign <= 0f) return 0f; // not coming towards this side of the net
        float dx = ball.Position.x - transform.position.x, dz = ball.Position.z - transform.position.z;
        float distance = Mathf.Sqrt(dx * dx + dz * dz);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ballLeanFar, ballLeanNear, distance));
    }

    public Vector3 Clamp(Vector3 p)
    {
        // Distance from the net on this racket's own side; a point beyond the net is held at the net (minDistance).
        float d = Mathf.Max(SideSign * p.z, minDistance);
        // Only the net limits the racket: it can go anywhere else, behind the baseline and past the sidelines included.
        return new Vector3(p.x, height, SideSign * d);
    }

    public void MoveTo(Vector3 p)
    {
        transform.position = Clamp(p);
    }

    // Forget the path of this frame: used when the racket is put somewhere on purpose (start of a point), so the jump is neither a
    // swept hit nor a huge swing speed.
    public void SnapSweep()
    {
        pose = Pose.None;
        lastPos = transform.position;
        HitCenter = transform.position;
    }

    void LateUpdate()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        ApplyPunch();
        if (pose != Pose.None) { ApplyPose(dt); return; }
        Vector3 v = (transform.position - lastPos) / dt;
        history[historyHead] = new Sample { time = Time.time, v = v };
        historyHead = (historyHead + 1) % history.Length;
        historyCount = Mathf.Min(historyCount + 1, history.Length);
        smoothVel = Vector3.Lerp(smoothVel, v, 1f - Mathf.Exp(-dt * velocitySmoothing));
        Velocity = smoothVel;
        lastPos = transform.position;

        // Side lean: with the side of the court the racket is on, or towards the ball once it is coming close.
        float fieldLean = SideSign * Mathf.Clamp(transform.position.x / maxX, -1f, 1f) * edgeRoll;
        float ballWeight = BallLeanWeight();
        float ballLean = SideSign * Mathf.Clamp((ball != null ? ball.Position.x - transform.position.x : 0f) / Mathf.Max(0.01f, ballLeanReach), -1f, 1f) * edgeRoll;
        float targetLean = Mathf.Lerp(fieldLean, ballLean, ballWeight);
        leanRoll = Mathf.Lerp(leanRoll, targetLean, 1f - Mathf.Exp(-dt * leanSmoothing));
        float roll = Mathf.Clamp(SideSpeed * rollPerSpeed * SideSign, -maxRoll, maxRoll) + leanRoll;
        float pitch = Mathf.Clamp(ForwardSpeed * pitchPerSpeed, -maxPitch, maxPitch) + restPitch;
        transform.rotation = Quaternion.Euler(0f, facingYaw, 0f) * Quaternion.Euler(pitch, 0f, roll);
    }

#if UNITY_EDITOR
    // Gizmos: the area the racket can move in (cyan), the hit area (green cylinder: ground reach, up to the height reach above the head)
    // and the path it swept in the last frame.
    void OnDrawGizmos()
    {
        Vector3 p = Application.isPlaying ? Position : transform.position;
        Vector3 ground = new Vector3(p.x, 0f, p.z);
        Vector3 headBase = new Vector3(p.x, height, p.z);
        GizmoShapes.Cylinder(headBase, hitRadius, hitHeight, GizmoShapes.HitArea);
        GizmoShapes.Circle(ground + Vector3.up * 0.02f, hitRadius, new Color(0.3f, 1f, 0.35f, 0.35f));

        if (limitToArea)
        {
            float side = SideSign; // -1: player side (z < 0), +1: opponent side
            float z0 = side * minDistance, z1 = side * maxDistance;
            GizmoShapes.Rect(new Vector3(0f, height, (z0 + z1) * 0.5f), maxX, Mathf.Abs(z1 - z0) * 0.5f, GizmoShapes.PlayArea);
        }
        if (Application.isPlaying)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(PreviousPosition, Position);
        }
    }
#endif
}
