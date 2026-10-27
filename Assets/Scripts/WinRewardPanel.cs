using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The "MATCH REWARD" panel of the win screen: the Rally coin, the total counting up, and one row per bonus that was earned (each pops in after the
// other). The panel is as tall as its rows need (Height); WinScreenLayout fits the whole screen around it, so any number of bonuses fits.
// Everything is scene content (object "RewardPanel" under the win title); this script fills it and animates it.
[DefaultExecutionOrder(100)] // after WinScreenLayout (it places the panel and the coin; the glow follows them)
public class WinRewardPanel : MonoBehaviour
{
    [Header("Parts")]
    public RectTransform coin;
    [Tooltip("Golden rays behind the coin; they turn slowly.")]
    public RectTransform glow;
    [Tooltip("Degrees per second the glow turns (negative = the other way).")]
    public float glowSpeed = 24f;
    public TMP_Text ribbonText;
    public TMP_Text amountText;
    public TMP_Text coinsLabel;
    [Tooltip("The dark box with the bonus rows (anchored to the bottom of the panel; its height follows the rows).")]
    public RectTransform listBox;
    [Tooltip("A row to copy (kept inactive): label on the left, value on the right, a thin line under it.")]
    public RectTransform rowTemplate;

    [Header("Double the coins (rewarded ad)")]
    [Tooltip("\"+N BONUS\" with a film icon: watching an ad gives the match's coins again. Part of the win screen layout, under the panel.")]
    public Button doubleButton;
    public TMP_Text doubleLabel;
    public event System.Action DoubleClicked;
    public bool DoubleShown => doubleButton != null && doubleButton.gameObject.activeSelf;
    public int Total => total;

    [Header("Sizes (canvas units)")]
    public float headerHeight = 138f;
    public float rowHeight = 32f;
    public float listPadding = 8f;
    public float gap = 12f;
    public float bottomPadding = 14f;

    [Header("Animation (seconds)")]
    public float countDelay = 0.35f;
    public float countTime = 0.9f;
    public float firstRowDelay = 0.4f;
    public float rowInterval = 0.2f;
    public float rowPopTime = 0.3f;
    [Range(0f, 0.15f)] public float coinPulse = 0.05f;
    [Header("Number pulse")]
    [Tooltip("The numbers swell and shrink while they count up (fraction of their size) and pop once when they land on the final value.")]
    [Range(0f, 0.5f)] public float countPulse = 0.12f;
    [Tooltip("Pulses per second while counting.")]
    public float countPulseSpeed = 7f;
    [Range(0f, 0.8f)] public float landingPop = 0.3f;
    [Tooltip("Seconds the landing pop takes to settle.")]
    public float landingPopTime = 0.35f;
    [Tooltip("Seconds each bonus value takes to count up from its pop-in.")]
    public float rowCountTime = 0.5f;

    public float Height { get; private set; } = 212f; // set by Show (the panel may be shown before it is ever active, so Awake must not touch it)

    readonly List<RectTransform> rows = new List<RectTransform>();
    readonly List<TMP_Text> rowValues = new List<TMP_Text>();
    readonly List<int> rowAmounts = new List<int>();
    int total, countFrom;
    float shownAt, amountAt;
    bool running;

    float HeightFor(int rowCount) { return headerHeight + gap + 2f * listPadding + rowCount * rowHeight + bottomPadding; }

    void Awake()
    {
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        if (doubleButton != null) { doubleButton.onClick.RemoveAllListeners(); doubleButton.onClick.AddListener(() => DoubleClicked?.Invoke()); doubleButton.gameObject.SetActive(false); }
    }

    // The ad was watched: the coins are given again; the number counts up to the new total and the button goes away.
    public void Double()
    {
        countFrom = total;
        total += total;
        amountAt = Time.unscaledTime - countDelay; // the amount counts again from where it was
        SetDoubleAvailable(false);
    }

    public void SetDoubleAvailable(bool available)
    {
        if (doubleButton == null || doubleButton.gameObject.activeSelf == available) return;
        doubleButton.gameObject.SetActive(available);
        if (available && doubleLabel != null) doubleLabel.text = "+" + total + " " + I18n.Tr("BONUS");
    }

    public void SetDoubleInteractable(bool interactable) { if (doubleButton != null) doubleButton.interactable = interactable; }

    // The lines are translated here (their label is the English text).
    public void Show(List<RewardLine> lines, int totalCoins)
    {
        ClearRows();
        total = totalCoins;
        countFrom = 0;
        int n = Mathf.Max(1, lines.Count);
        Height = HeightFor(n);
        var rt = (RectTransform)transform;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, Height);
        if (listBox != null) { listBox.anchorMin = new Vector2(0f, 0f); listBox.anchorMax = new Vector2(1f, 0f); listBox.pivot = new Vector2(0.5f, 0f); listBox.anchoredPosition = new Vector2(0f, bottomPadding); listBox.sizeDelta = new Vector2(-2f * bottomPadding, 2f * listPadding + n * rowHeight); }
        for (int i = 0; i < lines.Count; i++)
        {
            var row = Instantiate(rowTemplate, listBox);
            row.gameObject.SetActive(true);
            row.name = "Row";
            row.anchorMin = new Vector2(0f, 1f); row.anchorMax = new Vector2(1f, 1f); row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(-2f * listPadding - 8f, rowHeight);
            row.anchoredPosition = new Vector2(0f, -(listPadding + i * rowHeight));
            var texts = row.GetComponentsInChildren<TMP_Text>(true);
            texts[0].text = I18n.Tr(lines[i].label);
            texts[1].text = "+0";
            rowValues.Add(texts[1]);
            rowAmounts.Add(lines[i].amount);
            var line = row.Find("Line");
            if (line != null) line.gameObject.SetActive(i < lines.Count - 1);
            var group = row.GetComponent<CanvasGroup>();
            if (group == null) group = row.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            row.localScale = Vector3.zero;
            rows.Add(row);
        }
        if (ribbonText != null) ribbonText.text = I18n.Tr("MATCH REWARD");
        if (coinsLabel != null) coinsLabel.text = I18n.Tr("RALLY COINS");
        if (amountText != null) amountText.text = "+0";
        shownAt = amountAt = Time.unscaledTime;
        running = true;
        SetDoubleAvailable(totalCoins > 0);
    }

    // Seconds after Show until the window has finished (coins counted, every row in place).
    public float OpenDuration
    {
        get
        {
            float rowsEnd = rows.Count > 0 ? firstRowDelay + (rows.Count - 1) * rowInterval + rowCountTime : 0f;
            return Mathf.Max(countDelay + countTime + landingPopTime * 0.5f, rowsEnd);
        }
    }

    void ClearRows()
    {
        foreach (var r in rows) if (r != null) { r.gameObject.SetActive(false); Destroy(r.gameObject); }
        rows.Clear();
        rowValues.Clear();
        rowAmounts.Clear();
        running = false;
    }

    void Update()
    {
        if (!running) return;
        float t = Time.unscaledTime - shownAt;
        if (amountText != null)
        {
            float ta = Time.unscaledTime - amountAt;
            float k = Mathf.Clamp01((ta - countDelay) / Mathf.Max(0.05f, countTime));
            amountText.text = "+" + Mathf.RoundToInt(countFrom + (total - countFrom) * (1f - Mathf.Pow(1f - k, 3f)));
            amountText.transform.localScale = Vector3.one * CountScale(ta - countDelay, countTime);
        }
        for (int i = 0; i < rows.Count; i++)
        {
            float local = t - firstRowDelay - i * rowInterval;
            float k = Mathf.Clamp01(local / Mathf.Max(0.05f, rowPopTime));
            rows[i].localScale = Vector3.one * (1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f)); // ease-out-back
            rows[i].GetComponent<CanvasGroup>().alpha = Mathf.Clamp01(k * 3f);
            float c = Mathf.Clamp01(local / Mathf.Max(0.05f, rowCountTime));
            rowValues[i].text = "+" + Mathf.RoundToInt(rowAmounts[i] * (1f - Mathf.Pow(1f - c, 3f)));
            rowValues[i].transform.localScale = Vector3.one * CountScale(local, rowCountTime);
        }
        if (coin != null) coin.localScale = Vector3.one * (1f + coinPulse * Mathf.Sin(Time.unscaledTime * 3.2f));
    }

    // After the layout of the screen has placed the panel and the coin: the glow is centred on the coin and turns.
    void LateUpdate()
    {
        if (glow == null) return;
        if (coin != null) glow.position = coin.position; // whatever the width of the panel (portrait / landscape)
        glow.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * glowSpeed);
    }

    // Size of a number `time` seconds after it started counting for `duration`: a steady pulse while it counts, a pop when it lands.
    float CountScale(float time, float duration)
    {
        if (time <= 0f) return 1f;
        if (time < duration) return 1f + countPulse * (0.5f - 0.5f * Mathf.Cos(time * countPulseSpeed * Mathf.PI * 2f));
        float after = (time - duration) / Mathf.Max(0.05f, landingPopTime);
        return after >= 1f ? 1f : 1f + landingPop * Mathf.Sin(after * Mathf.PI) * (1f - after);
    }

    public void Hide() { ClearRows(); SetDoubleAvailable(false); }
}
