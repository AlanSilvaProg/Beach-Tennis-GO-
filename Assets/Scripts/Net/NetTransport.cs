using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public enum NetEventType { None, Ready, Lobby, List, Connected, Disconnected, Message, Left, Failed, Closed, Error }

public struct NetEvent
{
    public NetEventType type;
    public string channel; // Message: "reliable" or "unreliable"
    public string data;    // Lobby: the code; List: the lobbies as a JSON array; Message: the text; Error: what went wrong
    public string peer;    // Connected / Disconnected / Message: the id of the other player
}

[Serializable] public class LobbyCustom { public int btg; }

[Serializable]
public class LobbyEntry
{
    public string code;
    public int playerCount;
    public int maxPlayers;
    public LobbyCustom customData;
}

[Serializable] class LobbyList { public LobbyEntry[] items; }

// What the online mode needs from the network: lobbies (create / list / join) and text messages to the other player in the lobby.
// Implementations: NetlibTransport (the Poki Netlib in the browser), NullTransport (anywhere else) and LoopbackTransport (two sessions in one
// process, for tests). Events are queued and read with Poll, once a frame.
public interface INetTransport
{
    bool Available { get; }
    void Init(string gameId);
    void List();
    void Create(string customJson);
    void Join(string code);
    void Send(bool reliable, string text);
    void Leave();
    void Close();
    bool Poll(out NetEvent e);
}

public static class NetTransportFactory
{
    public static INetTransport Create()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return new NetlibTransport();
#else
        return new NullTransport();
#endif
    }

    public static LobbyEntry[] ParseLobbies(string json)
    {
        try
        {
            var list = JsonUtility.FromJson<LobbyList>("{\"items\":" + (string.IsNullOrEmpty(json) ? "[]" : json) + "}");
            return list != null && list.items != null ? list.items : new LobbyEntry[0];
        }
        catch { return new LobbyEntry[0]; }
    }
}

// No network (the Editor, a standalone build): the online mode then goes straight to the fake opponent.
public class NullTransport : INetTransport
{
    public bool Available => false;
    public void Init(string gameId) { }
    public void List() { }
    public void Create(string customJson) { }
    public void Join(string code) { }
    public void Send(bool reliable, string text) { }
    public void Leave() { }
    public void Close() { }
    public bool Poll(out NetEvent e) { e = default; return false; }
}

#if UNITY_WEBGL && !UNITY_EDITOR
// The Poki Netlib through Netlib.jslib / BtgNet.jspre.
public class NetlibTransport : INetTransport
{
    [DllImport("__Internal")] static extern void Netlib_Init(string gameId);
    [DllImport("__Internal")] static extern void Netlib_List();
    [DllImport("__Internal")] static extern void Netlib_Create(string customJson);
    [DllImport("__Internal")] static extern void Netlib_Join(string code);
    [DllImport("__Internal")] static extern void Netlib_Send(string channel, string text);
    [DllImport("__Internal")] static extern void Netlib_Leave();
    [DllImport("__Internal")] static extern void Netlib_Close();
    [DllImport("__Internal")] static extern IntPtr Netlib_Poll();
    [DllImport("__Internal")] static extern void Netlib_Free(IntPtr ptr);

    [Serializable] class Raw { public string t; public string c; public string d; public string p; }

    public bool Available => true;
    public void Init(string gameId) { Netlib_Init(gameId); }
    public void List() { Netlib_List(); }
    public void Create(string customJson) { Netlib_Create(customJson); }
    public void Join(string code) { Netlib_Join(code); }
    public void Send(bool reliable, string text) { Netlib_Send(reliable ? "reliable" : "unreliable", text); }
    public void Leave() { Netlib_Leave(); }
    public void Close() { Netlib_Close(); }

    public bool Poll(out NetEvent e)
    {
        e = default;
        IntPtr ptr = Netlib_Poll();
        if (ptr == IntPtr.Zero) return false;
        string json = Marshal.PtrToStringUTF8(ptr);
        Netlib_Free(ptr);
        var raw = JsonUtility.FromJson<Raw>(json);
        if (raw == null) return false;
        e.channel = raw.c; e.data = raw.d; e.peer = raw.p;
        switch (raw.t)
        {
            case "ready": e.type = NetEventType.Ready; break;
            case "lobby": e.type = NetEventType.Lobby; break;
            case "list": e.type = NetEventType.List; break;
            case "connected": e.type = NetEventType.Connected; e.peer = raw.d; break;
            case "disconnected": e.type = NetEventType.Disconnected; e.peer = raw.d; break;
            case "message": e.type = NetEventType.Message; break;
            case "left": e.type = NetEventType.Left; break;
            case "failed": e.type = NetEventType.Failed; break;
            case "closed": e.type = NetEventType.Closed; break;
            default: e.type = NetEventType.Error; break;
        }
        return true;
    }
}
#endif
