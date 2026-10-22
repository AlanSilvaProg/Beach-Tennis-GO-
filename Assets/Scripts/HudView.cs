using UnityEngine;
using UnityEngine.UI;

// HUD: result board (player points, CPU points, match time), info line and centre banner. The Canvas and everything on it live in the
// scene (object "HUD"); edit font, size, colour and position directly on them. This script only sets the text.
// Text is only touched when it changes, because the game logic rewrites the banner every frame.
public class HudView : MonoBehaviour
{
    [Header("Result board (one Text per digit; the board image has the dim digits and the colons)")]
    public GameObject board;
    [Tooltip("Player points: tens, units.")]
    public Text[] playerDigits = new Text[2];
    [Tooltip("CPU points: tens, units.")]
    public Text[] npcDigits = new Text[2];
    [Tooltip("Match time as HH:MM:SS: hours tens, hours units, minutes tens, minutes units, seconds tens, seconds units.")]
    public Text[] timeDigits = new Text[6];

    [Header("Lose screen buttons (Restart = standard, Retry = rewarded ad)")]
    public GameObject loseButtons;
    public Button restartButton;
    public Button retryButton;
    public Text restartLabel;
    public Text retryLabel;
    public event System.Action RestartClicked;
    public event System.Action RetryClicked;

    [Header("Text")]
    public Text infoText;
    public Text bannerText;

    string info = "", banner = "";
    int player = -1, npc = -1, shownSeconds = -1;
    bool showScore;

    void Awake()
    {
        if (restartButton != null) { restartButton.onClick.RemoveAllListeners(); restartButton.onClick.AddListener(() => RestartClicked?.Invoke()); }
        if (retryButton != null) { retryButton.onClick.RemoveAllListeners(); retryButton.onClick.AddListener(() => RetryClicked?.Invoke()); }
        SetLoseButtons(false, false);
        Apply(infoText, info);
        Apply(bannerText, banner);
        SetScoreVisible(showScore);
    }

    public bool ShowScore
    {
        get => showScore;
        set
        {
            if (showScore == value) return;
            showScore = value;
            SetScoreVisible(value);
        }
    }

    public void SetScore(int playerPoints, int npcPoints)
    {
        if (player == playerPoints && npc == npcPoints) return;
        player = playerPoints;
        npc = npcPoints;
        SetDigits(playerDigits, 0, playerPoints % 100, 2);
        SetDigits(npcDigits, 0, npcPoints % 100, 2);
    }

    // Match duration, shown as hours : minutes : seconds.
    public void SetTime(float seconds)
    {
        int total = Mathf.Max(0, (int)seconds);
        if (total == shownSeconds) return;
        shownSeconds = total;
        SetDigits(timeDigits, 0, total / 3600 % 100, 2);
        SetDigits(timeDigits, 2, total / 60 % 60, 2);
        SetDigits(timeDigits, 4, total % 60, 2);
    }

    // Writes `value` (zero padded to `count` digits) into digits[start ...], one character per Text.
    static void SetDigits(Text[] digits, int start, int value, int count)
    {
        if (digits == null) return;
        for (int i = 0; i < count; i++)
        {
            int index = start + i;
            if (index >= digits.Length || digits[index] == null) continue;
            int digit = value / (int)Mathf.Pow(10, count - 1 - i) % 10;
            digits[index].text = digit.ToString();
        }
    }

    // The two buttons of the lose screen: shown only when `visible`, clickable only when `interactable` (not during the first moments, not during an ad).
    public void SetLoseButtons(bool visible, bool interactable)
    {
        if (loseButtons != null && loseButtons.activeSelf != visible) loseButtons.SetActive(visible);
        if (restartButton != null) restartButton.interactable = interactable;
        if (retryButton != null) retryButton.interactable = interactable;
    }

    public void SetLoseLabels(string restart, string retry)
    {
        Apply(restartLabel, restart);
        Apply(retryLabel, retry);
    }

    public string Info
    {
        get => info;
        set { if (info == value) return; info = value; Apply(infoText, value); }
    }

    public string Banner
    {
        get => banner;
        set { if (banner == value) return; banner = value; Apply(bannerText, value); }
    }

    void SetScoreVisible(bool visible)
    {
        if (board != null) board.SetActive(visible);
        if (infoText != null) infoText.enabled = visible;
    }

    static void Apply(Text t, string value)
    {
        if (t != null) t.text = value;
    }
}
