using System.Collections.Generic;
using UnityEngine;

// Announcer voice: calls the type of shot after a hit (Smash, Drop shot, Lob, Drive, Acceleration, Block) and the big moments
// (Out, Ace, No-Ad, Match point). Every call has several clips and one is picked at random (never the same twice in a row).
// To use the final voice lines, replace the files in Assets/Audio/Voice (Voice_<Call>_<n>.wav) keeping the names, then run
// Tools > Beach Tennis GO > Add Game Sounds, or drag the clips into the lines below.
// The numbers below are in the game's own units (ball speed before the "ballSpeed" multiplier): turn on logShots to tune them.
public class ShotAnnouncer : MonoBehaviour
{
    public enum Call { Smash, DropShot, Lob, Drive, Acceleration, Block, Out, Ace, NoAd, MatchPoint }

    [System.Serializable]
    public class Line
    {
        public Call call;
        public AudioClip[] clips;
    }

    [Header("Voice")]
    public AudioSource voice;
    public Line[] lines;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Shot calls")]
    [Tooltip("Chance of calling a shot that matches a type.")]
    [Range(0f, 1f)] public float shotCallChance = 0.8f;
    [Tooltip("Minimum time between two shot calls (s), so a long rally is not a constant chatter.")]
    public float shotCallCooldown = 1.5f;
    [Tooltip("Also call the CPU's shots.")]
    public bool callCpuShots = true;
    [Tooltip("Writes the measurements of every hit to the console (to tune the thresholds).")]
    public bool logShots;

    [Header("Shot types")]
    [Tooltip("Smash: contact at least this far above the racket height, going down (or nearly flat), and at least this fast.")]
    public float smashMinAbove = 0.5f;
    public float smashMaxRise = 1.5f;
    public float smashMinSpeed = 12f;
    [Tooltip("Drop shot: lands this close to the net or closer, and is slow.")]
    public float dropMaxDepth = 2.4f;
    public float dropMaxSpeed = 9f;
    [Tooltip("Lob: the ball goes at least this high, lands at least this far from the net, and the opponent is within this distance of the net.")]
    public float lobMinApex = 3.5f;
    public float lobMinDepth = 5.5f;
    public float lobOpponentNetDistance = 4.5f;
    [Tooltip("Acceleration: a very fast shot landing wide (beyond this |x|) or down the middle (within the second value).")]
    public float accelMinSpeed = 17f;
    public float accelSideX = 2.2f;
    public float accelCentreX = 0.9f;
    [Tooltip("Drive: fast and flat, hit around chest/shoulder height (within this of the racket height).")]
    public float driveMinSpeed = 11f;
    public float driveMaxRise = 0.8f;
    public float driveHeightRange = 0.45f;
    [Tooltip("Block: the incoming ball was at least this fast and the return is at most this fraction of it.")]
    public float blockIncomingSpeed = 13f;
    [Range(0.1f, 1f)] public float blockRatio = 0.6f;

    readonly Dictionary<Call, AudioClip[]> clips = new Dictionary<Call, AudioClip[]>();
    readonly Dictionary<Call, int> lastPick = new Dictionary<Call, int>();
    readonly Queue<AudioClip> queue = new Queue<AudioClip>();
    float nextShotCall;

    void Awake()
    {
        if (lines != null)
            foreach (var l in lines)
                if (l != null && l.clips != null && l.clips.Length > 0) clips[l.call] = l.clips;
    }

    void Update()
    {
        if (voice != null && !voice.isPlaying && queue.Count > 0) Play(queue.Dequeue());
    }

    AudioClip Pick(Call call)
    {
        if (!clips.TryGetValue(call, out var list) || list.Length == 0) return null;
        int last = lastPick.TryGetValue(call, out var l) ? l : -1;
        int i = Random.Range(0, list.Length);
        if (list.Length > 1 && i == last) i = (i + 1 + Random.Range(0, list.Length - 1)) % list.Length;
        lastPick[call] = i;
        return list[i];
    }

    void Play(AudioClip clip)
    {
        if (voice == null || clip == null) return;
        voice.clip = clip;
        voice.volume = volume;
        voice.Play();
    }

    // Plays right away, cutting off whatever the announcer was saying.
    public void Say(Call call)
    {
        var clip = Pick(call);
        if (clip == null) return;
        queue.Clear();
        Play(clip);
    }

    // Plays once the announcer has finished what they are saying.
    public void SayNext(Call call)
    {
        var clip = Pick(call);
        if (clip != null) queue.Enqueue(clip);
    }

    // Called by the GameManager for every hit after the serve. `incoming` is the ball velocity just before the racket touched it,
    // `outgoing` the one it leaves with.
    public void OnHit(Ball ball, Court court, Racket hitter, Racket opponent, int side, Vector3 contact, Vector3 incoming, Vector3 outgoing)
    {
        if (side == 1 && !callCpuShots) return;
        bool matched = Classify(ball, court, hitter, opponent, contact, incoming, outgoing, out Call call);
        if (!matched || Time.time < nextShotCall || Random.value > shotCallChance) return;
        nextShotCall = Time.time + shotCallCooldown;
        Say(call);
    }

    bool Classify(Ball ball, Court court, Racket hitter, Racket opponent, Vector3 contact, Vector3 incoming, Vector3 v, out Call call)
    {
        call = Call.Drive;
        float speed = v.magnitude;
        float g = ball.gravity;
        float y0 = court.groundY + ball.radius;
        float disc = v.y * v.y + 2f * g * (contact.y - y0);
        float t = disc >= 0f ? (v.y + Mathf.Sqrt(disc)) / g : 0f;
        Vector2 land = new Vector2(contact.x + v.x * t, contact.z + v.z * t);
        float depth = Mathf.Abs(land.y);
        float apex = contact.y + (v.y > 0f ? v.y * v.y / (2f * g) : 0f);
        float above = contact.y - hitter.height;
        float opponentNet = Mathf.Abs(opponent.Position.z);

        if (logShots)
            Debug.Log($"[ShotAnnouncer] speed {speed:F1} (in {incoming.magnitude:F1}) above {above:F2} rise {apex - contact.y:F2} apex {apex:F2} land x {land.x:F1} depth {depth:F1} opponent {opponentNet:F1}");

        if (incoming.magnitude >= blockIncomingSpeed && speed <= incoming.magnitude * blockRatio) { call = Call.Block; return true; }
        if (above >= smashMinAbove && v.y <= smashMaxRise && speed >= smashMinSpeed) { call = Call.Smash; return true; }
        if (depth <= dropMaxDepth && speed <= dropMaxSpeed) { call = Call.DropShot; return true; }
        if (apex >= lobMinApex && depth >= lobMinDepth && opponentNet <= lobOpponentNetDistance) { call = Call.Lob; return true; }
        if (speed >= accelMinSpeed && (Mathf.Abs(land.x) >= accelSideX || Mathf.Abs(land.x) <= accelCentreX)) { call = Call.Acceleration; return true; }
        if (speed >= driveMinSpeed && apex - contact.y <= driveMaxRise && Mathf.Abs(above) <= driveHeightRange) { call = Call.Drive; return true; }
        return false;
    }
}
