using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The stars of a Training match, in a row at the top of the screen: one empty (outlined) star for every point needed to win. A point for the
// player fills the next star (a bouncy wobble and a burst of sparkles); a point for the CPU empties the last filled star and shakes the row.
// The row is scene content ("StarMeter"); the template star (kept inactive) has an "Outline" Image and a "Fill" Image on top of it.
public class StarMeter : MonoBehaviour
{
    public RectTransform row;
    public RectTransform starTemplate;
    public UiSparkles sparkles;
    [Header("Size (canvas units)")]
    public float maxSize = 58f;
    public float minSize = 20f;
    [Tooltip("Free space kept at both sides of the screen.")]
    public float sideMargin = 20f;
    [Tooltip("Distance between two stars as a multiple of their size.")]
    public float spacing = 1.18f;

    class Star { public RectTransform rt, fill; public float fillAmount, punchAt = -10f, emptyAt = -10f; public bool filled; }
    readonly List<Star> stars = new List<Star>();
    int total;
    float shakeAt = -10f, shakeStrength = 1f, laidOutWidth = -1f;
    bool visible;

    void Awake()
    {
        if (starTemplate != null) starTemplate.gameObject.SetActive(false);
        ClearRow();
        SetVisible(false);
    }

    // Only the template lives in the scene: stars left there by an editing session would be extra stars.
    void ClearRow()
    {
        if (row == null) return;
        for (int i = row.childCount - 1; i >= 0; i--)
            if (row.GetChild(i) != starTemplate) Destroy(row.GetChild(i).gameObject);
    }

    public void SetVisible(bool on)
    {
        if (visible == on && row != null && row.gameObject.activeSelf == on) return;
        visible = on;
        if (!on && sparkles != null) sparkles.Clear();
        if (row != null) row.gameObject.SetActive(on);
    }

    // A new match: `count` empty stars.
    public void Setup(int count)
    {
        foreach (var s in stars) if (s.rt != null) { s.rt.gameObject.SetActive(false); Destroy(s.rt.gameObject); }
        stars.Clear();
        ClearRow();
        total = count;
        for (int i = 0; i < count; i++)
        {
            var rt = Instantiate(starTemplate, row);
            rt.gameObject.SetActive(true);
            rt.name = "Star";
            var fill = rt.Find("Fill") as RectTransform;
            if (fill != null) fill.localScale = Vector3.zero;
            stars.Add(new Star { rt = rt, fill = fill });
        }
        laidOutWidth = -1f;
        LayOut();
    }

    void LayOut()
    {
        if (row == null || stars.Count == 0) return;
        float canvasW = ((RectTransform)row.parent).rect.width;
        laidOutWidth = canvasW;
        float size = Mathf.Clamp((canvasW - 2f * sideMargin) / (total * spacing), minSize, maxSize);
        float pitch = size * spacing;
        row.sizeDelta = new Vector2(pitch * total, size);
        for (int i = 0; i < stars.Count; i++)
        {
            stars[i].rt.sizeDelta = new Vector2(size, size);
            stars[i].rt.anchoredPosition = new Vector2((i - (total - 1) / 2f) * pitch, 0f);
        }
    }

    // The player scored: `filledNow` stars are full, the last one just got filled.
    public void Gain(int filledNow)
    {
        int i = filledNow - 1;
        if (i < 0 || i >= stars.Count) return;
        stars[i].filled = true;
        stars[i].punchAt = Time.unscaledTime;
        if (sparkles != null) sparkles.Burst(stars[i].rt.position, stars[i].rt.sizeDelta.x / 56f);
    }

    // The CPU scored: with a filled star, the last one empties (`filledNow` are left) and the row shakes; without one, only a lighter shake.
    public void Lose(int filledNow, bool hadStar)
    {
        if (hadStar && filledNow >= 0 && filledNow < stars.Count) { stars[filledNow].filled = false; stars[filledNow].emptyAt = Time.unscaledTime; }
        shakeAt = Time.unscaledTime;
        shakeStrength = hadStar ? 1f : 0.45f;
    }

    void Update()
    {
        if (row == null || !row.gameObject.activeInHierarchy) return;
        if (!Mathf.Approximately(laidOutWidth, ((RectTransform)row.parent).rect.width)) LayOut();
        float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        float shakeT = now - shakeAt, shake = shakeT < 0.8f ? Mathf.Exp(-shakeT * 5f) * shakeStrength : 0f;
        for (int i = 0; i < stars.Count; i++)
        {
            var s = stars[i];
            s.fillAmount = Mathf.MoveTowards(s.fillAmount, s.filled ? 1f : 0f, dt * (s.filled ? 7f : 5f));
            float t = now - s.punchAt, e = Mathf.Exp(-t * 6f);
            float punch = t < 1f ? 0.6f * e * Mathf.Abs(Mathf.Cos(t * 14f)) : 0f;
            float wobble = t < 1f ? 22f * e * Mathf.Sin(t * 26f) : 0f;
            float lost = now - s.emptyAt;
            float lostWobble = lost < 0.8f ? 25f * Mathf.Exp(-lost * 6f) * Mathf.Sin(lost * 30f) : 0f;
            if (s.fill != null)
            {
                s.fill.localScale = Vector3.one * s.fillAmount;
                s.fill.localRotation = Quaternion.Euler(0f, 0f, s.filled ? 0f : (1f - s.fillAmount) * 120f);
            }
            float sh = shake * Mathf.Sin(shakeT * 38f + i * 0.9f);
            s.rt.localScale = Vector3.one * (1f + punch);
            s.rt.localRotation = Quaternion.Euler(0f, 0f, wobble + lostWobble + sh * 16f);
        }
    }
}
