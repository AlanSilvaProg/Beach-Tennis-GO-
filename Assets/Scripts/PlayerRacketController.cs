using UnityEngine;

// Mouse / touch controlled racket. It only slides left/right and forward/back, inside the Racket's play area.
// Forward swipe = power, sideways swipe = power + angle, still or backwards = weak shot.
public class PlayerRacketController : RacketController
{
    [Header("Input")]
    public Camera cam;
    public Vector3 restPosition = new Vector3(0f, 1f, -4.8f);
    [Tooltip("The racket sits this far above the finger/mouse, as a fraction of the screen height (so the pointer does not cover it).")]
    [Range(0f, 0.25f)] public float pointerLift = 0.06f;

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
    [Tooltip("Within this distance of the net, a hit with the racket almost still is a drop shot: high and short over the net.")]
    public float dropShotDistance = 2.5f;
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

    [Header("Baseline style (Training)")]
    [Tooltip("Flight time (simulated seconds) of the ball from the racket to its bounce: lower = faster ball.")]
    public float baselineFlight = 1.05f;

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
        if (gm.Baseline) { MoveAlongBaseline(pointer, gm); return; }
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

    // Baseline style: the racket stays on the baseline and only follows the pointer sideways (where the pointer's ray crosses that line).
    void MoveAlongBaseline(Vector2 pointer, GameManager gm)
    {
        float z = -gm.BaselineZ;
        Ray ray = cam.ScreenPointToRay(pointer);
        if (Mathf.Abs(ray.direction.z) < 0.0001f) return;
        float t = (z - ray.origin.z) / ray.direction.z;
        if (t <= 0f) return;
        float reach = gm.Court.halfWidth + 0.15f; // a little past the sidelines (the fixed camera frames just that)
        Racket.MoveTo(new Vector3(Mathf.Clamp(ray.origin.x + ray.direction.x * t, -reach, reach), Racket.height, z));
    }

    // Baseline style: the ball goes the opposite way of the racket (straight when it is in the middle); hitting off-centre bends it a bit more.
    Vector3 BaselineShot(GameManager gm, Vector3 contact)
    {
        Court court = gm.Court;
        float reach = court.halfWidth - landMargin;
        float side = Mathf.Clamp(Racket.Position.x / court.halfWidth, -1f, 1f);
        float dx = contact.x - Racket.HitCenter.x;
        float aimX = Mathf.Clamp(-side * reach - dx * offCentreFactor, -reach, reach);
        return gm.Ball.SolveBounceShot(contact, aimX, -Racket.SideSign * gm.BaselineZ, baselineFlight, gm.Net, court.groundY);
    }

    // Any shot above the minimum power lands inside the opponent's court; only a weak touch can fail.
    public override Vector3 ComputeShot(GameManager gm, Vector3 contact)
    {
        if (gm.Baseline) return BaselineShot(gm, contact);
        float forwardSpeed = Racket.SwingForwardSpeed, sideSpeed = Racket.SwingSideSpeed;
        float speedScore = forwardSpeed * forwardWeight + Mathf.Abs(sideSpeed) * sideWeight;
        float power = Mathf.Clamp01(speedScore / maxSwipeSpeed);
        float dx = contact.x - Racket.HitCenter.x; // off-centre hit, measured where the racket met the ball

        Court court = gm.Court;
        float maxX = court.halfWidth - landMargin;
        float maxDepth = court.halfLength - landMargin;
        float landX = Mathf.Clamp(sideSpeed * sideSwipeFactor - dx * offCentreFactor, -maxX, maxX);
        // Power above the minimum is remapped to 0-1 for depth and speed.
        float aimed = Mathf.Clamp01((power - minPowerForAimedShot) / (1f - minPowerForAimedShot));
        float landZ = -Racket.SideSign * (shortDepth + (maxDepth - shortDepth) * aimed);

        if (power < minPowerForAimedShot && Mathf.Abs(contact.z) <= dropShotDistance)
        {
            float dropX = Mathf.Clamp(landX, -maxX, maxX);
            return gm.Ball.SolveHit(contact, dropX, -Racket.SideSign * dropShotDepth, court.groundY + gm.Ball.radius, dropShotFlight, gm.Net);
        }

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

#if UNITY_EDITOR
    // Gizmo: where the racket rests (outside a match).
    void OnDrawGizmos() { GizmoShapes.Cross(restPosition, 0.3f, GizmoShapes.PlayArea); }
#endif
}
