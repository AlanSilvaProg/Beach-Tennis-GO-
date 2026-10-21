using UnityEngine;

// Match flow and rules for the beach tennis PvE game: first to N points (win by 2) against an AI that gets tougher each level.
// Point rules: the hitter scores only when the ball lands inside the opponent's half; out, net or own-side landings lose the point.
public class GameManager : MonoBehaviour
{
    enum State { Menu, Serving, Rally, PointOver, MatchOver }

    [Header("Scene references")]
    public Court court;
    public Net net;
    public Ball ball;
    public PlayerRacketController player;
    public AiRacketController ai;
    public CameraRig cameraRig;
    public HudView hud;
    public ShotAnnouncer announcer;

    public enum LanguageChoice { Auto, English, Portuguese, Spanish }

    [Header("Language")]
    [Tooltip("Auto = the browser's language (pt, es or en; anything else is English). Pick one here to test; in the browser use ?lang=pt|es|en.")]
    public LanguageChoice language = LanguageChoice.Auto;

    [Header("Audio")]
    [Tooltip("Plays the sound effects (an AudioSource on this object).")]
    public AudioSource sfx;
    [Tooltip("Every racket hit and serve by the player.")]
    public AudioClip playerHit;
    [Tooltip("Every racket hit and serve by the opponent.")]
    public AudioClip enemyHit;
    [Range(0f, 1f)] public float hitVolume = 0.8f;
    [Tooltip("Random pitch variation so repeated hits do not sound identical (+/-).")]
    [Range(0f, 0.3f)] public float hitPitchVariance = 0.08f;
    [Tooltip("The ball touching the sand.")]
    public AudioClip sandLanding;
    [Tooltip("Crowd cheering, played every time a match ends (win or lose).")]
    public AudioClip crowdEnd;
    [Range(0f, 1f)] public float crowdVolume = 0.9f;
    [Range(0f, 1f)] public float landVolume = 0.7f;
    [Range(0f, 0.3f)] public float landPitchVariance = 0.1f;

    [Header("Match rules")]
    public int pointsToWin = 11;
    public int winBy = 2;
    [Tooltip("Points each player serves in a row (1 once both are near the end).")]
    public int servesPerTurn = 2;
    public int startLevel = 1;
    [Tooltip("No-Ad: once both players reach this score, the next point wins the match (0 = off).")]
    public int noAdScore = 15;

    [Header("Timing (s)")]
    public float serveDelay = 1.0f;
    public float pointOverDelay = 1.6f;
    public float matchOverInputDelay = 0.6f;
    [Tooltip("A rally longer than this is resolved as out.")]
    public float rallyTimeout = 20f;

    [Header("Serve")]
    [Tooltip("The serve ball hangs this far inside the baseline, on the server's side.")]
    public float serveBaselineOffset = 0.8f;
    public float serveBallHeight = 1.1f;
    [Tooltip("The CPU's serve ball is placed randomly this far to each side of the centre line.")]
    public float serveBallSpread = 1.0f;
    [Tooltip("The player's serve ball follows the racket sideways but stays within this distance of the sidelines.")]
    public float serveSideMargin = 0.5f;
    [Tooltip("The racket must first go this far behind the ball (away from the net) before the serve can be hit.")]
    public float serveArmDistance = 0.4f;
    [Tooltip("The player must swing the racket forward at least this fast (m/s) to hit the serve ball.")]
    public float serveMinHitSpeed = 0.4f;
    [Tooltip("The serve is hit when the racket is this close to the ball (m) or has passed its closest point, so the full speed and direction of the swipe are measured.")]
    public float serveContactRadius = 0.12f;

    [Header("Simulation")]
    [Tooltip("Multiplies how fast the ball travels. Arcs stay the same, the ball just gets there sooner.")]
    public float ballSpeed = 1.6f;
    public int targetFrameRate = 60;
    [Tooltip("Physics step in seconds (smaller = more precise).")]
    public float physicsStep = 1f / 240f;
    public float maxFrameTime = 0.1f;

    State state = State.Menu;
    float stateTimer;
    int level;
    int playerScore, aiScore;
    int server;
    int lastHitter;
    int infoLevel = -1;
    string info = "";
    int hitsInPoint; // racket hits since the serve (1 = only the serve has been hit)
    float rallyTime;
    float accumulator;
    bool serveArmed;
    float servePrevDz = float.NaN; // racket-to-ball distance along z last frame (NaN = it was out of reach)

    public Ball Ball => ball;
    public Net Net => net;
    public AiRacketController Ai => ai;
    public Court Court => court;
    // Simulated ball seconds per real second (the ball arcs are the same, it just gets there sooner).
    public float BallSpeed => ballSpeed;
    public PlayerRacketController Player => player;
    public bool AiShouldChase => state == State.Rally && lastHitter == 0;
    public bool AiServing => state == State.Serving && server == 1;

    void Awake()
    {
        Application.targetFrameRate = targetFrameRate;
        if (language != LanguageChoice.Auto) I18n.Current = (I18n.Language)((int)language - 1);
        if (announcer != null) announcer.LoadVoices(); // downloads only the voices of the chosen language
        level = startLevel;
        ai.SetLevel(level);
        player.ResetPosition();
        ai.ResetPosition();
        ball.SetVisible(false);
    }

    void Update()
    {
        stateTimer += Time.deltaTime;
        player.Tick(this);
        ai.Tick(this);

        switch (state)
        {
            case State.Menu:
                hud.ShowScore = false;
                hud.Banner = I18n.Tr("BEACH TENNIS GO!\nClick / tap to play");
                if (Pressed()) StartMatch();
                break;
            case State.Serving:
                hud.Banner = server == 0 ? I18n.Tr("Your serve\nSwing the racket at the ball") : I18n.Tr("CPU serve");
                if (server == 0) { TryPlayerServe(); if (!serveArmed) hud.Banner += "\n" + I18n.Tr("(pull the racket back first)"); }
                else if (stateTimer > serveDelay) TryAiServe();
                break;
            case State.Rally:
                hud.Banner = "";
                Simulate(true);
                break;
            case State.PointOver:
                Simulate(false);
                if (stateTimer > pointOverDelay) NextPoint();
                break;
            case State.MatchOver:
                if (stateTimer > matchOverInputDelay && Pressed())
                {
                    if (playerScore > aiScore) level++;
                    StartMatch();
                }
                break;
        }
        UpdateHud();
    }

    static bool Pressed()
    {
        return Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
    }

    void UpdateHud()
    {
        hud.ShowScore = state != State.Menu;
        hud.Score = playerScore + "  -  " + aiScore;
        if (level != infoLevel) { infoLevel = level; info = I18n.Tr("You  vs  CPU   |   Level {0}", level); }
        hud.Info = info;
    }

    void StartMatch()
    {
        playerScore = aiScore = 0;
        server = 0;
        ai.SetLevel(level);
        player.ResetPosition();
        ai.ResetPosition();
        PrepareServe();
    }

    void EnterState(State s)
    {
        state = s;
        stateTimer = 0f;
    }

    // ---------- ball ----------
    // The serve ball hangs in the air until the server's racket hits it.
    void PrepareServe()
    {
        float z = (server == 0 ? -1f : 1f) * (court.halfLength - serveBaselineOffset);
        serveArmed = false;
        servePrevDz = float.NaN;
        ball.Place(new Vector3(server == 0 ? player.Racket.Position.x : Random.Range(-serveBallSpread, serveBallSpread),
                               serveBallHeight, z), Vector3.zero);
        ball.SetVisible(true);
        lastHitter = server;
        cameraRig.SetServing(true);
        hitsInPoint = 0;
        EnterState(State.Serving);
        AnnouncePointStakes();
    }

    // Would the next point win the match for this side?
    bool WinsMatchWithNextPoint(int side)
    {
        int a = (side == 0 ? playerScore : aiScore) + 1, b = side == 0 ? aiScore : playerScore;
        return IsMatchOver(a, b);
    }

    // Win at pointsToWin with a lead of winBy; once both reach noAdScore, the next point wins (no advantage).
    bool IsMatchOver(int a, int b)
    {
        if (noAdScore > 0 && Mathf.Max(a, b) > noAdScore) return true;
        return Mathf.Max(a, b) >= pointsToWin && Mathf.Abs(a - b) >= winBy;
    }

    bool NoAdPoint => noAdScore > 0 && playerScore == noAdScore && aiScore == noAdScore;

    void AnnouncePointStakes()
    {
        if (announcer == null) return;
        bool matchPoint = WinsMatchWithNextPoint(0) || WinsMatchWithNextPoint(1);
        if (NoAdPoint) { announcer.Say(ShotAnnouncer.Call.NoAd); announcer.SayNext(ShotAnnouncer.Call.MatchPoint); }
        else if (matchPoint) announcer.Say(ShotAnnouncer.Call.MatchPoint);
    }

    bool InReach(Racket r)
    {
        Vector3 p = ball.Position, c = r.Position;
        float dx = p.x - c.x, dz = p.z - c.z;
        return dx * dx + dz * dz <= r.hitRadius * r.hitRadius && p.y <= r.MaxHitY; // balls above the vertical reach fly over
    }

    // The ball follows the racket sideways. It only accepts a hit after the racket has been pulled back behind it,
    // and then only when the racket swings forward into it.
    void TryPlayerServe()
    {
        Racket r = player.Racket;
        float maxX = court.halfWidth - serveSideMargin;
        ball.Position = new Vector3(Mathf.Clamp(r.Position.x, -maxX, maxX), ball.Position.y, ball.Position.z);
        ball.Sync(court.groundY);

        if (Mathf.Abs(r.Position.z) > Mathf.Abs(ball.Position.z) + serveArmDistance) serveArmed = true;
        // Do not hit the moment the racket enters reach (its speed is still building up): wait for the closest
        // point of the swipe, then read force and direction from the swing.
        bool inReach = InReach(r);
        float dz = r.Position.z - ball.Position.z;
        bool wasIn = !float.IsNaN(servePrevDz);
        bool crossed = wasIn && dz * servePrevDz <= 0f; // went through the ball between two frames
        bool contact = Mathf.Abs(dz) <= serveContactRadius || crossed || (wasIn && Mathf.Abs(dz) > Mathf.Abs(servePrevDz));
        if (serveArmed && (inReach || crossed) && contact && r.SwingForwardSpeed >= serveMinHitSpeed)
        {
            Hit(0, player);
            return;
        }
        servePrevDz = inReach ? dz : float.NaN;
    }

    void TryAiServe()
    {
        if (InReach(ai.Racket)) Hit(1, ai);
    }

    void Simulate(bool rules)
    {
        accumulator = Mathf.Min(accumulator + Time.deltaTime, maxFrameTime);
        while (accumulator >= physicsStep && (state == State.Rally || !rules))
        {
            accumulator -= physicsStep;
            StepBall(rules);
        }
        ball.Sync(court.groundY);
    }

    void StepBall(bool rules)
    {
        Vector3 prev;
        float fall = Mathf.Max(0f, -ball.Velocity.y - ball.gravity * physicsStep * ballSpeed); // vertical speed on impact
        bool landed = ball.Step(physicsStep * ballSpeed, court, net, out prev);
        rallyTime += physicsStep;
        if (landed) PlayLanding(fall);
        if (!rules) return;

        if (landed) { ResolveLanding(); return; }

        // Rackets hit the ball when it is within reach and flying towards them.
        if (lastHitter != 0 && ball.Velocity.z < 0f) CheckRacket(0, player);
        if (state == State.Rally && lastHitter != 1 && ball.Velocity.z > 0f) CheckRacket(1, ai);

        if (state == State.Rally && (rallyTime > rallyTimeout || ball.Position.magnitude > ball.maxDistance))
            Score(1 - lastHitter);
    }

    void CheckRacket(int side, RacketController ctrl)
    {
        if (InReach(ctrl.Racket)) Hit(side, ctrl);
    }

    void Hit(int side, RacketController ctrl)
    {
        Vector3 incoming = ball.Velocity, contact = ball.Position;
        ball.Velocity = ctrl.ComputeShot(this, contact);
        lastHitter = side;
        hitsInPoint++;
        PlayHit(side, ball.Velocity.magnitude);
        if (announcer != null && state != State.Serving)
            announcer.OnHit(ball, court, ctrl.Racket, side == 0 ? ai.Racket : player.Racket, side, contact, incoming, ball.Velocity);
        if (state == State.Serving)
        {
            rallyTime = 0f;
            accumulator = 0f;
            cameraRig.SetServing(false);
            EnterState(State.Rally);
        }
        if (side == 0) ai.OnOpponentHit(this);
    }

    void Play(AudioClip clip, float volume, float pitch)
    {
        sfx.pitch = pitch;
        sfx.PlayOneShot(clip, volume);
    }

    // Harder hits are louder (and a touch higher pitched). The player and the opponent have their own sound.
    void PlayHit(int side, float speed)
    {
        var clip = side == 0 ? playerHit : enemyHit;
        if (sfx == null || clip == null) return;
        float power = Mathf.Clamp01(speed / 14f);
        Play(clip, hitVolume * Mathf.Lerp(0.55f, 1f, power), 1f + Random.Range(-hitPitchVariance, hitPitchVariance) + power * 0.05f);
    }

    void PlayCrowd()
    {
        if (sfx == null || crowdEnd == null) return;
        Play(crowdEnd, crowdVolume, 1f);
    }

    // The ball touching the sand; the impact speed sets the loudness.
    void PlayLanding(float impactSpeed)
    {
        if (sfx == null || sandLanding == null) return;
        float power = Mathf.Clamp01(impactSpeed / 9f);
        Play(sandLanding, landVolume * Mathf.Lerp(0.4f, 1f, power), 1f + Random.Range(-landPitchVariance, landPitchVariance));
    }

    // ---------- rules ----------
    // The hitter scores only if the ball lands inside the opponent's half of the court.
    void ResolveLanding()
    {
        int landSide = ball.Position.z < 0f ? 0 : 1;
        bool inside = court.Contains(ball.Position);
        if (announcer != null)
        {
            if (inside && landSide != lastHitter && hitsInPoint == 1) announcer.Say(ShotAnnouncer.Call.Ace);   // serve nobody touched
            else if (!inside) announcer.Say(ShotAnnouncer.Call.Out);
        }
        Score(inside && landSide != lastHitter ? lastHitter : 1 - lastHitter);
    }

    void Score(int winner)
    {
        if (state != State.Rally) return;
        if (winner == 0) playerScore++; else aiScore++;
        hud.Banner = winner == 0 ? I18n.Tr("Point!") : I18n.Tr("CPU point");
        bool over = IsMatchOver(playerScore, aiScore);
        if (over)
        {
            hud.Banner = playerScore > aiScore ? I18n.Tr("YOU WIN!\nClick / tap for next level") : I18n.Tr("YOU LOSE\nClick / tap to retry");
            EnterState(State.MatchOver);
            PlayCrowd();
            return;
        }
        EnterState(State.PointOver);
    }

    void NextPoint()
    {
        int total = playerScore + aiScore;
        bool endgame = playerScore >= pointsToWin - 1 && aiScore >= pointsToWin - 1;
        server = endgame ? total % 2 : (total / servesPerTurn) % 2;
        ball.SetVisible(false);
        player.ResetPosition();
        ai.ResetPosition();
        PrepareServe();
    }
}
