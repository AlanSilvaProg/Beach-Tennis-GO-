using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

// Poki custom events (PokiSDK.customEvent(noun, verb, data)): they let Poki's dashboards show what the players do in the game.
// PokiEvents.Track("match", "start", "level", 3, "points_to_win", 11) sends noun "match", verb "start" and data { level: 3, points_to_win: 11 }.
// The data is given as name / value pairs (text, whole numbers, decimals, true / false). Safe everywhere: in the editor and outside the web
// build the event is only written to the Console (turn that off with LogInEditor); with no Poki SDK on the page nothing is sent.
//
// Events sent (noun / verb: data):
//   game / loaded: language                                  the game is up
//   ui / click: button                                       any UI button (the name of the button, with its parent)
//   mode_menu / open | close                                 the game mode window
//   mode / select: mode, available                           a game mode picked in that window
//   settings / open | close                                  the settings window
//   settings / change: setting, value                        points_to_win, sets_to_win, voice_muted, music_muted, sfx_muted, sound_muted
//   match / tap_to_play: level                               the tap on the menu that starts a match
//   match / start: level, points_to_win, sets_to_win, reason   reason: menu, next_level, restart, retry
//   set / end: level, set_number, winner, player_points, cpu_points, player_sets, cpu_sets
//   match / end: result, level, duration_s, player_sets, cpu_sets, player_points, cpu_points   result: win | lose
//   level / reached: level                                   a level seen for the first time in this session
//   level / complete | failed: level
//   match / continue: level, next_level                      CONTINUE on the win screen
//   match / restart: level                                   RESTART on the lose screen
//   match / retry: level, granted                            RETRY (rewarded ad) on the lose screen; granted = the player watched the ad
//   match / back: from, level                                BACK to the menu; from: win | lose
public static class PokiEvents
{
    public static bool LogInEditor = true;
    // Master switch of the custom events sent to Poki (PokiSDK.customEvent). Off for now, while the core events (loading, gameplayStart / Stop, ads) are
    // being checked in Poki's inspector. The game does not depend on them: turn it on again to send them.
    public static bool SendToPoki = false;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void PokiCustomEvent(string noun, string verb, string json);
#endif

    public static void Track(string noun, string verb, params object[] data)
    {
        string json = ToJson(data);
#if UNITY_WEBGL && !UNITY_EDITOR
        if (SendToPoki) { try { PokiCustomEvent(noun, verb, json); } catch { } }
#else
        if (LogInEditor) Debug.Log("[PokiEvent] " + noun + " / " + verb + " " + json);
#endif
    }

    static string ToJson(object[] pairs)
    {
        var sb = new StringBuilder("{");
        for (int i = 0; pairs != null && i + 1 < pairs.Length; i += 2)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(Escape(pairs[i].ToString())).Append("\":");
            AppendValue(sb, pairs[i + 1]);
        }
        return sb.Append('}').ToString();
    }

    static void AppendValue(StringBuilder sb, object v)
    {
        switch (v)
        {
            case null: sb.Append("null"); break;
            case bool b: sb.Append(b ? "true" : "false"); break;
            case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
            case float f: sb.Append(f.ToString("0.##", CultureInfo.InvariantCulture)); break;
            case double d: sb.Append(d.ToString("0.##", CultureInfo.InvariantCulture)); break;
            default: sb.Append('"').Append(Escape(v.ToString())).Append('"'); break;
        }
    }

    static string Escape(string s) { return s.Replace("\\", "\\\\").Replace("\"", "\\\""); }
}
