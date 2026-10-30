using System.Globalization;
using UnityEngine;

// What the two players send each other (plain text, fields separated by "|", numbers in the invariant culture).
//
// The player who made the lobby is the HOST: the whole match is simulated on the host (rules, ball, hits). The other one is the GUEST: it only shows what
// the host says and sends its own racket. Everything in a message is written from the HOST's point of view (host at z < 0); the guest turns it round
// (x -> -x, z -> -z, host <-> guest) before using it, so on both screens "me" is at the bottom.
//
//   H|name|skin|version              hello, both ways, as soon as the two are connected
//   G|pointsToWin|setsToWin          host -> guest: the rules of the match (the host's settings)
//   R                                ready: the ad break is over, both ways; the host starts when it has both
//   P|x|z                            guest -> host (unreliable, ~30 per second): where the guest's racket is, already in host coordinates
//   S|...                            host -> guest: the state of the match (see NetSnapshot); unreliable ~20 per second, reliable when the state changes
//                                    (banner: 0 none, 1 point for the host, 2 for the guest, 3 set for the host, 4 for the guest)
//   E|H|side|speed  E|L|impact  E|C|value   host -> guest: a racket hit, the ball landing, the crowd cheering (sounds and effects)
//   N|time / O|time                  ping / pong, to measure the delay
//   X                                leaving
public struct NetSnapshot
{
    public int serial;                 // grows with every change of state: the guest knows when the state changed
    public int state;                  // GameManager.State
    public int server;                 // 0 = A serves, 1 = B
    // A is the player at the bottom (z < 0) and B the one at the top. As sent by the host, A is the host and B the guest; ForGuest turns it round.
    public int scoreA, scoreB, setsA, setsB;
    public int banner;                 // 0 none, 1 point host, 2 point guest, 3 set host, 4 set guest
    public float time;                 // match clock (s)
    public bool ballVisible;
    public Vector3 ballPos, ballVel;
    public Vector2 racketA, racketB;   // x, z
    public int ack;                    // the last hit of the guest that the host has dealt with (see GuestHit)
}

public static class NetProtocol
{
    public const int Version = 1;
    public const string GameTag = "btg";  // customData of the lobbies of this game

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static string F(float v) { return v.ToString("0.###", Inv); }
    static float P(string s) { return float.Parse(s, Inv); }
    static int I(string s) { return int.Parse(s, Inv); }
    static string Clean(string text) { return (text ?? "").Replace('|', '_'); }

    public static string CreateCustomData() { return "{\"btg\":" + Version + "}"; }

    // ---- hello / go / misc ----

    public static string Hello(string name, string skin) { return "H|" + Clean(name) + "|" + Clean(skin) + "|" + Version; }

    public static bool ParseHello(string[] f, out string name, out string skin, out int version)
    {
        name = skin = ""; version = 0;
        if (f.Length < 4 || f[0] != "H") return false;
        name = f[1]; skin = f[2];
        return int.TryParse(f[3], NumberStyles.Integer, Inv, out version);
    }

    public static string Go(int pointsToWin, int setsToWin) { return "G|" + pointsToWin + "|" + setsToWin; }

    public static bool ParseGo(string[] f, out int pointsToWin, out int setsToWin)
    {
        pointsToWin = setsToWin = 0;
        if (f.Length < 3 || f[0] != "G") return false;
        return int.TryParse(f[1], NumberStyles.Integer, Inv, out pointsToWin) && int.TryParse(f[2], NumberStyles.Integer, Inv, out setsToWin);
    }

    public const string Ready = "R";
    public const string Bye = "X";
    public static string Ping(float time) { return "N|" + F(time); }
    public static string Pong(float time) { return "O|" + F(time); }

    public static string[] Split(string message) { return message.Split('|'); }

    // ---- guest racket ----

    public static string RacketInput(Vector2 hostSpace) { return "P|" + F(hostSpace.x) + "|" + F(hostSpace.y); }

    public static bool ParseRacketInput(string[] f, out Vector2 hostSpace)
    {
        hostSpace = default;
        if (f.Length < 3 || f[0] != "P") return false;
        try { hostSpace = new Vector2(P(f[1]), P(f[2])); return true; } catch { return false; }
    }

    // ---- snapshot ----

    public static string Encode(NetSnapshot s)
    {
        return "S|" + s.serial + "|" + s.state + "|" + s.server + "|" + s.scoreA + "|" + s.scoreB + "|" + s.setsA + "|" + s.setsB + "|" + F(s.time) + "|" + (s.ballVisible ? 1 : 0)
               + "|" + F(s.ballPos.x) + "|" + F(s.ballPos.y) + "|" + F(s.ballPos.z) + "|" + F(s.ballVel.x) + "|" + F(s.ballVel.y) + "|" + F(s.ballVel.z)
               + "|" + F(s.racketA.x) + "|" + F(s.racketA.y) + "|" + F(s.racketB.x) + "|" + F(s.racketB.y) + "|" + s.banner + "|" + s.ack;
    }

    public static bool TryDecode(string[] f, out NetSnapshot s)
    {
        s = default;
        if (f.Length < 21 || f[0] != "S") return false;
        try
        {
            s.serial = I(f[1]); s.state = I(f[2]); s.server = I(f[3]);
            s.scoreA = I(f[4]); s.scoreB = I(f[5]); s.setsA = I(f[6]); s.setsB = I(f[7]);
            s.time = P(f[8]); s.ballVisible = f[9] == "1";
            s.ballPos = new Vector3(P(f[10]), P(f[11]), P(f[12]));
            s.ballVel = new Vector3(P(f[13]), P(f[14]), P(f[15]));
            s.racketA = new Vector2(P(f[16]), P(f[17]));
            s.racketB = new Vector2(P(f[18]), P(f[19]));
            s.banner = I(f[20]);
            s.ack = f.Length > 21 ? I(f[21]) : 0;
            return true;
        }
        catch { return false; }
    }

    // The same moment as the guest sees it: the guest is the player at the bottom, so A and B change places and everything is turned round.
    public static NetSnapshot ForGuest(NetSnapshot s)
    {
        var g = s;
        g.server = 1 - s.server;
        g.scoreA = s.scoreB; g.scoreB = s.scoreA;
        g.setsA = s.setsB; g.setsB = s.setsA;
        g.ballPos = Turn(s.ballPos); g.ballVel = Turn(s.ballVel);
        g.racketA = Turn(s.racketB);
        g.racketB = Turn(s.racketA);
        g.banner = s.banner == 1 ? 2 : s.banner == 2 ? 1 : s.banner == 3 ? 4 : s.banner == 4 ? 3 : 0;
        return g;
    }

    public static Vector3 Turn(Vector3 v) { return new Vector3(-v.x, v.y, -v.z); }
    public static Vector2 Turn(Vector2 v) { return new Vector2(-v.x, -v.y); }

    // ---- guest hit ----
    // The guest judges and plays its own hits at once (so the ball never goes through its racket while a message crosses the network) and tells
    // the host where the ball is and where it goes now, already in host coordinates. `latency` is the guest's estimate of the one-way delay.
    public static string GuestHit(int id, Vector3 hostPos, Vector3 hostVel, int quality, float latency)
    {
        return "B|" + id + "|" + F(hostPos.x) + "|" + F(hostPos.y) + "|" + F(hostPos.z) + "|" + F(hostVel.x) + "|" + F(hostVel.y) + "|" + F(hostVel.z) + "|" + quality + "|" + F(latency);
    }

    public static bool ParseGuestHit(string[] f, out int id, out Vector3 pos, out Vector3 vel, out int quality, out float latency)
    {
        id = quality = 0; pos = vel = default; latency = 0f;
        if (f.Length < 10 || f[0] != "B") return false;
        try
        {
            id = I(f[1]);
            pos = new Vector3(P(f[2]), P(f[3]), P(f[4]));
            vel = new Vector3(P(f[5]), P(f[6]), P(f[7]));
            quality = Mathf.Clamp(I(f[8]), 0, 4);
            latency = Mathf.Clamp(P(f[9]), 0f, 0.4f);
            return true;
        }
        catch { return false; }
    }

    // ---- events ----

    // `quality` is the ShotQuality of the hit (0-4), so the other screen shows the same burst; older clients ignore the extra field.
    public static string EventHit(int side, float speed, int quality = 0) { return "E|H|" + side + "|" + F(speed) + "|" + quality; }

    // The grade carried by a hit event (0 when it has none).
    public static int HitQuality(string[] f)
    {
        try { return f.Length > 4 ? Mathf.Clamp(I(f[4]), 0, 4) : 0; } catch { return 0; }
    }
    public static string EventLand(float impact) { return "E|L|" + F(impact); }
    public static string EventCheer(float value) { return "E|C|" + F(value); }

    public static bool ParseEvent(string[] f, out char kind, out float a, out float b)
    {
        kind = '\0'; a = b = 0f;
        if (f.Length < 3 || f[0] != "E" || f[1].Length == 0) return false;
        try
        {
            kind = f[1][0];
            if (kind == 'H') { a = I(f[2]); b = f.Length > 3 ? P(f[3]) : 0f; }
            else a = P(f[2]);
            return true;
        }
        catch { return false; }
    }
}
