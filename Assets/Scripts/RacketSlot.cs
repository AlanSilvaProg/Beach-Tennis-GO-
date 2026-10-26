using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One card of the Rackets window. Three looks:
//   owned          a card in the racket's own colour with its racket, and its name in the strip at the bottom; green border + check when in use
//   locked         the same racket on a grey card, a chain with a padlock across it, its name and (very short) requirement in the strip
//   hidden (no skin) dark silhouette on a dark card, chain and padlock, name "????"
// The card is built in the scene (the template under the window's list, every part anchored in fractions of the card so it scales);
// the window only fills it and sizes it to the screen.
public class RacketSlot : MonoBehaviour
{
    public Button button;
    [Tooltip("Coloured background of the card (tinted per racket).")]
    public Image card;
    [Tooltip("Light frame around the card.")]
    public Image border;
    public RawImage preview;
    public TMP_Text nameText;
    [Tooltip("Requirement of a locked racket, in the strip under its name.")]
    public TMP_Text requirementText;
    [Tooltip("Chain across the racket and the padlock at its end: both shown on a locked card.")]
    public GameObject chain;
    public GameObject lockIcon;
    [Tooltip("Green check of the racket in use.")]
    public GameObject equippedIcon;
    [Tooltip("Small Rally coin shown before the price of a racket that is bought with coins.")]
    public GameObject coinIcon;
    [Tooltip("Small video icon shown before \"FREE\" on a racket that is unlocked by watching an ad.")]
    public GameObject adIcon;
    [Tooltip("Dot on a racket unlocked and not yet seen.")]
    public GameObject newDot;

    [Header("Colours")]
    public Color ownedBorder = new Color(1f, 1f, 1f, 1f);
    public Color equippedBorder = new Color(0.45f, 1f, 0.5f, 1f);
    public Color lockedBorder = new Color(0.78f, 0.8f, 0.85f, 1f);
    public Color lockedCard = new Color(0.36f, 0.37f, 0.4f, 1f);
    public Color hiddenCard = new Color(0.27f, 0.28f, 0.31f, 1f);
    [Tooltip("Tint of the picture of a locked racket (a little darker than an owned one).")]
    public Color lockedPicture = new Color(0.72f, 0.72f, 0.76f, 1f);
    [Tooltip("Tint of the picture of a hidden racket: it turns into a flat dark shape.")]
    public Color silhouette = new Color(0.12f, 0.13f, 0.17f, 1f);

    [Header("Price")]
    public Color affordable = new Color(1f, 0.86f, 0.3f, 1f);
    [Tooltip("Price colour when the player does not have the coins yet.")]
    public Color notAffordable = new Color(1f, 0.42f, 0.38f, 1f);
    [Tooltip("Horizontal range (fractions of the card width) of the requirement text: alone, and next to the coin.")]
    public Vector2 requirementAlone = new Vector2(0.04f, 0.96f);
    public Vector2 requirementWithCoin = new Vector2(0.38f, 0.92f);

    [Header("Strip texts (fractions of the card height)")]
    [Tooltip("Name when there is no requirement under it (min, max of y).")]
    public Vector2 nameAlone = new Vector2(0.03f, 0.27f);
    [Tooltip("Name above the requirement.")]
    public Vector2 nameAbove = new Vector2(0.15f, 0.29f);
    [Tooltip("Requirement under the name.")]
    public Vector2 requirementBelow = new Vector2(0.01f, 0.15f);

    public RacketSkin Skin { get; private set; }

    // skin == null: a hidden slot (more rackets to come).
    public void Bind(RacketSkin skin, Texture picture, bool owned, bool equipped, bool isNew, bool canAfford, System.Action onClick)
    {
        Skin = skin;
        bool hidden = skin == null;
        string req = hidden || owned ? "" : skin.Requirement;
        if (preview != null) { preview.texture = picture; preview.color = hidden ? silhouette : (owned ? Color.white : lockedPicture); }
        if (card != null) card.color = owned ? skin.color : (hidden ? hiddenCard : lockedCard);
        if (border != null) border.color = owned ? (equipped ? equippedBorder : ownedBorder) : lockedBorder;
        if (nameText != null)
        {
            nameText.text = hidden ? "????" : skin.Name;
            SetBand((RectTransform)nameText.transform, req.Length > 0 ? nameAbove : nameAlone);
        }
        bool coins = !hidden && !owned && skin.unlock != null && skin.unlock.kind == UnlockKind.RallyCoins;
        bool ad = !hidden && !owned && skin.unlock != null && skin.unlock.kind == UnlockKind.RewardedAd;
        if (requirementText != null)
        {
            requirementText.text = req;
            requirementText.gameObject.SetActive(req.Length > 0);
            requirementText.color = coins && !canAfford ? notAffordable : affordable;
            var rt = (RectTransform)requirementText.transform;
            var x = coins || ad ? requirementWithCoin : requirementAlone;
            rt.anchorMin = new Vector2(x.x, rt.anchorMin.y); rt.anchorMax = new Vector2(x.y, rt.anchorMax.y);
        }
        if (coinIcon != null) coinIcon.SetActive(coins);
        if (adIcon != null) adIcon.SetActive(ad);
        if (chain != null) chain.SetActive(!owned);
        if (lockIcon != null) lockIcon.SetActive(!owned);
        if (equippedIcon != null) equippedIcon.SetActive(owned && equipped);
        if (newDot != null) newDot.SetActive(owned && isNew);
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if ((owned || coins || ad) && onClick != null) button.onClick.AddListener(() => onClick());
            button.enabled = owned || coins || ad; // nothing to press on a card that cannot be bought
        }
    }

    // A short wobble: "not enough coins".
    public void Shake() { shakeStart = Time.unscaledTime; }

    float shakeStart = -10f;

    void Update()
    {
        float t = Time.unscaledTime - shakeStart;
        transform.localRotation = t < 0.45f ? Quaternion.Euler(0f, 0f, Mathf.Sin(t * 40f) * 5f * (1f - t / 0.45f)) : Quaternion.identity;
    }

    static void SetBand(RectTransform rt, Vector2 yRange)
    {
        rt.anchorMin = new Vector2(0.04f, yRange.x);
        rt.anchorMax = new Vector2(0.96f, yRange.y);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
