using System.Collections.Generic;
using UnityEngine;

// Match flow and rules for the beach tennis PvE game. Training: stars to fill (one per point needed, more every level), no defeat, five trophies per level.
// Online: first to 11 points (win by 2), win or lose.
// Point rules: the hitter scores only when the ball lands inside the opponent's half; out, net or own-side landings lose the point.
public class GameManager : MonoBehaviour
{
    public enum State { Menu, Serving, Rally, PointOver, MatchOver }
    public enum NetRole { None, Host, Guest }

    [Header("Scene references")]
    public Court court;
    public Net net;
    public Ball ball;
    public PlayerRacketController player;
    public AiRacketController ai;
    public CameraRig cameraRig;
    public HudView hud;
    public ShotAnnouncer announcer;
    [Tooltip("Racket hit effects and the PERFECT / GREAT SHOT words. Made at run time when left empty.")]
    public ShotFx shotFx;
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
    [Header("Online (only in the Online scene)")]
    [Tooltip("This is the Online scene: no levels, no mode menu; OnlineMatch finds the opponent (a real player or the fake one) and starts the match.")]
    public bool onlineScene;
    [Tooltip("Seconds the scoreboard hangs down when a match starts, before it is pulled up to show only the clock.")]
    public float scoreboardRevealTime = 1.6f;
    [Header("Rally coin reward (win screen)")]
    [Tooltip("The rules and values of the reward of a won match (Assets/Data/RallyRewardConfig.asset).")]
    public RallyRewardConfig rewardConfig;
    public WinRewardPanel rewardPanel;
    [Header("Training: stars and trophies")]
    [Tooltip("The stars at the top of the screen during a Training match (one per point needed to win).")]
    public StarMeter stars;
    [Tooltip("The path of trophies on the win screen of a Training match.")]
    public TrophyPath trophies;
    [Tooltip("Trophies (matches) to win to finish level 1; every level asks for one more, up to Max Trophies; the level only goes up when the last one is won.")]
    public int firstLevelTrophies = 2;
    public int maxTrophies = 5;
    [Tooltip("Points to win at level 1; every level needs one more, up to the maximum.")]
    public int firstLevelPoints = 2;
    public int maxTrainingPoints = 11;
    [Tooltip("Played when the CPU wins a point in Training (a star is lost, or nothing happens when there is none).")]
    public AudioClip starLost;
    [Tooltip("Played when the player fills a star.")] public AudioClip starGained;
    [Range(0f, 1f)] public float starGainedVolume = 0.9f;
    [Range(0f, 1f)] public float starLostVolume = 0.9f;
    [Header("End of match")]
    [Tooltip("Seconds the winner's racket celebrates (somersault and hops, with the confetti) before the scoreboard goes and the win screen comes.")]
    public float winCelebrationTime = 2.6f;
    [Tooltip("Seconds the loser's racket takes to drop (and the winner's to cheer) before the lose screen comes.")]
    public float loseFallTime = 1.4f;

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
    public const int OnlinePoints = 11, OnlineSets = 1;
    int NoAdScore => noAdExtra > 0 ? pointsToWin + noAdExtra : 0;
    int server;
    int lastHitter;
    float matchTime; // seconds since the match started (stops when it ends)
    int trainingStage;     // trophies already won in the current level (0..TrophiesFor(level)-1)
    int winLevel, winStage; // the level and trophy of the match that was just won (what the win screen shows)
    int pointsPlayed;      // Training: points played in the match (the stars go up and down, so they cannot tell who serves)
    float resultDelay;     // seconds after the end of a match before its win / lose screen
    bool resultBuilt;      // the win screen's extras (reward panel, trophies) have been shown
    List<RewardLine> pendingLines; int pendingTotal; bool rewardDoubled;
    int infoLevel = -1;
    string shownNpcName = "", shownMyName = "";
    bool loseLabelsSet, winLabelsSet, namesSet, backLabelsSet;
    string startReason = "menu"; // why the next match starts (for the Poki events): menu, next_level, restart, retry
    int highestLevelSeen;
    float retryNoticeUntil; // "Retry!" confirmation after a rewarded ad
    string info = "";
    float scoreboardRevealUntil; // the board stays down until then (it drops when a match starts)
    RacketController opponent;     // who plays the far side right now (see Opponent)
    string opponentName = "CPU";
    string localName = "";
    int netSerial;                 // grows with every change of state (sent to the guest)
    int bannerKind;                // what the banner says, as a number for the guest (see NetSnapshot)
    MatchStats stats; // what happened in the match so far (for the reward)
    int hitsInPoint; // racket hits since the serve (1 = only the serve has been hit)
    float rallyTime;
    float accumulator;
    bool serveArmed;
    float servePrevDz = float.NaN; // racket-to-ball distance along z last frame (NaN = it was out of reach)

    // The ball is waiting for the server's racket (the next hit is the serve).
    public bool IsServing => state == State.Serving;
    public Ball Ball => ball;
    public Net Net => net;
    public AiRacketController Ai => ai;
    // Whoever plays the far side: the training CPU, the fake online opponent (both `ai`) or, online, the other player (a RemoteRacketController).
    public RacketController Opponent => opponent;
    public NetRole Role { get; private set; }
    public bool PlayerServingFor(int side) { return state == State.Serving && server == (side < 0 ? 0 : 1); } // (the far side is side +1)
    public State CurrentState => state;
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
    // The AI may swing at its serve (a short moment after the ball is set up).
    public bool AiServeReady => AiServing && stateTimer > serveDelay;
    public bool Online => onlineScene;

    void Awake()
    {
        Application.targetFrameRate = targetFrameRate;
        if (poki == null) poki = FindFirstObjectByType<PokiService>();
        if (menu == null) menu = FindFirstObjectByType<GameModeMenu>();
        opponent = ai;
        if (menu != null) { menu.ModeSelected -= OnModeSelected; menu.ModeSelected += OnModeSelected; }
        if (hud != null)
        {
            hud.RestartClicked -= OnRestartClicked; hud.RestartClicked += OnRestartClicked; // -= first: never subscribe twice
            hud.RetryClicked -= OnRetryClicked; hud.RetryClicked += OnRetryClicked;
            hud.ContinueClicked -= OnContinueClicked; hud.ContinueClicked += OnContinueClicked;
            if (rewardPanel != null) { rewardPanel.DoubleClicked -= OnDoubleClicked; rewardPanel.DoubleClicked += OnDoubleClicked; }
            hud.BackClicked -= OnBackClicked; hud.BackClicked += OnBackClicked;
        }
        // ?mode=online in the address opens the online mode at once (to try it with two browser tabs); only the first time, so BACK still comes here
        if (!onlineScene && !urlModeHandled)
        {
            urlModeHandled = true;
            if (I18n.UrlParameter("mode") == "online") { SceneFlow.OpenOnline(); return; }
        }
        ChannelMute.Attach(sfx, AudioChannel.Sfx);
        if (!loadedReported) { loadedReported = true; PokiEvents.Track("game", "loaded", "language", I18n.Code); } // once, not at every scene
        if (language != LanguageChoice.Auto) I18n.Current = (I18n.Language)((int)language - 1);
        if (shotFx == null) shotFx = hud != null && hud.winLine1 != null ? ShotFx.Create(gameObject, hud.winLine1.font, hud.winLine1.fontSharedMaterial) : ShotFx.Create(gameObject, null, null);
        if (announcer != null) announcer.LoadVoices(); // downloads only the voices of the chosen language
        level = startLevel;
        ai.SetLevel(level);
        ResetRackets();
        ball.SetVisible(false);
    }

    void Update()
    {
        if (Role == NetRole.Guest) { GuestUpdate(); return; }
        stateTimer += Time.deltaTime;
        if (state != State.Menu && state != State.MatchOver) matchTime += Time.deltaTime;
        // The racket does not follow the pointer when no match is on (menu, win screen, lose screen) nor while an ad plays: a touch there is for
        // the buttons, not for moving the racket.
        bool inputLocked = state == State.Menu || state == State.MatchOver;
        if (!PokiService.AdPlaying && !inputLocked) player.Tick(this);
        if (state != State.MatchOver) opponent.Tick(this); // after the match the rackets only celebrate or fall

        switch (state)
        {
            case State.Menu:
                hud.Banner = ""; // no text: the TapHint hand shows what to do
                // A tap on the menu's own buttons is not a "play" tap; with the mode window open, a tap elsewhere only closes it.
                if (!onlineScene && Pressed() && !PointerOverUi())
                {
                    if (menu != null && menu.WindowOpen) menu.CloseWindow();
                    else if (menu == null || menu.Mode == GameMode.Training) BeginMatch();
                }
                break;
            case State.Serving:
                hud.Banner = Time.time < retryNoticeUntil ? I18n.Tr("Retry!") : ""; // no text about who serves or how: the ServeHint ghost teaches it
                if (server == 0) TryHumanServe(player.Racket, 0, player);
                else if (Role == NetRole.Host) TryHumanServe(opponent.Racket, 1, opponent); // the other player serves with their own racket
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

    // Both rackets back on their resting places (after the celebration / fall poses too).
    void ResetRackets()
    {
        player.Racket.ClearPose();
        opponent.Racket.ClearPose();
        player.ResetPosition();
        opponent.ResetPosition();
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
        ResetRackets();
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
        string npcName = Online ? opponentName : "CPU";
        string myName = Online && localName.Length > 0 ? localName : I18n.Tr("PLAYER");
        if (!namesSet || npcName != shownNpcName || myName != shownMyName) { hud.SetNames(myName, npcName); shownNpcName = npcName; shownMyName = myName; namesSet = true; }
        bool training = !Online;
        bool matchEnded = state == State.MatchOver;
        bool resultShown = matchEnded && stateTimer >= resultDelay; // after the celebration / fall: the win or lose screen
        bool wonMatch = resultShown && playerScore > aiScore;
        bool lostMatch = resultShown && aiScore > playerScore;
        // Online, the board hangs out of sight in the menu, drops when a match starts (and for every point and result), and in between stays pulled
        // up with only the clock showing; it is pulled up for the lose screen. Training has no board: the stars show the score.
        bool revealing = state == State.PointOver || matchEnded || Time.unscaledTime < scoreboardRevealUntil;
        hud.SetScoreboard(state == State.Menu || training || lostMatch ? HangingSign.SignState.Hidden : revealing ? HangingSign.SignState.Shown : HangingSign.SignState.Peek);
        if (stars != null) stars.SetVisible(training && (MatchOn || (matchEnded && !resultShown)));
        if (menu != null) menu.SetVisible(state == State.Menu, true); // the Game mode button is for the menu only (not on the result screens)
        if (matchEnded && !backLabelsSet) { hud.SetBackLabels(I18n.Tr("BACK")); backLabelsSet = true; }
        if (wonMatch && !winLabelsSet) { hud.SetWinLabels(I18n.Tr("YOU"), WonText(), Online ? I18n.Tr("PLAY AGAIN") : I18n.Tr("CONTINUE")); winLabelsSet = true; }
        hud.SetWinTitle(wonMatch);
        if (wonMatch && !resultBuilt) ShowWinExtras();
        bool inputOpen = resultShown && stateTimer > resultDelay + matchOverInputDelay && !PokiService.AdPlaying;
        hud.SetWinContinueInteractable(wonMatch && inputOpen);
        if (rewardPanel != null) rewardPanel.SetDoubleInteractable(wonMatch && inputOpen);
        hud.SetLoseTitle(lostMatch);
        hud.SetRetryVisible(false); // the lose screen is only used online: PLAY AGAIN and BACK
        hud.SetLoseButtons(lostMatch, lostMatch && inputOpen);
        if (lostMatch && !loseLabelsSet)
        {
            hud.SetLoseTitleLabels(I18n.Tr("YOU"), I18n.Tr("LOSE"));
            hud.SetLoseLabels(Online ? I18n.Tr("PLAY AGAIN") : I18n.Tr("RESTART"), I18n.Tr("RETRY"));
            loseLabelsSet = true;
        }
        hud.SetScore(playerScore, aiScore);
        hud.SetTime(matchTime);
        int infoKey = level * 1000 + playerSets * 100 + aiSets * 10 + setsToWin + (Online ? 100000 + opponentName.GetHashCode() % 50000 : 0);
        if (infoKey != infoLevel)
        {
            infoLevel = infoKey;
            info = Online ? I18n.Tr("You  vs  {0}", opponentName) : I18n.Tr("You  vs  CPU   |   Level {0}", level);
            if (setsToWin > 1) info += "   |   " + I18n.Tr("Sets {0} - {1}", playerSets, aiSets);
        }
        hud.Info = info;
        if (settings != null) settings.SetButtonVisible(!onlineScene && (state == State.Menu || resultShown));
        if (rackets != null) rackets.SetButtonVisible((state == State.Menu || lostMatch) && (settings == null || !settings.IsOpen) && !onlineScene);
    }

    // The win screen proper (after the celebration): the reward panel counting up and, in Training, the path of trophies.
    void ShowWinExtras()
    {
        resultBuilt = true;
        if (rewardPanel != null && pendingLines != null) rewardPanel.Show(pendingLines, pendingTotal);
        if (!Online && trophies != null) { trophies.Won = () => { if (sfx != null && starGained != null) Play(starGained, starGainedVolume, 1f); }; trophies.Show(winLevel, winStage, TrophiesFor(winLevel)); }
    }

    // "+N BONUS" (rewarded ad): the coins of the match again, but only when the player really watched the ad.
    void OnDoubleClicked()
    {
        if (!CanUseContinue || rewardDoubled || pendingTotal <= 0) return;
        System.Action<bool> done = granted =>
        {
            PokiEvents.Track("reward", "double", "coins", pendingTotal, "granted", granted);
            if (!granted || state != State.MatchOver || rewardDoubled) return;
            rewardDoubled = true;
            RallyCoins.Add(pendingTotal);
            if (rewardPanel != null) rewardPanel.Double();
            pendingTotal *= 2;
        };
        if (poki == null) done(true); else poki.RewardedBreak(done);
    }

    // ---------- online ----------

    // The ONLINE VS button of the mode window: the Online scene has its own match flow (and its own objects), so it is a scene of its own.
    void OnModeSelected(GameMode mode)
    {
        if (mode != GameMode.Online || state != State.Menu || onlineScene) return;
        PokiEvents.Track("online", "enter");
        SceneFlow.OpenOnline();
    }

    // What the player chose on the result screens of the Online scene: true = look for another opponent, false = back to the menu.
    public event System.Action<bool> OnlineResult;

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

    bool CanUseContinue => state == State.MatchOver && playerScore > aiScore && stateTimer > resultDelay + matchOverInputDelay && !PokiService.AdPlaying;

    // CONTINUE (win screen): next level. Back into gameplay: the ad break (if any) comes here, then the next match starts.
    void OnContinueClicked()
    {
        if (!CanUseContinue) return;
        if (Online) { OnlineResult?.Invoke(true); return; } // there are no levels online: CONTINUE looks for the next opponent
        PokiEvents.Track("match", "continue", "level", level, "stage", trainingStage);
        startReason = "next_level"; // (the level and trophy already advanced when the match was won)
        if (poki != null) poki.CommercialBreak(StartMatch); else StartMatch();
    }

    // BACK (win and lose screens): to the main menu, where the game mode can be changed. The level reached is kept.
    void OnBackClicked()
    {
        if (state != State.MatchOver || stateTimer <= resultDelay + matchOverInputDelay || PokiService.AdPlaying) return;
        PokiEvents.Track("match", "back", "from", playerScore > aiScore ? "win" : "lose", "level", level);
        if (Online) { OnlineResult?.Invoke(false); return; }
        ReturnToMenu();
    }

    bool CanUseLoseButtons => state == State.MatchOver && aiScore > playerScore && stateTimer > resultDelay + matchOverInputDelay && !PokiService.AdPlaying;

    // RESTART (standard, always available): starts everything over from the first level.
    void OnRestartClicked()
    {
        if (!CanUseLoseButtons) return;
        if (Online) { OnlineResult?.Invoke(true); return; } // online, RESTART looks for another opponent
        PokiEvents.Track("match", "restart", "level", level);
        startReason = "restart";
        level = startLevel; trainingStage = 0;
        if (poki != null) poki.CommercialBreak(StartMatch); else StartMatch();
    }

    // RETRY (rewarded ad): plays the same level again, but only when the player really watched the ad. No ad / ad blocker = nothing happens.
    void OnRetryClicked()
    {
        if (!CanUseLoseButtons || Online) return; // (no rewarded rematch online)
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
        // online: 11 points, one set; training: one star per point, and the number grows with the level (the settings window does not matter)
        pointsToWin = Online ? OnlinePoints : TrainingPoints(level);
        setsToWin = Online ? OnlineSets : 1;
        PokiEvents.Track("match", "start", "level", level, "points_to_win", pointsToWin, "sets_to_win", setsToWin, "reason", startReason);
        if (!Online) RacketProgress.ReportTrainingLevel(level); // rackets that unlock by Training level
        if (!Online && level > highestLevelSeen) { highestLevelSeen = level; PokiEvents.Track("level", "reached", "level", level); }
        playerScore = aiScore = 0;
        playerSets = aiSets = 0;
        newSetPending = false;
        matchTime = 0f;
        pointsPlayed = 0;
        resultDelay = 0f; resultBuilt = false; rewardDoubled = false; pendingLines = null; pendingTotal = 0;
        if (rewardPanel != null) rewardPanel.Hide();
        if (trophies != null) trophies.Hide();
        if (stars != null) { if (Online) stars.SetVisible(false); else { stars.Setup(pointsToWin); stars.SetVisible(true); } }
        scoreboardRevealUntil = Time.unscaledTime + scoreboardRevealTime;
        stats = new MatchStats { level = Online ? 1 : level };
        if (shotFx != null) shotFx.Clear();
        if (confetti != null) confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (poki != null) poki.GameplayStart(); // the player's first input of the match
        server = 0;
        bannerKind = 0;
        if (opponent == ai) ai.SetLevel(level);
        ResetRackets();
        PrepareServe();
    }

    public int TrophiesFor(int forLevel) { return Mathf.Clamp(firstLevelTrophies + Mathf.Max(0, forLevel - 1), 1, Mathf.Max(1, maxTrophies)); }
    public int TrainingPoints(int forLevel) { return Mathf.Min(maxTrainingPoints, firstLevelPoints + Mathf.Max(0, forLevel - 1)); }

    // The Rally coins of a won match: worked out from what happened, added to the player's coins and listed on the win screen.
    void GiveReward()
    {
        stats.won = true;
        stats.playerSets = playerSets; stats.cpuSets = aiSets;
        stats.lastSetPlayerPoints = Online ? playerScore : stats.playerPoints; stats.lastSetCpuPoints = Online ? aiScore : stats.cpuPoints;
        stats.duration = matchTime;
        if (rewardConfig == null) rewardConfig = ScriptableObject.CreateInstance<RallyRewardConfig>(); // the defaults
        var lines = rewardConfig.Calculate(stats, out int total);
        RallyCoins.Add(total);
        pendingLines = lines; pendingTotal = total; // shown with the win screen, after the celebration
        PokiEvents.Track("reward", "match", "coins", total, "bonuses", lines.Count - 1);
    }

    void EnterState(State s)
    {
        state = s;
        stateTimer = 0f;
        netSerial++;
    }

    // ---------- ball ----------
    // The serve ball hangs in the air until the server's racket hits it.
    void PrepareServe()
    {
        float z = (server == 0 ? -1f : 1f) * (court.halfLength - serveBaselineOffset);
        serveArmed = false;
        servePrevDz = float.NaN;
        float serveX = server == 0 ? player.Racket.Position.x : Role == NetRole.Host ? opponent.Racket.Position.x : Random.Range(-serveBallSpread, serveBallSpread);
        ball.Place(new Vector3(serveX,
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
        if (!Online) return a >= pointsToWin; // Training: the stars are full
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
    void TryHumanServe(Racket r, int side, RacketController who)
    {
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
            Hit(side, who);
            return;
        }
        servePrevDz = inReach ? dz : float.NaN;
    }

    void TryAiServe()
    {
        if (InReach(opponent.Racket)) Hit(1, opponent);
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
        if (state == State.Rally && lastHitter != 1 && ball.Velocity.z > 0f) CheckRacket(1, opponent);

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
        var quality = ShotQuality.Normal;
        if (shotFx != null)
        {
            quality = state == State.Serving ? ShotQuality.Normal : shotFx.Evaluate(ball, court, contact, ball.Velocity, ctrl as PlayerRacketController);
            float speed = ball.Velocity.magnitude;
            shotFx.Hit(contact, quality, speed, ball.Velocity, side == 0);
            ctrl.Racket.Punch(shotFx.Power(speed));
            if (side == 0)
            {
                if (quality == ShotQuality.Perfect) stats.perfectShots++;
                else if (quality == ShotQuality.VeryGood) stats.greatShots++;
            }
        }
        if (announcer != null && state != State.Serving)
            announcer.OnHit(ball, court, ctrl.Racket, side == 0 ? opponent.Racket : player.Racket, side, contact, incoming, ball.Velocity, quality);
        if (state == State.Serving)
        {
            accumulator = 0f;
            cameraRig.SetServing(false);
            EnterState(State.Rally);
        }
        if (side == 0) opponent.OnOpponentHit(this);
        NetMessage?.Invoke(NetProtocol.EventHit(side, ball.Velocity.magnitude)); // the other player hears it too
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
        if (!Online) { ScoreTraining(winner); return; }
        if (winner == 0) { playerScore++; stats.playerPoints++; } else { aiScore++; stats.cpuPoints++; }
        stats.longestRally = Mathf.Max(stats.longestRally, hitsInPoint);
        stats.maxDeficit = Mathf.Max(stats.maxDeficit, aiScore - playerScore);
        bannerKind = winner == 0 ? 1 : 2;
        hud.Banner = BannerText(bannerKind);
        float cheer = winner == 0 ? 0.9f : 0.4f;
        Bleachers.Cheer(cheer); // the crowd reacts to every point (more to the player's)
        NetMessage?.Invoke(NetProtocol.EventCheer(cheer));
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
                if (matchWon && !Online) RacketProgress.ReportTrainingLevel(level + 1); // the next level is reached: rackets may unlock (the new racket screen follows)
                bannerKind = 0;
                EnterState(State.MatchOver);
                AfterMatchOver(matchWon, true);
                return;
            }
            bannerKind = playerScore > aiScore ? 3 : 4;
            hud.Banner = BannerText(bannerKind); // the match goes on: a new set starts after the pause
            newSetPending = true;
        }
        EnterState(State.PointOver);
    }

    // Training: a point for the player fills a star, a point for the CPU empties one (nothing happens when none is filled; there is no defeat).
    // The match is won when every star is full.
    void ScoreTraining(int winner)
    {
        bool hadStar = playerScore > 0;
        pointsPlayed++;
        if (winner == 0) { playerScore++; stats.playerPoints++; }
        else { stats.cpuPoints++; if (hadStar) playerScore--; }
        stats.longestRally = Mathf.Max(stats.longestRally, hitsInPoint);
        stats.maxDeficit = Mathf.Max(stats.maxDeficit, stats.cpuPoints - stats.playerPoints);
        bannerKind = winner == 0 ? 1 : 2;
        hud.Banner = BannerText(bannerKind);
        Bleachers.Cheer(winner == 0 ? 0.9f : 0.4f);
        if (stars != null) { if (winner == 0) stars.Gain(playerScore); else stars.Lose(playerScore, hadStar); }
        if (winner == 0 && sfx != null && starGained != null) Play(starGained, starGainedVolume, 1f);
        if (winner != 0 && sfx != null && starLost != null) Play(starLost, starLostVolume, 1f);
        if (playerScore < pointsToWin) { EnterState(State.PointOver); return; }

        // every star is full: a trophy is won, and the level goes up with the last one of the level
        winLevel = level; winStage = trainingStage;
        bool levelDone = trainingStage + 1 >= TrophiesFor(level);
        PokiEvents.Track("match", "end", "result", "win", "level", level, "stage", trainingStage, "duration_s", Mathf.RoundToInt(matchTime),
                         "player_points", stats.playerPoints, "cpu_points", stats.cpuPoints);
        PokiEvents.Track("level", levelDone ? "complete" : "stage_complete", "level", level, "stage", trainingStage);
        if (levelDone) { RacketProgress.ReportTrainingLevel(level + 1); trainingStage = 0; level++; } else trainingStage++;
        playerSets = 1;
        bannerKind = 0;
        EnterState(State.MatchOver);
        AfterMatchOver(true, true);
    }

    // The banner of a point or a set, from its number (see NetSnapshot).
    string BannerText(int kind)
    {
        switch (kind)
        {
            case 1: return I18n.Tr("Point!");
            case 2: return Online ? I18n.Tr("Opponent point") : I18n.Tr("CPU point");
            case 3: return I18n.Tr("SET TO YOU");
            case 4: return Online ? I18n.Tr("SET TO OPPONENT") : I18n.Tr("SET TO CPU");
            default: return "";
        }
    }

    // The confetti explodes from the centre of what the camera sees and then floats down; the speeds follow the visible height so it stays on screen.
    void FitConfetti()
    {
        var cam = confetti.transform.parent != null ? confetti.transform.parent.GetComponent<Camera>() : Camera.main;
        if (cam == null) return;
        var t = confetti.transform;
        float dist = Mathf.Max(1f, t.localPosition.z);
        float halfH = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), halfW = halfH * cam.aspect;
        float k = Mathf.Min(halfH, halfW * 1.4f) / 3.3f; // 1 for the landscape view; smaller on a narrow screen
        t.localRotation = Quaternion.identity;
        t.localPosition = new Vector3(0f, halfH * 0.1f, dist);
        var main = confetti.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f * k, 9f * k);
        main.startSize = new ParticleSystem.MinMaxCurve(0.09f * k, 0.16f * k);
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5f);
        main.gravityModifier = 0.6f * k;
        var em = confetti.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 110), new ParticleSystem.Burst(0.25f, 50) });
        var shape = confetti.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f * k; shape.scale = Vector3.one;
        shape.rotation = Vector3.zero; shape.position = Vector3.zero;
        var lim = confetti.limitVelocityOverLifetime;
        lim.enabled = true; lim.limit = 1.3f * k; lim.dampen = 0.1f;
    }

    // What happens when a match ends: the reward of a win, the confetti behind the win title, the end of the Poki gameplay, the crowd.
    void AfterMatchOver(bool matchWon, bool reward)
    {
        if (matchWon && reward) GiveReward();
        if (matchWon && reward && Online) RacketProgress.ReportOnlineWin(); // (a win because the other player left does not count)
        hud.Banner = ""; // the win and lose screens show their own big centred titles (UpdateHud)
        // the winner's racket celebrates (somersault, hops) and the loser's drops; the screen comes after
        resultDelay = matchWon ? winCelebrationTime : loseFallTime;
        resultBuilt = false;
        (matchWon ? player : opponent).Racket.Celebrate();
        (matchWon ? opponent : player).Racket.Fall();
        if (matchWon && confetti != null) { FitConfetti(); confetti.Clear(true); confetti.Play(true); } // confetti behind the win title
        if (poki != null) poki.GameplayStop();
        Bleachers.Cheer(matchWon ? 1f : 0.3f);
        PlayCrowd();
    }

    void NextPoint()
    {
        if (newSetPending) { newSetPending = false; playerScore = aiScore = 0; }
        int total = Online ? playerScore + aiScore : pointsPlayed;
        bool endgame = Online && playerScore >= pointsToWin - 1 && aiScore >= pointsToWin - 1;
        server = ((endgame ? total % 2 : (total / servesPerTurn) % 2) + playerSets + aiSets) % 2; // each new set starts with the other side serving
        ball.SetVisible(false);
        ResetRackets();
        PrepareServe();
    }

    // ---------- online: the two players ----------
    // The HOST (the player who made the lobby) simulates everything exactly as in a training match, with the other player's racket where the CPU's
    // would be. The GUEST runs no rules at all: it draws what the host says (GuestApply) and moves its own racket. OnlineMatch sends the messages.

    // Messages for the guest (hits and crowd cheers); the state itself is sent by OnlineMatch from ExportSnapshot.
    public event System.Action<string> NetMessage;

    bool peerGone;
    static bool loadedReported, urlModeHandled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { loadedReported = false; urlModeHandled = false; } // static state survives a Play without domain reload

    // Online match with `other` as the far-side racket (a RemoteRacketController for a real player, `ai` for the fake opponent).
    public void BeginOnline(NetRole role, RacketController other, string myName, string theirName)
    {
        Role = role;
        opponent = other;
        localName = myName; opponentName = theirName;
        namesSet = false;
        peerGone = false;
    }

    // Host or fake: the match starts now (with the host's settings).
    public void StartOnlineMatch()
    {
        startReason = "online";
        StartMatch();
    }

    // Guest: the rules come from the host; the first state arrives with the first snapshot.
    public void StartGuestMatch(int points, int sets)
    {
        pointsToWin = OnlinePoints; setsToWin = OnlineSets; // fixed online rules (the host sends the same values)
        playerScore = aiScore = 0; playerSets = aiSets = 0;
        matchTime = 0f; peerGone = false;
        resultDelay = 0f; resultBuilt = false; rewardDoubled = false; pendingLines = null; pendingTotal = 0;
        if (rewardPanel != null) rewardPanel.Hide();
        stats = new MatchStats { level = 1 };
        scoreboardRevealUntil = Time.unscaledTime + scoreboardRevealTime;
        if (confetti != null) confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (poki != null) poki.GameplayStart();
        ResetRackets();
        ball.SetVisible(false);
        PokiEvents.Track("match", "start", "level", 1, "points_to_win", points, "sets_to_win", sets, "reason", "online_guest");
    }

    public NetSnapshot ExportSnapshot()
    {
        return new NetSnapshot
        {
            serial = netSerial, state = (int)state, server = server,
            scoreA = playerScore, scoreB = aiScore, setsA = playerSets, setsB = aiSets,
            banner = bannerKind, time = matchTime, ballVisible = ball.gameObject.activeSelf,
            ballPos = ball.Position, ballVel = ball.Velocity,
            racketA = new Vector2(player.Racket.Position.x, player.Racket.Position.z),
            racketB = new Vector2(opponent.Racket.Position.x, opponent.Racket.Position.z),
        };
    }

    // Guest: the state of the match as the host sent it, already turned round (NetProtocol.ForGuest). `latency` is the one-way delay in seconds.
    public void GuestApply(NetSnapshot v, float latency)
    {
        if (peerGone || Role != NetRole.Guest) return;
        bool changed = v.serial != netSerial || v.state != (int)state;
        playerScore = v.scoreA; aiScore = v.scoreB; playerSets = v.setsA; aiSets = v.setsB;
        server = v.server;
        if (Mathf.Abs(matchTime - v.time) > 0.5f) matchTime = v.time;
        if (changed) { GuestEnter((State)v.state, v); netSerial = v.serial; }
        if (opponent is RemoteRacketController remote) remote.SetTarget(v.racketB);

        if (v.ballVisible)
        {
            if (!ball.gameObject.activeSelf) ball.SetVisible(true);
            ball.SetState(v.ballPos, v.state == (int)State.Serving ? Vector3.zero : v.ballVel);
            if (v.state != (int)State.Serving) AdvanceBall(latency); // it has travelled on while the message was on its way
            ball.Sync(court.groundY);
        }
        else if (ball.gameObject.activeSelf) ball.SetVisible(false);
    }

    void AdvanceBall(float seconds)
    {
        int steps = Mathf.Clamp(Mathf.RoundToInt(seconds / physicsStep), 0, 120);
        for (int i = 0; i < steps; i++) ball.Step(physicsStep * ballSpeed, court, net, out Vector3 prev);
    }

    // A change of state on the host: what the guest does about it.
    void GuestEnter(State st, NetSnapshot v)
    {
        EnterState(st);
        switch (st)
        {
            case State.Serving:
                ball.SetVisible(true);
                lastHitter = server;
                cameraRig.SetServing(true);
                hitsInPoint = 0;
                ResetRackets();
                hud.Banner = "";
                break;
            case State.Rally:
                cameraRig.SetServing(false);
                hud.Banner = "";
                break;
            case State.PointOver:
                bannerKind = v.banner;
                hud.Banner = BannerText(v.banner);
                break;
            case State.MatchOver:
                bannerKind = 0;
                AfterMatchOver(v.scoreA > v.scoreB, true);
                break;
        }
    }

    // The frame of a guest: its own racket, the other racket (moved by GuestApply's target) and the ball flying on its own between two states.
    void GuestUpdate()
    {
        float dt = Time.deltaTime;
        stateTimer += dt;
        if (state != State.Menu && state != State.MatchOver) matchTime += dt;
        bool inputLocked = state == State.Menu || state == State.MatchOver;
        if (!PokiService.AdPlaying && !inputLocked) player.Tick(this);
        if (state != State.MatchOver) opponent.Tick(this);
        if (state == State.Rally || state == State.PointOver) Simulate(false); else ball.Sync(court.groundY);
        UpdateHud();
    }

    // Guest: a hit or a cheer that the host reports (side 0 is the host, so 1 - side for this player).
    public void GuestEvent(string[] f)
    {
        if (!NetProtocol.ParseEvent(f, out char kind, out float a, out float b)) return;
        if (kind == 'H') PlayHit(1 - (int)a, b);
        else if (kind == 'C') Bleachers.Cheer(a);
    }

    // The other player left (or stopped answering) before the match was over: the match is won, without a reward.
    public void OpponentLeft()
    {
        if (peerGone || state == State.MatchOver || state == State.Menu) return;
        peerGone = true;
        PokiEvents.Track("online", "opponent_left", "state", state.ToString());
        playerScore = Mathf.Max(playerScore, aiScore + 1);
        playerSets = Mathf.Max(playerSets, setsToWin);
        bannerKind = 0;
        EnterState(State.MatchOver);
        AfterMatchOver(true, false);
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
