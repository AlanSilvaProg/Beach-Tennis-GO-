using UnityEngine;

// The opponent of the (fake) online matches: it plays like a person rather than like the training CPU.
//  - Movement: not smooth. The speed changes in sudden steps (a burst, a pause, a burst), it only looks at where the ball will be every now and
//    then (so its target is a little out of date) and it brakes late. Never a straight line at a constant speed.
//  - Waiting: it does not run back to the middle after a point and it does not mirror the player: it wanders from one spot to another on its own.
//  - Swings: after every hit the racket follows through; sometimes a small tidy stroke, sometimes a big exaggerated one (which also leaves it
//    slower to recover).
//  - Serves: it walks round the ball, waits a moment and swings. Now and then it swings and misses the ball ("moves but does not hit") and
//    tries again, but it always hits more serves than it misses.
//  - Difficulty: one of five tiers, drawn at random for every opponent (SetTier). Even the weakest one returns most balls.
// The shot choice (where the ball goes) is the training CPU's, tuned by the tier's base level.
public class OnlineRacketController : AiRacketController
{
    [System.Serializable]
    public struct Tier
    {
        [Tooltip("Level of the training CPU used for the shot choice (aim noise, ball speed).")]
        public int baseLevel;
        [Tooltip("Top running speed (m/s), before the bursts.")]
        public float speed;
        [Tooltip("Reaction time after the player hits (s).")]
        public float reaction;
        [Tooltip("Positioning error (m).")]
        public float error;
        [Tooltip("Chance to blunder a return (extra positioning error).")]
        [Range(0f, 1f)] public float mistake;
        [Tooltip("Chance to hit the ball out.")]
        [Range(0f, 1f)] public float shotMistake;
        [Tooltip("Chance that a serve swing misses the ball. Keep it under 0.5: it must hit more serves than it misses.")]
        [Range(0f, 0.49f)] public float serveMiss;
        [Tooltip("Chance of an exaggerated follow-through after a hit.")]
        [Range(0f, 1f)] public float exaggerate;
    }

    [Header("Difficulty tiers (1 = easiest ... 5 = hardest)")]
    public Tier[] tiers =
    {
        new Tier { baseLevel = 2,  speed = 5.8f, reaction = 0.30f, error = 0.85f, mistake = 0.17f, shotMistake = 0.09f,  serveMiss = 0.30f, exaggerate = 0.55f },
        new Tier { baseLevel = 4,  speed = 6.6f, reaction = 0.24f, error = 0.65f, mistake = 0.13f, shotMistake = 0.07f,  serveMiss = 0.24f, exaggerate = 0.45f },
        new Tier { baseLevel = 6,  speed = 7.4f, reaction = 0.19f, error = 0.50f, mistake = 0.10f, shotMistake = 0.05f,  serveMiss = 0.18f, exaggerate = 0.40f },
        new Tier { baseLevel = 8,  speed = 8.2f, reaction = 0.15f, error = 0.38f, mistake = 0.07f, shotMistake = 0.035f, serveMiss = 0.13f, exaggerate = 0.35f },
        new Tier { baseLevel = 10, speed = 9.0f, reaction = 0.11f, error = 0.28f, mistake = 0.045f, shotMistake = 0.02f, serveMiss = 0.08f, exaggerate = 0.30f },
    };

    [Header("Movement (jerky on purpose)")]
    [Tooltip("The speed is held for a random time between these (s), then it jumps to a new one.")]
    public Vector2 stepTime = new Vector2(0.07f, 0.2f);
    [Tooltip("Each new speed is this fraction of the top speed (min, max). Above 1 = a burst.")]
    public Vector2 stepSpeed = new Vector2(0.5f, 1.5f);
    [Tooltip("Chance that a step is a near stop (a pause). Pauses never happen when the ball is about to arrive.")]
    [Range(0f, 0.5f)] public float pauseChance = 0.14f;
    [Tooltip("How quickly it picks up and loses speed (m/s per second): high = abrupt.")]
    public float acceleration = 75f;
    [Tooltip("How hard it brakes towards its target: higher = stops tighter, lower = overshoots more.")]
    public float arriveGain = 20f;
    [Tooltip("It looks at where the ball will be only this often (s): in between, it runs to where it last decided.")]
    public float lookInterval = 0.11f;

    [Header("Waiting (no returning to the centre, no copying the player)")]
    [Tooltip("When it has nothing to chase it picks a spot of its own: how far to the side and how far back from the net (m, around its resting place).")]
    public float homeRangeX = 2.4f;
    public float homeRangeZ = 0.9f;
    [Tooltip("Seconds before it picks another spot (min, max).")]
    public Vector2 homeTime = new Vector2(1.2f, 3.4f);
    [Tooltip("Restless wobble around that spot (m).")]
    public float wanderAmount = 0.2f;
    [Tooltip("The reaction time varies by this factor (min, max).")]
    public Vector2 reactionVariation = new Vector2(0.8f, 1.35f);

    [Header("Follow-through after a hit")]
    [Tooltip("Distance (m) the racket travels past the ball: a tidy stroke and an exaggerated one.")]
    public Vector2 smallSwing = new Vector2(0.15f, 0.4f);
    public Vector2 bigSwing = new Vector2(0.8f, 1.5f);
    [Tooltip("Seconds the follow-through takes: small, big.")]
    public Vector2 swingTime = new Vector2(0.12f, 0.17f);

    [Header("Serve")]
    [Tooltip("Seconds it takes before it swings (min, max): it is not instant.")]
    public Vector2 serveWait = new Vector2(0.3f, 0.9f);
    [Tooltip("How far behind the ball (m) it stands before swinging, and how far to the side it walks round it.")]
    public float serveBehind = 1.3f;
    public float walkAround = 1.4f;
    [Tooltip("Seconds of the serve swing (min, max) and how far past the ball it goes (m).")]
    public Vector2 serveSwingTime = new Vector2(0.17f, 0.3f);
    public Vector2 serveFollowThrough = new Vector2(1.2f, 2.2f);
    [Tooltip("A serve that misses passes the ball this far to the side (m): more than the racket's reach.")]
    public Vector2 missOffset = new Vector2(0.95f, 1.4f);
    [Tooltip("Pause (s) after a miss before the next try.")]
    public Vector2 missPause = new Vector2(0.5f, 0.9f);

    public int TierNumber { get; private set; } = 3;

    Tier tier;
    Vector3 velocity;
    float clock, seed;
    float stepUntil, stepFactor = 1f;
    float lookAt;
    Vector3 lookGoal;
    bool lookHurry;
    float homeUntil;
    Vector3 home;
    bool snapNext = true;

    // follow-through
    float swingT = -1f, swingLength;
    Vector3 swingFrom, swingTo;

    // serving
    enum ServeStage { None, Walk, Wait, Swing, Recover }
    ServeStage stage;
    float stageT, waitTime, serveSwingLength;
    bool whiff, passedBall;
    float standX;               // where it stands, sideways from the ball: right behind it, or well to the side when it is going to miss
    int attempts;
    float aroundSide = 1f;
    Vector3 serveFrom, serveTo;

    void Awake()
    {
        seed = Random.value * 100f;
        SetTier(TierNumber);
    }

    // The training level does not matter here: the difficulty is the tier (1..5).
    public override void SetLevel(int level) { }

    public void SetTier(int number)
    {
        TierNumber = Mathf.Clamp(number, 1, tiers.Length);
        tier = tiers[TierNumber - 1];
        snapNext = true; // a new opponent: the next ResetPosition starts from the resting place
        base.SetLevel(tier.baseLevel);
        speed = tier.speed;
        reaction = tier.reaction;
        error = tier.error;
        mistake = tier.mistake;
        shotMistake = tier.shotMistake;
    }

    // Only the first call of a match puts the racket on its resting place; after that it stays where it is (it does not go back to the middle
    // after every point).
    public override void ResetPosition()
    {
        velocity = Vector3.zero;
        swingT = -1f;
        stage = ServeStage.None;
        if (snapNext) { base.ResetPosition(); snapNext = false; home = Racket.Position; }
    }

    public override void OnOpponentHit(GameManager gm)
    {
        base.OnOpponentHit(gm);
        reactTimer = reaction * Random.Range(reactionVariation.x, reactionVariation.y); // a person is not as regular
    }

    public override void Tick(GameManager gm) { Step(gm, Time.deltaTime); }

    public void Step(GameManager gm, float dt)
    {
        clock += dt;
        if (gm.AiServing) { ServeStep(gm, dt); return; }
        stage = ServeStage.None;

        if (swingT >= 0f && FollowThrough(dt)) return;

        bool hurry = false;
        float timeToBall = 9f;
        if (gm.AiShouldChase)
        {
            reactTimer -= dt;
            if (reactTimer > 0f) { lookGoal = Racket.Position; lookHurry = false; lookAt = clock; } // still reacting: stays where it is
            else if (clock >= lookAt)
            {
                // it decides where to go only every Look Interval: in between it runs to a spot that is a little out of date
                lookAt = clock + lookInterval;
                if (FindIntercept(gm, out Vector3 point, out float time))
                {
                    float scale = Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(time));
                    if (blunder) scale = 1f;
                    lookGoal = Racket.Clamp(point + errorVec * scale);
                    lookHurry = true;
                    lookTime = time;
                }
                else { lookGoal = Home(); lookHurry = false; }
            }
            hurry = lookHurry;
            timeToBall = lookHurry ? Mathf.Max(0f, lookTime - (clock - (lookAt - lookInterval))) : 9f;
        }
        else
        {
            lookGoal = Home();
            lookHurry = false;
        }
        Drive(lookGoal, dt, hurry, timeToBall);
    }

    float lookTime;

    // Runs towards `goal` in steps: the speed is picked anew every few hundredths of a second (a burst, a pause...), it picks up and loses speed
    // abruptly and brakes late. Pauses are not allowed when the ball is about to arrive.
    void Drive(Vector3 goal, float dt, bool hurry, float timeToBall)
    {
        Vector3 pos = Racket.Position;
        Vector3 to = goal - pos; to.y = 0f;
        float dist = to.magnitude;
        if (clock >= stepUntil)
        {
            stepUntil = clock + Random.Range(stepTime.x, stepTime.y);
            bool pause = Random.value < pauseChance && !(hurry && timeToBall < 0.6f);
            stepFactor = pause ? 0.08f : Random.Range(stepSpeed.x, stepSpeed.y);
            if (hurry && timeToBall < 0.6f) stepFactor = Mathf.Max(stepFactor, 0.85f);
        }
        float top = speed * stepFactor * (hurry ? 1.1f : 0.7f);
        Vector3 wanted = dist > 0.001f ? to / dist * Mathf.Min(top, dist * arriveGain) : Vector3.zero;
        velocity = Vector3.MoveTowards(velocity, wanted, acceleration * dt);
        Racket.MoveTo(pos + velocity * dt);
    }

    // The spot it hangs about at while there is nothing to chase: its own, changing every few seconds, never the middle by habit and never
    // following the player.
    Vector3 Home()
    {
        if (clock >= homeUntil)
        {
            homeUntil = clock + Random.Range(homeTime.x, homeTime.y);
            float x = Random.Range(-homeRangeX, homeRangeX);
            float z = restPosition.z + Random.Range(-homeRangeZ * 0.5f, homeRangeZ);
            home = new Vector3(x, Racket.height, z);
        }
        float wx = (Mathf.PerlinNoise(clock * 0.7f, seed + 1f) - 0.5f) * 2f * wanderAmount;
        float wz = (Mathf.PerlinNoise(clock * 0.6f, seed + 2f) - 0.5f) * 2f * wanderAmount;
        return Racket.Clamp(home + new Vector3(wx, 0f, wz));
    }

    // ---------- follow-through ----------

    public override Vector3 ComputeShot(GameManager gm, Vector3 contact)
    {
        Vector3 shot = base.ComputeShot(gm, contact);
        BeginFollowThrough(shot);
        return shot;
    }

    void BeginFollowThrough(Vector3 shot)
    {
        bool big = Random.value < tier.exaggerate;
        float distance = big ? Random.Range(bigSwing.x, bigSwing.y) : Random.Range(smallSwing.x, smallSwing.y);
        Vector3 dir = new Vector3(shot.x, 0f, shot.z).normalized;
        swingFrom = Racket.Position;
        swingTo = Racket.Clamp(swingFrom + dir * distance);
        swingLength = big ? swingTime.y : swingTime.x;
        swingT = 0f;
    }

    // Returns true while the follow-through is still going on (it takes over the racket).
    bool FollowThrough(float dt)
    {
        swingT += dt;
        float k = Mathf.Clamp01(swingT / Mathf.Max(0.05f, swingLength));
        float eased = 1f - (1f - k) * (1f - k);
        Racket.MoveTo(Vector3.Lerp(swingFrom, swingTo, eased));
        if (k < 1f) return true;
        velocity = (swingTo - swingFrom) / Mathf.Max(0.05f, swingLength) * 0.45f; // the stroke carries on a little
        swingT = -1f;
        return false;
    }

    // ---------- serving ----------

    // Behind the ball it walks round it (so that it does not touch it by accident), waits, swings through it and sometimes misses.
    void ServeStep(GameManager gm, float dt)
    {
        Vector3 ball = gm.Ball.Position;
        float back = Racket.SideSign;                    // away from the net
        if (stage == ServeStage.None) BeginServe(ball);
        stageT += dt;
        Vector3 behind = new Vector3(ball.x + standX, Racket.height, ball.z + back * serveBehind);

        switch (stage)
        {
            case ServeStage.Walk:
            case ServeStage.Recover:
            {
                // beside the ball first, then behind it
                Vector3 pos = Racket.Position;
                if (!passedBall && (pos.z - ball.z) * back >= serveBehind * 0.7f) passedBall = true;
                Vector3 side = new Vector3(ball.x + aroundSide * walkAround, Racket.height, passedBall ? behind.z : pos.z);
                Vector3 goal = passedBall ? behind : (Mathf.Abs(pos.x - side.x) > 0.35f ? side : new Vector3(side.x, Racket.height, behind.z));
                Drive(goal, dt, false, 9f);
                if (passedBall && Flat(Racket.Position, behind) < 0.25f) { stage = ServeStage.Wait; stageT = 0f; }
                break;
            }
            case ServeStage.Wait:
                Drive(behind, dt, false, 9f);
                if (stageT >= waitTime && gm.AiServeReady) BeginServeSwing(ball, back);
                break;
            case ServeStage.Swing:
            {
                float k = Mathf.Clamp01(stageT / serveSwingLength);
                Racket.MoveTo(Vector3.Lerp(serveFrom, serveTo, Mathf.Pow(k, 1.35f)));  // speeds up through the ball
                velocity = (serveTo - serveFrom) / serveSwingLength * 0.5f;
                if (k >= 1f)
                {
                    // the swing ended and the ball is still there: it missed. A short pause, then another try.
                    attempts++;
                    DecideServe(tier.serveMiss * Mathf.Pow(0.5f, attempts));
                    stage = ServeStage.Recover; stageT = 0f; passedBall = false;
                    waitTime = Random.Range(missPause.x, missPause.y);
                    aroundSide = ball.x > 0f ? -1f : 1f;
                }
                break;
            }
        }
    }

    void BeginServe(Vector3 ball)
    {
        stage = ServeStage.Walk; stageT = 0f; attempts = 0; passedBall = false;
        waitTime = Random.Range(serveWait.x, serveWait.y);
        DecideServe(tier.serveMiss);
        aroundSide = ball.x > 0f ? -1f : 1f; // round the side that has more room
        velocity = Vector3.zero;
    }

    // Is the next swing going to miss? A miss passes the ball to the side, further than the racket reaches (it moves, but it does not hit).
    void DecideServe(float missChance)
    {
        whiff = Random.value < missChance;
        standX = whiff ? (Random.value < 0.5f ? -1f : 1f) * Random.Range(missOffset.x, missOffset.y) : Random.Range(-0.12f, 0.12f);
    }

    void BeginServeSwing(Vector3 ball, float back)
    {
        serveFrom = Racket.Position;
        serveTo = Racket.Clamp(new Vector3(Racket.Position.x, Racket.height, ball.z - back * Random.Range(serveFollowThrough.x, serveFollowThrough.y)));
        serveSwingLength = Random.Range(serveSwingTime.x, serveSwingTime.y);
        stage = ServeStage.Swing; stageT = 0f;
    }

    static float Flat(Vector3 a, Vector3 b) { return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)); }
}
