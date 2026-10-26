using System;
using UnityEngine;

// How a racket gets unlocked. To add a new rule: add a value here, handle it in RacketProgress.RuleMet (if it can be checked from the saved
// progress) or call RacketProgress.Unlock(id) from wherever the rule is decided (a purchase, an achievement...).
public enum UnlockKind
{
    [Tooltip("Owned from the start.")] Always,
    [Tooltip("Only unlocked when a script calls RacketProgress.Unlock(id) (purchase, reward...). Describe it in Requirement Text.")] Manual,
    [Tooltip("Unlocked when the player reaches this level of the Training mode.")] TrainingLevel,
    [Tooltip("Bought with Rally coins: tap the locked racket in the window and pay Value coins.")] RallyCoins,
    [Tooltip("Free for watching a rewarded ad: tap the locked racket in the window.")] RewardedAd,
}

[Serializable]
public class UnlockRule
{
    public UnlockKind kind = UnlockKind.Always;
    [Tooltip("TrainingLevel: the level to reach. RallyCoins: the price in coins.")]
    public int value = 2;
    [Tooltip("Very short text shown on the locked racket (e.g. \"LV 5\", \"VIP\", \"$1\"). Empty = made from the rule (\"LV <value>\" for TrainingLevel).")]
    public string requirementText = "";
}

[Serializable]
public class RacketSkin
{
    [Tooltip("Saved with the player's progress and used by RacketProgress.Unlock(id). Never change it once released. Empty = the texture's name.")]
    public string id = "";
    [Header("Name")]
    public string nameEn = "Racket";
    public string namePt = "";
    public string nameEs = "";
    [Header("Look")]
    [Tooltip("The model's texture (the same layout as default.jpg).")]
    public Texture2D texture;
    [Tooltip("Colour of the background of the \"new racket\" screen.")]
    public Color color = new Color(0.2f, 0.5f, 0.9f);
    [Header("Unlock")]
    public UnlockRule unlock = new UnlockRule();

    public string Id => !string.IsNullOrEmpty(id) ? id : (texture != null ? texture.name : nameEn);

    public string Name
    {
        get
        {
            string n = I18n.Current == I18n.Language.Pt ? namePt : I18n.Current == I18n.Language.Es ? nameEs : nameEn;
            return string.IsNullOrEmpty(n) ? nameEn : n;
        }
    }

    // What the player has to do, in a few characters.
    public string Requirement
    {
        get
        {
            if (unlock == null) return "";
            if (!string.IsNullOrEmpty(unlock.requirementText)) return unlock.requirementText;
            if (unlock.kind == UnlockKind.TrainingLevel) return I18n.Tr("LV {0}", unlock.value);
            if (unlock.kind == UnlockKind.RewardedAd) return I18n.Tr("FREE");
            return unlock.kind == UnlockKind.RallyCoins ? unlock.value.ToString() : "";
        }
    }
}

// The list of rackets of the game: edit it here (Assets/Data/RacketCatalog.asset). One entry per texture; the order is the order on screen.
[CreateAssetMenu(fileName = "RacketCatalog", menuName = "Beach Tennis GO/Racket Catalog")]
public class RacketCatalog : ScriptableObject
{
    [Tooltip("How many extra locked slots (dark silhouette, \"????\") are shown after the rackets above: hints of what is still to come.")]
    [Min(0)] public int lockedSlots = 2;
    public RacketSkin[] skins = new RacketSkin[0];

    public RacketSkin Find(string id)
    {
        if (skins == null || string.IsNullOrEmpty(id)) return null;
        foreach (var s in skins) if (s != null && s.Id == id) return s;
        return null;
    }
}
