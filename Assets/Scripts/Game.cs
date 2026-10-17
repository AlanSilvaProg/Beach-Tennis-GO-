using UnityEngine;

// Single-player (PvE) match: first to 11 (win by 2) against an AI that gets tougher every level.
public class Game : MonoBehaviour
{
    enum State { Menu, Serving, Rally, PointOver, MatchOver }

    [Header("Assets")]
    public GameObject racketPrefab;
    public Material tableMat, sandMat, ballMat, netMat, shadowMat;

    const float PlayerZ = -1.7f;
    const float AiZ = 1.7f;
    const float MinPaddleY = 0.86f;
    const float MaxPaddleY = 1.75f;
    const int PointsToWin = 11;
    const float Step = 1f / 240f;

    State state = State.Menu;
    float stateTimer;
    int level = 1;
    int playerScore, aiScore;
    int server; // 0 player, 1 AI
    string banner = "";

    // Ball
    Transform ball, shadow;
    Vector3 ballPos, ballVel;
    float accumulator;
    int lastHitter;
    int bouncesSinceHit;
    bool awaitingServeBounce;
    float rallyTime;

    // Paddles
    Paddle playerPaddle, aiPaddle;
    Camera cam;
    Vector3 aiTarget;
    float aiReactTimer;
    float aiSpeed, aiError, aiMistake, aiReaction, aiFlight;

    void Awake()
    {
        Application.targetFrameRate = 60;
        BuildWorld();
        SetupLevel();
        ResetPaddles();
        HideBall();
    }

    // ---------- world ----------
    void BuildWorld()
    {
        cam = Camera.main;
        if (cam == null) cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.transform.position = new Vector3(0f, 1.55f, -2.85f);
        cam.transform.LookAt(new Vector3(0f, 0.7f, 0.4f));
        cam.fieldOfView = 55f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.82f, 0.95f);
        cam.allowHDR = false;
        cam.allowMSAA = false;

        Block("Sand", new Vector3(0f, -0.025f, 0f), new Vector3(30f, 0.05f, 30f), sandMat);
        Block("TableTop", new Vector3(0f, BallPhysics.TableY - 0.02f, 0f), new Vector3(1.525f, 0.04f, 2.74f), tableMat);
        Block("Net", new Vector3(0f, BallPhysics.TableY + BallPhysics.NetHeight * 0.5f, 0f),
              new Vector3(1.83f, BallPhysics.NetHeight, 0.01f), netMat);
        foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                Block("Leg", new Vector3(sx * 0.7f, (BallPhysics.TableY - 0.04f) * 0.5f, sz * 1.25f),
                      new Vector3(0.06f, BallPhysics.TableY - 0.04f, 0.06f), tableMat);

        var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        b.name = "Ball";
        Destroy(b.GetComponent<Collider>());
        b.transform.localScale = Vector3.one * BallPhysics.Radius * 2f;
        b.GetComponent<Renderer>().sharedMaterial = ballMat;
        ball = b.transform;

        var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        s.name = "BallShadow";
        Destroy(s.GetComponent<Collider>());
        s.transform.localScale = new Vector3(0.09f, 0.001f, 0.09f);
        s.GetComponent<Renderer>().sharedMaterial = shadowMat;
        shadow = s.transform;

        playerPaddle = Paddle.Create(racketPrefab, "PlayerRacket", true, Color.white);
        aiPaddle = Paddle.Create(racketPrefab, "AIRacket", false, Color.white);
    }

    static void Block(string name, Vector3 pos, Vector3 size, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        Destroy(g.GetComponent<Collider>());
        g.transform.position = pos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = mat;
    }

    void ResetPaddles()
    {
        playerPaddle.Move(new Vector3(0f, 1.1f, PlayerZ));
        aiPaddle.Move(new Vector3(0f, 1.1f, AiZ));
        aiTarget = aiPaddle.transform.position;
    }

    void HideBall()
    {
        ball.gameObject.SetActive(false);
        shadow.gameObject.SetActive(false);
    }

    // ---------- difficulty ----------
    void SetupLevel()
    {
        int l = Mathf.Clamp(level, 1, 12);
        aiSpeed = 1.5f + 0.38f * l;          // m/s the AI racket can travel
        aiError = Mathf.Max(0.02f, 0.30f - 0.022f * l);
        aiMistake = Mathf.Max(0.02f, 0.20f - 0.016f * l);
        aiReaction = Mathf.Max(0.03f, 0.30f - 0.025f * l);
        aiFlight = Mathf.Max(0.42f, 0.74f - 0.025f * l);
    }

    // ---------- loop ----------
    void Update()
    {
        stateTimer += Time.deltaTime;
        UpdatePlayerPaddle();
        UpdateAiPaddle();

        switch (state)
        {
            case State.Menu:
                banner = "BEACH TENNIS GO!\nClick / tap to play";
                if (Pressed()) StartMatch();
                break;
            case State.Serving:
                banner = server == 0 ? "Your serve" : "CPU serve";
                if (stateTimer > 1.0f) LaunchServe();
                break;
            case State.Rally:
                banner = "";
                Simulate();
                break;
            case State.PointOver:
                Simulate();
                if (stateTimer > 1.6f) NextPoint();
                break;
            case State.MatchOver:
                if (stateTimer > 0.6f && Pressed())
                {
                    if (playerScore > aiScore) level++;
                    StartMatch();
                }
                break;
        }
    }

    static bool Pressed()
    {
        return Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
    }

    void StartMatch()
    {
        playerScore = aiScore = 0;
        server = 0;
        SetupLevel();
        ResetPaddles();
        EnterState(State.Serving);
    }

    void EnterState(State s)
    {
        state = s;
        stateTimer = 0f;
    }

    // ---------- paddles ----------
    void UpdatePlayerPaddle()
    {
        Vector3 pointer = Input.mousePosition;
        if (Input.touchCount > 0) pointer = Input.GetTouch(0).position;
        Ray ray = cam.ScreenPointToRay(pointer);
        if (Mathf.Abs(ray.direction.z) < 0.0001f) return;
        float t = (PlayerZ - ray.origin.z) / ray.direction.z;
        Vector3 p = ray.origin + ray.direction * t;
        p.x = Mathf.Clamp(p.x, -1.45f, 1.45f);
        p.y = Mathf.Clamp(p.y, MinPaddleY, MaxPaddleY);
        p.z = PlayerZ;
        playerPaddle.Move(p);
    }

    void UpdateAiPaddle()
    {
        Vector3 pos = aiPaddle.transform.position;
        Vector3 goal = new Vector3(0f, 1.1f, AiZ);
        bool chasing = state == State.Rally && lastHitter == 0;
        if (chasing)
        {
            aiReactTimer -= Time.deltaTime;
            if (aiReactTimer <= 0f) goal = aiTarget;
        }
        Vector3 next = Vector3.MoveTowards(pos, goal, aiSpeed * Time.deltaTime);
        next.z = AiZ;
        aiPaddle.Move(next);
    }

    // Called when the player has hit: work out where the AI should go.
    void PlanAiIntercept()
    {
        aiReactTimer = aiReaction;
        Vector3 hit;
        if (BallPhysics.Predict(ballPos, ballVel, AiZ, out hit))
        {
            float err = aiError;
            if (Random.value < aiMistake) err += 0.45f;
            float sx = Random.value < 0.5f ? -1f : 1f;
            aiTarget = new Vector3(
                Mathf.Clamp(hit.x + sx * Random.Range(0f, err), -1.45f, 1.45f),
                Mathf.Clamp(hit.y + Random.Range(-err, err) * 0.5f, MinPaddleY, MaxPaddleY),
                AiZ);
        }
        else aiTarget = new Vector3(0f, 1.1f, AiZ);
    }

    // ---------- ball ----------
    void LaunchServe()
    {
        int dir = server == 0 ? 1 : -1;
        ballPos = new Vector3(Random.Range(-0.25f, 0.25f), 1.25f, -dir * 1.35f);
        ballVel = new Vector3(Random.Range(-0.2f, 0.2f), 0f, dir * 2.6f);
        lastHitter = server;
        bouncesSinceHit = 0;
        awaitingServeBounce = true;
        rallyTime = 0f;
        accumulator = 0f;
        ball.gameObject.SetActive(true);
        shadow.gameObject.SetActive(true);
        EnterState(State.Rally);
        if (server == 0) { PlanAiIntercept(); aiReactTimer = 0.45f; }
    }

    void Simulate()
    {
        accumulator = Mathf.Min(accumulator + Time.deltaTime, 0.1f);
        while (accumulator >= Step)
        {
            accumulator -= Step;
            if (state == State.PointOver) { StepBall(false); }
            else StepBall(true);
        }
        ball.position = ballPos;
        float shY = BallPhysics.OnTable(ballPos) && ballPos.y > BallPhysics.TableY ? BallPhysics.TableY + 0.002f : 0.003f;
        shadow.position = new Vector3(ballPos.x, shY, ballPos.z);
    }

    void StepBall(bool rules)
    {
        Vector3 prev = ballPos;
        ballVel.y -= BallPhysics.Gravity * Step;
        ballPos += ballVel * Step;
        rallyTime += Step;

        // table bounce
        if (ballVel.y < 0f && ballPos.y - BallPhysics.Radius < BallPhysics.TableY
            && BallPhysics.OnTable(ballPos) && prev.y - BallPhysics.Radius >= BallPhysics.TableY - 0.05f)
        {
            ballPos.y = BallPhysics.TableY + BallPhysics.Radius;
            ballVel.y = -ballVel.y * BallPhysics.Restitution;
            ballVel.x *= BallPhysics.TableFriction;
            ballVel.z *= BallPhysics.TableFriction;
            if (rules) OnTableBounce(ballPos.z < 0f ? 0 : 1);
        }

        // net
        if (prev.z * ballPos.z < 0f && Mathf.Abs(ballPos.x) < BallPhysics.NetHalfWidth
            && ballPos.y < BallPhysics.TableY + BallPhysics.NetHeight + BallPhysics.Radius)
        {
            ballPos.z = prev.z;
            ballVel.z = -ballVel.z * 0.25f;
            ballVel.x *= 0.5f;
        }

        if (!rules) { if (ballPos.y < BallPhysics.Radius) { ballPos.y = BallPhysics.Radius; ballVel = Vector3.zero; } return; }

        // paddles
        if (lastHitter != 0 && prev.z >= PlayerZ && ballPos.z < PlayerZ) TryHit(0, prev);
        else if (lastHitter != 1 && prev.z <= AiZ && ballPos.z > AiZ) TryHit(1, prev);

        // out of play
        if (ballPos.y < 0.2f || rallyTime > 20f) ResolveOut();
    }

    void TryHit(int side, Vector3 prev)
    {
        Paddle pad = side == 0 ? playerPaddle : aiPaddle;
        float planeZ = side == 0 ? PlayerZ : AiZ;
        float k = Mathf.InverseLerp(prev.z, ballPos.z, planeZ);
        Vector3 at = Vector3.Lerp(prev, ballPos, k);
        Vector3 pp = pad.transform.position;
        float dx = at.x - pp.x, dy = at.y - pp.y;
        if (dx * dx + dy * dy > Paddle.HitRadius * Paddle.HitRadius) return;

        float landX, depth, flight;
        if (side == 0)
        {
            Vector3 v = pad.Velocity;
            float power = Mathf.Clamp01(new Vector2(v.x, v.y).magnitude / 6f);
            landX = Mathf.Clamp(v.x * 0.22f - dx * 2.5f, -0.62f, 0.62f);
            depth = Mathf.Lerp(0.65f, 1.25f, power);
            flight = Mathf.Lerp(0.70f, 0.45f, power);
        }
        else
        {
            float away = -Mathf.Sign(playerPaddle.transform.position.x + 0.001f);
            landX = Mathf.Clamp(Random.Range(0.1f, 0.6f) * (Random.value < 0.7f ? away : -away), -0.62f, 0.62f);
            depth = Random.Range(0.6f, 1.25f);
            flight = aiFlight * Random.Range(0.9f, 1.15f);
        }
        float landZ = side == 0 ? depth : -depth;
        ballPos = at;
        ballVel = BallPhysics.SolveHit(at, landX, landZ, flight);
        lastHitter = side;
        bouncesSinceHit = 0;
        awaitingServeBounce = false;
        if (side == 0) PlanAiIntercept();
    }

    // ---------- rules ----------
    void OnTableBounce(int tableSide)
    {
        if (awaitingServeBounce)
        {
            awaitingServeBounce = false;
            if (tableSide != lastHitter) Score(1 - lastHitter);
            return;
        }
        if (tableSide == lastHitter) { Score(1 - lastHitter); return; }
        bouncesSinceHit++;
        if (bouncesSinceHit >= 2) Score(lastHitter);
    }

    void ResolveOut()
    {
        if (awaitingServeBounce || bouncesSinceHit == 0) Score(1 - lastHitter);
        else Score(lastHitter);
    }

    void Score(int winner)
    {
        if (state != State.Rally) return;
        if (winner == 0) playerScore++; else aiScore++;
        banner = winner == 0 ? "Point!" : "CPU point";
        bool over = (playerScore >= PointsToWin || aiScore >= PointsToWin) && Mathf.Abs(playerScore - aiScore) >= 2;
        if (over)
        {
            banner = playerScore > aiScore ? "YOU WIN!\nClick / tap for next level" : "YOU LOSE\nClick / tap to retry";
            EnterState(State.MatchOver);
            return;
        }
        EnterState(State.PointOver);
    }

    void NextPoint()
    {
        int total = playerScore + aiScore;
        bool deuce = playerScore >= PointsToWin - 1 && aiScore >= PointsToWin - 1;
        server = deuce ? total % 2 : (total / 2) % 2;
        HideBall();
        ResetPaddles();
        EnterState(State.Serving);
    }

    // ---------- HUD ----------
    GUIStyle big, mid;

    void OnGUI()
    {
        float s = Screen.height / 720f;
        if (big == null)
        {
            big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            mid = new GUIStyle(big);
        }
        big.fontSize = Mathf.RoundToInt(56 * s);
        mid.fontSize = Mathf.RoundToInt(24 * s);

        if (state != State.Menu)
        {
            DrawText(new Rect(0, 10 * s, Screen.width, 70 * s), playerScore + "  -  " + aiScore, big);
            DrawText(new Rect(0, 78 * s, Screen.width, 30 * s), "You  vs  CPU   |   Level " + level, mid);
        }
        if (!string.IsNullOrEmpty(banner))
            DrawText(new Rect(0, Screen.height * 0.38f, Screen.width, 140 * s), banner, mid);
    }

    static void DrawText(Rect r, string text, GUIStyle style)
    {
        var shadowStyle = new GUIStyle(style);
        shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, shadowStyle);
        style.normal.textColor = Color.white;
        GUI.Label(r, text, style);
    }
}
