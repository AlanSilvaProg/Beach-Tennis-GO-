using UnityEngine;

// A rigged character that runs on the court and swings the racket it holds. One for each racket: the human player's (run by the joystick and the
// swing button), the CPU's and the other online player's (they only stand where their racket is held). It lives in the scene as a normal object
// (made by Tools / Beach Tennis GO / Setup characters), and is made at run time if a scene has none.
// There is no Animator: every pose is made here, in LateUpdate, by turning the bones of the rig on top of its rest pose (a T-pose). All turns are
// about the character's own axes (right / up / forward), so the bones' own orientation does not matter.
//  - Running: the legs and arms swing along the direction it is moving (it can run sideways while its body faces the ball), it leans into the
//    run and bobs.
//  - Looking: the body turns towards the ball (with a limited turning speed) and the head and neck follow it.
//  - Swing: Swing() plays a forehand: the arm and the body wind up, hit and follow through. SwingElapsed tells the game where the swing is.
//  - End of the match (what the Racket says with PoseKind): the winner jumps with the arms up; the loser throws the racket away, falls on their back
//    and has a tantrum, drumming the ground with arms and legs.
// The racket model is taken from the Racket (so the skins still work) and is held by the right hand.
public class PlayerCharacter : MonoBehaviour
{
    [Header("Owner")]
    [Tooltip("The racket this character holds. Its model goes to the hand; its end-of-match pose (celebrate / fall) is played by the body.")]
    public Racket racket;
    [Tooltip("The CPU and the other online player: the feet follow the racket and the body looks at the ball by itself. Off for the human player, whose controller places the feet.")]
    public bool followRacket;
    [Tooltip("Direction the body faces when it looks at nothing: 0 = towards +Z (the player's side), 180 = towards -Z (the far side).")]
    public float forwardYaw;
    [Tooltip("Multiplies the colours of the character (white = as painted).")]
    public Color tint = Color.white;
    [Tooltip("Where the racket head is held, from the feet: to the right and towards the net (m).")]
    public Vector2 handOffset = new Vector2(0.45f, 0.5f);

    [Header("Size")]
    [Tooltip("Scale of the model (the boy is about 0.95 m tall at 1).")]
    public float modelScale = 1.8f;
    [Tooltip("Length of the racket in the hand (m).")]
    public float racketLength = 0.75f;
    [Tooltip("Where the racket sits in the hand (hand's own axes: Y runs along the arm) and how it is turned there (degrees).")]
    public Vector3 racketOffset = new Vector3(0f, 0.015f, 0f);
    public Vector3 racketEuler = new Vector3(0f, 90f, 0f);

    [Header("Running")]
    [Tooltip("Speed (m/s) at which the run is at its fullest.")]
    public float fullRunSpeed = 7f;
    [Tooltip("Turning speed of the body towards what it looks at (degrees per second).")]
    public float turnSpeed = 720f;
    [Tooltip("How far the body may turn away from facing the net (degrees); the head turns further.")]
    public float maxBodyYaw = 110f;
    [Tooltip("How far the head may turn from the body (degrees, sideways / up-down).")]
    public Vector2 maxHeadTurn = new Vector2(75f, 35f);

    [Header("Swing")]
    [Tooltip("Total length of the swing animation (s).")]
    public float swingDuration = 0.5f;
    [Tooltip("Moment of the swing at which the racket meets the ball (s after the press).")]
    public float contactTime = 0.07f;

    [Header("Main menu")]
    [Tooltip("In the main menu the player's character turns to the camera and shows the racket with the arm down. Direction the body faces then (degrees; 180 = looking at the camera) and how fast it changes between this and the game (per second).")]
    public float showcaseYaw = 180f;
    public float showcaseSpeed = 3f;

    [Header("Losing")]
    [Tooltip("The racket is thrown with this speed (m/s, along the ground and up) and spins at this rate (degrees per second).")]
    public Vector2 throwSpeed = new Vector2(3.4f, 5.6f);
    public float throwSpin = 700f;
    [Tooltip("Time to fall on the back (s), and the speed of the tantrum (beats per second).")]
    public float fallTime = 0.45f;
    public float tantrumBeats = 3.4f;

    public bool Swinging => swingTime >= 0f && swingTime < swingDuration;
    public float SwingElapsed => swingTime;
    public Transform Hand => rHand;

    // ---- bones ----
    Transform visual, hips, spine, chest, upperChest, neck, head;
    Transform lUL, lLL, rUL, rLL, lUA, lLA, rUA, rLA, lHand, rHand, lFoot, rFoot;
    Transform[] all;
    Quaternion[] rest;
    Vector3 hipsRestPos;
    bool bound;

    // ---- state ----
    Vector3 lastPos, velocity;
    bool haveLast;
    float runPhase, swingTime = -1f, yaw, poseTime, tantrumSeed;
    int lastKind;
    float showBlend;   // 0 = playing, 1 = showing the racket in the main menu
    Camera camRef;
    Vector3 lookPoint;
    bool hasLook;
    Vector3 headDir = Vector3.forward;   // smoothed world direction the head looks along
    Transform racketModel;
    SpriteRenderer blob;
    Ball ballRef;
    GameManager gameRef;

    // The thrown racket.
    bool thrown, thrownResting;
    Vector3 throwVelocity, throwAxis;
    float throwSpinNow;

    // ---------- making it ----------

    public static PlayerCharacter For(Racket racket)
    {
        foreach (var c in FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (c.racket == racket) return c;
        return null;
    }

    // The character of this racket: the one already in the scene, or a new one (made now) when there is none.
    public static PlayerCharacter Create(Racket racket, bool follow, Color tint)
    {
        foreach (var c in FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.racket == racket) { c.Bind(); return c; }
        var model = Resources.Load<GameObject>("PlayerCharacterModel");
        if (model == null) return null;
        var go = new GameObject(racket.name + "Character");
        var pc = go.AddComponent<PlayerCharacter>();
        pc.racket = racket;
        pc.followRacket = follow;
        pc.tint = tint;
        pc.forwardYaw = racket.SideSign > 0f ? 180f : 0f;
        pc.Build(model);
        return pc;
    }

    // Builds the body from the model, puts it where the racket is held and gives it the racket.
    void Build(GameObject modelPrefab)
    {
        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        var model = Instantiate(modelPrefab, visual);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one * modelScale;
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var smr = r as SkinnedMeshRenderer;
            if (smr != null) smr.updateWhenOffscreen = true; // the bounds of the rest pose are not the bounds of a swing
        }
        BuildBlob();
        Bind();
        PlaceAtRacket();
        yaw = forwardYaw;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // Finds the bones of the body that is already built (this runs after a scene is loaded too) and hands over the racket.
    void Bind()
    {
        if (bound) return;
        if (visual == null) visual = transform.Find("Visual");
        if (visual == null) return;
        Transform m = visual.Find("Model");
        if (m == null) return;
        hips = Find(m, "Hips"); spine = Find(m, "Spine"); chest = Find(m, "Chest");
        upperChest = Find(m, "UpperChest"); neck = Find(m, "Neck"); head = Find(m, "Head");
        lUL = Find(m, "Left_UpperLeg"); lLL = Find(m, "Left_LowerLeg"); lFoot = Find(m, "Left_Foot");
        rUL = Find(m, "Right_UpperLeg"); rLL = Find(m, "Right_LowerLeg"); rFoot = Find(m, "Right_Foot");
        lUA = Find(m, "Left_UpperArm"); lLA = Find(m, "Left_LowerArm"); lHand = Find(m, "Left_Hand");
        rUA = Find(m, "Right_UpperArm"); rLA = Find(m, "Right_LowerArm"); rHand = Find(m, "Right_Hand");
        all = new[] { hips, spine, chest, upperChest, neck, head, lUL, lLL, lFoot, rUL, rLL, rFoot, lUA, lLA, lHand, rUA, rLA, rHand };
        rest = new Quaternion[all.Length];
        for (int i = 0; i < all.Length; i++) rest[i] = all[i] != null ? all[i].localRotation : Quaternion.identity;
        hipsRestPos = hips != null ? hips.localPosition : Vector3.zero;
        if (blob == null) { var b = transform.Find("Shadow"); if (b != null) blob = b.GetComponent<SpriteRenderer>(); }
        ApplyTint(m);
        HoldRacket();
        bound = true;
    }

    void Awake() { Bind(); }

    void ApplyTint(Transform m)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor("_Tint", tint);
        foreach (var r in m.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(block);
    }

    static Transform Find(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    // The racket model leaves the (invisible) racket body and goes to the hand. The body stays where the hit is made.
    void HoldRacket()
    {
        // (Only when playing: the model is part of the racket's prefab in the scene, which cannot be rearranged in the editor.)
        if (!Application.isPlaying) return;
        if (racket == null || racket.model == null || rHand == null) return;
        racketModel = racket.model;
        racket.modelDetached = true;
        if (racketModel.parent != rHand) racketModel.SetParent(rHand, false);
        ApplyRacketPose();
        foreach (var sh in FindObjectsByType<RacketShadow>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (sh.racket == racket) sh.gameObject.SetActive(false);
    }

    void ApplyRacketPose()
    {
        if (racketModel == null) return;
        racketModel.localPosition = racketOffset;
        racketModel.localRotation = Quaternion.Euler(racketEuler);
        racketModel.localScale = Vector3.one * (racketLength / modelScale);
    }

    void OnValidate() { if (Application.isPlaying) ApplyRacketPose(); }

    // A soft round shadow on the ground.
    void BuildBlob()
    {
        var go = new GameObject("Shadow");
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        blob = go.AddComponent<SpriteRenderer>();
        var sprite = Resources.Load<Sprite>("BlobShadow");
        if (sprite == null)
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
                }
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 1.2f);
        }
        blob.sprite = sprite;
        blob.color = new Color(0.22f, 0.14f, 0.04f, 0.5f);
        blob.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        blob.receiveShadows = false;
        go.transform.localPosition = new Vector3(0.1f, 0.02f, 0.05f);
    }

    // ---------- the controller's side ----------

    // The feet stand where the racket is held: the racket is `handOffset` to the right of and in front of them (seen from the body).
    public Vector3 FeetFor(Vector3 racketPosition, float groundY)
    {
        float s = racket != null ? racket.SideSign : -1f; // -1: the player's side
        return new Vector3(racketPosition.x + s * handOffset.x, groundY, racketPosition.z + s * handOffset.y);
    }

    void PlaceAtRacket()
    {
        if (racket == null) return;
        transform.position = FeetFor(racket.transform.position, 0f);
    }

    // Puts the body on the ground at p (its feet).
    public void Place(Vector3 groundPosition)
    {
        transform.position = groundPosition;
        lastPos = groundPosition;
        velocity = Vector3.zero;
        haveLast = true;
    }

    // Starts a new body direction at once (e.g. at the start of a point).
    public void SnapFacing() { yaw = forwardYaw; transform.rotation = Quaternion.Euler(0f, yaw, 0f); }

    // What the character looks at: a point in the world (the ball) or nothing (it looks at the net).
    public void LookAt(Vector3 point, bool has) { lookPoint = point; hasLook = has; }

    public void Swing() { swingTime = 0f; }
    public void CancelSwing() { swingTime = -1f; }

    // ---------- the pose ----------

    void LateUpdate()
    {
        if (!bound) { Bind(); if (!bound) return; }
        // The body is only there while its racket is (a racket that is switched off, e.g. the one of a mode that is not played, has no body either).
        bool present = racket == null || racket.gameObject.activeInHierarchy;
        if (visual.gameObject.activeSelf != present) visual.gameObject.SetActive(present);
        if (blob != null && blob.gameObject.activeSelf != present) blob.gameObject.SetActive(present);
        if (!present) return;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        int kind = racket != null ? racket.PoseKind : 0;
        PoseChanged(kind);

        if (followRacket && kind == 0) FollowRacket();
        Vector3 p = transform.position;
        if (!haveLast) { lastPos = p; haveLast = true; }
        Vector3 raw = (p - lastPos) / dt;
        raw.y = 0f;
        lastPos = p;
        velocity = Vector3.Lerp(velocity, raw, 1f - Mathf.Exp(-dt * 14f));
        if (velocity.magnitude > 30f) velocity = Vector3.zero; // a jump (the point started), not a run
        float speed = velocity.magnitude;
        float run = Mathf.Clamp01(speed / fullRunSpeed);
        float moving = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 1.2f, speed));

        // The main menu: the player's character looks at the camera and shows the racket.
        if (gameRef == null) gameRef = FindFirstObjectByType<GameManager>();
        bool showcase = !followRacket && kind == 0 && gameRef != null && gameRef.InMenu;
        showBlend = Mathf.MoveTowards(showBlend, showcase ? 1f : 0f, dt * showcaseSpeed);
        if (showcase)
        {
            if (camRef == null) camRef = Camera.main;
            if (camRef != null) LookAt(camRef.transform.position, true);
        }

        // Body direction: towards the ball, but never turning more than `maxBodyYaw` away from the net.
        float target = forwardYaw;
        if (hasLook && kind == 0)
        {
            Vector3 d = lookPoint - p;
            if (new Vector2(d.x, d.z).sqrMagnitude > 0.04f) target = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }
        target = forwardYaw + Mathf.Clamp(Mathf.DeltaAngle(forwardYaw, target), -maxBodyYaw, maxBodyYaw);
        if (showBlend > 0f) target = showcase ? showcaseYaw : Mathf.LerpAngle(target, showcaseYaw, showBlend);
        yaw = Mathf.MoveTowardsAngle(yaw, target, turnSpeed * dt);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        for (int i = 0; i < all.Length; i++) if (all[i] != null) all[i].localRotation = rest[i];
        hips.localPosition = hipsRestPos;
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        if (blob != null) blob.enabled = true;

        Vector3 right = transform.right, up = Vector3.up, fwd = transform.forward;
        float k = modelScale; // lengths in the rig are in model units

        if (thrown) SimulateRacket(dt);
        if (kind == 2) { PoseTantrum(dt); return; }
        if (kind == 1) poseTime += dt;

        // ----- run cycle, along the direction of movement -----
        Vector3 dir = speed > 0.01f ? velocity / speed : fwd;
        Vector3 axis = Vector3.Cross(up, dir).normalized; // turning about it swings the legs backwards (+) / forwards (-) along `dir`
        if (moving > 0.01f) runPhase += dt * (5f + 8f * run);
        float s = Mathf.Sin(runPhase), c = Mathf.Cos(runPhase);
        float legAmp = (18f + 32f * run) * moving;
        float kneeAmp = (25f + 60f * run) * moving;
        float breath = Mathf.Sin(Time.time * 2.6f);

        // Hips: lower in the ready stance, bob while running, lean into the run, turn a little with the stride.
        float bob = Mathf.Abs(s) * 0.035f * run * moving;
        float crouch = 0.035f * (1f - moving);
        hips.position += up * ((bob - crouch + 0.004f * breath * (1f - moving)) * k);
        Rot(hips, axis, (6f + 10f * run) * moving);
        Rot(hips, right, 7f * (1f - moving));
        Rot(hips, up, s * 7f * moving);

        // Torso: stays upright against the lean.
        Rot(spine, axis, -3f * moving);
        Rot(chest, axis, -3f * moving);

        // Legs: ready stance (knees a little bent) blending into the stride.
        float readyHip = -14f * (1f - moving), readyKnee = 24f * (1f - moving);
        Rot(lUL, axis, -s * legAmp); Rot(lUL, right, readyHip);
        Rot(rUL, axis, s * legAmp); Rot(rUL, right, readyHip);
        Rot(lLL, axis, Mathf.Max(0f, c) * kneeAmp + 8f * moving); Rot(lLL, right, readyKnee);
        Rot(rLL, axis, Mathf.Max(0f, -c) * kneeAmp + 8f * moving); Rot(rLL, right, readyKnee);
        Rot(lFoot, axis, -Mathf.Max(0f, c) * 12f * moving);
        Rot(rFoot, axis, -Mathf.Max(0f, -c) * 12f * moving);

        // Left arm: down at the side, swinging against the left leg while running.
        Rot(lUA, fwd, 68f);
        Rot(lUA, axis, s * (14f + 26f * run) * moving);
        Rot(lUA, right, -10f * (1f - moving));
        Rot(lLA, axis, -(30f + 55f * run) * moving - 18f * (1f - moving));

        // Right arm (the racket): ready position, it only bobs a little with the stride, and then the swing.
        ArmReady(right, up, fwd, axis, s, moving, run, breath, 1f - showBlend);
        if (showBlend > 0f) ArmShowcase(right, up, fwd, breath, showBlend);

        // Head: looks at the ball.
        LookHead(p, right, up, fwd);

        // Celebration: jump with both arms up.
        if (kind == 1) PoseCelebrate(right, up, fwd);

        // Swing.
        if (swingTime >= 0f)
        {
            swingTime += dt;
            if (swingTime >= swingDuration) swingTime = -1f;
            else ArmSwing(swingTime, right, up, fwd);
        }
    }

    // The CPU / the other player: the feet follow the racket, the body looks at the ball.
    void FollowRacket()
    {
        transform.position = FeetFor(racket.transform.position, 0f);
        if (ballRef == null) ballRef = FindFirstObjectByType<Ball>();
        if (gameRef == null) gameRef = FindFirstObjectByType<GameManager>();
        bool on = ballRef != null && ballRef.gameObject.activeInHierarchy && gameRef != null && gameRef.MatchOn;
        LookAt(ballRef != null ? ballRef.Position : Vector3.zero, on);
    }

    // Entering / leaving the end-of-match poses: the loser throws the racket, and gets it back for the next match.
    void PoseChanged(int kind)
    {
        if (kind == lastKind) return;
        if (kind == 2) { poseTime = 0f; tantrumSeed = Random.value * 100f; ThrowRacket(); }
        else if (kind == 1) poseTime = 0f;
        if (kind != 2 && thrown) CatchRacket();
        if (kind == 0) swingTime = -1f;
        lastKind = kind;
    }

    // The racket's pose at the end of the match is over: it is back in the hand.
    void CatchRacket()
    {
        thrown = false;
        if (racketModel == null) return;
        racketModel.SetParent(rHand, false);
        ApplyRacketPose();
    }

    // ---------- the thrown racket ----------

    void ThrowRacket()
    {
        if (racketModel == null || thrown) return;
        thrown = true;
        thrownResting = false;
        racketModel.SetParent(null, true); // keeps its place and size in the world
        Vector3 away = transform.forward * 0.55f + transform.right * (Random.value < 0.5f ? -0.85f : 0.85f);
        away.y = 0f;
        throwVelocity = away.normalized * throwSpeed.x + Vector3.up * throwSpeed.y;
        throwAxis = Random.onUnitSphere;
        throwSpinNow = throwSpin;
    }

    void SimulateRacket(float dt)
    {
        if (racketModel == null || thrownResting) return;
        throwVelocity += Vector3.down * 9.8f * dt;
        Vector3 pos = racketModel.position + throwVelocity * dt;
        const float floor = 0.05f;
        if (pos.y < floor)
        {
            pos.y = floor;
            if (throwVelocity.y < -1.2f)
            {
                throwVelocity = new Vector3(throwVelocity.x * 0.55f, -throwVelocity.y * 0.38f, throwVelocity.z * 0.55f);
                throwSpinNow *= 0.5f;
            }
            else { throwVelocity = Vector3.zero; thrownResting = true; }
        }
        racketModel.position = pos;
        if (thrownResting)
        {
            // Lies flat, the face up, with its long side pointing where it was going.
            Vector3 h = racketModel.up; h.y = 0f;
            if (h.sqrMagnitude < 1e-4f) h = Vector3.forward;
            racketModel.rotation = Quaternion.LookRotation(Vector3.up, h.normalized);
        }
        else racketModel.Rotate(throwAxis, throwSpinNow * dt, Space.World);
    }

    // ---------- poses ----------

    // The racket arm waiting for the ball: elbow bent, racket up and a little back.
    void ArmReady(Vector3 right, Vector3 up, Vector3 fwd, Vector3 axis, float s, float moving, float run, float breath, float w)
    {
        Rot(rUA, fwd, -68f * w);
        Rot(rUA, right, (-38f + breath * 1.5f) * w);
        Rot(rUA, up, 22f * w);
        Rot(rLA, right, -75f * w);
        Rot(rLA, axis, -s * 6f * moving * w);
    }

    // The main menu: the right arm hangs down with the racket held out a little, the face towards whoever looks.
    void ArmShowcase(Vector3 right, Vector3 up, Vector3 fwd, float breath, float w)
    {
        Rot(rUA, fwd, -72f * w);
        Rot(rUA, right, (-14f + breath * 1.2f) * w);
        Rot(rUA, up, 12f * w);
        Rot(rLA, right, -38f * w);
    }

    // The forehand: wind-up (the ready pose, a bit further back), contact in front of the right shoulder, follow-through across the body.
    // Each key is (time, arm forward, arm sideways, elbow, torso turn).
    struct Key { public float t, fwd, side, elbow, torso; public Key(float t, float f, float s, float e, float o) { this.t = t; fwd = f; side = s; elbow = e; torso = o; } }
    static readonly Key[] Keys =
    {
        new Key(0.00f, 0f, 0f, 0f, 0f),        // ready (the offsets are relative to the ready pose)
        new Key(0.07f, -40f, -8f, 55f, -28f),  // contact: arm out in front, nearly straight
        new Key(0.22f, -48f, -55f, 25f, -55f), // follow-through: the racket has gone across the body
        new Key(0.50f, 0f, 0f, 0f, 0f),        // back to ready
    };

    void ArmSwing(float t, Vector3 right, Vector3 up, Vector3 fwd)
    {
        int i = 0;
        while (i < Keys.Length - 2 && t > Keys[i + 1].t) i++;
        Key a = Keys[i], b = Keys[i + 1];
        float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - a.t) / Mathf.Max(0.001f, b.t - a.t)));
        float f = Mathf.Lerp(a.fwd, b.fwd, u), sd = Mathf.Lerp(a.side, b.side, u), e = Mathf.Lerp(a.elbow, b.elbow, u), o = Mathf.Lerp(a.torso, b.torso, u);
        Rot(spine, up, o * 0.35f);
        Rot(chest, up, o * 0.35f);
        Rot(upperChest, up, o * 0.3f);
        Rot(rUA, right, f);
        Rot(rUA, up, sd);
        Rot(rLA, right, e);
        Rot(lUA, up, o * 0.4f);   // the free arm opens against the swing
    }

    void LookHead(Vector3 p, Vector3 right, Vector3 up, Vector3 fwd)
    {
        Vector3 want = fwd;
        if (hasLook)
        {
            Vector3 headPos = head != null ? head.position : p + up * 1.5f;
            Vector3 d = lookPoint - headPos;
            if (d.sqrMagnitude > 0.01f) want = d.normalized;
        }
        // Limit the turn relative to the body.
        float yawLocal = Mathf.DeltaAngle(0f, Mathf.Atan2(Vector3.Dot(want, right), Vector3.Dot(want, fwd)) * Mathf.Rad2Deg);
        float pitchLocal = Mathf.Asin(Mathf.Clamp(want.y, -1f, 1f)) * Mathf.Rad2Deg;
        yawLocal = Mathf.Clamp(yawLocal, -maxHeadTurn.x, maxHeadTurn.x);
        pitchLocal = Mathf.Clamp(pitchLocal, -maxHeadTurn.y, maxHeadTurn.y);
        Vector3 limited = Quaternion.AngleAxis(yawLocal, up) * Quaternion.AngleAxis(-pitchLocal, right) * fwd;
        headDir = Vector3.Slerp(headDir, limited, 1f - Mathf.Exp(-Time.deltaTime * 14f));
        Quaternion delta = Quaternion.FromToRotation(fwd, headDir.normalized);
        if (neck != null) neck.rotation = Quaternion.Slerp(Quaternion.identity, delta, 0.4f) * neck.rotation;
        if (head != null) head.rotation = Quaternion.Slerp(Quaternion.identity, delta, 0.6f) * head.rotation;
    }

    void PoseCelebrate(Vector3 right, Vector3 up, Vector3 fwd)
    {
        float hop = Mathf.Abs(Mathf.Sin(poseTime * 6.5f));
        hips.position += up * (hop * 0.25f * modelScale);
        float wave = Mathf.Sin(poseTime * 13f) * 12f;
        // both arms up: undo the ready pose, then raise
        for (int i = 0; i < all.Length; i++) if (all[i] == rUA || all[i] == rLA || all[i] == lUA || all[i] == lLA) all[i].localRotation = rest[i];
        Rot(rUA, fwd, 150f + wave); Rot(lUA, fwd, -150f - wave);
        Rot(rLA, right, -10f); Rot(lLA, right, -10f);
        Rot(spine, right, -6f);
    }

    // The loser falls on their back (the whole body tips about the feet) and has a tantrum: arms and legs drum the ground, the head rolls from side
    // to side and the body shakes.
    void PoseTantrum(float dt)
    {
        poseTime += dt;
        float u = Mathf.Clamp01(poseTime / Mathf.Max(0.05f, fallTime));
        float ease = u * u;
        visual.localRotation = Quaternion.Euler(-84f * ease, 0f, 0f);
        visual.localPosition = new Vector3(0f, 0.1f * modelScale * ease, 0f);
        // A tantrum starts once the body is down.
        float t = Mathf.Max(0f, poseTime - fallTime);
        float amount = Mathf.SmoothStep(0f, 1f, t / 0.25f);
        Vector3 across = visual.right, along = visual.up, lift = Vector3.up;
        float beat = t * tantrumBeats * Mathf.PI * 2f;
        float a = Mathf.Sin(beat), b = Mathf.Sin(beat + Mathf.PI);
        float shake = Mathf.PerlinNoise(tantrumSeed, t * 11f) - 0.5f;
        // Arms: they start out (T-pose) and are raised off the ground and slammed down about the axis along the body.
        Rot(rUA, along, -(38f + 38f * a) * amount);
        Rot(lUA, along, (38f + 38f * b) * amount);
        Rot(rLA, across, -20f * amount * (0.5f + 0.5f * a));
        Rot(lLA, across, -20f * amount * (0.5f + 0.5f * b));
        // Legs: kicked up and dropped, alternately.
        Rot(rUL, across, (30f + 38f * Mathf.Max(0f, b)) * amount);
        Rot(lUL, across, (30f + 38f * Mathf.Max(0f, a)) * amount);
        Rot(rLL, across, -25f * Mathf.Max(0f, b) * amount);
        Rot(lLL, across, -25f * Mathf.Max(0f, a) * amount);
        // Head rolls, back arches a little, the whole body shakes.
        Rot(head, lift, 40f * Mathf.Sin(beat * 0.5f) * amount);
        Rot(spine, across, 10f * Mathf.Abs(a) * amount);
        visual.localPosition += new Vector3(shake * 0.05f, Mathf.Abs(a) * 0.03f, shake * 0.04f) * amount * modelScale;
        visual.localRotation *= Quaternion.Euler(0f, shake * 8f * amount, shake * 6f * amount);
    }

    // Turns a bone about a world axis (its own parents' turns stay in effect, so call parents first).
    static void Rot(Transform bone, Vector3 axis, float degrees)
    {
        if (bone == null || Mathf.Abs(degrees) < 0.001f || axis.sqrMagnitude < 1e-6f) return;
        bone.rotation = Quaternion.AngleAxis(degrees, axis.normalized) * bone.rotation;
    }
}
