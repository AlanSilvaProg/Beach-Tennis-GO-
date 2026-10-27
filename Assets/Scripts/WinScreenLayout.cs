using UnityEngine;

// Arranges the win screen for any screen shape: the title ("YOU" / "WON!"), the reward panel and the CONTINUE / BACK buttons.
//   portrait   one column: title, panel, CONTINUE, BACK
//   landscape  the title on the left (centred vertically on the reward panel), the panel on the right, the buttons under both in the middle of the screen
// They are laid out in a design size and the whole group is scaled down (never up past Max Scale) to fit between the scoreboard at the top and the
// bottom of the screen, so a tall panel (many bonuses) or a small screen never breaks the layout.
public class WinScreenLayout : MonoBehaviour
{
    [Tooltip("Holds the parts below; this is what is scaled and centred.")]
    public RectTransform stack;
    public RectTransform line1;
    public RectTransform line2;
    public WinRewardPanel panel;
    public RectTransform doubleButton;   // "+N BONUS" (rewarded ad); only takes room while it is shown
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
    public Vector2 portraitDouble = new Vector2(230f, 52f);
    public Vector2 portraitContinue = new Vector2(180f, 54f);
    public Vector2 portraitBack = new Vector2(130f, 40f);

    [Header("Landscape (design size, canvas units)")]
    public float landscapeTitleWidth = 360f;
    public float landscapePanelWidth = 520f;
    public float columnGap = 40f;
    public Vector2 landscapeLine1 = new Vector2(360f, 110f);
    public Vector2 landscapeLine2 = new Vector2(360f, 130f);
    public Vector2 landscapeDouble = new Vector2(250f, 56f);
    public Vector2 landscapeContinue = new Vector2(220f, 58f);
    public Vector2 landscapeBack = new Vector2(150f, 42f);

    public float verticalGap = 12f;
    public float buttonGap = 8f;

    Vector2 lastSize;
    float lastPanelHeight = -1f;
    bool lastDouble;

    [Header("Attention")]
    [Tooltip("The title and the bonus button breathe to catch the eye (size change, fraction).")]
    public float pulse = 0.06f;
    public float pulseSpeed = 2.2f;

    void Animate()
    {
        float t = Time.unscaledTime * pulseSpeed;
        if (line1 != null) line1.localScale = Vector3.one * (1f + pulse * Mathf.Sin(t * Mathf.PI * 2f * 0.5f));
        if (line2 != null) line2.localScale = Vector3.one * (1f + pulse * 1.4f * Mathf.Sin(t * Mathf.PI * 2f * 0.5f - 1.1f));
        if (doubleButton != null && doubleButton.gameObject.activeInHierarchy)
        {
            float k = 1f + pulse * 1.6f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 0.6f));
            doubleButton.localScale = Vector3.one * k;
            float wig = Mathf.Max(0f, Mathf.Sin(t * 1.3f)); // a little wiggle now and then
            doubleButton.localRotation = Quaternion.Euler(0f, 0f, 3f * wig * wig * wig * Mathf.Sin(t * 14f));
        }
    }

    void LateUpdate()
    {
        Animate();
        var rect = ((RectTransform)transform).rect;
        float panelHeight = panel != null ? panel.Height : 0f;
        bool dbl = panel != null && panel.DoubleShown;
        if (rect.size == lastSize && Mathf.Approximately(panelHeight, lastPanelHeight) && dbl == lastDouble) return;
        lastDouble = dbl;
        lastSize = rect.size;
        lastPanelHeight = panelHeight;
        Layout(rect.width, rect.height);
    }

    public void Layout(float w, float h)
    {
        if (stack == null || panel == null || w <= 0f || h <= 0f) return;
        bool landscape = w / h >= landscapeAspect;
        float panelH = panel.Height;
        bool dbl = panel.DoubleShown;
        var panelRt = (RectTransform)panel.transform;
        float designW, designH;

        if (!landscape)
        {
            // the panel and the buttons sit at the bottom; the title is centred in the free space between the trophies (top) and the panel
            designW = portraitWidth;
            float titleH = portraitLine1.y + portraitLine2.y - 10f;
            float y = 0f;
            panelRt.sizeDelta = new Vector2(designW, panelH);
            Put(panelRt, designW / 2f, y, new Vector2(designW, panelH)); y += panelH + verticalGap;
            if (dbl) { Put(doubleButton, designW / 2f, y, portraitDouble); y += portraitDouble.y + buttonGap; }
            Put(continueButton, designW / 2f, y, portraitContinue); y += portraitContinue.y + buttonGap;
            Put(backButton, designW / 2f, y, portraitBack); y += portraitBack.y;
            designH = y;
            float availW = w - 2f * sideMargin, availH = h - topReserve - bottomReserve;
            float scale = Mathf.Min(maxScale, availW / designW, availH / (designH + titleH));
            stack.anchorMin = stack.anchorMax = new Vector2(0.5f, 0.5f);
            stack.pivot = new Vector2(0.5f, 0.5f);
            stack.sizeDelta = new Vector2(designW, designH);
            stack.localScale = Vector3.one * scale;
            float stackBottom = -h / 2f + bottomReserve, panelTopY = stackBottom + designH * scale;
            stack.anchoredPosition = new Vector2(0f, stackBottom + designH * scale / 2f);
            float gapCentre = (h / 2f - topReserve + panelTopY) / 2f;
            float pTitleTop = -(gapCentre - panelTopY) / scale - titleH / 2f; // above the stack: negative, in stack units
            Put(line1, designW / 2f, pTitleTop, portraitLine1);
            Put(line2, designW / 2f, pTitleTop + portraitLine1.y - 10f, portraitLine2);
            return;
        }

        designW = landscapeTitleWidth + columnGap + landscapePanelWidth;
        float leftH = landscapeLine1.y + landscapeLine2.y - 10f;
        // the title is centred vertically on the reward panel; the buttons are centred on the screen (the middle of the group), under both
        float shift = Mathf.Max(0f, (leftH - panelH) / 2f);          // room above the panel when the title is taller than it
        float panelTop = shift;
        float titleTop = panelTop + (panelH - leftH) / 2f;
        float leftX = landscapeTitleWidth / 2f, rightX = landscapeTitleWidth + columnGap + landscapePanelWidth / 2f, centreX = designW / 2f;
        Put(line1, leftX, titleTop, landscapeLine1);
        Put(line2, leftX, titleTop + landscapeLine1.y - 10f, landscapeLine2);
        panelRt.sizeDelta = new Vector2(landscapePanelWidth, panelH);
        Put(panelRt, rightX, panelTop, new Vector2(landscapePanelWidth, panelH));
        float by = panelTop + panelH + verticalGap;
        if (dbl) { Put(doubleButton, centreX, by, landscapeDouble); by += landscapeDouble.y + buttonGap; }
        Put(continueButton, centreX, by, landscapeContinue); by += landscapeContinue.y + buttonGap;
        Put(backButton, centreX, by, landscapeBack); by += landscapeBack.y;
        designH = Mathf.Max(by, titleTop + leftH);
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
