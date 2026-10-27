using UnityEngine;
using UnityEngine.UI;
using TMPro;

// HUD: result board (player points, CPU points, match time), info line and centre banner. The Canvas and everything on it live in the
// scene (object "HUD"); edit font, size, colour and position directly on them. This script only sets the text.
// Text is only touched when it changes, because the game logic rewrites the banner every frame.
public class HudView : MonoBehaviour
{
    [Header("Result board (one Text per digit; the board image has the dim digits and the colons)")]
    public GameObject board;
    [Tooltip("Hangs the board from two chains and animates it in and out through the top of the screen. When empty the board is switched on / off.")]
    public HangingSign scoreboardSign;
    [Tooltip("Player points: tens, units.")]
    public TMP_Text[] playerDigits = new TMP_Text[2];
    [Tooltip("CPU points: tens, units.")]
    public TMP_Text[] npcDigits = new TMP_Text[2];
    [Tooltip("Names above the two scores on the board.")]
    public TMP_Text playerNameLabel;
    public TMP_Text npcNameLabel;
    [Tooltip("Match time as HH:MM:SS: hours tens, hours units, minutes tens, minutes units, seconds tens, seconds units.")]
    public TMP_Text[] timeDigits = new TMP_Text[6];

    [Header("Lose screen buttons (Restart = standard, Retry = rewarded ad)")]
    public GameObject loseButtons;
    public Button restartButton;
    public Button retryButton;
    public TMP_Text restartLabel;
    public TMP_Text retryLabel;
    [Tooltip("BACK on the lose screen: to the main menu.")]
    public Button loseBackButton;
    public TMP_Text loseBackLabel;
    public event System.Action BackClicked;
    public event System.Action RestartClicked;
    public event System.Action RetryClicked;

    [Header("Win title (big two-line text shown when the match is won)")]
    public GameObject winTitle;
    public TMP_Text winLine1;     // white
    public TMP_Text winLine2;     // yellow
    [Tooltip("CONTINUE: starts the next level. Part of the same centred group, below the title.")]
    public Button winContinueButton;
    public TMP_Text winContinueLabel;
    [Tooltip("BACK on the win screen: to the main menu.")]
    public Button winBackButton;
    public TMP_Text winBackLabel;
    public event System.Action ContinueClicked;
    [Tooltip("Rotating sunburst behind the whole win screen (RawImage with the RaysBackground shader); fades in with the title.")]
    public RawImage winBackground;
    [Tooltip("Seconds of the pop-in animation.")]
    public float winPopTime = 0.45f;
    [Tooltip("Seconds the backgrounds (win and lose) take to fade in.")]
    public float winBackgroundFade = 0.35f;

    [Header("Lose title (\"YOU LOSE\": white, then red; same look and pop-in as the win title)")]
    public GameObject loseTitle;
    public TMP_Text loseLine1;
    public TMP_Text loseLine2;

    [Header("Lose screen background")]
    [Tooltip("The same rotating sunburst in tones of red, without the flying dashes; shown with the lose buttons.")]
    public RawImage loseBackground;

    [Header("Text")]
    public TMP_Text infoText;
    public TMP_Text bannerText;

    string info = "", banner = "";
    int player = -1, npc = -1, shownSeconds = -1;

    void Awake()
    {
        if (restartButton != null) { restartButton.onClick.RemoveAllListeners(); restartButton.onClick.AddListener(() => RestartClicked?.Invoke()); }
        if (retryButton != null) { retryButton.onClick.RemoveAllListeners(); retryButton.onClick.AddListener(() => RetryClicked?.Invoke()); }
        if (loseBackButton != null) { loseBackButton.onClick.RemoveAllListeners(); loseBackButton.onClick.AddListener(() => BackClicked?.Invoke()); }
        if (winBackButton != null) { winBackButton.onClick.RemoveAllListeners(); winBackButton.onClick.AddListener(() => BackClicked?.Invoke()); }
        if (winContinueButton != null) { winContinueButton.onClick.RemoveAllListeners(); winContinueButton.onClick.AddListener(() => ContinueClicked?.Invoke()); }
        SetLoseButtons(false, false);
        Apply(infoText, info);
        Apply(bannerText, banner);
        SetScoreboard(HangingSign.SignState.Hidden);
    }

    // Where the board hangs (see HangingSign): Hidden in the menus, Peek (only the clock shows) during the rounds, Shown for a point or a result.
    public void SetScoreboard(HangingSign.SignState state)
    {
        if (scoreboardSign != null) scoreboardSign.SetState(state);
        else if (board != null) board.SetActive(state != HangingSign.SignState.Hidden);
        if (infoText != null) infoText.enabled = state == HangingSign.SignState.Shown;
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
    static void SetDigits(TMP_Text[] digits, int start, int value, int count)
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
        if (loseButtons != null && loseButtons.activeSelf != visible)
        {
            loseButtons.SetActive(visible);
            if (loseBackground != null)
            {
                loseBackground.gameObject.SetActive(visible);
                var c = loseBackground.color; c.a = 0f; loseBackground.color = c; // fades in from nothing
            }
        }
        if (restartButton != null) restartButton.interactable = interactable;
        if (retryButton != null) retryButton.interactable = interactable;
        if (loseBackButton != null) loseBackButton.interactable = interactable;
    }

    // Level 1 has nothing to retry (RETRY replays the current level, RESTART starts over): the button goes away and BACK takes its place.
    float backY = float.NaN, retryY;
    public void SetRetryVisible(bool visible)
    {
        if (retryButton == null || loseBackButton == null) return;
        var back = loseBackButton.GetComponent<RectTransform>();
        var retry = retryButton.GetComponent<RectTransform>();
        if (float.IsNaN(backY)) { backY = back.anchoredPosition.y; retryY = retry.anchoredPosition.y; }
        if (retryButton.gameObject.activeSelf != visible) retryButton.gameObject.SetActive(visible);
        var p = back.anchoredPosition; p.y = visible ? backY : retryY; back.anchoredPosition = p;
    }

    public void SetLoseLabels(string restart, string retry)
    {
        Apply(restartLabel, restart);
        Apply(retryLabel, retry);
    }

    float winShownAt = -10f, loseShownAt = -10f;

    // The big "YOU LOSE" title: the same pop-in as the win title.
    public void SetLoseTitle(bool visible)
    {
        if (loseTitle == null || loseTitle.activeSelf == visible) return;
        loseTitle.SetActive(visible);
        if (visible) { loseShownAt = Time.unscaledTime; loseTitle.transform.localScale = Vector3.zero; }
    }

    public void SetLoseTitleLabels(string line1, string line2)
    {
        Apply(loseLine1, line1);
        Apply(loseLine2, line2);
    }

    // Ease-out-back: overshoots a little, then settles.
    void PopIn(GameObject title, float shownAt)
    {
        if (title == null || !title.activeSelf) return;
        float k = Mathf.Clamp01((Time.unscaledTime - shownAt) / Mathf.Max(0.05f, winPopTime));
        float back = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
        title.transform.localScale = Vector3.one * back;
    }

    // The big "YOU WON" title: pops in (with a small overshoot) when it appears.
    public void SetWinTitle(bool visible)
    {
        if (winTitle == null || winTitle.activeSelf == visible) return;
        winTitle.SetActive(visible);
        if (winBackground != null)
        {
            winBackground.gameObject.SetActive(visible);
            var c = winBackground.color; c.a = 0f; winBackground.color = c; // fades in from nothing
        }
        if (visible) { winShownAt = Time.unscaledTime; winTitle.transform.localScale = Vector3.zero; }
    }

    public void SetNames(string player, string npc)
    {
        Apply(playerNameLabel, player);
        Apply(npcNameLabel, npc);
    }

    public void SetWinLabels(string line1, string line2, string continueText)
    {
        Apply(winLine1, line1);
        Apply(winLine2, line2);
        Apply(winContinueLabel, continueText);
    }

    // CONTINUE is clickable only when `interactable` (not during the first moments, not during an ad).
    public void SetWinContinueInteractable(bool interactable)
    {
        if (winContinueButton != null) winContinueButton.interactable = interactable;
        if (winBackButton != null) winBackButton.interactable = interactable;
    }

    public void SetBackLabels(string back)
    {
        Apply(winBackLabel, back);
        Apply(loseBackLabel, back);
    }

    void Update()
    {
        FadeIn(winBackground);
        FadeIn(loseBackground);
        PopIn(winTitle, winShownAt);
        PopIn(loseTitle, loseShownAt);
    }

    void FadeIn(RawImage background)
    {
        if (background == null || !background.gameObject.activeSelf) return;
        var c = background.color;
        c.a = Mathf.MoveTowards(c.a, 1f, Time.unscaledDeltaTime / Mathf.Max(0.05f, winBackgroundFade));
        background.color = c;
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

    static void Apply(TMP_Text t, string value)
    {
        if (t != null) t.text = value;
    }
}
