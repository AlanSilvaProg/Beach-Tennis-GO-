using UnityEngine;

// CPU opponent. Level values are "base + perLevel * level" (clamped), so the curve is easy to tune.
//  - Movement: once it has reacted, it keeps predicting the flight of the ball and walks to the earliest point
//    it can actually reach in time. It lets balls that will land out go, and ignores balls that will hit the net.
//  - Recovery: after hitting it goes back to a ready position that leans towards the player's side.
//  - Shots: picks the landing spot that is hardest for the player to reach (away from the player's racket),
//    shoots faster when the player is far out of position. The lower the level, the noisier that choice is.
public class AiRacketController : RacketController
{
    [Header("Ready position")]
    public Vector3 restPosition = new Vector3(0f, 1f, 4.8f);
    [Tooltip("How much the ready position follows the player's sideways position (0 = stay centred).")]
    [Range(0f, 1f)] public float readyFollowX = 0.3f;

    [Header("Movement speed (m/s)")]
    public float speedBase = 5.5f;
    public float speedPerLevel = 0.9f;

    [Header("Positioning error (m)")]
    public float errorBase = 0.7f;
    public float errorPerLevel = -0.05f;
    public float errorMin = 0.05f;
    [Tooltip("The error shrinks to this fraction as the ball gets close (1 = never shrinks).")]
    [Range(0f, 1f)] public float errorCloseFraction = 0.4f;

    [Header("Chance to blunder a return")]
    [Range(0f, 1f)] public float mistakeBase = 0.12f;
    public float mistakePerLevel = -0.012f;
    [Range(0f, 1f)] public float mistakeMin = 0.01f;
    [Tooltip("Extra positioning error (m) when the AI blunders.")]
    public float mistakeOffset = 1.2f;

    [Header("Chance to hit the ball out")]
    [Range(0f, 1f)] public float shotMistakeBase = 0.06f;
    public float shotMistakePerLevel = -0.005f;
    [Range(0f, 1f)] public float shotMistakeMin = 0.005f;

    [Header("Reaction time after the player hits (s)")]
    public float reactionBase = 0.25f;
    public float reactionPerLevel = -0.022f;
    public float reactionMin = 0.03f;
    [Tooltip("Reaction time when the AI has to chase the player's serve.")]
    public float serveReaction = 0.4f;

    [Header("Reading the ball")]
    [Tooltip("Longest flight (s) it predicts ahead.")]
    public float predictTime = 3f;
    [Tooltip("Let balls that will land outside the lines go (the player loses the point).")]
    public bool letOutBallsGo = true;
    [Tooltip("A ball landing within this distance of the lines still counts as in (m).")]
    public float outTolerance = 0.25f;

    [Header("Return shot")]
    public float flightBase = 1.05f;
    public float flightPerLevel = -0.04f;
    public float flightMin = 0.5f;
    [Tooltip("Random variation applied to the flight time (min, max multiplier).")]
    public Vector2 flightJitter = new Vector2(0.9f, 1.1f);
    [Tooltip("Flight time multiplier when the player is far out of position (faster ball).")]
    [Range(0.5f, 1f)] public float pressureFlight = 0.8f;
    [Tooltip("Where the return arrives, measured from the net (min, max).")]
    public Vector2 depthRange = new Vector2(1.5f, 7.4f);
    [Tooltip("Up to this level the CPU plays fewer short balls (balls that land close to the net).")]
    public int fewerShortShotsUpToLevel = 2;
    [Tooltip("A target shallower than this (m from the net) counts as a short ball.")]
    public float shortShotDepth = 4.5f;
    [Tooltip("Chance (0..1) that such a short target is replaced by a deeper one in those levels; 0 = unchanged, 1 = never short.")]
    [Range(0f, 1f)] public float shortShotReroll = 0.5f;
    [Tooltip("Aimed shots stay at least this far inside the sidelines (m).")]
    public float edgeMargin = 0.5f;
    [Tooltip("How far (m) ahead the player's racket is assumed to keep moving when choosing the target.")]
    public float playerLookAhead = 0.25f;
    [Tooltip("Noise (m) added to the choice of target: base + perLevel * level, clamped to min.")]
    public float aimNoiseBase = 3f;
    public float aimNoisePerLevel = -0.25f;
    public float aimNoiseMin = 0.25f;
    [Tooltip("Variety of the targets: the landing spot is drawn at random, weighted by how hard it is for the player to reach. Higher = more varied, 0 = always the hardest spot.")]
    [Min(0f)] public float aimVariety = 1.2f;
    [Tooltip("Bonus (m of 'difficulty') for deep shots, so the ball is not always played short near the net.")]
    public float deepBias = 1.8f;
    [Tooltip("Penalty (m of 'difficulty') for a target close to one of the last few shots, so the CPU does not repeat itself.")]
    public float repeatPenalty = 2f;
    [Tooltip("A target closer than this to a recent shot counts as a repeat (m).")]
    public float repeatRadius = 2f;

    const int GridX = 9, GridZ = 5, Recent = 4;
    readonly Vector2[] recentTargets = new Vector2[Recent];
    int recentCount, recentHead;
    readonly float[] scores = new float[GridX * GridZ];

    float speed, error, mistake, shotMistake, reaction, flight, aimNoise;
    int currentLevel = 1;
    float reactTimer;
    Vector3 errorVec;
    bool blunder;

    public void SetLevel(int level)
    {
        int l = Mathf.Clamp(level, 1, 12);
        currentLevel = l;
        speed = speedBase + speedPerLevel * l;
        error = Mathf.Max(errorMin, errorBase + errorPerLevel * l);
        mistake = Mathf.Max(mistakeMin, mistakeBase + mistakePerLevel * l);
        shotMistake = Mathf.Max(shotMistakeMin, shotMistakeBase + shotMistakePerLevel * l);
        reaction = Mathf.Max(reactionMin, reactionBase + reactionPerLevel * l);
        flight = Mathf.Max(flightMin, flightBase + flightPerLevel * l);
        aimNoise = Mathf.Max(aimNoiseMin, aimNoiseBase + aimNoisePerLevel * l);
    }

    public override void ResetPosition()
    {
        Racket.MoveTo(restPosition);
        Racket.SnapSweep();
    }

    public void DelayReaction(float seconds) { reactTimer = seconds; }

    Vector3 ReadyPosition(GameManager gm)
    {
        float x = gm.Player.Racket.Position.x * readyFollowX;
        return new Vector3(x, Racket.height, restPosition.z);
    }

    public override void Tick(GameManager gm)
    {
        Vector3 goal = ReadyPosition(gm);
        if (gm.AiServing)
            goal = new Vector3(gm.Ball.Position.x, Racket.height, gm.Ball.Position.z);
        else if (gm.AiShouldChase)
        {
            reactTimer -= Time.deltaTime;
            goal = Racket.Position; // still reacting: hold position
            if (reactTimer <= 0f && FindIntercept(gm, out Vector3 point, out float time))
            {
                float scale = Mathf.Lerp(errorCloseFraction, 1f, Mathf.Clamp01(time));
                if (blunder) scale = 1f;
                goal = Racket.Clamp(point + errorVec * scale);
            }
            else if (reactTimer <= 0f)
                goal = ReadyPosition(gm); // ball will hit the net or go out: nothing to chase
        }
        Racket.MoveTo(Vector3.MoveTowards(Racket.Position, goal, speed * Time.deltaTime));
    }

    public override void OnOpponentHit(GameManager gm)
    {
        reactTimer = reaction;
        blunder = Random.value < mistake;
        float err = error + (blunder ? mistakeOffset : 0f);
        float sx = Random.value < 0.5f ? -1f : 1f;
        float sz = Random.value < 0.5f ? -1f : 1f;
        errorVec = new Vector3(sx * Random.Range(0f, err), 0f, sz * Random.Range(0f, err));
    }

    // Walks the ball's flight forward and returns the earliest point on the AI's side that the racket can reach
    // in time (after the remaining reaction time). If none is reachable, returns the landing point so the AI
    // still runs towards it. Returns false when the ball will hit the net, or will land out and is let go.
    bool FindIntercept(GameManager gm, out Vector3 point, out float time)
    {
        const float dt = 1f / 30f;
        Ball ball = gm.Ball;
        Net net = gm.Net;
        Court court = gm.Court;
        float simDt = dt * gm.BallSpeed; // ball seconds per step
        float side = Racket.SideSign;
        float reach = Mathf.Max(0f, Racket.hitRadius * 0.7f);
        float delay = Mathf.Max(0f, reactTimer);

        Vector3 p = ball.Position, v = ball.Velocity;
        Vector3 pos = Racket.Position;
        point = p;
        time = 0f;
        bool found = false;
        Vector3 first = p;

        for (float t = 0f; t < predictTime; t += dt)
        {
            Vector3 prev = p;
            v.y -= ball.gravity * simDt;
            p += v * simDt;

            if (prev.z * p.z < 0f && Mathf.Abs(p.x) < net.HalfWidth && p.y < net.TopY + ball.radius) return false;

            bool landed = p.y - ball.radius <= court.groundY;
            if (landed) p.y = court.groundY + ball.radius;

            if (p.z * side > 0f && p.y <= Racket.MaxHitY)
            {
                Vector3 flat = Racket.Clamp(new Vector3(p.x, Racket.height, p.z));
                float dist = Mathf.Max(0f, Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(flat.x, 0f, flat.z)) - reach);
                if (!found && delay + dist / speed <= t + dt)
                {
                    found = true;
                    point = p;
                    time = t;
                    first = p;
                }
            }

            if (landed)
            {
                if (letOutBallsGo && OutOfCourt(court, p)) return false;
                if (!found) { point = p; time = t; }
                return true;
            }
        }
        if (!found) { point = p; time = predictTime; }
        return true;
    }

    bool OutOfCourt(Court court, Vector3 p)
    {
        return Mathf.Abs(p.x - court.transform.position.x) > court.halfWidth + outTolerance
            || Mathf.Abs(p.z - court.transform.position.z) > court.halfLength + outTolerance;
    }

    // Aims at the spot that is hardest for the player to reach, with noise that shrinks as the level rises.
    public override Vector3 ComputeShot(GameManager gm, Vector3 contact)
    {
        Court court = gm.Court;
        Racket player = gm.Player.Racket;
        Vector3 pp = player.Position + player.Velocity * playerLookAhead;
        float maxX = court.halfWidth - edgeMargin;
        float minD = Mathf.Min(depthRange.x, court.halfLength - edgeMargin);
        float maxD = Mathf.Min(depthRange.y, court.halfLength - edgeMargin);
        float zSign = -Racket.SideSign;

        // Score every target: far from the player is good, deep is good, repeating a recent shot is bad.
        float maxScore = float.MinValue;
        for (int ix = 0; ix < GridX; ix++)
            for (int iz = 0; iz < GridZ; iz++)
            {
                float x = Mathf.Lerp(-maxX, maxX, ix / (float)(GridX - 1));
                float depth = Mathf.Lerp(minD, maxD, iz / (float)(GridZ - 1));
                Vector2 spot = new Vector2(x, zSign * depth);
                float score = Vector2.Distance(new Vector2(pp.x, pp.z), spot)
                              + deepBias * (iz / (float)(GridZ - 1))
                              + Random.Range(-aimNoise, aimNoise);
                for (int r = 0; r < recentCount; r++)
                    if (Vector2.Distance(recentTargets[r], spot) < repeatRadius) { score -= repeatPenalty; break; }
                scores[ix * GridZ + iz] = score;
                maxScore = Mathf.Max(maxScore, score);
            }

        // Draw one target at random, favouring the better scores (softmax); variety 0 picks the best.
        float total = 0f;
        for (int i = 0; i < scores.Length; i++)
        {
            scores[i] = aimVariety <= 0.0001f ? (scores[i] >= maxScore ? 1f : 0f) : Mathf.Exp((scores[i] - maxScore) / aimVariety);
            total += scores[i];
        }
        float pick = Random.value * total;
        int chosen = scores.Length - 1;
        for (int i = 0; i < scores.Length; i++)
        {
            pick -= scores[i];
            if (pick <= 0f) { chosen = i; break; }
        }
        float bestX = Mathf.Lerp(-maxX, maxX, (chosen / GridZ) / (float)(GridX - 1));
        float bestDepth = Mathf.Lerp(minD, maxD, (chosen % GridZ) / (float)(GridZ - 1));
        // The first levels drop it short less often: some short targets are swapped for a deeper one.
        if (currentLevel <= fewerShortShotsUpToLevel && bestDepth < shortShotDepth && Random.value < shortShotReroll)
            bestDepth = Random.Range(Mathf.Max(shortShotDepth, minD), maxD);
        float bestDist = Vector2.Distance(new Vector2(pp.x, pp.z), new Vector2(bestX, zSign * bestDepth));
        recentTargets[recentHead] = new Vector2(bestX, zSign * bestDepth);
        recentHead = (recentHead + 1) % Recent;
        recentCount = Mathf.Min(recentCount + 1, Recent);

        if (Random.value < shotMistake) bestDepth = court.halfLength + Random.Range(0.8f, 1.8f);
        float pressure = Mathf.InverseLerp(2f, 6f, bestDist);
        float t = flight * Random.Range(flightJitter.x, flightJitter.y) * Mathf.Lerp(1f, pressureFlight, pressure);
        t = Mathf.Max(flightMin * 0.8f, t);
        return gm.Ball.SolveHit(contact, bestX, zSign * bestDepth, court.groundY + gm.Ball.radius, t, gm.Net);
    }

#if UNITY_EDITOR
    // Gizmos: where the CPU rests, the zone its returns aim at (on the player's side) and the "short ball" line used by the first levels.
    void OnDrawGizmos()
    {
        GizmoShapes.Cross(restPosition, 0.3f, GizmoShapes.Aim);
        var court = FindFirstObjectByType<Court>();
        if (court == null) return;
        float z = -Racket.SideSign;              // the player's side, where the CPU sends the ball
        float maxX = court.halfWidth - edgeMargin;
        float maxD = Mathf.Min(depthRange.y, court.halfLength - edgeMargin);
        float minD = Mathf.Min(depthRange.x, maxD);
        float y = court.groundY + 0.03f;
        GizmoShapes.Rect(new Vector3(0f, y, z * (minD + maxD) * 0.5f), maxX, (maxD - minD) * 0.5f, GizmoShapes.Aim);
        Gizmos.color = new Color(GizmoShapes.Aim.r, GizmoShapes.Aim.g, GizmoShapes.Aim.b, 0.6f);
        Gizmos.DrawLine(new Vector3(-maxX, y, z * shortShotDepth), new Vector3(maxX, y, z * shortShotDepth));
    }
#endif
}
