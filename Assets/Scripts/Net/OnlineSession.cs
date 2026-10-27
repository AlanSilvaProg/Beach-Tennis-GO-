using System;
using UnityEngine;

public enum OnlineRole { None, Host, Guest, Fake }

public struct OnlineMatchInfo
{
    public OnlineRole role;
    public string opponentName;
    public string opponentSkin;   // id of the opponent's racket skin (empty = the first one)
    public int tier;              // Fake: difficulty 1..5 of the fake opponent
    public int pointsToWin, setsToWin; // Guest: the rules the host chose
}

// Looks for an opponent for the online mode and keeps the connection to them.
//   1. Real player: asks the network for a lobby that is waiting for someone. Found: joins it (GUEST). None: makes one and waits (HOST). While it
//      waits it keeps looking, and if another waiting lobby shows up whose code is smaller, it joins that one instead (so two players who made a lobby
//      at the same time still find each other).
//   2. Nobody after Fallback Seconds (20): the FAKE opponent (a random name and difficulty; OnlineRacketController plays it).
//   3. No network at all (the Editor, an old browser): the fake opponent after a random time (Unavailable Delay).
// The two players say hello when they connect (name, racket, version); the host then sends the rules and both report Found.
public class OnlineSession : MonoBehaviour
{
    [Header("Network")]
    [Tooltip("Identifies this game on the Poki Netlib. For the real release use the game id of the Poki for Developers page (it is in the URL); any UUID works to test.")]
    public string gameId = PokiService.GameId;

    [Header("Search")]
    [Tooltip("Seconds without finding a player before the fake opponent takes over.")]
    public float fallbackSeconds = 20f;
    [Tooltip("When there is no network, the fake opponent is \"found\" after a time drawn between these (seconds).")]
    public Vector2 unavailableDelay = new Vector2(1f, 20f);
    [Tooltip("Seconds between two looks at the waiting lobbies while it is hosting one.")]
    public float listInterval = 2.5f;
    [Tooltip("A player who connected has this many seconds to say hello, or is dropped.")]
    public float handshakeTimeout = 6f;
    [Tooltip("A connection that is being made when the fallback time ends gets this much extra time.")]
    public float grace = 6f;
    [Tooltip("A lobby that was joined but never connected (its owner is gone) is given up after this many seconds.")]
    public float joinTimeout = 6f;

    public bool Searching => phase != Phase.Idle && phase != Phase.Matched && phase != Phase.Fake;
    public bool Matched => phase == Phase.Matched;
    public OnlineRole Role { get; private set; }
    public bool NetworkAvailable { get; private set; }
    public string OpponentName { get; private set; } = "";

    // Raised once per search. Host / Guest: connected and ready to play; Fake: nobody came.
    public event Action<OnlineMatchInfo> Found;
    // A message of the other player once the match is on (anything that is not part of the search).
    public event Action<string[]> Message;
    // The other player left or the connection was lost after the match was found.
    public event Action PeerLeft;

    enum Phase { Idle, Connecting, Listing, Creating, Hosting, Joining, Handshake, Matched, Fake }

    INetTransport transport;
    Phase phase;
    string localName = "", localSkin = "";
    float startedAt, fallbackAt, nextList, handshakeAt;
    string myLobby;
    bool amHost, gotHello;
    string oppSkin = "";
    int lastTier;
    float lastMessageAt;
    float joinAt;
    string pendingJoin;
    readonly System.Collections.Generic.HashSet<string> badLobbies = new System.Collections.Generic.HashSet<string>(); // lobbies that did not work this search

    // For tests: use this transport instead of the real one.
    public void UseTransport(INetTransport t) { transport = t; }

    public void Begin(string playerName, string skinId)
    {
        Stop();
        localName = playerName; localSkin = skinId ?? "";
        startedAt = Time.unscaledTime;
        fallbackAt = startedAt + fallbackSeconds;
        if (transport == null) transport = NetTransportFactory.Create();
        NetworkAvailable = transport.Available;
        myLobby = null; amHost = false; gotHello = false; OpponentName = ""; Role = OnlineRole.None;
        pendingJoin = null; badLobbies.Clear();
        if (!NetworkAvailable)
        {
            phase = Phase.Connecting;
            fallbackAt = startedAt + UnityEngine.Random.Range(unavailableDelay.x, unavailableDelay.y);
            PokiEvents.Track("online", "no_network");
            return;
        }
        phase = Phase.Connecting;
        transport.Init(gameId);
    }

    // Gives up the search (or leaves the match): nothing more is reported.
    public void Stop()
    {
        if (transport != null && NetworkAvailable && phase != Phase.Idle)
        {
            if (phase == Phase.Matched) transport.Send(true, NetProtocol.Bye);
            transport.Close();
        }
        phase = Phase.Idle;
        Role = OnlineRole.None;
    }

    void OnDestroy() { Stop(); }

    // The other player (Host / Guest matches). Unreliable messages may be lost.
    public void Send(string text, bool reliable)
    {
        if (phase == Phase.Matched && transport != null) transport.Send(reliable, text);
    }

    void Update()
    {
        if (phase == Phase.Idle || phase == Phase.Fake) return;
        if (transport != null && NetworkAvailable)
        {
            int guard = 0;
            while (guard++ < 200 && transport.Poll(out NetEvent e)) Handle(e);
        }
        if (phase == Phase.Idle) return;
        float now = Time.unscaledTime;

        if (phase == Phase.Matched)
        {
            return;
        }

        if (phase == Phase.Handshake && now - handshakeAt > handshakeTimeout) DropPeer();
        if (phase == Phase.Joining && now - joinAt > joinTimeout) GiveUpJoin();
        if (phase == Phase.Hosting && now >= nextList) { nextList = now + listInterval; transport.List(); }

        bool connecting = phase == Phase.Handshake || phase == Phase.Joining;
        if (now >= fallbackAt && (!connecting || now >= fallbackAt + grace)) StartFake();
    }

    // ---- events of the network ----

    void Handle(NetEvent e)
    {
        if (e.type != NetEventType.Message) Debug.Log("[Online] net " + e.type + (string.IsNullOrEmpty(e.data) ? "" : " " + (e.data.Length > 160 ? e.data.Substring(0, 160) : e.data)) + " (" + phase + ")");
        switch (e.type)
        {
            case NetEventType.Ready:
                if (phase == Phase.Connecting) { phase = Phase.Listing; transport.List(); }
                break;
            case NetEventType.List:
                OnList(NetTransportFactory.ParseLobbies(e.data));
                break;
            case NetEventType.Lobby:
                if (phase == Phase.Creating) { myLobby = e.data; amHost = true; phase = Phase.Hosting; nextList = Time.unscaledTime + listInterval; }
                else if (phase == Phase.Joining) { myLobby = e.data; amHost = false; }
                break;
            case NetEventType.Connected:
                if (phase == Phase.Hosting || phase == Phase.Joining || phase == Phase.Listing)
                {
                    phase = Phase.Handshake; handshakeAt = Time.unscaledTime;
                    gotHello = false;
                    transport.Send(true, NetProtocol.Hello(localName, localSkin));
                }
                break;
            case NetEventType.Disconnected:
                if (phase == Phase.Matched) { PeerLeft?.Invoke(); }
                else if (phase == Phase.Handshake) DropPeer();
                break;
            case NetEventType.Message:
                OnMessage(e.data);
                break;
            case NetEventType.Error:
                OnError(e.data);
                break;
            case NetEventType.Failed:
            case NetEventType.Closed:
                if (phase == Phase.Matched) PeerLeft?.Invoke(); else OnError("connection " + e.type);
                break;
        }
    }

    // Lobbies that are waiting for a second player (and are ours: made by this game).
    void OnList(LobbyEntry[] lobbies)
    {
        string best = null;
        foreach (var l in lobbies)
        {
            if (l == null || string.IsNullOrEmpty(l.code) || l.code == myLobby || badLobbies.Contains(l.code)) continue;
            if (l.playerCount < 1) continue; // its owner has left
            if (l.customData == null || l.customData.btg != NetProtocol.Version) continue;
            if (l.playerCount >= (l.maxPlayers > 0 ? l.maxPlayers : 2)) continue;
            if (best == null || string.CompareOrdinal(l.code, best) < 0) best = l.code;
        }
        if (phase == Phase.Listing)
        {
            if (best != null) { phase = Phase.Joining; pendingJoin = best; joinAt = Time.unscaledTime; transport.Join(best); }
            else { phase = Phase.Creating; transport.Create(NetProtocol.CreateCustomData()); }
        }
        else if (phase == Phase.Hosting && best != null && string.CompareOrdinal(best, myLobby) < 0)
        {
            // someone else is waiting and their code is smaller: they stay, we go (so that the two of us do not wait for each other for ever)
            transport.Leave();
            myLobby = null; amHost = false;
            phase = Phase.Joining; pendingJoin = best; joinAt = Time.unscaledTime;
            transport.Join(best);
        }
    }

    void OnMessage(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        lastMessageAt = Time.unscaledTime;
        var f = NetProtocol.Split(text);
        if (phase == Phase.Matched) { Message?.Invoke(f); return; }
        if (phase != Phase.Handshake) return;

        if (f[0] == "H")
        {
            if (!NetProtocol.ParseHello(f, out string name, out string skin, out int version) || version != NetProtocol.Version) { DropPeer(); return; }
            gotHello = true; OpponentName = name; oppSkin = skin;
            if (amHost) Match(new OnlineMatchInfo { role = OnlineRole.Host, opponentName = name, opponentSkin = skin }, true);
        }
        else if (f[0] == "G" && !amHost)
        {
            if (NetProtocol.ParseGo(f, out int points, out int sets) && gotHello)
                Match(new OnlineMatchInfo { role = OnlineRole.Guest, opponentName = OpponentName, opponentSkin = oppSkin, pointsToWin = points, setsToWin = sets }, false);
        }
    }

    void Match(OnlineMatchInfo info, bool sendGo)
    {
        phase = Phase.Matched;
        Role = info.role;
        if (sendGo)
        {
            info.pointsToWin = GameManager.OnlinePoints; info.setsToWin = GameManager.OnlineSets;
            transport.Send(true, NetProtocol.Go(info.pointsToWin, info.setsToWin));
        }
        PokiEvents.Track("online", "found", "role", info.role.ToString(), "searched_s", Mathf.RoundToInt(Time.unscaledTime - startedAt));
        Found?.Invoke(info);
    }

    // The other player did not say hello (or an old version): forget them and look again.
    void DropPeer()
    {
        if (myLobby != null && !amHost) badLobbies.Add(myLobby);
        if (pendingJoin != null) badLobbies.Add(pendingJoin);
        transport.Leave();
        myLobby = null; amHost = false; gotHello = false;
        phase = Phase.Listing;
        transport.List();
    }

    // The lobby we joined never connected: its owner is gone. Forget it and look again.
    void GiveUpJoin()
    {
        if (pendingJoin != null) badLobbies.Add(pendingJoin);
        pendingJoin = null; myLobby = null;
        transport.Leave();
        phase = Phase.Listing;
        transport.List();
    }

    void OnError(string what)
    {
        if (phase == Phase.Matched || phase == Phase.Idle) return;
        // join failed (the lobby filled up), or the network is down: for a failed join look again; otherwise the fake opponent comes early
        if (phase == Phase.Joining) { if (pendingJoin != null) badLobbies.Add(pendingJoin); pendingJoin = null; phase = Phase.Listing; transport.List(); return; }
        if (phase == Phase.Connecting)
        {
            NetworkAvailable = false;
            fallbackAt = Mathf.Min(fallbackAt, startedAt + UnityEngine.Random.Range(unavailableDelay.x, unavailableDelay.y));
            PokiEvents.Track("online", "network_error", "what", what);
        }
    }

    // ---- the fake opponent ----

    void StartFake()
    {
        if (transport != null && NetworkAvailable) transport.Close();
        phase = Phase.Fake;
        Role = OnlineRole.Fake;
        int tier;
        do { tier = UnityEngine.Random.Range(1, 6); } while (tier == lastTier && UnityEngine.Random.value < 0.6f); // the same difficulty twice in a row is rarer
        lastTier = tier;
        OpponentName = OpponentNames.Next();
        PokiEvents.Track("online", "fallback", "tier", tier, "searched_s", Mathf.RoundToInt(Time.unscaledTime - startedAt), "network", NetworkAvailable);
        Found?.Invoke(new OnlineMatchInfo { role = OnlineRole.Fake, opponentName = OpponentName, tier = tier });
    }
}
