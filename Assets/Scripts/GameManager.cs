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
    [Tooltip("Poki SDK events (gameplay start/stop and the ad break between matches). Optional.")]
    public PokiService poki;
    [Tooltip("Main menu (game mode button and selection window), shown while no match is on.")]
    public GameModeMenu menu;
    [Tooltip("Confetti burst played behind the win title (a ParticleSystem in front of the camera).")]
    public ParticleSystem confetti;
    [Tooltip("Settings window (points / sets to win, sound switches) and its gear button.")]
    public SettingsWindow settings;
    [Tooltip("Rackets window (and its button): the skin on the player's racket and the new racket screen.")]
    public RacketSelection rackets;
    [Tooltip("Seconds the scoreboard hangs down when a match starts, before it is pulled up to show only the clock.")]
    public float scoreboardRevealTime = 1.6f;
    [Header("Rally coin reward (win screen)")]
    [Tooltip("The rules and values of the reward of a won match (Assets/Data/RallyRewardConfig.asset).")]
    public RallyRewardConfig rewardConfig;
    public WinRewardPanel rewardPanel;

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
    [Tooltip("Points to win a set and sets to win the match come from the settings window (GameSettings); they are read when a match starts.")]
    public int winBy = 2;
    [Tooltip("Points each player serves in a row (1 once both are near the end).")]
    public int servesPerTurn = 2;
    public int startLevel = 1;
    [Tooltip("No-Ad: once both players are this many points past the points-to-win, the next point wins the set (0 = off).")]
    public int noAdExtra = 4;

    [Header("Timing (s)")]
    public float serveDelay = 1.0f;
    public float pointOverDelay = 1.6f;
    public float matchOverInputDelay = 0.6f;
    [Tooltip("A ball that stays in play this long after the last hit (e.g. stuck) is resolved as out.")]
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
    int playerSets, aiSets;
    int pointsToWin = 11, setsToWin = 1; // rules of the current match (from GameSettings)
    bool newSetPending;                  // a set just ended and the match goes on: the next point starts a new set
    int NoAdScore => noAdExtra > 0 ? pointsToWin + noAdExtra : 0;
    int server;
    int lastHitter;
    float matchTime; // seconds since the match started (stops when it ends)
    int infoLevel = -1;
    bool loseLabelsSet, winLabelsSet, namesSet, backLabelsSet;
    string startReason = "menu"; // why the next match starts (for the Poki events): menu, next_level, restart, retry
    int highestLevelSeen;
    float retryNoticeUntil; // "Retry!" confirmation after a rewarded ad
    string info = "";
    float scoreboardRevealUntil; // the board stays down until then (it drops when a match starts)
    MatchStats stats; // what happened in the match so far (for the reward)
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
    public bool InMenu => state == State.Menu;
    // The player is about to serve (the CameraRig backs off to show the area behind the baseline).
    public bool PlayerServing => state == State.Serving && server == 0;
    // A match is on: serving, rallying or pausing between points (not the menu or the result screens).
    public bool MatchOn => state == State.Serving || state == State.Rally || state == State.PointOver;
    public bool AiServing => state == State.Serving && server == 1;

    void Awake()
    {
        Application.targetFrameRate = targetFrameRate;
        if (poki == null) poki = FindFirstObjectByType<PokiService>();
        if (menu == null) menu = FindFirstObjectByType<GameModeMenu>();
        if (hud != null)
        {
            hud.RestartClicked -= OnRestartClicked; hud.RestartClicked += OnRestartClicked; // -= first: never subscribe twice
            hud.RetryClicked -= OnRetryClicked; hud.RetryClicked += OnRetryClicked;
            hud.ContinueClicked -= OnContinueClicked; hud.ContinueClicked += OnContinueClicked;
            hud.BackClicked -= OnBackClicked; hud.BackClicked += OnBackClicked;
        }
        ChannelMute.Attach(sfx, AudioChannel.Sfx);
        PokiEvents.Track("game", "loaded", "language", I18n.Code);
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
        if (state != State.Menu && state != State.MatchOver) matchTime += Time.deltaTime;
        // The racket does not follow the pointer when no match is on (menu, win screen, lose screen) nor while an ad plays: a touch there is for
        // the buttons, not for moving the racket.
        bool inputLocked = state == State.Menu || state == State.MatchOver;
        if (!PokiService.AdPlaying && !inputLocked) player.Tick(this);
        ai.Tick(this);

        switch (state)
        {
            case State.Menu:
                hud.Banner = ""; // no text: the TapHint hand shows what to do
                // A tap on the menu's own buttons is not a "play" tap; with the mode window open, a tap elsewhere only closes it.
                if (Pressed() && !PointerOverUi())
                {
                    if (menu != null && menu.WindowOpen) menu.CloseWindow();
                    else if (menu == null || menu.Mode == GameMode.Training) BeginMatch(); // Training is the only mode so far
                }
                break;
            case State.Serving:
                hud.Banner = Time.time < retryNoticeUntil ? I18n.Tr("Retry!") : ""; // no text about who serves or how: the ServeHint ghost teaches it
                if (server == 0) TryPlayerServe();
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
                // The win screen has its own buttons (CONTINUE, and Game mode); with the mode window open, a tap elsewhere only closes it.
                if (playerScore > aiScore && Pressed() && !PointerOverUi() && menu != null && menu.WindowOpen) menu.CloseWindow();
                break;
        }
        UpdateHud();
    }

    // Is the pointer (mouse or first finger) over a UI element (buttons, windows)?
    static bool PointerOverUi()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null) return false;
        return Input.touchCount > 0 ? es.IsPointerOverGameObject(Input.GetTouch(0).fingerId) : es.IsPointerOverGameObject();
    }

    // Back to the main menu (leaving a mode): the scene stays behind it.
    public void ReturnToMenu()
    {
        if (confetti != null) confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        playerScore = aiScore = 0; // the board starts from zero again: points, sets and the clock
        playerSets = aiSets = 0;
        newSetPending = false;
        matchTime = 0f;
        if (poki != null) poki.GameplayStop();
        ball.SetVisible(false);
        player.ResetPosition();
        ai.ResetPosition();
        cameraRig.SetServing(true);
        EnterState(State.Menu);
    }

    static bool Pressed()
    {
        if (PokiService.AdPlaying) return false;
        return Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
    }

    void UpdateHud()
    {
        if (!namesSet) { hud.SetNames(I18n.Tr("PLAYER"), "CPU"); namesSet = true; }
        // The board hangs out of sight in the menu, drops when a match starts (and for every point and result), and in between stays pulled up
        // with only the clock showing.
        bool revealing = state == State.PointOver || state == State.MatchOver || Time.unscaledTime < scoreboardRevealUntil;
        hud.SetScoreboard(state == State.Menu ? HangingSign.SignState.Hidden : revealing ? HangingSign.SignState.Shown : HangingSign.SignState.Peek);
        bool wonMatch = state == State.MatchOver && playerScore > aiScore;
        if (menu != null) menu.SetVisible(state == State.Menu, true); // the Game mode button is for the menu only (not on the result screens)
        if (state == State.MatchOver && !backLabelsSet) { hud.SetBackLabels(I18n.Tr("BACK")); backLabelsSet = true; }
        if (wonMatch && !winLabelsSet) { hud.SetWinLabels(I18n.Tr("YOU"), WonText(), I18n.Tr("CONTINUE")); winLabelsSet = true; }
        hud.SetWinTitle(wonMatch);
        hud.SetWinContinueInteractable(wonMatch && stateTimer > matchOverInputDelay && !PokiService.AdPlaying);
        bool lostMatch = state == State.MatchOver && aiScore > playerScore;
        hud.SetLoseTitle(lostMatch);
        hud.SetLoseButtons(lostMatch, lostMatch && stateTimer > matchOverInputDelay && !PokiService.AdPlaying);
        if (lostMatch && !loseLabelsSet)
        {
            hud.SetLoseTitleLabels(I18n.Tr("YOU"), I18n.Tr("LOSE"));
            hud.SetLoseLabels(I18n.Tr("RESTART"), I18n.Tr("RETRY"));
            loseLabelsSet = true;
        }
        hud.SetScore(playerScore, aiScore);
        hud.SetTime(matchTime);
        int infoKey = level * 1000 + playerSets * 100 + aiSets * 10 + setsToWin;
        if (infoKey != infoLevel)
        {
            infoLevel = infoKey;
            info = I18n.Tr("You  vs  CPU   |   Level {0}", level);
            if (setsToWin > 1) info += "   |   " + I18n.Tr("Sets {0} - {1}", playerSets, aiSets);
        }
        hud.Info = info;
        if (settings != null) settings.SetButtonVisible(state == State.Menu || state == State.MatchOver);
        if (rackets != null) rackets.SetButtonVisible((state == State.Menu || (state == State.MatchOver && !wonMatch)) && (settings == null || !settings.IsOpen));
    }

    static string WonText()
    {
        string won = I18n.Tr("WON");
        return won.EndsWith("!") ? won : won + "!";
    }

    // Poki: an ad break comes before every start of gameplay (it only plays when Poki decides it is time), then the match starts.
    void BeginMatch()
    {
        PokiEvents.Track("match", "tap_to_play", "level", level);
        startReason = "menu";
        if (poki != null) poki.CommercialBreak(StartMatch); else StartMatch();
    }

    bool CanUseContinue => state == State.MatchOver && playerScore > aiScore && stateTimer > matchOverInputDelay && !PokiService.AdPlaying;

    // CONTINUE (win screen): next level. Back into gameplay: the ad break (if any) comes here, then the next match starts.
    void OnContinueClicked()
    {
        if (!CanUseContinue) return;
        PokiEvents.Track("match", "continue", "level", level, "next_level", level + 1);
        startReason = "next_level";
        level++;
        if (poki != null) poki.CommercialBreak(StartMatch); else StartMatch();
    }

    // BACK (win and lose screens): to the main menu, where the game mode can be changed. The level reached is kept.
    void OnBackClicked()
    {
        if (state != State.MatchOver || stateTimer <= matchOverInputDelay || PokiService.AdPlaying) return;
        PokiEvents.Track("match", "back", "from", playerScore > aiScore ? "win" : "lose", "level", level);
        ReturnToMenu();
    }

    bool CanUseLoseButtons => state == State.MatchOver && aiScore > playerScore && stateTimer > matchOverInputDelay && !PokiService.AdPlaying;

    // RESTART (standard, always available): starts everything over from the first level.
    void OnRestartClicked()
    {
        if (!CanUseLoseButtons) return;
        PokiEvents.Track("match", "restart", "level", level);
        startReason = "restart";
        level = startLevel;
        if (poki != null) poki.CommercialBreak(StartMatch); else StartMatch();
    }

    // RETRY (rewarded ad): plays the same level again, but only when the player really watched the ad. No ad / ad blocker = nothing happens.
    void OnRetryClicked()
    {
        if (!CanUseLoseButtons) return;
        startReason = "retry";
        if (poki == null) { PokiEvents.Track("match", "retry", "level", level, "granted", true); retryNoticeUntil = Time.time + 2f; StartMatch(); return; }
        poki.RewardedBreak(granted =>
        {
            PokiEvents.Track("match", "retry", "level", level, "granted", granted);
            if (!granted || state != State.MatchOver) return;
            retryNoticeUntil = Time.time + 2f; // visible confirmation of the reward
            StartMatch();
        });
    }

    void StartMatch()
    {
        pointsToWin = GameSettings.PointsToWin;
        setsToWin = GameSettings.SetsToWin;
        PokiEvents.Track("match", "start", "level", level, "points_to_win", pointsToWin, "sets_to_win", setsToWin, "reason", startReason);
        RacketProgress.ReportTrainingLevel(level); // Training is the only mode so far; rackets that unlock by level
        if (level > highestLevelSeen) { highestLevelSeen = level; PokiEvents.Track("level", "reached", "level", level); }
        playerScore = aiScore = 0;
        playerSets = aiSets = 0;
        newSetPending = false;
        matchTime = 0f;
        scoreboardRevealUntil = Time.unscaledTime + scoreboardRevealTime;
        stats = new MatchStats { level = level };
        if (confetti != null) confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (poki != null) poki.GameplayStart(); // the player's first input of the match
        server = 0;
        ai.SetLevel(level);
        player.ResetPosition();
        ai.ResetPosition();
        PrepareServe();
    }

    // The Rally coins of a won match: worked out from what happened, added to the player's coins and listed on the win screen.
    void GiveReward()
    {
        stats.won = true;
        stats.playerSets = playerSets; stats.cpuSets = aiSets;
        stats.lastSetPlayerPoints = playerScore; stats.lastSetCpuPoints = aiScore;
        stats.duration = matchTime;
        if (rewardConfig == null) rewardConfig = ScriptableObject.CreateInstance<RallyRewardConfig>(); // the defaults
        var lines = rewardConfig.Calculate(stats, out int total);
        RallyCoins.Add(total);
        if (rewardPanel != null) rewardPanel.Show(lines, total);
        PokiEvents.Track("reward", "match", "coins", total, "bonuses", lines.Count - 1);
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

    // Would the next point win the set for this side (and with it the match, in the last set)?
    bool WinsMatchWithNextPoint(int side)
    {
        int a = (side == 0 ? playerScore : aiScore) + 1, b = side == 0 ? aiScore : playerScore;
        return IsMatchOver(a, b);
    }

    // Win at pointsToWin with a lead of winBy; once both reach noAdScore, the next point wins (no advantage).
    bool IsMatchOver(int a, int b)
    {
        if (NoAdScore > 0 && Mathf.Max(a, b) > NoAdScore) return true;
        return Mathf.Max(a, b) >= pointsToWin && Mathf.Abs(a - b) >= winBy;
    }

    bool NoAdPoint => NoAdScore > 0 && playerScore == NoAdScore && aiScore == NoAdScore;

    void AnnouncePointStakes()
    {
        if (announcer == null) return;
        bool matchPoint = WinsMatchWithNextPoint(0) || WinsMatchWithNextPoint(1);
        if (NoAdPoint) { announcer.Say(ShotAnnouncer.Call.NoAd); announcer.SayNext(ShotAnnouncer.Call.MatchPoint); }
        else if (matchPoint) announcer.Say(ShotAnnouncer.Call.MatchPoint);
    }

    // The point of the racket's path this frame (previous position -> current position) that is closest to the ball, on the ground plane.
    // Checking the whole path instead of only the final position means a very fast swipe cannot skip over the ball between two frames.
    Vector3 SweptCentre(Racket r, Vector3 ballPos)
    {
        Vector3 a = r.PreviousPosition, b = r.Position;
        float abx = b.x - a.x, abz = b.z - a.z, len2 = abx * abx + abz * abz;
        float t = len2 > 1e-8f ? Mathf.Clamp01(((ballPos.x - a.x) * abx + (ballPos.z - a.z) * abz) / len2) : 1f;
        return new Vector3(a.x + abx * t, b.y, a.z + abz * t);
    }

    bool InReach(Racket r)
    {
        Vector3 p = ball.Position;
        if (p.y > r.MaxHitY) return false; // balls above the vertical reach fly over
        Vector3 c = SweptCentre(r, p);
        float dx = p.x - c.x, dz = p.z - c.z;
        return dx * dx + dz * dz <= r.hitRadius * r.hitRadius;
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
        ctrl.Racket.HitCenter = SweptCentre(ctrl.Racket, contact); // where on its path the racket met the ball
        ball.Velocity = ctrl.ComputeShot(this, contact);
        lastHitter = side;
        hitsInPoint++;
        rallyTime = 0f; // the timeout counts from the last hit, not from the serve (a long rally is not a stuck ball)
        PlayHit(side, ball.Velocity.magnitude);
        if (announcer != null && state != State.Serving)
            announcer.OnHit(ball, court, ctrl.Racket, side == 0 ? ai.Racket : player.Racket, side, contact, incoming, ball.Velocity);
        if (state == State.Serving)
        {
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
        if (winner == 0) { playerScore++; stats.playerPoints++; } else { aiScore++; stats.cpuPoints++; }
        stats.longestRally = Mathf.Max(stats.longestRally, hitsInPoint);
        stats.maxDeficit = Mathf.Max(stats.maxDeficit, aiScore - playerScore);
        hud.Banner = winner == 0 ? I18n.Tr("Point!") : I18n.Tr("CPU point");
        Bleachers.Cheer(winner == 0 ? 0.9f : 0.4f); // the crowd reacts to every point (more to the player's)
        bool setOver = IsMatchOver(playerScore, aiScore);
        if (setOver)
        {
            if (playerScore > aiScore) playerSets++; else aiSets++;
            bool matchWon = playerScore > aiScore;
            if (matchWon && aiScore == 0) stats.perfectSets++;
            PokiEvents.Track("set", "end", "level", level, "set_number", playerSets + aiSets, "winner", matchWon ? "player" : "cpu",
                             "player_points", playerScore, "cpu_points", aiScore, "player_sets", playerSets, "cpu_sets", aiSets);
            if (playerSets >= setsToWin || aiSets >= setsToWin)
            {
                PokiEvents.Track("match", "end", "result", matchWon ? "win" : "lose", "level", level, "duration_s", Mathf.RoundToInt(matchTime),
                                 "player_sets", playerSets, "cpu_sets", aiSets, "player_points", playerScore, "cpu_points", aiScore);
                PokiEvents.Track("level", matchWon ? "complete" : "failed", "level", level);
                if (matchWon) RacketProgress.ReportTrainingLevel(level + 1); // the next level is reached: rackets may unlock (the new racket screen follows)
                if (matchWon) GiveReward();
                hud.Banner = ""; // the win and lose screens show their own big centred titles (UpdateHud)
                EnterState(State.MatchOver);
                if (playerScore > aiScore && confetti != null) { confetti.Clear(true); confetti.Play(true); } // confetti behind the win title
                if (poki != null) poki.GameplayStop();
                Bleachers.Cheer(matchWon ? 1f : 0.3f);
                PlayCrowd();
                return;
            }
            hud.Banner = playerScore > aiScore ? I18n.Tr("SET TO YOU") : I18n.Tr("SET TO CPU"); // the match goes on: a new set starts after the pause
            newSetPending = true;
        }
        EnterState(State.PointOver);
    }

    void NextPoint()
    {
        if (newSetPending) { newSetPending = false; playerScore = aiScore = 0; }
        int total = playerScore + aiScore;
        bool endgame = playerScore >= pointsToWin - 1 && aiScore >= pointsToWin - 1;
        server = ((endgame ? total % 2 : (total / servesPerTurn) % 2) + playerSets + aiSets) % 2; // each new set starts with the other side serving
        ball.SetVisible(false);
        player.ResetPosition();
        ai.ResetPosition();
        PrepareServe();
    }

#if UNITY_EDITOR
    // Gizmos: where the serve ball hangs for each side (the CPU's spot varies along the line).
    void OnDrawGizmos()
    {
        if (court == null) return;
        float y = serveBallHeight;
        float zNear = -(court.halfLength - serveBaselineOffset), zFar = court.halfLength - serveBaselineOffset;
        GizmoShapes.Cross(new Vector3(0f, y, zNear), 0.3f, GizmoShapes.Serve);
        Gizmos.color = GizmoShapes.Serve;
        Gizmos.DrawLine(new Vector3(-serveBallSpread, y, zFar), new Vector3(serveBallSpread, y, zFar));
    }
#endif
}
