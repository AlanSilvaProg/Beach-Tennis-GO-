using UnityEngine;

// The human player. With a character (the normal case) the player runs a boy with a joystick and hits with a button:
//  - The racket is held by the character: the Racket body (where the ball is hit) sits a little to the right of and in front of the feet.
//  - Pressing the swing button hits a ball that is within reach at once; the stick, held at that moment, shapes the shot: towards the net = deeper
//    and stronger, away = softer and shorter, sideways = the angle. A ball met cleanly (close to the racket, at a good height) with the stick
//    forward is graded GREAT or PERFECT.
// Without a character (the old way) it is a mouse / touch controlled racket that only slides left/right and forward/back inside the play area:
// forward swipe = depth (gentle: short, fast: to the far end), sideways swipe = angle, backwards = the ball dies at the net.
public class PlayerRacketController : RacketController
{
    [Header("Character (joystick + swing button)")]
    [Tooltip("Run a character with a joystick and hit with a button. Off = the old mouse / touch racket.")]
    public bool useCharacter = true;
    [Tooltip("Running speed (m/s) at full stick and how fast it picks up and loses speed (m/s per second).")]
    public float moveSpeed = 7f;
    public float acceleration = 50f;
    [Tooltip("Where the hit is made, from the feet: to the right of the body and towards the net (m). Sideways 0 (the default) puts the reach of the racket in the middle of the body: the same on the right and on the left.")]
    public Vector2 handOffset = new Vector2(0f, 0.5f);
    [Tooltip("Reach of the swing on the ground (m): a ball closer than this to the racket is hit when the button is pressed.")]
    public float buttonHitRadius = 1.1f;
    [Tooltip("The racket can hit from this long after the press (the moment the racket meets the ball) until this long (s).")]
    public Vector2 contactWindow = new Vector2(0.06f, 0.3f);
    [Tooltip("The swing button works again after this long (s).")]
    public float swingCooldown = 0.35f;
    [Tooltip("Power (0-1) of a shot with the stick pulled back and pushed fully forward; the stick at rest is in the middle of the two.")]
    public Vector2 stickPower = new Vector2(0.3f, 1f);
    [Tooltip("How much the off-centre position of the ball on the racket still moves the shot, compared with the old swipe (the button reach is wide).")]
    [Range(0f, 1f)] public float buttonOffCentre = 0.3f;
    [Tooltip("The serve: how far behind the ball the racket is kept (m, nearest and furthest).")]
    public Vector2 serveStand = new Vector2(0.15f, 0.8f);

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

    [Header("Serve")]
    [Tooltip("A serve is never a net or a short ball: whatever the swipe, it has at least this power (0-1). 0.5 is a medium ball, 1 the deepest.")]
    [Range(0f, 1f)] public float serveMinPower = 0.5f;

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

    // Takes the tuning of another controller (the remote player's racket plays by the local player's numbers).
    public void CopyTuningFrom(PlayerRacketController o)
    {
        fullPowerSpeed = o.fullPowerSpeed; powerCurve = o.powerCurve; backwardNetSpeed = o.backwardNetSpeed;
        stillReach = o.stillReach; fullReach = o.fullReach; minClearance = o.minClearance;
        serveMinPower = o.serveMinPower; strongSwipeSpeed = o.strongSwipeSpeed; greatSwipeSpeed = o.greatSwipeSpeed; perfectSwipeSpeed = o.perfectSwipeSpeed;
        weakShotSpeed = o.weakShotSpeed; weakNetHeightFraction = o.weakNetHeightFraction; slowFlight = o.slowFlight; fastFlight = o.fastFlight;
        shortDepth = o.shortDepth; dropShotDistance = o.dropShotDistance; dropShotMaxPower = o.dropShotMaxPower; dropShotDepth = o.dropShotDepth;
        dropShotFlight = o.dropShotFlight; sideSwipeFactor = o.sideSwipeFactor; offCentreFactor = o.offCentreFactor; landMargin = o.landMargin;
    }

    bool sawTouch;          // once a touch has been seen the mouse is ignored (a phone also reports an emulated mouse)
    bool havePointer;       // false until the pointer has been read since the last time it was lost
    Vector2 lastPointer;

    // ---------- character ----------
    PlayerCharacter character;
    PlayerControls controls;
    Vector3 feet, runVelocity;
    protected float swingStart = -10f;
    protected bool swingOpen;
    float lastTick = -10f;
    // The character started a swing, with the stick as it was (the other player's copy of this racket is told).
    public event System.Action<Vector2> SwingStarted;

    protected virtual bool WantsCharacter => useCharacter;
    // True when the shot is made with a button (the character's swing): hits then only happen during a swing.
    public virtual bool UsesButton => character != null;
    // Moment in the swing when a ball in reach is hit.
    public virtual bool CanHitNow
    {
        get
        {
            if (!UsesButton) return true;
            float t = Time.time - swingStart;
            return swingOpen && t >= contactWindow.x && t <= contactWindow.y;
        }
    }
    // The stick the shot is read from.
    // It is the stick as it was when the button was pressed (not at the moment of contact a moment later).
    protected virtual Vector2 SwingStick => stickAtPress;
    Vector2 stickAtPress;
    public PlayerCharacter Character => character;

    void EnsureCharacter()
    {
        if (character != null || !WantsCharacter) return;
        character = PlayerCharacter.Create(Racket, false, Color.white); // the one in the scene, or a new one
        if (character == null) return;
        character.handOffset = handOffset;
        Racket.hitRadius = buttonHitRadius;
        var hud = FindFirstObjectByType<HudView>();
        TMPro.TMP_FontAsset font = hud != null && hud.winLine1 != null ? hud.winLine1.font : null;
        Material mat = hud != null && hud.winLine1 != null ? hud.winLine1.fontSharedMaterial : null;
        controls = PlayerControls.Create(font, mat);
    }

    void OnDestroy()
    {
        if (character != null) Destroy(character.gameObject);
        if (controls != null) Destroy(controls.gameObject);
    }

    // The feet go where the racket is, minus the grip.
    void PlaceFeet(float groundY)
    {
        feet = character != null ? character.FeetFor(Racket.Position, groundY) : new Vector3(Racket.Position.x - handOffset.x, groundY, Racket.Position.z - handOffset.y);
        if (character != null) character.transform.position = feet;
    }

    public override void ResetPosition()
    {
        havePointer = false;
        EnsureCharacter();
        Racket.MoveTo(restPosition);
        Racket.SnapSweep();
        if (character != null)
        {
            runVelocity = Vector3.zero;
            swingOpen = false;
            PlaceFeet(0f);
            character.CancelSwing();
            character.Place(feet);
            character.SnapFacing();
            character.LookAt(Vector3.zero, false);
            if (controls != null) controls.SetVisible(false);
        }
    }

    void LateUpdate()
    {
        // Tick only runs while a match is on and no ad plays: otherwise the controls go away.
        if (controls != null && controls.Visible && Time.unscaledTime - lastTick > 0.25f) controls.SetVisible(false);
    }

    void TickCharacter(GameManager gm)
    {
        lastTick = Time.unscaledTime;
        controls.SetVisible(true);
        float dt = Time.deltaTime;
        float groundY = gm.Court.groundY;
        Vector2 m = controls.Move;
        Vector3 wish = new Vector3(m.x, 0f, m.y) * moveSpeed;

        // Serving: the racket stays just behind the ball. If it is not there yet (the last point ended elsewhere) it runs there by itself.
        if (gm.PlayerServing)
        {
            float ballZ = gm.Ball.Position.z;                 // negative: the player's side
            float lo = ballZ - serveStand.y, hi = ballZ - serveStand.x; // range of the racket's z
            float zNow = Racket.Position.z;
            if (zNow < lo - 0.05f) wish.z = moveSpeed;        // too far back: run forward
            else if (zNow > hi + 0.05f) wish.z = -moveSpeed;  // too far forward: run back
            else if (zNow + wish.z * dt < lo || zNow + wish.z * dt > hi) wish.z = 0f;
        }
        runVelocity = Vector3.MoveTowards(runVelocity, wish, acceleration * dt);
        Vector3 hand = new Vector3(Racket.Position.x + runVelocity.x * dt, Racket.height, Racket.Position.z + runVelocity.z * dt);
        Racket.MoveTo(hand);
        PlaceFeet(groundY);
        if (Mathf.Abs(Racket.Position.x - hand.x) > 1e-4f) runVelocity.x = 0f;
        if (Mathf.Abs(Racket.Position.z - hand.z) > 1e-4f) runVelocity.z = 0f;

        // The body looks at the ball while it is in play.
        bool ballOn = gm.Ball != null && gm.Ball.gameObject.activeInHierarchy && gm.MatchOn;
        character.LookAt(gm.Ball != null ? gm.Ball.Position : Vector3.zero, ballOn);

        // The swing button.
        if (controls.ConsumeSwing() && Time.time - swingStart >= swingCooldown)
        {
            swingStart = Time.time;
            swingOpen = true;
            stickAtPress = controls.Move;
            character.Swing();
            SwingStarted?.Invoke(stickAtPress);
        }
        if (swingOpen && Time.time - swingStart > contactWindow.y) swingOpen = false;
    }

    public override void Tick(GameManager gm)
    {
        EnsureCharacter();
        if (character != null && controls != null) { TickCharacter(gm); return; }
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

    // How clean the ball was met, 0-1: close to the racket head and at a comfortable height.
    float Cleanness(Vector3 contact)
    {
        Vector3 c = Racket.HitCenter;
        float d = Mathf.Sqrt((contact.x - c.x) * (contact.x - c.x) + (contact.z - c.z) * (contact.z - c.z));
        float flat = 1f - Mathf.Clamp01(d / Mathf.Max(0.1f, buttonHitRadius));
        float high = Mathf.Clamp01(Mathf.Abs(contact.y - (Racket.height + 0.15f)) / Mathf.Max(0.2f, Racket.hitHeight));
        return flat * (1f - 0.5f * high);
    }

    // The button swing has no swipe to read: the stick makes one. Towards the net = fast (deep), away = slow (short); how cleanly the ball is met
    // adds to it, so that a good timing with the stick forward gives the swing speeds of GREAT and PERFECT. Sideways stick = sideways swing.
    void PrepareButtonSwing(GameManager gm, Vector3 contact, float maxX)
    {
        Vector2 m = SwingStick;
        float power = Mathf.Lerp(stickPower.x, stickPower.y, (m.y + 1f) * 0.5f);
        float baseSpeed = fullPowerSpeed * Mathf.Pow(power, 1f / Mathf.Max(0.1f, powerCurve));
        float clean = Cleanness(contact);
        float boost = gm.IsServing ? 0f : clean * clean * Mathf.Clamp01(m.y);
        float speed = Mathf.Lerp(baseSpeed, perfectSwipeSpeed * 1.05f, boost);
        float side = m.x * -Racket.SideSign * maxX / Mathf.Max(0.05f, sideSwipeFactor); // right on their screen: +x for the near side, -x for the far one
        Racket.ForceSwing(new Vector3(side, 0f, -Racket.SideSign * speed), 0.1f);
        swingOpen = false; // one hit per swing
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
        Court court = gm.Court;
        float maxX = court.halfWidth - landMargin;
        if (UsesButton) PrepareButtonSwing(gm, contact, maxX);
        float forwardSpeed = Racket.SwingForwardSpeed, sideSpeed = Racket.SwingSideSpeed;
        LastSwipeSpeed = forwardSpeed;
        float dx = (contact.x - Racket.HitCenter.x) * (UsesButton ? buttonOffCentre : 1f); // off-centre hit, measured where the racket met the ball

        float maxDepth = court.halfLength - landMargin;
        float landX = Mathf.Clamp(sideSpeed * sideSwipeFactor - dx * offCentreFactor, -maxX, maxX);

        // The serve cannot fail or be short: no net, no drop shot, no limit on the reach, and a medium ball at the least.
        bool serve = gm.IsServing;
        // Swinging backwards: the ball only reaches the net, hits it and drops on this side.
        if (!serve && forwardSpeed < -backwardNetSpeed) return NetShot(gm, contact, landX);

        float power = Mathf.Pow(Mathf.Clamp01(forwardSpeed / Mathf.Max(0.1f, fullPowerSpeed)), powerCurve);
        if (serve) power = Mathf.Max(power, serveMinPower);
        // A gentle swipe cannot send the ball far: from near the net it still drops over, from further back it falls short and dies at the net.
        float reach = serve ? fullReach : Mathf.Lerp(stillReach, fullReach, power);
        float fromNet = Mathf.Abs(contact.z);
        if (reach - fromNet < minClearance) return NetShot(gm, contact, landX);
        if (!serve && power <= dropShotMaxPower && Mathf.Abs(contact.z) <= dropShotDistance)
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
