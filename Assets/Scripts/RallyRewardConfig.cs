using System.Collections.Generic;
using UnityEngine;

// The rules of the Rally coin reward of a match and their values (Assets/Data/RallyRewardConfig.asset). A bonus with an amount of 0 is switched off.
// To add a bonus: a field here and a line in Calculate.
[CreateAssetMenu(fileName = "RallyRewardConfig", menuName = "Beach Tennis GO/Rally Reward Config")]
public class RallyRewardConfig : ScriptableObject
{
    [Header("Victory")]
    [Tooltip("Coins for winning a match.")]
    public int victoryBase = 20;
    [Tooltip("Extra coins for every level above the first.")]
    public int victoryPerLevel = 4;

    [Header("Bonuses")]
    [Tooltip("PERFECT MATCH: the CPU did not score a single point in the whole match.")]
    public int perfectMatch = 10;
    [Tooltip("PERFECT SET: coins for every set won without the CPU scoring (only when it is not a perfect match and the match had more than one set).")]
    public int perfectSet = 10;
    [Tooltip("BIG LEAD: the last set was won by at least this many points of difference (not given with a perfect match).")]
    public int bigLeadMargin = 6;
    public int bigLead = 8;
    [Tooltip("LONG MATCH: the match lasted at least this many seconds.")]
    public int longMatchSeconds = 180;
    public int longMatch = 12;
    [Tooltip("PERFECT RALLY: the longest point had at least this many racket hits.")]
    public int perfectRallyHits = 12;
    public int perfectRally = 10;
    [Tooltip("COMEBACK: the player was behind by at least this many points in a set (or lost a set) and still won the match.")]
    public int comebackDeficit = 3;
    public int comeback = 15;

    [Tooltip("PERFECT SHOTS: coins for every perfect shot of the player (a fast one that crosses the whole court), up to the cap.")]
    public int perfectShot = 3;
    public int perfectShotCap = 4;
    [Tooltip("GREAT SHOTS: coins for every very good shot of the player, up to the cap.")]
    public int greatShot = 1;
    public int greatShotCap = 6;

    // The list of lines (victory first) and, in `total`, their sum. A match that was lost pays nothing.
    public List<RewardLine> Calculate(MatchStats s, out int total)
    {
        var lines = new List<RewardLine>();
        total = 0;
        if (!s.won) return lines;
        Add(lines, "VICTORY BONUS", victoryBase + victoryPerLevel * Mathf.Max(0, s.level - 1));
        bool perfect = s.cpuPoints == 0;
        if (perfect) Add(lines, "PERFECT MATCH", perfectMatch);
        else if (s.playerSets + s.cpuSets > 1 && s.perfectSets > 0) Add(lines, "PERFECT SET", perfectSet * s.perfectSets);
        if (!perfect && s.lastSetPlayerPoints - s.lastSetCpuPoints >= bigLeadMargin) Add(lines, "BIG LEAD", bigLead);
        if (s.duration >= longMatchSeconds) Add(lines, "LONG MATCH", longMatch);
        if (s.longestRally >= perfectRallyHits) Add(lines, "PERFECT RALLY", perfectRally);
        if (s.maxDeficit >= comebackDeficit || s.cpuSets > 0) Add(lines, "COMEBACK", comeback);
        Add(lines, "PERFECT SHOTS", perfectShot * Mathf.Min(s.perfectShots, perfectShotCap));
        Add(lines, "GREAT SHOTS", greatShot * Mathf.Min(s.greatShots, greatShotCap));
        foreach (var l in lines) total += l.amount;
        return lines;
    }

    static void Add(List<RewardLine> lines, string label, int amount)
    {
        if (amount > 0) lines.Add(new RewardLine { label = label, amount = amount });
    }
}
