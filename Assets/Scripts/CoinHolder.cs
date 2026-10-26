using System.Globalization;
using TMPro;
using UnityEngine;

// Shows the player's Rally coins (the pill with the coin). When the amount changes the number counts to the new value and swells a little.
// Everything is scene content; this only writes the number.
public class CoinHolder : MonoBehaviour
{
    public TMP_Text amountText;
    [Tooltip("Seconds the number takes to reach a new amount.")]
    public float countTime = 0.6f;
    [Range(0f, 0.4f)] public float swell = 0.15f;

    float shown, from;
    int target;
    float changedAt = -10f;

    void OnEnable()
    {
        target = RallyCoins.Balance;
        shown = from = target;
        changedAt = -10f;
        RallyCoins.Changed += OnChanged;
        Apply();
    }

    void OnDisable() { RallyCoins.Changed -= OnChanged; }

    void OnChanged(int balance)
    {
        from = shown;
        target = balance;
        changedAt = Time.unscaledTime;
    }

    void Update()
    {
        float t = Time.unscaledTime - changedAt;
        if (t < countTime + 0.3f)
        {
            float k = Mathf.Clamp01(t / Mathf.Max(0.05f, countTime));
            shown = Mathf.Lerp(from, target, 1f - Mathf.Pow(1f - k, 3f));
            if (amountText != null) amountText.transform.localScale = Vector3.one * (1f + swell * Mathf.Sin(Mathf.Clamp01(t / (countTime + 0.3f)) * Mathf.PI));
            Apply();
        }
        else if (shown != target) { shown = target; Apply(); if (amountText != null) amountText.transform.localScale = Vector3.one; }
    }

    void Apply()
    {
        if (amountText != null) amountText.text = Mathf.RoundToInt(shown).ToString("N0", CultureInfo.InvariantCulture);
    }
}
