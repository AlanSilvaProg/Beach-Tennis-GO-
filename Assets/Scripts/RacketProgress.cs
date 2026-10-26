using System;
using System.Collections.Generic;
using UnityEngine;

// What the player owns and uses, kept between sessions (PlayerPrefs, always inside try/catch: they can be unavailable, e.g. in a private window).
//   Unlock(id)                 unlocks a racket from anywhere (purchase, reward...).
//   ReportTrainingLevel(n)     tells that the player reached level n of the Training mode; rackets with that rule unlock.
// New rules: see UnlockKind and RuleMet. Rackets that are Always unlocked are never saved nor counted as new.
public static class RacketProgress
{
    static readonly HashSet<string> unlocked = new HashSet<string>(); // ids unlocked by a rule or by Unlock()
    static readonly HashSet<string> seen = new HashSet<string>();     // ids the player has already looked at in the Rackets window
    static string equipped = "";
    static int highestLevel = 1;
    static int onlineWins, tournamentWins;
    static bool loaded;
    static RacketCatalog catalog;

    // Raised once for every racket that gets unlocked (the "new racket" screen listens to it).
    public static event Action<RacketSkin> Unlocked;
    public static event Action Changed;

    public static int HighestTrainingLevel { get { Load(); return highestLevel; } }

    // The window gives the catalog; unlocks that became true while the game was closed (a rule edited, a level already reached) happen now.
    public static void Bind(RacketCatalog c)
    {
        Load();
        catalog = c;
        Evaluate();
        if (!IsUnlocked(equipped)) equipped = FirstOwnedId();
    }

    public static bool IsUnlocked(string id)
    {
        Load();
        var skin = catalog != null ? catalog.Find(id) : null;
        if (skin != null && skin.unlock != null && skin.unlock.kind == UnlockKind.Always) return true;
        return unlocked.Contains(id);
    }

    public static bool IsUnlocked(RacketSkin skin) { return skin != null && IsUnlocked(skin.Id); }

    // Unlocks a racket by hand. Returns false when it was already owned or does not exist.
    public static bool Unlock(string id)
    {
        Load();
        if (string.IsNullOrEmpty(id) || IsUnlocked(id)) return false;
        var skin = catalog != null ? catalog.Find(id) : null;
        if (catalog != null && skin == null) return false;
        unlocked.Add(id);
        Save();
        PokiEvents.Track("racket", "unlock", "id", id);
        if (skin != null) Unlocked?.Invoke(skin);
        return true;
    }

    public static void ReportTrainingLevel(int level)
    {
        Load();
        if (level <= highestLevel) return;
        highestLevel = level;
        Save();
        Evaluate();
    }

    // The player won a match of the Online mode: rackets that unlock by online wins may unlock.
    public static void ReportOnlineWin()
    {
        Load();
        onlineWins++;
        Save();
        Evaluate();
    }

    public static int OnlineWins { get { Load(); return onlineWins; } }

    // The player won a tournament. The Tournament mode does not exist yet: whoever makes it calls this when a tournament is won (the racket that
    // needs a tournament win, "champion", unlocks and the new racket screen follows).
    public static void ReportTournamentWin()
    {
        Load();
        tournamentWins++;
        Save();
        Evaluate();
    }

    public static int TournamentWins { get { Load(); return tournamentWins; } }

    // Rackets whose rule is met by the saved progress get unlocked.
    static void Evaluate()
    {
        if (catalog == null || catalog.skins == null) return;
        foreach (var s in catalog.skins)
            if (s != null && !IsUnlocked(s.Id) && RuleMet(s.unlock)) Unlock(s.Id);
    }

    static bool RuleMet(UnlockRule rule)
    {
        if (rule == null) return false;
        switch (rule.kind)
        {
            case UnlockKind.Always: return true;
            case UnlockKind.TrainingLevel: return highestLevel >= rule.value;
            case UnlockKind.OnlineWins: return onlineWins >= rule.value;
            case UnlockKind.TournamentWins: return tournamentWins >= rule.value;
            default: return false; // Manual: only Unlock(id)
        }
    }

    public static string EquippedId { get { Load(); return equipped; } }

    public static RacketSkin Equipped
    {
        get
        {
            Load();
            var s = catalog != null ? catalog.Find(equipped) : null;
            return s != null && IsUnlocked(s) ? s : null;
        }
    }

    public static bool Equip(string id)
    {
        Load();
        if (!IsUnlocked(id) || equipped == id) return false;
        equipped = id;
        Save();
        PokiEvents.Track("racket", "equip", "id", id);
        return true;
    }

    static string FirstOwnedId()
    {
        if (catalog != null && catalog.skins != null)
            foreach (var s in catalog.skins) if (s != null && IsUnlocked(s)) return s.Id;
        return "";
    }

    // Unlocked since the last time the Rackets window was opened: the number on the button.
    public static int NewCount
    {
        get
        {
            Load();
            int n = 0;
            foreach (var id in unlocked) if (!seen.Contains(id)) n++;
            return n;
        }
    }

    public static bool IsNew(string id) { Load(); return unlocked.Contains(id) && !seen.Contains(id); }

    public static void MarkAllSeen()
    {
        Load();
        int before = seen.Count;
        foreach (var id in unlocked) seen.Add(id);
        if (seen.Count != before) Save();
    }

    // For tests: forget everything.
    public static void ResetAll()
    {
        unlocked.Clear(); seen.Clear(); equipped = ""; highestLevel = 1; onlineWins = 0; tournamentWins = 0; loaded = true;
        Save();
        if (catalog != null) { Evaluate(); equipped = FirstOwnedId(); Save(); }
    }

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            Fill(unlocked, PlayerPrefs.GetString("racketUnlocked", ""));
            Fill(seen, PlayerPrefs.GetString("racketSeen", ""));
            equipped = PlayerPrefs.GetString("racketEquipped", "");
            highestLevel = Mathf.Max(1, PlayerPrefs.GetInt("racketHighestLevel", 1));
            onlineWins = Mathf.Max(0, PlayerPrefs.GetInt("racketOnlineWins", 0));
            tournamentWins = Mathf.Max(0, PlayerPrefs.GetInt("racketTournamentWins", 0));
        }
        catch { }
    }

    static void Fill(HashSet<string> set, string csv)
    {
        set.Clear();
        foreach (var id in csv.Split(',')) if (id.Length > 0) set.Add(id);
    }

    static void Save()
    {
        try
        {
            PlayerPrefs.SetString("racketUnlocked", string.Join(",", unlocked));
            PlayerPrefs.SetString("racketSeen", string.Join(",", seen));
            PlayerPrefs.SetString("racketEquipped", equipped);
            PlayerPrefs.SetInt("racketHighestLevel", highestLevel);
            PlayerPrefs.SetInt("racketOnlineWins", onlineWins);
            PlayerPrefs.SetInt("racketTournamentWins", tournamentWins);
            PlayerPrefs.Save();
        }
        catch { }
        Changed?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { loaded = false; catalog = null; Unlocked = null; Changed = null; unlocked.Clear(); seen.Clear(); onlineWins = 0; tournamentWins = 0; } // static state survives a Play without domain reload
}
