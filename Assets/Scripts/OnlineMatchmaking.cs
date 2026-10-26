using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The "Searching for players" screen of the Online scene: a full-screen window with a Cancel button, a spinning ball and the text with its dots
// (the search itself is the OnlineSession's). When an opponent is found the screen shows "Player found!" and the name for a moment (ShowFound).
// Everything is scene content (object "OnlineMatchmaking" under the HUD canvas). This component sits on an object that is always active.
public class OnlineMatchmaking : MonoBehaviour
{
    [Header("Window")]
    public GameObject window;
    public TMP_Text title;
    [Tooltip("\"Searching for players\" and its dots, which come and go.")]
    public TMP_Text searchText;
    [Tooltip("Turns while the search goes on.")]
    public RectTransform spinner;
    public Button cancelButton;
    public TMP_Text cancelLabel;
    [Tooltip("Shown instead of the search text when a player is found: \"Player found!\" and the name.")]
    public GameObject foundGroup;
    public TMP_Text foundLabel;
    public TMP_Text opponentName;

    [Header("Look")]
    [Tooltip("Seconds the name of the found player stays on screen before the match starts.")]
    public float foundTime = 1.8f;
    [Tooltip("Seconds per step of the dots.")]
    public float dotTime = 0.4f;
    public float spinnerSpeed = 220f;
    [Tooltip("The name pops in with a little overshoot.")]
    public float popTime = 0.4f;

    public bool IsOpen => window != null && window.activeSelf;

    enum Phase { Closed, Searching, Found }
    Phase phase;
    float startedAt, foundAt;
    Action onCancel, onFoundDone;

    void Awake()
    {
        if (cancelButton != null) { cancelButton.onClick.RemoveAllListeners(); cancelButton.onClick.AddListener(Cancel); }
        if (window != null) window.SetActive(false);
    }

    // Shows the search. `canceled` is called when the player taps CANCEL.
    public void Open(Action canceled)
    {
        if (window == null) return;
        onCancel = canceled;
        phase = Phase.Searching;
        startedAt = Time.unscaledTime;
        window.SetActive(true);
        window.transform.SetAsLastSibling();
        if (foundGroup != null) foundGroup.SetActive(false);
        if (searchText != null) searchText.gameObject.SetActive(true);
        if (cancelButton != null) cancelButton.gameObject.SetActive(true);
        if (spinner != null) spinner.gameObject.SetActive(true);
        if (title != null) title.text = GameModeMenu.ModeName(GameMode.Online);
        if (cancelLabel != null) cancelLabel.text = I18n.Tr("CANCEL");
        UpdateDots();
    }

    // "Player found!" with the name, then `done` after Found Time seconds (the screen stays up; the game closes it with Close).
    public void ShowFound(string opponent, Action done)
    {
        if (window == null) { done?.Invoke(); return; }
        phase = Phase.Found;
        foundAt = Time.unscaledTime;
        onFoundDone = done;
        if (searchText != null) searchText.gameObject.SetActive(false);
        if (spinner != null) spinner.gameObject.SetActive(false);
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);
        if (foundGroup != null) foundGroup.SetActive(true);
        if (foundLabel != null) foundLabel.text = I18n.Tr("Player found!");
        if (opponentName != null) opponentName.text = opponent;
    }

    public void Close()
    {
        phase = Phase.Closed;
        if (window != null) window.SetActive(false);
    }

    void Cancel()
    {
        if (phase != Phase.Searching) return; // too late once someone was found
        var callback = onCancel;
        onCancel = null;
        Close();
        callback?.Invoke();
    }

    void Update()
    {
        if (phase == Phase.Searching)
        {
            if (spinner != null) spinner.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * spinnerSpeed);
            UpdateDots();
        }
        else if (phase == Phase.Found)
        {
            if (foundGroup != null)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - foundAt) / Mathf.Max(0.05f, popTime));
                foundGroup.transform.localScale = Vector3.one * (1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f)); // ease-out-back
            }
            if (Time.unscaledTime - foundAt >= foundTime)
            {
                var callback = onFoundDone;
                onFoundDone = null;
                phase = Phase.Closed; // the caller decides when the screen goes (Close): the match may still be waiting for the other player
                callback?.Invoke();
            }
        }
    }

    // "Searching for players", then one, two, three dots. The text itself never moves: three invisible dots in front balance the three
    // (visible or invisible) dots behind it, so it stays exactly in the middle while the dots come and go.
    void UpdateDots()
    {
        if (searchText == null) return;
        int n = Mathf.FloorToInt((Time.unscaledTime - startedAt) / Mathf.Max(0.05f, dotTime)) % 4;
        string dots = new string('.', n);
        string hidden = new string('.', 3 - n);
        searchText.text = "<alpha=#00>...<alpha=#FF>" + I18n.Tr("Searching for players") + dots + (hidden.Length > 0 ? "<alpha=#00>" + hidden : ""); // (there is no closing tag for alpha: #FF turns it back on)
    }
}
