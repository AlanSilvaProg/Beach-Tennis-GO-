using System.Collections.Generic;

// A tiny in-process "network" with the same behaviour as the Netlib for what we use (a lobby holds two players; create / list / join; messages go to
// the other player). It exists for tests: two OnlineSessions can find each other in the Editor without a browser.
public class LoopbackHub
{
    class Lobby { public string code; public bool isPublic = true; public int max = 2; public string custom = "{}"; public readonly List<LoopbackTransport> members = new List<LoopbackTransport>(); }

    readonly Dictionary<string, Lobby> lobbies = new Dictionary<string, Lobby>();
    int nextCode = 1000;
    int nextPeer = 1;

    public int LobbyCount => lobbies.Count;

    internal string NewPeerId() { return "peer" + (nextPeer++); }

    internal string CreateLobby(LoopbackTransport creator, string custom)
    {
        var lobby = new Lobby { code = "L" + (nextCode++), custom = string.IsNullOrEmpty(custom) ? "{}" : custom };
        lobby.members.Add(creator);
        lobbies[lobby.code] = lobby;
        return lobby.code;
    }

    internal string ListJson()
    {
        var sb = new System.Text.StringBuilder("[");
        bool first = true;
        foreach (var l in lobbies.Values)
        {
            if (!l.isPublic) continue;
            if (!first) sb.Append(',');
            first = false;
            sb.Append("{\"code\":\"").Append(l.code).Append("\",\"playerCount\":").Append(l.members.Count).Append(",\"maxPlayers\":").Append(l.max).Append(",\"customData\":").Append(l.custom).Append('}');
        }
        return sb.Append(']').ToString();
    }

    internal bool Join(string code, LoopbackTransport joiner)
    {
        if (!lobbies.TryGetValue(code, out var l) || l.members.Count >= l.max) return false;
        foreach (var m in l.members) { m.Queue(NetEventType.Connected, peer: joiner.PeerId); joiner.Queue(NetEventType.Connected, peer: m.PeerId); }
        l.members.Add(joiner);
        return true;
    }

    internal void Leave(string code, LoopbackTransport who)
    {
        if (code == null || !lobbies.TryGetValue(code, out var l)) return;
        l.members.Remove(who);
        foreach (var m in l.members) m.Queue(NetEventType.Disconnected, peer: who.PeerId);
        if (l.members.Count == 0) lobbies.Remove(code);
    }

    internal void Deliver(string code, LoopbackTransport from, bool reliable, string text)
    {
        if (code == null || !lobbies.TryGetValue(code, out var l)) return;
        foreach (var m in l.members) if (m != from) m.Queue(NetEventType.Message, reliable ? "reliable" : "unreliable", text, from.PeerId);
    }
}

public class LoopbackTransport : INetTransport
{
    readonly LoopbackHub hub;
    readonly Queue<NetEvent> events = new Queue<NetEvent>();
    string lobby;

    public string PeerId { get; }
    public bool Available => true;
    public string Lobby => lobby;

    public LoopbackTransport(LoopbackHub hub) { this.hub = hub; PeerId = hub.NewPeerId(); }

    internal void Queue(NetEventType type, string channel = null, string data = null, string peer = null)
    {
        events.Enqueue(new NetEvent { type = type, channel = channel, data = data, peer = peer });
    }

    public void Init(string gameId) { Queue(NetEventType.Ready); }
    public void List() { Queue(NetEventType.List, data: hub.ListJson()); }

    public void Create(string customJson)
    {
        lobby = hub.CreateLobby(this, customJson);
        Queue(NetEventType.Lobby, data: lobby);
    }

    public void Join(string code)
    {
        if (hub.Join(code, this)) { lobby = code; Queue(NetEventType.Lobby, data: code); }
        else Queue(NetEventType.Error, data: "join: lobby not found or full");
    }

    public void Send(bool reliable, string text) { hub.Deliver(lobby, this, reliable, text); }

    public void Leave()
    {
        hub.Leave(lobby, this);
        lobby = null;
        Queue(NetEventType.Left);
    }

    public void Close() { Leave(); }

    public bool Poll(out NetEvent e)
    {
        if (events.Count > 0) { e = events.Dequeue(); return true; }
        e = default;
        return false;
    }
}
