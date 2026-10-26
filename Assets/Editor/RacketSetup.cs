using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds the Rackets feature in the scene (button, window, new racket screen) and the RacketCatalog asset. Safe to run again: nothing that exists is replaced.
// Menu: Tools > Beach Tennis GO > Rackets
public static class RacketSetup
{
    const string ScenePath = "Assets/Scene/Gameplay.unity";
    const string CatalogPath = "Assets/Data/RacketCatalog.asset";
    const string FontPath = "Assets/Fonts/LilitaOne-Regular.ttf";
    const string TexDir = "Assets/Models/padel+racket+3d+model.fbm/";

    [MenuItem("Tools/Beach Tennis GO/Rackets/Add Racket Selection")]
    public static void Add() { Ensure(); }

    [MenuItem("Tools/Beach Tennis GO/Rackets/Reset Racket Progress (test)")]
    public static void ResetProgress()
    {
        foreach (var k in new[] { "racketUnlocked", "racketSeen", "racketEquipped", "racketHighestLevel" }) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
        Debug.Log("[BeachTennisGO] Racket progress cleared.");
    }

    // Slices a sheet of equally sized cells (left to right) into sprites with the given names.
    static Sprite[] Slice(string path, int cellW, int cellH, string[] names, int maxSize)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return null;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Bilinear;
        imp.maxTextureSize = maxSize;
        imp.npotScale = TextureImporterNPOTScale.None;
        imp.textureCompression = TextureImporterCompression.Compressed;
        imp.crunchedCompression = true;
        imp.compressionQuality = 85;
        var rects = new SpriteMetaData[names.Length];
        int h = cellH;
        for (int i = 0; i < names.Length; i++)
            rects[i] = new SpriteMetaData { name = names[i], rect = new Rect(i * cellW, 0, cellW, h), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
#pragma warning disable CS0618
        imp.spritesheet = rects;
#pragma warning restore CS0618
        imp.SaveAndReimport();
        var found = new Sprite[names.Length];
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite sp) { int i = System.Array.IndexOf(names, sp.name); if (i >= 0) found[i] = sp; }
        return found;
    }

    static RacketCatalog EnsureCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<RacketCatalog>(CatalogPath);
        if (catalog != null) return catalog;
        if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
        catalog = ScriptableObject.CreateInstance<RacketCatalog>();
        catalog.lockedSlots = 2;
        catalog.skins = new[]
        {
            new RacketSkin
            {
                id = "classic", nameEn = "Classic", namePt = "Clássica", nameEs = "Clásica", color = new Color(0.16f, 0.42f, 0.95f),
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "default.jpg"),
                unlock = new UnlockRule { kind = UnlockKind.Always },
            },
            new RacketSkin
            {
                id = "oak", nameEn = "Oak", namePt = "Carvalho", nameEs = "Roble", color = new Color(0.82f, 0.48f, 0.2f),
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "wood.jpeg"),
                unlock = new UnlockRule { kind = UnlockKind.TrainingLevel, value = 3 },
            },
            new RacketSkin
            {
                id = "golden", nameEn = "Golden", namePt = "Dourada", nameEs = "Dorada", color = new Color(1f, 0.78f, 0.18f),
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "golden.jpeg"),
                unlock = new UnlockRule { kind = UnlockKind.Manual, requirementText = "VIP" },
            },
        };
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        return catalog;
    }

    static void Ensure()
    {
        var active = EditorSceneManager.GetActiveScene();
        var scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath);
        var view = Object.FindFirstObjectByType<HudView>();
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (view == null || view.infoText == null || gm == null) return;
        if (Object.FindFirstObjectByType<RacketSelection>() != null) return;

        var catalog = EnsureCatalog();
        var buttonSprites = Slice("Assets/Sprites/racket-selection-button.png", 192, 192, new[] { "racket-en", "racket-pt", "racket-es" }, 512);
        var icons = Slice("Assets/Sprites/racket-icons.png", 128, 128, new[] { "lock", "check" }, 256);

        var sheet = Slice("Assets/Sprites/racket-card.png", 234, 300, new[] { "card-fill", "card-border", "card-strip" }, 512);
        var chainSprite = Slice("Assets/Sprites/racket-chain.png", 445, 56, new[] { "chain" }, 512)[0];

        var canvas = view.infoText.canvas.transform;
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        var box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        var navy = new Color(0.03f, 0.12f, 0.38f, 1f);

        // preview material
        const string matPath = "Assets/Materials/RacketPreview.mat";
        var previewMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (previewMat == null) { previewMat = new Material(Shader.Find("BeachTennisGO/RacketPreview")); AssetDatabase.CreateAsset(previewMat, matPath); }

        TMPro.TMP_Text Label(string name, Transform parent, int size, TMPro.TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.color = color; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false; t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.text = name;
            var o = go.GetComponent<Outline>(); o.effectColor = navy; o.effectDistance = new Vector2(3f, -3f);
            var tmp = TmpConvert.Convert(t);
            tmp.alignment = align; tmp.enableAutoSizing = true; tmp.fontSizeMin = 8; tmp.fontSizeMax = size; tmp.enableWordWrapping = false;
            return tmp;
        }

        RectTransform Rect(string name, Transform parent, params System.Type[] extra)
        {
            var types = new System.Type[extra.Length + 1];
            types[0] = typeof(RectTransform);
            System.Array.Copy(extra, 0, types, 1, extra.Length);
            var go = new GameObject(name, types);
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        void Stretch(RectTransform rt, float margin = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margin, margin); rt.offsetMax = new Vector2(-margin, -margin);
        }

        Button BlueButton(string name, Transform parent, string text, int size, out TMPro.TMP_Text label)
        {
            var rt = Rect(name, parent, typeof(Image), typeof(Button));
            var img = rt.GetComponent<Image>(); img.sprite = box; img.type = Image.Type.Sliced; img.color = Color.white;
            var btn = rt.GetComponent<Button>(); btn.targetGraphic = img;
            var color = new Color(0.18f, 0.5f, 0.87f);
            var c = btn.colors; c.normalColor = color; c.highlightedColor = Color.Lerp(color, Color.white, 0.2f); c.pressedColor = Color.Lerp(color, Color.black, 0.25f);
            c.selectedColor = color; c.disabledColor = new Color(color.r, color.g, color.b, 0.4f); btn.colors = c;
            label = Label("Label", rt, size, TMPro.TextAlignmentOptions.Center, Color.white);
            label.text = text;
            Stretch((RectTransform)label.transform, 6f);
            return btn;
        }

        var holderRt = Rect("RacketSelection", canvas);
        Stretch(holderRt);
        var sel = holderRt.gameObject.AddComponent<RacketSelection>();
        sel.catalog = catalog;
        sel.game = gm;
        sel.racket = gm.player != null ? gm.player.Racket : null;
        sel.previewMaterial = previewMat;
        sel.previewModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/padel+racket+3d+model.fbx");

        // --- round button, bottom right (mirror of the Game mode button) ---
        var btnRt = Rect("RacketButton", holderRt, typeof(Image), typeof(Button));
        btnRt.anchorMin = btnRt.anchorMax = new Vector2(1f, 0f); btnRt.pivot = new Vector2(0.5f, 0.5f);
        btnRt.anchoredPosition = new Vector2(-70f, 70f); btnRt.sizeDelta = new Vector2(90f, 90f);
        var bimg = btnRt.GetComponent<Image>(); bimg.sprite = buttonSprites[0]; bimg.preserveAspect = true;
        sel.openButton = btnRt.GetComponent<Button>(); sel.openButton.targetGraphic = bimg;
        var bc = sel.openButton.colors; bc.normalColor = Color.white; bc.highlightedColor = Color.white; bc.selectedColor = Color.white; bc.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        sel.openButton.colors = bc;
        sel.openButtonSprites = buttonSprites;

        var badgeRt = Rect("NewBadge", btnRt, typeof(Image));
        Place(badgeRt, new Vector2(1f, 1f), new Vector2(6f, 6f), new Vector2(40f, 40f));
        var badgeImg = badgeRt.GetComponent<Image>(); badgeImg.sprite = knob; badgeImg.color = new Color(0.9f, 0.12f, 0.15f); badgeImg.raycastTarget = false;
        var outline = badgeRt.gameObject.AddComponent<Outline>(); outline.effectColor = Color.white; outline.effectDistance = new Vector2(2f, -2f);
        sel.badgeText = Label("Count", badgeRt, 26, TMPro.TextAlignmentOptions.Center, Color.white);
        Stretch((RectTransform)sel.badgeText.transform, 2f);
        sel.badge = badgeRt.gameObject;

        // --- window ---
        var win = Rect("RacketWindow", holderRt);
        Stretch(win);
        sel.window = win.gameObject;
        var bg = Rect("Background", win, typeof(RawImage), typeof(RaysBackground));
        Stretch(bg);
        var bgImg = bg.GetComponent<RawImage>();
        bgImg.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SettingsBackground.mat"); bgImg.raycastTarget = true;

        var bandColor = new Color(0.02f, 0.08f, 0.26f, 0.55f);
        const float bandHeight = 100f;

        // header band: title, with a light line under it
        var header = Rect("Header", win, typeof(Image));
        header.anchorMin = new Vector2(0f, 1f); header.anchorMax = new Vector2(1f, 1f); header.pivot = new Vector2(0.5f, 1f);
        header.anchoredPosition = Vector2.zero; header.sizeDelta = new Vector2(0f, bandHeight);
        var himg = header.GetComponent<Image>(); himg.color = bandColor; himg.raycastTarget = true;
        var hline = Rect("Line", header, typeof(Image));
        hline.anchorMin = new Vector2(0f, 0f); hline.anchorMax = new Vector2(1f, 0f); hline.pivot = new Vector2(0.5f, 0.5f);
        hline.anchoredPosition = Vector2.zero; hline.sizeDelta = new Vector2(0f, 4f);
        var hl = hline.GetComponent<Image>(); hl.color = new Color(1f, 1f, 1f, 0.9f); hl.raycastTarget = false;
        sel.title = Label("Title", header, 64, TMPro.TextAlignmentOptions.Center, Color.white);
        var trt = (RectTransform)sel.title.transform;
        Stretch(trt, 0f); trt.offsetMin = new Vector2(24f, 6f); trt.offsetMax = new Vector2(-24f, -6f);

        // footer band: BACK on the left, with a light line over it
        var footer = Rect("Footer", win, typeof(Image));
        footer.anchorMin = new Vector2(0f, 0f); footer.anchorMax = new Vector2(1f, 0f); footer.pivot = new Vector2(0.5f, 0f);
        footer.anchoredPosition = Vector2.zero; footer.sizeDelta = new Vector2(0f, bandHeight);
        var fimg = footer.GetComponent<Image>(); fimg.color = bandColor; fimg.raycastTarget = true;
        var fline = Rect("Line", footer, typeof(Image));
        fline.anchorMin = new Vector2(0f, 1f); fline.anchorMax = new Vector2(1f, 1f); fline.pivot = new Vector2(0.5f, 0.5f);
        fline.anchoredPosition = Vector2.zero; fline.sizeDelta = new Vector2(0f, 4f);
        var fl = fline.GetComponent<Image>(); fl.color = new Color(1f, 1f, 1f, 0.9f); fl.raycastTarget = false;
        sel.closeButton = BlueButton("Back", footer, "BACK", 34, out sel.closeLabel);
        var cr = (RectTransform)sel.closeButton.transform;
        cr.anchorMin = cr.anchorMax = new Vector2(0f, 0.5f); cr.pivot = new Vector2(0f, 0.5f); cr.anchoredPosition = new Vector2(28f, 0f); cr.sizeDelta = new Vector2(220f, 64f);

        // scroll list between the bands, with a light scroll bar on the right edge
        var scrollRt = Rect("Scroll", win, typeof(Image), typeof(ScrollRect));
        scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(0f, bandHeight); scrollRt.offsetMax = new Vector2(0f, -bandHeight);
        var scrollImg = scrollRt.GetComponent<Image>(); scrollImg.color = new Color(0f, 0f, 0f, 0f); // takes the drags
        var viewport = Rect("Viewport", scrollRt, typeof(RectMask2D));
        Stretch(viewport);
        var listRt = Rect("List", viewport, typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        listRt.anchorMin = new Vector2(0f, 1f); listRt.anchorMax = new Vector2(1f, 1f); listRt.pivot = new Vector2(0.5f, 1f);
        listRt.anchoredPosition = Vector2.zero; listRt.sizeDelta = new Vector2(0f, 0f);
        var grid = listRt.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(190f, 244f); grid.spacing = new Vector2(14f, 14f); grid.padding = new RectOffset(18, 18, 18, 18);
        grid.childAlignment = TextAnchor.UpperCenter; grid.constraint = GridLayoutGroup.Constraint.Flexible;
        listRt.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var barRt = Rect("Scrollbar", scrollRt, typeof(Image), typeof(Scrollbar));
        barRt.anchorMin = new Vector2(1f, 0f); barRt.anchorMax = new Vector2(1f, 1f); barRt.pivot = new Vector2(1f, 0.5f);
        barRt.anchoredPosition = Vector2.zero; barRt.sizeDelta = new Vector2(14f, 0f);
        var barImg = barRt.GetComponent<Image>(); barImg.sprite = box; barImg.type = Image.Type.Sliced; barImg.color = new Color(0f, 0f, 0f, 0.35f);
        var slide = Rect("Sliding Area", barRt);
        Stretch(slide, 2f);
        var handle = Rect("Handle", slide, typeof(Image));
        handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one; handle.offsetMin = handle.offsetMax = Vector2.zero;
        var handleImg = handle.GetComponent<Image>(); handleImg.sprite = box; handleImg.type = Image.Type.Sliced; handleImg.color = new Color(1f, 1f, 1f, 0.95f);
        var bar = barRt.GetComponent<Scrollbar>();
        bar.handleRect = handle; bar.targetGraphic = handleImg; bar.direction = Scrollbar.Direction.BottomToTop;
        var sr = scrollRt.GetComponent<ScrollRect>();
        sr.viewport = viewport; sr.content = listRt; sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 30f;
        sr.movementType = ScrollRect.MovementType.Elastic;
        sr.verticalScrollbar = bar; sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport; sr.verticalScrollbarSpacing = 4f;
        sel.scroll = sr; sel.list = listRt; sel.grid = grid;

        // card template: every part anchored in fractions of the card, so it scales with the card
        void Frac(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1); rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        var card = Rect("SlotTemplate", listRt, typeof(Button), typeof(RacketSlot));
        card.sizeDelta = new Vector2(190f, 244f);
        var slot = card.GetComponent<RacketSlot>();
        var fillRt = Rect("Fill", card, typeof(Image)); Stretch(fillRt);
        slot.card = fillRt.GetComponent<Image>(); slot.card.sprite = sheet[0]; slot.card.raycastTarget = true;
        slot.button = card.GetComponent<Button>(); slot.button.targetGraphic = slot.card; slot.button.transition = Selectable.Transition.None;
        var stripRt = Rect("Strip", card, typeof(Image)); Stretch(stripRt);
        var stripImg = stripRt.GetComponent<Image>(); stripImg.sprite = sheet[2]; stripImg.raycastTarget = false;
        var pic = Rect("Picture", card, typeof(RawImage)); Frac(pic, 0.06f, 0.30f, 0.94f, 0.97f);
        slot.preview = pic.GetComponent<RawImage>(); slot.preview.raycastTarget = false;
        var borderRt = Rect("Border", card, typeof(Image)); Stretch(borderRt);
        slot.border = borderRt.GetComponent<Image>(); slot.border.sprite = sheet[1]; slot.border.raycastTarget = false;
        var chainRt = Rect("Chain", card, typeof(Image)); Frac(chainRt, -0.07f, 0.38f, 1.07f, 0.50f);
        chainRt.localRotation = Quaternion.Euler(0f, 0f, 11f);
        var chainImg = chainRt.GetComponent<Image>(); chainImg.sprite = chainSprite; chainImg.preserveAspect = true; chainImg.raycastTarget = false;
        slot.chain = chainRt.gameObject;
        var lockRt = Rect("Lock", card, typeof(Image)); Frac(lockRt, 0.66f, 0.24f, 0.97f, 0.50f);
        lockRt.localRotation = Quaternion.Euler(0f, 0f, 11f);
        var limg = lockRt.GetComponent<Image>(); limg.sprite = icons[0]; limg.preserveAspect = true; limg.raycastTarget = false;
        slot.lockIcon = lockRt.gameObject;
        slot.nameText = Label("Name", card, 34, TMPro.TextAlignmentOptions.Center, Color.white);
        Frac((RectTransform)slot.nameText.transform, 0.04f, 0.03f, 0.96f, 0.27f);
        slot.requirementText = Label("Requirement", card, 30, TMPro.TextAlignmentOptions.Center, new Color(1f, 0.86f, 0.3f));
        Frac((RectTransform)slot.requirementText.transform, 0.04f, 0.01f, 0.96f, 0.15f);
        var chk = Rect("Equipped", card, typeof(Image)); Frac(chk, 0.74f, 0.79f, 0.98f, 0.99f);
        var cimg = chk.GetComponent<Image>(); cimg.sprite = icons[1]; cimg.preserveAspect = true; cimg.raycastTarget = false;
        slot.equippedIcon = chk.gameObject;
        var dot = Rect("New", card, typeof(Image)); Frac(dot, 0.03f, 0.90f, 0.14f, 0.99f);
        var dimg = dot.GetComponent<Image>(); dimg.sprite = knob; dimg.color = new Color(0.95f, 0.2f, 0.2f); dimg.preserveAspect = true; dimg.raycastTarget = false;
        slot.newDot = dot.gameObject;
        sel.slotTemplate = slot;
        card.gameObject.SetActive(false);

        // --- new racket screen (every part anchored in fractions, so it fits portrait and landscape) ---
        var popup = Rect("NewRacketScreen", holderRt);
        Stretch(popup);
        sel.popup = popup.gameObject;
        var pbg = Rect("Background", popup, typeof(RawImage), typeof(RaysBackground));
        Stretch(pbg);
        sel.popupBackground = pbg.GetComponent<RawImage>();
        sel.popupBackground.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RaysBackground.mat");
        sel.popupBackground.raycastTarget = true;
        var content = Rect("Content", popup);
        Stretch(content);
        sel.popupContent = content;
        sel.popupTitle = Label("Title", content, 84, TMPro.TextAlignmentOptions.Center, Color.white);
        Frac((RectTransform)sel.popupTitle.transform, 0.03f, 0.84f, 0.97f, 0.97f);
        var area = Rect("PictureArea", content); Frac(area, 0.05f, 0.34f, 0.95f, 0.84f);
        var ppic = Rect("Picture", area, typeof(RawImage), typeof(AspectRatioFitter));
        Stretch(ppic);
        var fitter = ppic.GetComponent<AspectRatioFitter>(); fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = 1f;
        sel.popupPicture = ppic.GetComponent<RawImage>(); sel.popupPicture.raycastTarget = false;
        sel.popupName = Label("Name", content, 64, TMPro.TextAlignmentOptions.Center, Color.white);
        Frac((RectTransform)sel.popupName.transform, 0.05f, 0.22f, 0.95f, 0.33f);
        sel.popupOk = BlueButton("Ok", content, "OK", 40, out sel.popupOkLabel);
        var okr = (RectTransform)sel.popupOk.transform;
        okr.anchorMin = okr.anchorMax = new Vector2(0.5f, 0f); okr.pivot = new Vector2(0.5f, 0f); okr.anchoredPosition = new Vector2(0f, 34f); okr.sizeDelta = new Vector2(260f, 76f);
        popup.gameObject.SetActive(false);
        win.gameObject.SetActive(false);

        // the window and the screen are drawn over everything of the HUD but the button
        btnRt.SetAsFirstSibling();
        win.SetAsLastSibling();
        popup.SetAsLastSibling();

        gm.rackets = sel;
        EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[BeachTennisGO] Racket button, window and new racket screen added. Rackets are edited in " + CatalogPath);
    }
}
