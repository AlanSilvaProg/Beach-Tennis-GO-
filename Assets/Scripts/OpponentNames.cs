using System.Collections.Generic;
using UnityEngine;

// The names of the fake online players: about 1000 of them (Resources/OpponentNames.txt, one per line). A name is not used twice by the same
// player: the ones already met are remembered between sessions, and only when every name has been met do they start over.
public static class OpponentNames
{
    static string[] all;
    static readonly HashSet<int> used = new HashSet<int>();
    static bool loaded;

    public static int Count { get { Load(); return all.Length; } }

    // A name that this player has not met yet.
    public static string Next()
    {
        Load();
        if (all.Length == 0) return "Player";
        if (used.Count >= all.Length) used.Clear(); // met everyone: start over
        int index;
        do { index = Random.Range(0, all.Length); } while (used.Contains(index));
        used.Add(index);
        Save();
        return all[index];
    }

    // A name for this player (the one the opponents see): drawn once and kept. It does not use up the names of the opponents.
    public static string LocalName()
    {
        Load();
        string name = "";
        try { name = PlayerPrefs.GetString("onlineName", ""); } catch { }
        if (name.Length == 0)
        {
            name = all.Length > 0 ? all[Random.Range(0, all.Length)] : "Player";
            try { PlayerPrefs.SetString("onlineName", name); PlayerPrefs.Save(); } catch { }
        }
        return name;
    }

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        var file = Resources.Load<TextAsset>("OpponentNames");
        var list = new List<string>();
        if (file != null)
            foreach (var line in file.text.Split('\n')) { var n = line.Trim(); if (n.Length > 0) list.Add(n); }
        all = list.ToArray();
        try
        {
            foreach (var part in PlayerPrefs.GetString("opponentsMet", "").Split(','))
                if (int.TryParse(part, out int i) && i >= 0 && i < all.Length) used.Add(i);
        }
        catch { }
    }

    static void Save()
    {
        try { PlayerPrefs.SetString("opponentsMet", string.Join(",", used)); PlayerPrefs.Save(); } catch { }
    }

    // For tests: forget who was met.
    public static void Forget()
    {
        Load();
        used.Clear();
        Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { loaded = false; used.Clear(); all = null; } // static state survives a Play without domain reload
}
