using UnityEngine;

// The player's body: a rigged character that runs on the court and swings the racket it holds. There is no Animator: every pose is made here, in
// LateUpdate, by turning the bones of the rig on top of its rest pose (a T-pose). All turns are about the character's own axes (right / up / forward),
// so the bones' own orientation does not matter.
//  - Running: the legs and arms swing along the direction it is moving (it can run sideways while its body faces the ball), it leans into the
//    run and bobs.
//  - Looking: the body turns towards the ball (with a limited turning speed) and the head and neck follow it.
//  - Swing: Swing() plays a forehand: the arm and the body wind up, hit and follow through. SwingElapsed tells the game where the swing is.
//  - End of the match: the winner jumps with the arms up, the loser falls on their back (what the Racket says with PoseKind).
// The racket model is taken from the Racket (so the skins still work) and is held by the right hand.
public class PlayerCharacter : MonoBehaviour
{
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

    public bool Swinging => swingTime >= 0f && swingTime < swingDuration;
    public float SwingElapsed => swingTime;
    public Transform Hand => rHand;

    // ---- bones ----
    Transform visual, hips, spine, chest, upperChest, neck, head;
    Transform lUL, lLL, rUL, rLL, lUA, lLA, rUA, rLA, lHand, rHand, lFoot, rFoot;
    Transform[] all;
    Quaternion[] rest;
    Vector3 hipsRestPos;

    // ---- state ----
    Vector3 lastPos, velocity;
    bool haveLast;
    float runPhase, swingTime = -1f, yaw, hopTime;
    Vector3 lookPoint;
    bool hasLook;
    Vector3 headDir = Vector3.forward;   // smoothed world direction the head looks along
    Racket racket;
    Transform racketModel;
    SpriteRenderer blob;
    Material toon;

    // Builds the character from the model: finds the bones, brightens it and puts the racket model in the hand.
    public void Setup(GameObject modelPrefab, Racket racketToHold)
    {
        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        var model = Instantiate(modelPrefab, visual);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one * modelScale;

        hips = Find(model.transform, "Hips"); spine = Find(model.transform, "Spine"); chest = Find(model.transform, "Chest");
        upperChest = Find(model.transform, "UpperChest"); neck = Find(model.transform, "Neck"); head = Find(model.transform, "Head");
        lUL = Find(model.transform, "Left_UpperLeg"); lLL = Find(model.transform, "Left_LowerLeg"); lFoot = Find(model.transform, "Left_Foot");
        rUL = Find(model.transform, "Right_UpperLeg"); rLL = Find(model.transform, "Right_LowerLeg"); rFoot = Find(model.transform, "Right_Foot");
        lUA = Find(model.transform, "Left_UpperArm"); lLA = Find(model.transform, "Left_LowerArm"); lHand = Find(model.transform, "Left_Hand");
        rUA = Find(model.transform, "Right_UpperArm"); rLA = Find(model.transform, "Right_LowerArm"); rHand = Find(model.transform, "Right_Hand");
        all = new[] { hips, spine, chest, upperChest, neck, head, lUL, lLL, lFoot, rUL, rLL, rFoot, lUA, lLA, lHand, rUA, rLA, rHand };
        rest = new Quaternion[all.Length];
        for (int i = 0; i < all.Length; i++) rest[i] = all[i] != null ? all[i].localRotation : Quaternion.identity;
        hipsRestPos = hips != null ? hips.localPosition : Vector3.zero;

        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            if (toon == null)
            {
                var src = r.sharedMaterial;
                if (src != null && src.shader != null && src.shader.name == "BeachTennisGO/CharacterToon") toon = new Material(src); // the prefab's own (so the shader is in the build)
                else
                {
                    var shader = Shader.Find("BeachTennisGO/CharacterToon");
                    toon = new Material(shader != null ? shader : Shader.Find("Unlit/Texture"));
                    if (src != null && src.HasProperty("_MainTex")) toon.mainTexture = src.mainTexture;
                }
            }
            r.sharedMaterial = toon;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var smr = r as SkinnedMeshRenderer;
            if (smr != null) smr.updateWhenOffscreen = true; // the bounds of the rest pose are not the bounds of a swing
        }

        BuildBlob();
        HoldRacket(racketToHold);
    }

    static Transform Find(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    // The racket model leaves the (invisible) racket body and goes to the hand. The body stays where the hit is made.
    void HoldRacket(Racket r)
    {
        racket = r;
        if (r == null || r.model == null || rHand == null) return;
        racketModel = r.model;
        r.modelDetached = true;
        racketModel.SetParent(rHand, false);
        ApplyRacketPose();
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
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
            }
        tex.Apply();
        var go = new GameObject("Shadow");
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        blob = go.AddComponent<SpriteRenderer>();
        blob.sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 1.2f);
        blob.color = new Color(0.22f, 0.14f, 0.04f, 0.5f);
        blob.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        blob.receiveShadows = false;
        go.transform.localPosition = new Vector3(0.1f, 0.02f, 0.05f);
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
    public void SnapFacing(float yawDegrees) { yaw = yawDegrees; transform.rotation = Quaternion.Euler(0f, yaw, 0f); }

    // What the character looks at: a point in the world (the ball) or nothing (it looks at the net).
    public void LookAt(Vector3 point, bool has) { lookPoint = point; hasLook = has; }

    public void Swing() { swingTime = 0f; }
    public void CancelSwing() { swingTime = -1f; }

    // ---- the pose ----

    void LateUpdate()
    {
        if (hips == null) return;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
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

        // Body direction: towards the ball, but never turning more than `maxBodyYaw` away from the net.
        float forwardYaw = 0f; // the player's side looks at +Z
        float target = forwardYaw;
        if (hasLook)
        {
            Vector3 d = lookPoint - p;
            if (new Vector2(d.x, d.z).sqrMagnitude > 0.04f) target = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }
        target = forwardYaw + Mathf.Clamp(Mathf.DeltaAngle(forwardYaw, target), -maxBodyYaw, maxBodyYaw);
        yaw = Mathf.MoveTowardsAngle(yaw, target, turnSpeed * dt);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        int kind = racket != null ? racket.PoseKind : 0;
        for (int i = 0; i < all.Length; i++) if (all[i] != null) all[i].localRotation = rest[i];
        hips.localPosition = hipsRestPos;
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        if (blob != null) blob.enabled = true;

        Vector3 right = transform.right, up = Vector3.up, fwd = transform.forward;
        float k = modelScale; // lengths in the rig are in model units

        if (kind == 2) { PoseFall(dt, right, up, fwd); return; }
        hopTime = kind == 1 ? hopTime + dt : 0f;

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
        ArmReady(right, up, fwd, axis, s, moving, run, breath);

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

    // The racket arm waiting for the ball: elbow bent, racket up and a little back.
    void ArmReady(Vector3 right, Vector3 up, Vector3 fwd, Vector3 axis, float s, float moving, float run, float breath)
    {
        Rot(rUA, fwd, -68f);
        Rot(rUA, right, -38f + breath * 1.5f);
        Rot(rUA, up, 22f);
        Rot(rLA, right, -75f);
        Rot(rLA, axis, -s * 6f * moving);
    }

    // The forehand: wind-up (the ready pose, a bit further back), contact in front of the right shoulder, follow-through across the body.
    // Each key is (time, arm forward, arm sideways, elbow, torso turn, forearm roll).
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
        float hop = Mathf.Abs(Mathf.Sin(hopTime * 6.5f));
        hips.position += up * (hop * 0.25f * modelScale);
        float wave = Mathf.Sin(hopTime * 13f) * 12f;
        // both arms up: undo the ready pose, then raise
        for (int i = 0; i < all.Length; i++) if (all[i] == rUA || all[i] == rLA || all[i] == lUA || all[i] == lLA) all[i].localRotation = rest[i];
        Rot(rUA, fwd, 150f + wave); Rot(lUA, fwd, -150f - wave);
        Rot(rLA, right, -10f); Rot(lLA, right, -10f);
        Rot(spine, right, -6f);
    }

    // The loser lies on their back: the whole body tips about the feet.
    void PoseFall(float dt, Vector3 right, Vector3 up, Vector3 fwd)
    {
        hopTime += dt;
        float u = Mathf.Clamp01(hopTime / 0.5f);
        float ease = u * u;
        visual.localRotation = Quaternion.Euler(-84f * ease, 0f, 0f);
        visual.localPosition = new Vector3(0f, 0.1f * modelScale * ease, 0f);
        Rot(lUA, fwd, 35f); Rot(rUA, fwd, -35f);
    }

    // Turns a bone about a world axis (its own parents' turns stay in effect, so call parents first).
    static void Rot(Transform bone, Vector3 axis, float degrees)
    {
        if (bone == null || Mathf.Abs(degrees) < 0.001f || axis.sqrMagnitude < 1e-6f) return;
        bone.rotation = Quaternion.AngleAxis(degrees, axis.normalized) * bone.rotation;
    }
}
