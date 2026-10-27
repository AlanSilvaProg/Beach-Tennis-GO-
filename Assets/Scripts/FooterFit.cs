using UnityEngine;

// The footer of the Rackets window (BACK on the left, the Rally coins pill on the right): on a narrow screen the pill shrinks first and then BACK
// gets narrower, so they never overlap.
public class FooterFit : MonoBehaviour
{
    public RectTransform back, coins;
    public float minBackWidth = 100f, gap = 10f;
    [Range(0.5f, 1f)] public float minCoinScale = 0.75f;

    float backWidth, coinsWidth, backX, coinsMargin, lastWidth = -1f;

    void Awake()
    {
        if (back == null || coins == null) return;
        backWidth = back.sizeDelta.x; coinsWidth = coins.sizeDelta.x;
        backX = back.anchoredPosition.x; coinsMargin = -coins.anchoredPosition.x;
    }

    void LateUpdate()
    {
        if (back == null || coins == null) return;
        float w = ((RectTransform)transform).rect.width;
        if (Mathf.Approximately(w, lastWidth)) return;
        lastWidth = w;
        float avail = w - backX - coinsMargin - gap;
        float scale = Mathf.Clamp((avail - backWidth) / coinsWidth, minCoinScale, 1f);
        coins.localScale = Vector3.one * scale;
        back.sizeDelta = new Vector2(Mathf.Clamp(avail - coinsWidth * scale, minBackWidth, backWidth), back.sizeDelta.y);
    }
}
