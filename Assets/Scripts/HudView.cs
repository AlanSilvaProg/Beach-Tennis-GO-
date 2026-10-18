using UnityEngine;

// Immediate-mode HUD (no Canvas, cheap for WebGL).
public class HudView : MonoBehaviour
{
    public int scoreFontSize = 56;
    public int infoFontSize = 24;
    public int bannerFontSize = 24;
    public Color textColor = Color.white;
    public Color shadowColor = new Color(0f, 0f, 0f, 0.7f);
    [Tooltip("Banner vertical position as a fraction of the screen height.")]
    [Range(0f, 1f)] public float bannerY = 0.38f;

    public bool ShowScore { get; set; }
    public string Score { get; set; } = "";
    public string Info { get; set; } = "";
    public string Banner { get; set; } = "";

    GUIStyle big, mid;

    void OnGUI()
    {
        float s = Screen.height / 720f;
        if (big == null)
        {
            big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            mid = new GUIStyle(big);
        }
        big.fontSize = Mathf.RoundToInt(scoreFontSize * s);
        mid.fontSize = Mathf.RoundToInt(infoFontSize * s);

        if (ShowScore)
        {
            Draw(new Rect(0, 10 * s, Screen.width, 70 * s), Score, big);
            Draw(new Rect(0, 78 * s, Screen.width, 30 * s), Info, mid);
        }
        if (!string.IsNullOrEmpty(Banner))
        {
            mid.fontSize = Mathf.RoundToInt(bannerFontSize * s);
            Draw(new Rect(0, Screen.height * bannerY, Screen.width, 140 * s), Banner, mid);
        }
    }

    void Draw(Rect r, string text, GUIStyle style)
    {
        style.normal.textColor = shadowColor;
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
        style.normal.textColor = textColor;
        GUI.Label(r, text, style);
    }
}
