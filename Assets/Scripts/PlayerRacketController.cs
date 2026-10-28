using UnityEngine;

// Mouse / touch controlled racket. It only slides left/right and forward/back, inside the Racket's play area.
// Forward swipe = depth (gentle: short, fast: to the far end), sideways swipe = angle, backwards = the ball dies at the net.
public class PlayerRacketController : RacketController
{
    [Header("Input")]
    public Camera cam;
    public Vector3 restPosition = new Vector3(0f, 1f, -4.8f);
    [Tooltip("The racket sits this far above the finger/mouse, as a fraction of the screen height (so the pointer does not cover it).")]
    [Range(0f, 0.25f)] public float pointerLift = 0.06f;

    [Header("Power (the swipe towards the net sets how deep the ball goes)")]
    [Tooltip("Speed towards the net (m/s) of a full-power swipe: the ball goes to the far end. Lower it to make deep shots easier (touch screens), raise it to demand faster swipes.")]
    public float fullPowerSpeed = 6.5f;
    [Tooltip("Below 1 the middle of the range is helped: a medium swipe already goes fairly deep. 1 = depth grows in a straight line with the swipe speed.")]
    [Range(0.5f, 1.5f)] public float powerCurve = 0.85f;
    [Tooltip("Moving away from the net faster than this (m/s) at the moment of contact: the ball is pushed into the net and dies there.")]
    public float backwardNetSpeed = 1.2f;

    [Header("Reach (how far a swipe can send the ball)")]
    [Tooltip("Furthest the ball can travel (m, measured along the court from the racket) when the racket is still. Near the net that is enough to drop it over; from further back the ball does not reach the net and dies there.")]
    public float stillReach = 3.2f;
    [Tooltip("The same for a full-power swipe: it has to cover the whole court, baseline to baseline.")]
    public float fullReach = 18f;
    [Tooltip("A ball that would land less than this far past the net (m) is not a shot that crosses: it hits the net instead.")]
    public float minClearance = 0.7f;

    [Header("Shot grades (speed towards the net, m/s, at the moment of contact: the same swipe that sets the depth)")]
    [Tooltip("STRONG: a medium swipe. Gets the bigger burst, no word.")]
    public float strongSwipeSpeed = 3.5f;
    [Tooltip("GREAT SHOT: the word, the voice and the coin bonus. Needs a clearly fast swipe.")]
    public float greatSwipeSpeed = 6f;
    [Tooltip("PERFECT: the fastest swipes. Above Full Power Speed the depth no longer grows, but the grade still does.")]
    public float perfectSwipeSpeed = 8.5f;
    [Tooltip("Speed towards the net of the last shot (m/s); read by ShotFx to grade it.")]
    public float LastSwipeSpeed { get; private set; }

    [Header("Shot")]
    [Tooltip("Ball speed (m/s) of the push that dies at the net.")]
    public float weakShotSpeed = 6f;
    [Tooltip("Height (as a fraction of the net height) at which that push reaches the net.")]
    [Range(0.1f, 0.95f)] public float weakNetHeightFraction = 0.55f;
    [Tooltip("Flight time of the gentlest aimed shot (seconds, before the ball speed multiplier).")]
    public float slowFlight = 1.3f;
    [Tooltip("Flight time of a full-power shot (lower = faster ball).")]
    public float fastFlight = 0.5f;
    [Tooltip("Where the gentlest aimed shot lands, measured from the net.")]
    public float shortDepth = 1.5f;
    [Tooltip("Within this distance of the net, a hit with the racket almost still is a drop shot: high and short over the net.")]
    public float dropShotDistance = 2.5f;
    [Tooltip("Power (0-1) below which a hit that close to the net is a drop shot.")]
    [Range(0f, 0.5f)] public float dropShotMaxPower = 0.15f;
    [Tooltip("Where the drop shot lands, measured from the net on the opponent's side.")]
    public float dropShotDepth = 1.3f;
    [Tooltip("Flight time of the drop shot; longer means a higher arc.")]
    public float dropShotFlight = 1.6f;
    [Tooltip("How much sideways swipe speed moves the landing spot.")]
    public float sideSwipeFactor = 0.5f;
    [Tooltip("How much hitting off-centre on the racket moves the landing spot (opposite way).")]
    public float offCentreFactor = 2.5f;
    [Tooltip("Aimed shots land at least this far inside the lines.")]
    public float landMargin = 0.5f;

    bool sawTouch;          // once a touch has been seen the mouse is ignored (a phone also reports an emulated mouse)
    bool havePointer;       // false until the pointer has been read since the last time it was lost
    Vector2 lastPointer;

    public override void ResetPosition()
    {
        havePointer = false;
        Racket.MoveTo(restPosition);
        Racket.SnapSweep();
    }

    public override void Tick(GameManager gm)
    {
        if (cam == null) cam = Camera.main;
        Vector3 pointer;
        if (Input.touchCount > 0)
        {
            sawTouch = true;
            pointer = Input.GetTouch(0).position;
        }
        else if (sawTouch || Application.isMobilePlatform)
        {
            // Touch screen with no finger on it: the racket stays where it is. (Without this it would follow the emulated mouse,
            // which sits at the screen corner when the game starts and at the last touch after the finger is lifted.)
            havePointer = false;
            return;
        }
        else pointer = Input.mousePosition;

        // Pointer that has not moved (finger resting, mouse untouched): keep the racket where it is. The camera slides with the racket,
        // so mapping the same screen point again would drag the racket along with it and it would drift on its own.
        if (havePointer && ((Vector2)pointer - lastPointer).sqrMagnitude < 1f) return;
        lastPointer = pointer;
        havePointer = true;

        pointer.x = Mathf.Clamp(pointer.x, 0f, Screen.width);
        pointer.y = Mathf.Clamp(pointer.y + pointerLift * Screen.height, 0f, Screen.height);
        Ray ray = cam.ScreenPointToRay(pointer);
        if (ray.direction.y > -0.0001f) return;
        float t = (Racket.height - ray.origin.y) / ray.direction.y;
        Vector3 hit = ray.origin + ray.direction * t;
        // Pointer beyond the net: the racket stops at the net but keeps following the pointer sideways. The sideways
        // position is then read where the pointer's ray crosses the net line, so it does not drift with the far plane.
        float netZ = Racket.SideSign * Racket.minDistance;
        if (Racket.SideSign * hit.z < Racket.minDistance && Mathf.Abs(ray.direction.z) > 0.0001f)
        {
            float tn = (netZ - ray.origin.z) / ray.direction.z;
            if (tn > 0f) hit = new Vector3(ray.origin.x + ray.direction.x * tn, hit.y, netZ);
        }
        Racket.MoveTo(hit);
    }

    // The ball is pushed so that it reaches the net, hits it and drops on the hitter's side.
    Vector3 NetShot(GameManager gm, Vector3 contact, float landX)
    {
        float netZ = -Racket.SideSign * 0.1f;
        float dist = Mathf.Max(0.5f, Mathf.Abs(netZ - contact.z));
        float tn = dist / weakShotSpeed;
        float targetY = gm.Net.TopY * weakNetHeightFraction;
        float g = gm.Ball.gravity;
        return new Vector3((landX - contact.x) / tn * 0.3f, (targetY - contact.y) / tn + 0.5f * g * tn, (netZ - contact.z) / tn);
    }

    // Depth comes from the swipe towards the net (sideways only aims it): a still or gentle racket drops the ball short over the net, a faster
    // one sends it deeper, a full-speed one to the far end. A racket moving away from the net pushes the ball into it.
    public override Vector3 ComputeShot(GameManager gm, Vector3 contact)
    {
        float forwardSpeed = Racket.SwingForwardSpeed, sideSpeed = Racket.SwingSideSpeed;
        LastSwipeSpeed = forwardSpeed;
        float dx = contact.x - Racket.HitCenter.x; // off-centre hit, measured where the racket met the ball

        Court court = gm.Court;
        float maxX = court.halfWidth - landMargin;
        float maxDepth = court.halfLength - landMargin;
        float landX = Mathf.Clamp(sideSpeed * sideSwipeFactor - dx * offCentreFactor, -maxX, maxX);

        // Swinging backwards: the ball only reaches the net, hits it and drops on this side.
        if (forwardSpeed < -backwardNetSpeed) return NetShot(gm, contact, landX);

        float power = Mathf.Pow(Mathf.Clamp01(forwardSpeed / Mathf.Max(0.1f, fullPowerSpeed)), powerCurve);
        // A gentle swipe cannot send the ball far: from near the net it still drops over, from further back it falls short and dies at the net.
        float reach = Mathf.Lerp(stillReach, fullReach, power);
        float fromNet = Mathf.Abs(contact.z);
        if (reach - fromNet < minClearance) return NetShot(gm, contact, landX);
        if (power <= dropShotMaxPower && Mathf.Abs(contact.z) <= dropShotDistance)
        {
            float dropX = Mathf.Clamp(landX, -maxX, maxX);
            return gm.Ball.SolveHit(contact, dropX, -Racket.SideSign * dropShotDepth, court.groundY + gm.Ball.radius, dropShotFlight, gm.Net);
        }
        float landDepth = Mathf.Min(shortDepth + (maxDepth - shortDepth) * power, reach - fromNet); // past the net, held to what the swipe can reach
        float landZ = -Racket.SideSign * landDepth;
        float t = Mathf.Lerp(slowFlight, fastFlight, power);
        return gm.Ball.SolveHit(contact, landX, landZ, court.groundY + gm.Ball.radius, t, gm.Net);
    }

#if UNITY_EDITOR
    // Gizmo: where the racket rests (outside a match).
    void OnDrawGizmos() { GizmoShapes.Cross(restPosition, 0.3f, GizmoShapes.PlayArea); }
#endif
}
