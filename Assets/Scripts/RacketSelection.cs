using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Rackets: the round button (bottom right), the full-screen window with one card per racket of the RacketCatalog, the "new racket" screen shown when
// one gets unlocked, and the skin on the player's racket. Everything is in the scene (object "RacketSelection" under the HUD canvas).
// What the rackets are and how they unlock: Assets/Data/RacketCatalog.asset. What the player owns: RacketProgress.
// The pictures are real renders of the racket model with each texture, made once with a small camera far away from the court and kept.
public class RacketSelection : MonoBehaviour
{
    [Header("Data")]
    public RacketCatalog catalog;
    [Tooltip("The player's racket: its model gets the equipped skin.")]
    public Racket racket;
    public GameManager game;
    PokiService poki;

    [Header("Button (bottom right)")]
    public Button openButton;
    [Tooltip("Picture of the button in each language, in the order English, Portuguese, Spanish.")]
    public Sprite[] openButtonSprites = new Sprite[3];
    [Tooltip("Red dot with the number of rackets unlocked since the player last opened the window.")]
    public GameObject badge;
    public TMP_Text badgeText;
    [Range(0f, 0.2f)] public float badgePulse = 0.08f;

    [Header("Window")]
    public GameObject window;
    public Button closeButton;
    public TMP_Text title;
    public TMP_Text closeLabel;
    [Tooltip("Content of the scroll list: the cards go here (grid layout).")]
    public RectTransform list;
    [Tooltip("Card to copy for each racket (kept inactive).")]
    public RacketSlot slotTemplate;
    public ScrollRect scroll;
    [Tooltip("The grid of the list: the cards are sized to the screen (see below).")]
    public GridLayoutGroup grid;
    [Tooltip("Wanted width of a card (canvas units). The number of columns follows from the width of the screen, so a portrait phone gets fewer, a wide screen more.")]
    public float cardWidth = 190f;
    public float maxCardWidth = 250f;
    [Min(1)] public int minColumns = 2;
    [Tooltip("Height of a card divided by its width (the card pictures are 234 x 300).")]
    public float cardAspect = 1.282f;

    [Header("Picture of a racket")]
    [Tooltip("The racket model (the fbx) that is drawn.")]
    public GameObject previewModel;
    [Tooltip("Rotation of the model so that its face looks at the camera (the camera looks along +Z).")]
    public Vector3 previewEuler = new Vector3(0f, 0f, 0f);
    [Tooltip("Free space around the racket in the picture (1 = touching the edges).")]
    [Range(1f, 1.5f)] public float previewMargin = 1.08f;
    public int previewSize = 256;
    public Material previewMaterial;

    [Header("New racket screen")]
    public GameObject popup;
    [Tooltip("Sunburst behind it: a copy of the win screen's, painted with the racket's colour.")]
    public RawImage popupBackground;
    public RectTransform popupContent;
    public TMP_Text popupTitle;
    public RawImage popupPicture;
    public TMP_Text popupName;
    public Button popupOk;
    public TMP_Text popupOkLabel;
    [Tooltip("Seconds between the unlock and the screen (so the win screen is seen first).")]
    public float popupDelay = 1f;
    public float popupPopTime = 0.45f;
    public float popupFadeTime = 0.35f;

    public bool IsOpen => window != null && window.activeSelf;
    public bool PopupOpen => popup != null && popup.activeSelf;

    readonly Dictionary<string, Texture2D> pictures = new Dictionary<string, Texture2D>();
    readonly Queue<RacketSkin> pending = new Queue<RacketSkin>();
    readonly HashSet<string> newOnOpen = new HashSet<string>(); // rackets that were new when the window was opened: they keep their dot until it closes
    float popupAt, popupShownAt;
    Material popupMaterial;
    MaterialPropertyBlock block;
    int shownLanguage = -1;
    Vector3 openButtonScale = Vector3.one;
    bool buttonWanted;

    // Preview rig (built the first time a picture is needed).
    GameObject rig;
    Camera rigCamera;
    Renderer[] rigRenderers;
    RenderTexture rigTarget;

    void Awake()
    {
        if (game == null) game = FindFirstObjectByType<GameManager>();
        if (openButton != null)
        {
            openButtonScale = openButton.transform.localScale;
            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(Open);
        }
        Hook(closeButton, Close);
        Hook(popupOk, ClosePopup);
        if (slotTemplate != null) slotTemplate.gameObject.SetActive(false);
        if (popupBackground != null && popupBackground.material != null)
        {
            popupMaterial = new Material(popupBackground.material); // own copy: it gets the colour of each racket
            popupBackground.material = popupMaterial;
        }
        RacketProgress.Unlocked += OnUnlocked;
        RacketProgress.Changed += OnProgressChanged;
        RallyCoins.Changed += OnCoinsChanged;
        RacketProgress.Bind(catalog);
        if (window != null) window.SetActive(false);
        if (popup != null) popup.SetActive(false);
        ApplyEquipped();
    }

    void OnDestroy()
    {
        RacketProgress.Unlocked -= OnUnlocked;
        RacketProgress.Changed -= OnProgressChanged;
        RallyCoins.Changed -= OnCoinsChanged;
        if (popupMaterial != null) Destroy(popupMaterial);
        if (rig != null) Destroy(rig);
        if (rigTarget != null) rigTarget.Release();
        foreach (var t in pictures.Values) if (t != null) Destroy(t);
    }

    static void Hook(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(action);
    }

    // ---- unlocking ----

    void OnUnlocked(RacketSkin skin)
    {
        if (pending.Count == 0) popupAt = Time.unscaledTime + popupDelay;
        pending.Enqueue(skin);
    }

    void OnCoinsChanged(int balance)
    {
        if (IsOpen) RefreshList(); // what the player can afford changed
    }

    void OnProgressChanged()
    {
        if (IsOpen) RefreshList();
    }

    // ---- button ----

    // The button is shown by the game in the menu and on the result screens, never under the window or the new racket screen.
    public void SetButtonVisible(bool visible)
    {
        buttonWanted = visible;
        if (openButton == null) return;
        visible &= !IsOpen && !PopupOpen;
        if (openButton.gameObject.activeSelf != visible) openButton.gameObject.SetActive(visible);
    }

    void UpdateBadge()
    {
        if (badge == null) return;
        int n = RacketProgress.NewCount;
        bool show = n > 0 && openButton != null && openButton.gameObject.activeInHierarchy;
        if (badge.activeSelf != show) badge.SetActive(show);
        if (!show) return;
        if (badgeText != null) badgeText.text = n > 99 ? "99+" : n.ToString();
        badge.transform.localScale = Vector3.one * (1f + badgePulse * Mathf.Sin(Time.unscaledTime * 5f));
    }

    void ApplyLanguage()
    {
        int lang = (int)I18n.Current;
        if (lang == shownLanguage) return;
        shownLanguage = lang;
        if (openButton != null && openButton.image != null && lang < openButtonSprites.Length && openButtonSprites[lang] != null)
            openButton.image.sprite = openButtonSprites[lang];
        RefreshTexts();
        if (IsOpen) RefreshList();
    }

    void RefreshTexts()
    {
        if (title != null) title.text = I18n.Tr("RACKETS");
        if (closeLabel != null) closeLabel.text = I18n.Tr("BACK");
        if (popupTitle != null) popupTitle.text = I18n.Tr("NEW RACKET!");
        if (popupOkLabel != null) popupOkLabel.text = I18n.Tr("OK");
    }

    // ---- window ----

    public void Open()
    {
        if (window == null) return;
        PokiEvents.Track("rackets", "open", "new", RacketProgress.NewCount);
        window.SetActive(true);
        window.transform.SetAsLastSibling();
        FitGrid();
        RefreshTexts();
        newOnOpen.Clear();
        if (catalog != null) foreach (var sk in catalog.skins) if (sk != null && RacketProgress.IsNew(sk.Id)) newOnOpen.Add(sk.Id);
        RefreshList();
        RacketProgress.MarkAllSeen(); // the number on the button goes away; the cards still show their "new" dot this time
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        SetButtonVisible(buttonWanted);
    }

    public void Close()
    {
        if (window != null && window.activeSelf) PokiEvents.Track("rackets", "close");
        if (window != null) window.SetActive(false);
        SetButtonVisible(buttonWanted);
    }

    void RefreshList()
    {
        if (list == null || slotTemplate == null || catalog == null) return;
        for (int i = list.childCount - 1; i >= 0; i--) // every card but the template
        {
            var child = list.GetChild(i).gameObject;
            if (child == slotTemplate.gameObject) continue;
            child.SetActive(false); // Destroy happens at the end of the frame; the layout must not count it meanwhile
            Destroy(child);
        }
        string equipped = RacketProgress.EquippedId;
        foreach (var skin in catalog.skins)
        {
            if (skin == null) continue;
            bool owned = RacketProgress.IsUnlocked(skin);
            var s = skin;
            var slot = NewSlot();
            bool coins = skin.unlock != null && skin.unlock.kind == UnlockKind.RallyCoins;
            slot.Bind(skin, Picture(skin), owned, owned && skin.Id == equipped, newOnOpen.Contains(skin.Id), coins && RallyCoins.Balance >= skin.unlock.value,
                      owned ? (System.Action)(() => Equip(s)) : skin.unlock != null && skin.unlock.kind == UnlockKind.RewardedAd ? (System.Action)(() => WatchAd(s, slot)) : () => Buy(s, slot));
        }
        for (int i = 0; i < catalog.lockedSlots; i++) NewSlot().Bind(null, Picture(null), false, false, false, false, null);
    }

    RacketSlot NewSlot()
    {
        var slot = Instantiate(slotTemplate, list);
        slot.gameObject.SetActive(true);
        slot.name = "Racket";
        return slot;
    }

    // A racket that costs Rally coins: pay and unlock it (the new racket screen follows), or wobble when there are not enough coins.
    void Buy(RacketSkin skin, RacketSlot slot)
    {
        if (RacketProgress.IsUnlocked(skin) || skin.unlock == null || skin.unlock.kind != UnlockKind.RallyCoins) return;
        int price = skin.unlock.value;
        if (RallyCoins.TrySpend(price))
        {
            PokiEvents.Track("racket", "buy", "id", skin.Id, "price", price);
            RacketProgress.Unlock(skin.Id);
        }
        else
        {
            PokiEvents.Track("racket", "buy_failed", "id", skin.Id, "price", price, "balance", RallyCoins.Balance);
            if (slot != null) slot.Shake();
        }
    }

    // A racket that is free for an ad: the rewarded ad plays and, if the player really watched it, the racket is unlocked (the new racket screen follows).
    // No ad / ad blocker = nothing happens, like RETRY. Outside Poki (the Editor) it is granted at once.
    void WatchAd(RacketSkin skin, RacketSlot slot)
    {
        if (PokiService.AdPlaying || RacketProgress.IsUnlocked(skin) || skin.unlock == null || skin.unlock.kind != UnlockKind.RewardedAd) return;
        string id = skin.Id;
        void Done(bool granted)
        {
            PokiEvents.Track("racket", "ad_unlock", "id", id, "granted", granted);
            if (granted) RacketProgress.Unlock(id);
            else if (slot != null) slot.Shake();
        }
        if (poki == null) poki = FindFirstObjectByType<PokiService>();
        if (poki != null) poki.RewardedBreak(Done); else Done(true);
    }

    void Equip(RacketSkin skin)
    {
        if (!RacketProgress.Equip(skin.Id)) return;
        ApplyEquipped();
        RefreshList();
    }

    // Columns from the width available (at least minColumns), cards as wide as the columns allow, up to maxCardWidth.
    void FitGrid()
    {
        if (grid == null || list == null) return;
        float available = ((RectTransform)list.parent).rect.width - grid.padding.left - grid.padding.right;
        if (available <= 0f) return;
        float gap = grid.spacing.x;
        int columns = Mathf.Max(minColumns, Mathf.FloorToInt((available + gap) / (cardWidth + gap)));
        float w = Mathf.Min(maxCardWidth, (available - (columns - 1) * gap) / columns);
        var size = new Vector2(w, w * cardAspect);
        if (grid.cellSize != size) grid.cellSize = size;
    }

    // ---- skin on the player's racket ----

    public void ApplyEquipped()
    {
        var skin = RacketProgress.Equipped;
        if (skin == null || racket == null) return;
        ApplySkin(racket, skin);
    }

    // The skin (by id) on another racket, e.g. the other player's in an online match. An unknown or empty id leaves the racket as it is.
    public void ApplySkinTo(Racket target, string id)
    {
        var skin = catalog != null ? catalog.Find(id) : null;
        if (skin != null && target != null) ApplySkin(target, skin);
    }

    void ApplySkin(Racket target, RacketSkin skin)
    {
        if (skin.texture == null || target.model == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        block.Clear();
        block.SetTexture("_MainTex", skin.texture);
        block.SetTexture("_EmissionMap", skin.texture); // the racket material also glows with its own texture
        foreach (var r in target.model.GetComponentsInChildren<Renderer>(true)) r.SetPropertyBlock(block);
    }

    // ---- new racket screen ----

    void ShowPopup(RacketSkin skin)
    {
        if (popup == null) return;
        RefreshTexts();
        if (popupName != null) popupName.text = skin.Name;
        if (popupPicture != null) popupPicture.texture = Picture(skin);
        Paint(skin.color);
        popup.SetActive(true);
        popup.transform.SetAsLastSibling();
        popupShownAt = Time.unscaledTime;
        if (popupBackground != null) { var c = popupBackground.color; c.a = 0f; popupBackground.color = c; }
        if (popupContent != null) popupContent.localScale = Vector3.zero;
        SetButtonVisible(buttonWanted);
        PokiEvents.Track("racket", "popup", "id", skin.Id);
    }

    void ClosePopup()
    {
        if (popup != null) popup.SetActive(false);
        SetButtonVisible(buttonWanted);
    }

    // Sunburst in the racket's colour: light centre, the colour itself, a deep edge.
    void Paint(Color color)
    {
        if (popupMaterial == null) return;
        Color.RGBToHSV(color, out float h, out float s, out float v);
        popupMaterial.SetColor("_CenterColor", Color.HSVToRGB(h, s * 0.4f, 1f));
        popupMaterial.SetColor("_MidColor", Color.HSVToRGB(h, s, Mathf.Max(v, 0.6f)));
        popupMaterial.SetColor("_EdgeColor", Color.HSVToRGB(h, Mathf.Min(1f, s * 1.05f), Mathf.Max(v, 0.6f) * 0.35f));
    }

    void Update()
    {
        ApplyLanguage();
        UpdateBadge();
        if (IsOpen) FitGrid();
        if (pending.Count > 0 && !PopupOpen && Time.unscaledTime >= popupAt && (game == null || !game.MatchOn) && !PokiService.AdPlaying)
            ShowPopup(pending.Dequeue());
        if (PopupOpen) AnimatePopup();
    }

    void AnimatePopup()
    {
        float t = Time.unscaledTime - popupShownAt;
        if (popupBackground != null)
        {
            var c = popupBackground.color;
            c.a = Mathf.Clamp01(t / Mathf.Max(0.05f, popupFadeTime));
            popupBackground.color = c;
        }
        if (popupContent != null)
        {
            float k = Mathf.Clamp01(t / Mathf.Max(0.05f, popupPopTime));
            popupContent.localScale = Vector3.one * (1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f)); // ease-out-back
        }
    }

    // ---- pictures ----

    // The render of a racket with its texture (skin == null: the dark shape of a hidden one; it is tinted by the card).
    public Texture Picture(RacketSkin skin)
    {
        string key = skin != null ? skin.Id : "";
        if (pictures.TryGetValue(key, out var cached) && cached != null) return cached;
        var texture = skin != null ? skin.texture : Texture2D.whiteTexture; // flat white: the slot tints it into a shape
        var picture = Render(texture);
        if (picture != null) pictures[key] = picture;
        return picture;
    }

    void BuildRig()
    {
        if (rig != null || previewModel == null || previewMaterial == null) return;
        rig = new GameObject("RacketPreviewRig");
        rig.hideFlags = HideFlags.HideAndDontSave;
        rig.transform.position = new Vector3(0f, -3000f, 0f); // far from everything the game camera sees
        var model = Instantiate(previewModel, rig.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(previewEuler);
        rigRenderers = model.GetComponentsInChildren<Renderer>();
        previewMaterial = new Material(previewMaterial); // the shared one is left untouched
        foreach (var r in rigRenderers) { var mats = new Material[r.sharedMaterials.Length]; for (int i = 0; i < mats.Length; i++) mats[i] = previewMaterial; r.sharedMaterials = mats; }

        var bounds = rigRenderers[0].bounds;
        foreach (var r in rigRenderers) bounds.Encapsulate(r.bounds);
        var camGo = new GameObject("Camera");
        camGo.transform.SetParent(rig.transform, false);
        rigCamera = camGo.AddComponent<Camera>();
        rigCamera.enabled = false; // rendered by hand
        rigCamera.orthographic = true;
        rigCamera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * previewMargin;
        rigCamera.clearFlags = CameraClearFlags.SolidColor;
        rigCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        rigCamera.nearClipPlane = 0.1f;
        rigCamera.farClipPlane = 30f;
        camGo.transform.position = bounds.center + Vector3.back * 10f;
        camGo.transform.rotation = Quaternion.identity;
        rigTarget = new RenderTexture(previewSize, previewSize, 16, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        rigCamera.targetTexture = rigTarget;
        rig.SetActive(false);
    }

    Texture2D Render(Texture texture)
    {
        BuildRig();
        if (rig == null || texture == null) return null;
        previewMaterial.mainTexture = texture;
        rig.SetActive(true);
        rigCamera.Render();
        rig.SetActive(false);
        var previous = RenderTexture.active;
        RenderTexture.active = rigTarget;
        var result = new Texture2D(previewSize, previewSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        result.ReadPixels(new Rect(0, 0, previewSize, previewSize), 0, 0);
        result.Apply(false, false);
        RenderTexture.active = previous;
        return result;
    }
}
