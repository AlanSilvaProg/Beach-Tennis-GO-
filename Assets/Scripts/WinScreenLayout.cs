using UnityEngine;

// Arranges the win screen for any screen shape: the title ("YOU" / "WON!"), the reward panel and the CONTINUE / BACK buttons.
//   portrait   one column: title, panel, CONTINUE, BACK
//   landscape  two columns: the title on the left, the panel with the buttons under it on the right
// They are laid out in a design size and the whole group is scaled down (never up past Max Scale) to fit between the scoreboard at the top and the
// bottom of the screen, so a tall panel (many bonuses) or a small screen never breaks the layout.
public class WinScreenLayout : MonoBehaviour
{
    [Tooltip("Holds the parts below; this is what is scaled and centred.")]
    public RectTransform stack;
    public RectTransform line1;
    public RectTransform line2;
    public WinRewardPanel panel;
    public RectTransform continueButton;
    public RectTransform backButton;

    [Header("Room")]
    [Tooltip("Space kept free at the top (the hanging scoreboard and the level line) and at the bottom (canvas units).")]
    public float topReserve = 240f;
    public float bottomReserve = 24f;
    public float sideMargin = 16f;
    public float maxScale = 1.1f;
    [Tooltip("Screens at least this wide (width / height) get the two-column layout.")]
    public float landscapeAspect = 1.1f;

    [Header("Portrait (design size, canvas units)")]
    public float portraitWidth = 360f;
    public Vector2 portraitLine1 = new Vector2(360f, 80f);
    public Vector2 portraitLine2 = new Vector2(360f, 100f);
    public Vector2 portraitContinue = new Vector2(180f, 54f);
    public Vector2 portraitBack = new Vector2(130f, 40f);

    [Header("Landscape (design size, canvas units)")]
    public float landscapeTitleWidth = 360f;
    public float landscapePanelWidth = 520f;
    public float columnGap = 40f;
    public Vector2 landscapeLine1 = new Vector2(360f, 110f);
    public Vector2 landscapeLine2 = new Vector2(360f, 130f);
    public Vector2 landscapeContinue = new Vector2(220f, 58f);
    public Vector2 landscapeBack = new Vector2(150f, 42f);

    public float verticalGap = 12f;
    public float buttonGap = 8f;

    Vector2 lastSize;
    float lastPanelHeight = -1f;

    void LateUpdate()
    {
        var rect = ((RectTransform)transform).rect;
        float panelHeight = panel != null ? panel.Height : 0f;
        if (rect.size == lastSize && Mathf.Approximately(panelHeight, lastPanelHeight)) return;
        lastSize = rect.size;
        lastPanelHeight = panelHeight;
        Layout(rect.width, rect.height);
    }

    public void Layout(float w, float h)
    {
        if (stack == null || panel == null || w <= 0f || h <= 0f) return;
        bool landscape = w / h >= landscapeAspect;
        float panelH = panel.Height;
        var panelRt = (RectTransform)panel.transform;
        float designW, designH;

        if (!landscape)
        {
            designW = portraitWidth;
            float titleH = portraitLine1.y + portraitLine2.y - 10f;
            float y = 0f;
            Put(line1, designW / 2f, y, portraitLine1); y += portraitLine1.y - 10f;
            Put(line2, designW / 2f, y, portraitLine2); y = titleH + verticalGap;
            panelRt.sizeDelta = new Vector2(designW, panelH);
            Put(panelRt, designW / 2f, y, new Vector2(designW, panelH)); y += panelH + verticalGap;
            Put(continueButton, designW / 2f, y, portraitContinue); y += portraitContinue.y + buttonGap;
            Put(backButton, designW / 2f, y, portraitBack); y += portraitBack.y;
            designH = y;
            SetStack(designW, designH, w, h);
            return;
        }

        designW = landscapeTitleWidth + columnGap + landscapePanelWidth;
        float rightH = panelH + verticalGap + landscapeContinue.y + buttonGap + landscapeBack.y;
        float leftH = landscapeLine1.y + landscapeLine2.y - 10f;
        designH = Mathf.Max(rightH, leftH);
        float leftX = landscapeTitleWidth / 2f, rightX = landscapeTitleWidth + columnGap + landscapePanelWidth / 2f;
        float ty = (designH - leftH) / 2f;
        Put(line1, leftX, ty, landscapeLine1); ty += landscapeLine1.y - 10f;
        Put(line2, leftX, ty, landscapeLine2);
        float ry = (designH - rightH) / 2f;
        panelRt.sizeDelta = new Vector2(landscapePanelWidth, panelH);
        Put(panelRt, rightX, ry, new Vector2(landscapePanelWidth, panelH)); ry += panelH + verticalGap;
        Put(continueButton, rightX, ry, landscapeContinue); ry += landscapeContinue.y + buttonGap;
        Put(backButton, rightX, ry, landscapeBack);
        SetStack(designW, designH, w, h);
    }

    // `cx` (centre) and `top` are measured from the top left corner of the stack.
    static void Put(RectTransform rt, float cx, float top, Vector2 size)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(cx, -(top + size.y / 2f));
    }

    void SetStack(float designW, float designH, float w, float h)
    {
        float availW = w - 2f * sideMargin;
        float availH = h - topReserve - bottomReserve;
        float scale = Mathf.Min(maxScale, availW / designW, availH / designH);
        stack.anchorMin = stack.anchorMax = new Vector2(0.5f, 0.5f);
        stack.pivot = new Vector2(0.5f, 0.5f);
        stack.sizeDelta = new Vector2(designW, designH);
        stack.localScale = Vector3.one * scale;
        float centreY = (h / 2f - topReserve + (-h / 2f + bottomReserve)) / 2f;
        stack.anchoredPosition = new Vector2(0f, centreY);
    }
}
