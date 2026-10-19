using UnityEngine;
using UnityEngine.UI;

// HUD: score, info line and centre banner. The Canvas and its three Text objects live in the scene
// (object "HUD"); edit font, size, colour and position directly on them. This script only sets the text.
// Text is only touched when it changes, because the game logic rewrites the banner every frame.
public class HudView : MonoBehaviour
{
    public Text scoreText;
    public Text infoText;
    public Text bannerText;

    string score = "", info = "", banner = "";
    bool showScore;

    void Awake()
    {
        Apply(scoreText, score);
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

    public string Score
    {
        get => score;
        set { if (score == value) return; score = value; Apply(scoreText, value); }
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
        if (scoreText != null) scoreText.enabled = visible;
        if (infoText != null) infoText.enabled = visible;
    }

    static void Apply(Text t, string value)
    {
        if (t != null) t.text = value;
    }
}
