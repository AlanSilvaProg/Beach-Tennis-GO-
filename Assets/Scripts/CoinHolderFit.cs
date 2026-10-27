using UnityEngine;

// Keeps the Rally coins pill from covering the GAME MODE and RACKET buttons of the menu on narrow screens (a folded phone, 9:22): it keeps its
// place between the buttons while it fits, shrinks a little, and when even that is too much it moves up above them.
[RequireComponent(typeof(RectTransform))]
public class CoinHolderFit : MonoBehaviour
{
    [Tooltip("The two buttons at the bottom corners (anchored to the bottom left / right).")]
    public RectTransform leftButton, rightButton;
    public float gap = 10f;
    [Range(0.5f, 1f)] public float minScale = 0.75f;
    [Tooltip("Gap kept above the buttons when the pill moves up.")]
    public float aboveGap = 14f;

    RectTransform rt;
    float baseY, baseScale = 1f, lastWidth = -1f;

    void Awake() { rt = (RectTransform)transform; baseY = rt.anchoredPosition.y; baseScale = rt.localScale.x; }

    void LateUpdate()
    {
        var parent = rt.parent as RectTransform;
        if (parent == null || leftButton == null || rightButton == null) return;
        float w = parent.rect.width;
        if (Mathf.Approximately(w, lastWidth)) return;
        lastWidth = w;
        float leftEdge = leftButton.anchoredPosition.x + leftButton.sizeDelta.x * (1f - leftButton.pivot.x);
        float rightEdge = w - (-rightButton.anchoredPosition.x + rightButton.sizeDelta.x * (1f - rightButton.pivot.x)); // x of the right button's left edge
        float free = rightEdge - leftEdge - 2f * gap;
        float width = rt.sizeDelta.x;
        float s = Mathf.Min(baseScale, free / width);
        if (s >= minScale * baseScale)
        {
            rt.localScale = Vector3.one * s;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, baseY);
        }
        else
        {
            rt.localScale = Vector3.one * baseScale;
            float top = Mathf.Max(leftButton.anchoredPosition.y + leftButton.sizeDelta.y * (1f - leftButton.pivot.y), rightButton.anchoredPosition.y + rightButton.sizeDelta.y * (1f - rightButton.pivot.y));
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, top + aboveGap + rt.sizeDelta.y * rt.pivot.y * baseScale);
        }
    }
}
