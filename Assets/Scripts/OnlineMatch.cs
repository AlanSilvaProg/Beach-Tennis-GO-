using UnityEngine;
using UnityEngine.SceneManagement;

// The flow of the Online scene: looks for an opponent (OnlineSession), then plays the match against them.
//   Real player  one of the two is the HOST (it simulates the match, GameManager as in training) and the other the GUEST (it draws what the host says).
//                This class carries the messages: the host sends the state ~20 times a second and the guest its racket ~30 times a second.
//   Fake player  nobody came in 20 s: the match is local, against the OnlineRacketController.
// After a match the player chooses (the result screens): another opponent (the scene is loaded again, clean) or back to the menu.
public class OnlineMatch : MonoBehaviour
{
    [Header("Scene references")]
    public GameManager game;
    public OnlineSession session;
    public OnlineMatchmaking screen;
    [Tooltip("The other player's racket (on the far side, next to the fake opponent's controller).")]
    public RemoteRacketController remote;
    [Tooltip("The fake opponent (plays when nobody is found).")]
    public OnlineRacketController fake;
    public RacketSelection rackets;
    public PokiService poki;

    [Header("Network rates")]
    [Tooltip("Seconds between two states sent by the host.")]
    public float stateInterval = 0.05f;
    [Tooltip("Seconds between two racket positions sent by the guest.")]
    public float racketInterval = 1f / 30f;
    public float pingInterval = 1.5f;
    [Tooltip("The other player is gone after this many seconds of silence during a match.")]
    public float silenceTimeout = 6f;

    OnlineRole role;
    OnlineMatchInfo info;
    string myName = "";
    bool hostReady, guestReady, started;
    float nextSend, nextPing, lastHeard, oneWay = 0.05f;
    int lastSerialSent = -1;
    int sentStates, gotStates, gotRackets;
    float nextReport;

    void Start()
    {
        if (game == null) game = FindFirstObjectByType<GameManager>();
        if (session == null) session = FindFirstObjectByType<OnlineSession>();
        if (poki == null) poki = FindFirstObjectByType<PokiService>();
        session.Found += OnFound;
        session.Message += OnMessage;
        session.PeerLeft += OnPeerLeft;
        game.OnlineResult += OnResult;
        game.NetMessage += OnGameMessage;
        myName = OpponentNames.LocalName();
        string skin = RacketProgress.EquippedId;
        if (screen != null) screen.Open(OnCancel);
        session.Begin(myName, skin);
        Debug.Log("[Online] searching as " + myName + " (network " + (session.NetworkAvailable ? "on" : "off") + ")");
    }

    void OnDestroy()
    {
        if (session != null) { session.Found -= OnFound; session.Message -= OnMessage; session.PeerLeft -= OnPeerLeft; }
        if (game != null) { game.OnlineResult -= OnResult; game.NetMessage -= OnGameMessage; }
    }

    void OnCancel()
    {
        session.Stop();
        SceneFlow.OpenTraining();
    }

    void OnResult(bool again)
    {
        session.Stop();
        if (again) SceneManager.LoadScene(SceneFlow.OnlineScene); // a clean scene, and the search starts again by itself
        else SceneFlow.OpenTraining();
    }

    // ---- an opponent was found ----

    void OnFound(OnlineMatchInfo found)
    {
        info = found;
        role = found.role;
        Debug.Log("[Online] found: " + found.role + " vs " + found.opponentName);
        lastHeard = Time.unscaledTime;
        if (role == OnlineRole.Fake)
        {
            fake.SetTier(found.tier);
            game.BeginOnline(GameManager.NetRole.None, fake, myName, found.opponentName);
        }
        else
        {
            game.BeginOnline(role == OnlineRole.Host ? GameManager.NetRole.Host : GameManager.NetRole.Guest, remote, myName, found.opponentName);
            if (rackets != null) rackets.ApplySkinTo(remote.Racket, found.opponentSkin);
        }
        if (screen != null) screen.ShowFound(found.opponentName, AfterFoundScreen); else AfterFoundScreen();
    }

    // The name was shown. Poki's ad break comes now (it plays only when Poki decides it is time); then the match starts: at once against the fake
    // opponent, when both players are ready against a real one.
    void AfterFoundScreen()
    {
        if (poki != null) poki.CommercialBreak(BreakDone); else BreakDone();
    }

    void BreakDone()
    {
        if (role == OnlineRole.Fake) { Begin(); return; }
        if (role == OnlineRole.Host) { hostReady = true; TryStartHost(); }
        else { session.Send(NetProtocol.Ready, true); StartGuest(); }
    }

    void TryStartHost()
    {
        if (started || !hostReady || !guestReady) return;
        session.Send(NetProtocol.Ready, true); // (the guest starts when it has the first state anyway)
        Begin();
    }

    void StartGuest()
    {
        if (started) return;
        started = true;
        Debug.Log("[Online] guest waits for the host");
        if (screen != null) screen.Close();
        game.StartGuestMatch(info.pointsToWin, info.setsToWin);
        nextPing = 0f;
    }

    void Begin()
    {
        if (started) return;
        started = true;
        Debug.Log("[Online] match starts (" + role + ")");
        if (screen != null) screen.Close();
        game.StartOnlineMatch();
    }

    // ---- messages ----

    void OnMessage(string[] f)
    {
        lastHeard = Time.unscaledTime;
        switch (f[0])
        {
            case "P": // host: the guest's racket
                if (role == OnlineRole.Host && NetProtocol.ParseRacketInput(f, out Vector2 xz)) { remote.SetTarget(xz); gotRackets++; }
                break;
            case "R":
                if (role == OnlineRole.Host) { guestReady = true; TryStartHost(); }
                break;
            case "S": // guest: the state of the match
                if (role == OnlineRole.Guest && started && NetProtocol.TryDecode(f, out NetSnapshot s))
                {
                    game.GuestApply(NetProtocol.ForGuest(s), oneWay);
                    gotStates++;
                }
                break;
            case "E":
                if (role == OnlineRole.Guest && started) game.GuestEvent(f);
                break;
            case "N": // ping from the guest
                session.Send(NetProtocol.Pong(float.Parse(f[1], System.Globalization.CultureInfo.InvariantCulture)), false);
                break;
            case "O": // pong
                if (float.TryParse(f[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sent))
                    oneWay = Mathf.Clamp((Time.unscaledTime - sent) * 0.5f, 0f, 0.5f);
                break;
            case "X":
                OnPeerLeft();
                break;
        }
    }

    // Hits and cheers of the host's match, for the guest.
    void OnGameMessage(string message)
    {
        if (role == OnlineRole.Host && started) session.Send(message, true);
    }

    void OnPeerLeft()
    {
        if (role == OnlineRole.Host || role == OnlineRole.Guest) game.OpponentLeft();
    }

    void Update()
    {
        if (!started || (role != OnlineRole.Host && role != OnlineRole.Guest)) return;
        float now = Time.unscaledTime;
        if (now - lastHeard > silenceTimeout) { OnPeerLeft(); return; }
        if (now >= nextReport)
        {
            nextReport = now + 5f;
            var st = game.ExportSnapshot();
            Debug.Log("[Online] " + role + " states sent " + sentStates + ", states got " + gotStates + ", rackets got " + gotRackets + " | " + game.CurrentState + " " + st.scoreA + "-" + st.scoreB + " oneWay " + (oneWay * 1000f).ToString("F0") + "ms");
        }
        if (role == OnlineRole.Host)
        {
            var snapshot = game.ExportSnapshot();
            bool changed = snapshot.serial != lastSerialSent;
            if (changed || now >= nextSend)
            {
                nextSend = now + stateInterval;
                lastSerialSent = snapshot.serial;
                session.Send(NetProtocol.Encode(snapshot), changed); // a change of state must not get lost
                sentStates++;
            }
        }
        else if (now >= nextSend)
        {
            nextSend = now + racketInterval;
            var p = game.player.Racket.Position;
            session.Send(NetProtocol.RacketInput(NetProtocol.Turn(new Vector2(p.x, p.z))), false); // in the host's world
            if (now >= nextPing) { nextPing = now + pingInterval; session.Send(NetProtocol.Ping(now), false); }
        }
    }
}
