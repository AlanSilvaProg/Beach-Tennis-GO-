using UnityEngine;

// Mouse / touch controlled racket. It only slides left/right and forward/back.
// Forward swipe = power, sideways swipe = power + angle, still or backwards = weak shot.
public class PlayerRacketController : RacketController
{
    [Header("Input")]
    public Camera cam;
    public Vector3 restPosition = new Vector3(0f, 1f, -4.8f);

    [Header("Power")]
    [Tooltip("Weighted racket speed (m/s) that counts as a full-power shot.")]
    public float maxSwipeSpeed = 8f;
    [Tooltip("How much speed towards the net adds power (moving backwards removes it).")]
    public float forwardWeight = 1f;
    [Tooltip("How much sideways speed adds power.")]
    public float sideWeight = 0.6f;

    [Header("Shot")]
    [Tooltip("Below this power (0-1) the swing is too small: the ball is pushed weakly into the net. Raise it to demand bigger movements.")]
    [Range(0f, 1f)] public float minPowerForAimedShot = 0.4f;
    [Tooltip("Ball speed (m/s) of a weak push towards the net.")]
    public float weakShotSpeed = 6f;
    [Tooltip("Height (as a fraction of the net height) at which a weak push reaches the net.")]
    [Range(0.1f, 0.95f)] public float weakNetHeightFraction = 0.55f;
    [Tooltip("Flight time of the gentlest aimed shot (seconds, before the ball speed multiplier).")]
    public float slowFlight = 1.3f;
    [Tooltip("Flight time of a full-power shot (lower = faster ball).")]
    public float fastFlight = 0.5f;
    [Tooltip("Where the gentlest aimed shot lands, measured from the net.")]
    public float shortDepth = 1.5f;
    [Tooltip("How much sideways swipe speed moves the landing spot.")]
    public float sideSwipeFactor = 0.5f;
    [Tooltip("How much hitting off-centre on the racket moves the landing spot (opposite way).")]
    public float offCentreFactor = 2.5f;
    [Tooltip("Aimed shots land at least this far inside the lines.")]
    public float landMargin = 0.5f;

    void Awake()
    {
        // The player can move the racket anywhere the camera can see (the pointer is kept on screen below).
        Racket.limitToArea = false;
    }

    public override void ResetPosition()
    {
        Racket.MoveTo(restPosition);
    }

    public override void Tick(GameManager gm)
    {
        if (cam == null) cam = Camera.main;
        Vector3 pointer = Input.mousePosition;
        if (Input.touchCount > 0) pointer = Input.GetTouch(0).position;
        pointer.x = Mathf.Clamp(pointer.x, 0f, Screen.width);
        pointer.y = Mathf.Clamp(pointer.y, 0f, Screen.height);
        Ray ray = cam.ScreenPointToRay(pointer);
        if (ray.direction.y > -0.0001f) return;
        float t = (Racket.height - ray.origin.y) / ray.direction.y;
        Racket.MoveTo(ray.origin + ray.direction * t);
    }

    // Any shot above the minimum power lands inside the opponent's court; only a weak touch can fail.
    public override Vector3 ComputeShot(GameManager gm, Vector3 contact)
    {
        float speedScore = Racket.ForwardSpeed * forwardWeight + Mathf.Abs(Racket.SideSpeed) * sideWeight;
        float power = Mathf.Clamp01(speedScore / maxSwipeSpeed);
        float dx = contact.x - Racket.Position.x;

        Court court = gm.Court;
        float maxX = court.halfWidth - landMargin;
        float maxDepth = court.halfLength - landMargin;
        float landX = Mathf.Clamp(Racket.SideSpeed * sideSwipeFactor - dx * offCentreFactor, -maxX, maxX);
        // Power above the minimum is remapped to 0-1 for depth and speed.
        float aimed = Mathf.Clamp01((power - minPowerForAimedShot) / (1f - minPowerForAimedShot));
        float landZ = -Racket.SideSign * (shortDepth + (maxDepth - shortDepth) * aimed);

        if (power < minPowerForAimedShot)
        {
            // Too small a movement: the ball only reaches the net, hits it and drops on this side.
            float netZ = -Racket.SideSign * 0.1f;
            float dist = Mathf.Max(0.5f, Mathf.Abs(netZ - contact.z));
            float tn = dist / weakShotSpeed;
            float targetY = gm.Net.TopY * weakNetHeightFraction;
            float g = gm.Ball.gravity;
            return new Vector3((landX - contact.x) / tn * 0.3f, (targetY - contact.y) / tn + 0.5f * g * tn, (netZ - contact.z) / tn);
        }

        float t = Mathf.Lerp(slowFlight, fastFlight, aimed);
        return gm.Ball.SolveHit(contact, landX, landZ, court.groundY + gm.Ball.radius, t, gm.Net);
    }
}
