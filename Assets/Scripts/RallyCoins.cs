using System;
using UnityEngine;

// The player's Rally coins, kept between sessions (PlayerPrefs, always inside try/catch: they can be unavailable, e.g. in a private window).
public static class RallyCoins
{
    static int balance;
    static bool loaded;

    public static event Action<int> Changed;

    public static int Balance { get { Load(); return balance; } }

    public static void Add(int amount)
    {
        if (amount <= 0) return;
        Load();
        balance += amount;
        try { PlayerPrefs.SetInt("rallyCoins", balance); PlayerPrefs.Save(); } catch { }
        PokiEvents.Track("coins", "earn", "amount", amount, "balance", balance);
        Changed?.Invoke(balance);
    }

    // Pays `amount` coins if the player has them. Returns false (nothing changes) when there are not enough.
    public static bool TrySpend(int amount)
    {
        Load();
        if (amount < 0 || balance < amount) return false;
        if (amount == 0) return true;
        balance -= amount;
        try { PlayerPrefs.SetInt("rallyCoins", balance); PlayerPrefs.Save(); } catch { }
        PokiEvents.Track("coins", "spend", "amount", amount, "balance", balance);
        Changed?.Invoke(balance);
        return true;
    }

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        try { balance = Mathf.Max(0, PlayerPrefs.GetInt("rallyCoins", 0)); } catch { }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { loaded = false; Changed = null; } // static state survives a Play without domain reload
}
