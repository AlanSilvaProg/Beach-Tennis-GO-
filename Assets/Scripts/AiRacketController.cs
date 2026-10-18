using UnityEngine;

// CPU opponent. Every value is "base + perLevel * level" (clamped), so the curve is easy to tune.
public class AiRacketController : RacketController
{
    [Header("Idle")]
    public Vector3 restPosition = new Vector3(0f, 1f, 4.8f);

    [Header("Movement speed (m/s)")]
    public float speedBase = 4.5f;
    public float speedPerLevel = 0.9f;

    [Header("Positioning error (m)")]
    public float errorBase = 0.9f;
    public float errorPerLevel = -0.06f;
    public float errorMin = 0.1f;

    [Header("Chance to blunder a return")]
    [Range(0f, 1f)] public float mistakeBase = 0.20f;
    public float mistakePerLevel = -0.016f;
    [Range(0f, 1f)] public float mistakeMin = 0.02f;
    [Tooltip("Extra positioning error (m) when the AI blunders.")]
    public float mistakeOffset = 1.2f;

    [Header("Chance to hit the ball out")]
    [Range(0f, 1f)] public float shotMistakeBase = 0.10f;
    public float shotMistakePerLevel = -0.008f;
    [Range(0f, 1f)] public float shotMistakeMin = 0.01f;

    [Header("Reaction time after the player hits (s)")]
    public float reactionBase = 0.30f;
    public float reactionPerLevel = -0.025f;
    public float reactionMin = 0.03f;
    [Tooltip("Reaction time when the AI has to chase the player's serve.")]
    public float serveReaction = 0.45f;

    [Header("Return shot")]
    public float flightBase = 1.1f;
    public float flightPerLevel = -0.04f;
    public float flightMin = 0.55f;
    [Tooltip("Random variation applied to the flight time (min, max multiplier).")]
    public Vector2 flightJitter = new Vector2(0.9f, 1.15f);
    [Tooltip("Where the return arrives, measured from the net (min, max).")]
    public Vector2 depthRange = new Vector2(1.5f, 7.4f);
    [Tooltip("Sideways distance of the return from the centre line (min, max).")]
    public Vector2 landXRange = new Vector2(0.3f, 3.5f);
    [Tooltip("Chance to aim away from the player's racket.")]
    [Range(0f, 1f)] public float aimAwayChance = 0.7f;

    float speed, error, mistake, shotMistake, reaction, flight;
    float reactTimer;
    Vector3 target;

    public void SetLevel(int level)
    {
        int l = Mathf.Clamp(level, 1, 12);
        speed = speedBase + speedPerLevel * l;
        error = Mathf.Max(errorMin, errorBase + errorPerLevel * l);
        mistake = Mathf.Max(mistakeMin, mistakeBase + mistakePerLevel * l);
        shotMistake = Mathf.Max(shotMistakeMin, shotMistakeBase + shotMistakePerLevel * l);
        reaction = Mathf.Max(reactionMin, reactionBase + reactionPerLevel * l);
        flight = Mathf.Max(flightMin, flightBase + flightPerLevel * l);
    }

    public override void ResetPosition()
    {
        Racket.MoveTo(restPosition);
        target = restPosition;
    }

    public void DelayReaction(float seconds) { reactTimer = seconds; }

    public override void Tick(GameManager gm)
    {
        Vector3 goal = restPosition;
        if (gm.AiServing)
            goal = new Vector3(gm.Ball.Position.x, Racket.height, gm.Ball.Position.z);
        else if (gm.AiShouldChase)
        {
            reactTimer -= Time.deltaTime;
            if (reactTimer <= 0f) goal = target;
        }
        Racket.MoveTo(Vector3.MoveTowards(Racket.Position, goal, speed * Time.deltaTime));
    }

    public override void OnOpponentHit(GameManager gm)
    {
        reactTimer = reaction;
        Vector3 hit;
        gm.Ball.PredictAtHeight(gm.Ball.Position, gm.Ball.Velocity, Racket.height, gm.Court, out hit);
        float err = error;
        if (Random.value < mistake) err += mistakeOffset;
        float sx = Random.value < 0.5f ? -1f : 1f;
        float sz = Random.value < 0.5f ? -1f : 1f;
        target = Racket.Clamp(new Vector3(hit.x + sx * Random.Range(0f, err), Racket.height, hit.z + sz * Random.Range(0f, err)));
    }

    public override Vector3 ComputeShot(GameManager gm, Vector3 contact)
    {
        float away = -Mathf.Sign(gm.Player.Racket.Position.x + 0.001f);
        float side = Random.value < aimAwayChance ? away : -away;
        float landX = Random.Range(landXRange.x, landXRange.y) * side;
        float depth = Random.Range(depthRange.x, depthRange.y);
        if (Random.value < shotMistake) depth = gm.Court.halfLength + Random.Range(0.8f, 1.8f);
        float t = flight * Random.Range(flightJitter.x, flightJitter.y);
        return gm.Ball.SolveHit(contact, landX, -Racket.SideSign * depth, gm.Court.groundY + gm.Ball.radius, t, gm.Net);
    }
}
