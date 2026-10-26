using System;
using System.Collections.Generic;
using UnityEngine;

// What happened in a match, as far as the Rally coin reward cares. Filled by the GameManager while the match is played.
public struct MatchStats
{
    public int level;
    public bool won;
    public int playerPoints, cpuPoints;     // over the whole match (all sets)
    public int playerSets, cpuSets;
    public int lastSetPlayerPoints, lastSetCpuPoints;
    public int perfectSets;                 // sets the player won without the CPU scoring
    public float duration;                  // seconds
    public int longestRally;                // racket hits (serve included) in the longest point
    public int maxDeficit;                  // most points the player was behind in a set
}

[Serializable]
public struct RewardLine
{
    public string label;   // English text, translated when shown (I18n)
    public int amount;
}
