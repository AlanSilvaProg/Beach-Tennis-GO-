using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The path of trophies on the win screen (Training): one trophy for every match of the level, joined by dotted lines, the number of the match
// under each. The ones already won are filled; the one just won pops in filled with a burst of sparkles. The last trophy of the level has a star;
// winning it completes the level (the caption says so).
// Scene content ("TrophyPath" under the win title); the template (kept inactive) has an "Outline" Image, a "Fill" Image and a "Number" text.
public class TrophyPath : MonoBehaviour
{
    public RectTransform row;
    public RectTransform trophyTemplate;
    public TMP_Text caption;
    public UiSparkles sparkles;
    public Sprite normalOutline, normalFill, finalOutline, finalFill, dot;
    [Header("Size (canvas units)")]
    public float trophySize = 38f;
    [Tooltip("The last trophy (the one that completes the level) is bigger.")]
    public float finalSize = 62f;
    public float pitch = 54f;
    [Tooltip("Free space kept at both sides of the screen.")]
    public float sideMargin = 12f;
    [Header("Animation (seconds)")]
    public float stagger = 0.07f;
    public float winDelay = 0.45f;
    // Raised when the won trophy fills (the sound).
    public System.Action Won;

    class Item { public RectTransform rt, fill; public Image outline, fillImage; public TMP_Text number; public bool final; public List<Image> dotsBefore = new List<Image>(); }
    readonly List<Item> items = new List<Item>();
    readonly List<GameObject> extras = new List<GameObject>();
    int won = -1, total;
    float shownAt;
    bool burst;
    float startAfter;
    float laidOutWidth = -1f;

    void Awake()
    {
        if (trophyTemplate != null) trophyTemplate.gameObject.SetActive(false);
        if (row != null) for (int i = row.childCount - 1; i >= 0; i--) if (row.GetChild(i) != trophyTemplate) Destroy(row.GetChild(i).gameObject); // left by an editing session
    }

    public void Hide() { if (row != null) row.gameObject.SetActive(false); }

    // `stageWon` (0-based) is the trophy of the match that was just won; `count` trophies make a level.
    public void Show(int level, int stageWon, int count, float after = 0f)
    {
        foreach (var it in items) if (it.rt != null) { it.rt.gameObject.SetActive(false); Destroy(it.rt.gameObject); }
        foreach (var g in extras) if (g != null) Destroy(g);
        items.Clear(); extras.Clear();
        total = count; won = stageWon; startAfter = after; burst = false; shownAt = Time.unscaledTime;
        row.gameObject.SetActive(true);
        for (int i = 0; i < count; i++)
        {
            var rt = Instantiate(trophyTemplate, row);
            rt.gameObject.SetActive(true);
            rt.name = "Trophy";
            var it = new Item { rt = rt, final = i == count - 1 };
            var outline = rt.Find("Outline"); var fill = rt.Find("Fill"); var num = rt.Find("Number");
            it.outline = outline != null ? outline.GetComponent<Image>() : null;
            it.fill = fill as RectTransform;
            it.fillImage = fill != null ? fill.GetComponent<Image>() : null;
            it.number = num != null ? num.GetComponent<TMP_Text>() : null;
            if (it.outline != null) it.outline.sprite = it.final ? finalOutline : normalOutline;
            if (it.fillImage != null) it.fillImage.sprite = it.final ? finalFill : normalFill;
            if (it.number != null) it.number.text = (i + 1).ToString();
            items.Add(it);
        }
        // the three dots between two trophies
        for (int i = 1; i < count; i++)
            for (int d = 0; d < 3; d++)
            {
                var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(row, false);
                var img = go.GetComponent<Image>(); img.sprite = dot; img.raycastTarget = false;
                extras.Add(go);
                items[i].dotsBefore.Add(img);
            }
        if (caption != null)
            caption.text = stageWon >= count - 1 ? I18n.Tr("LEVEL {0} COMPLETE!", level) : I18n.Tr("LEVEL {0}", level);
        laidOutWidth = -1f;
        LayOut();
        Tick();
    }

    void LayOut()
    {
        if (items.Count == 0) return;
        float canvasW = ((RectTransform)row.parent).rect.width;
        laidOutWidth = canvasW;
        // centres are `pitch` apart between normal trophies; the big last one takes the extra room
        float gap = pitch - trophySize;
        float fullW = trophySize * (total - 1) + finalSize + gap * (total - 1);
        float k = Mathf.Min(1f, (canvasW - 2f * sideMargin) / fullW);
        float size = trophySize * k, big = finalSize * k, g = gap * k;
        row.sizeDelta = new Vector2(fullW * k, big + 30f);
        float x0 = -fullW * k / 2f, cursor = x0;
        float prevX = 0f, prevHalf = 0f;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            float s = it.final ? big : size;
            float x = cursor + s / 2f;
            cursor += s + g;
            it.rt.anchorMin = it.rt.anchorMax = new Vector2(0.5f, 1f);
            it.rt.pivot = new Vector2(0.5f, 1f);
            it.rt.sizeDelta = new Vector2(s, s);
            it.rt.anchoredPosition = new Vector2(x, it.final ? 0f : -(big - size) / 2f);
            for (int d = 0; d < it.dotsBefore.Count; d++)
            {
                var rt = (RectTransform)it.dotsBefore[d].transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                float dot = 7f * k;
                rt.sizeDelta = new Vector2(dot, dot);
                float a = prevX + prevHalf + g * 0.18f, b = x - s / 2f - g * 0.18f;
                rt.anchoredPosition = new Vector2(a + (b - a) * (d + 0.5f) / 3f, -(big - size) / 2f - size * 0.7f);
            }
            prevX = x; prevHalf = s / 2f;
        }
    }

    void Update() { if (items.Count > 0 && row.gameObject.activeInHierarchy) Tick(); }

    void Tick()
    {
        if (!Mathf.Approximately(laidOutWidth, ((RectTransform)row.parent).rect.width)) LayOut();
        float t = Time.unscaledTime - shownAt;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            float appear = Mathf.Clamp01((t - i * stagger) / 0.3f);
            float pop = 1f + 2.2f * Mathf.Pow(appear - 1f, 3f) + 1.2f * Mathf.Pow(appear - 1f, 2f); // ease-out-back
            float scale = pop;
            float fillAmount = i < won ? 1f : 0f;
            if (i == won)
            {
                float w = t - startAfter - winDelay;
                if (w >= 0f && !burst) { burst = true; Won?.Invoke(); if (sparkles != null) sparkles.Burst(it.rt.position, it.rt.sizeDelta.x / 56f * 1.6f); }
                fillAmount = Mathf.Clamp01(w / 0.18f);
                float e = Mathf.Exp(-Mathf.Max(0f, w) * 5f);
                if (w >= 0f) { scale *= 1f + 0.5f * e * Mathf.Abs(Mathf.Cos(w * 13f)); it.rt.localRotation = Quaternion.Euler(0f, 0f, 16f * e * Mathf.Sin(w * 24f)); }
            }
            it.rt.localScale = Vector3.one * Mathf.Max(0f, scale);
            if (it.fill != null) it.fill.localScale = Vector3.one * fillAmount;
            if (it.number != null) { var c = it.number.color; c.a = i <= won && fillAmount > 0.5f ? 1f : 0.75f; it.number.color = c; }
            foreach (var d in it.dotsBefore)
            {
                var c = Color.white; c.a = (i <= won ? 1f : 0.45f) * appear;
                d.color = c;
            }
        }
    }
}
