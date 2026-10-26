using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds the Rally coin reward panel of the win screen in the scene and the RallyRewardConfig asset. Safe to run again: nothing that exists is replaced.
// Menu: Tools > Beach Tennis GO > Add Win Reward Panel
public static class WinRewardSetup
{
    const string ScenePath = "Assets/Scene/Gameplay.unity";
    const string ConfigPath = "Assets/Data/RallyRewardConfig.asset";
    const string FontPath = "Assets/Fonts/LilitaOne-Regular.ttf";

    [MenuItem("Tools/Beach Tennis GO/Add Win Reward Panel")]
    public static void Add() { Ensure(); }

    static Sprite[] SliceSheet(string path, string[] names, int cell, int border)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return null;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.wrapMode = TextureWrapMode.Clamp; imp.filterMode = FilterMode.Bilinear;
        imp.maxTextureSize = 512; imp.npotScale = TextureImporterNPOTScale.None;
        imp.textureCompression = TextureImporterCompression.Compressed; imp.crunchedCompression = true; imp.compressionQuality = 90;
        var rects = new SpriteMetaData[names.Length];
        for (int i = 0; i < names.Length; i++)
            rects[i] = new SpriteMetaData { name = names[i], rect = new Rect(i * cell, 0, cell, cell), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f), border = new Vector4(border, border, border, border) };
#pragma warning disable CS0618
        imp.spritesheet = rects;
#pragma warning restore CS0618
        imp.SaveAndReimport();
        var found = new Sprite[names.Length];
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite sp) { int i = System.Array.IndexOf(names, sp.name); if (i >= 0) found[i] = sp; }
        return found;
    }

    static Sprite Single(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return null;
        imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
        imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.wrapMode = TextureWrapMode.Clamp; imp.filterMode = FilterMode.Bilinear;
        imp.maxTextureSize = 256; imp.npotScale = TextureImporterNPOTScale.None;
        imp.textureCompression = TextureImporterCompression.Compressed; imp.crunchedCompression = true; imp.compressionQuality = 90;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // The turning golden rays behind the coin (under it in the order of the panel's children).
    static void EnsureGlow(RectTransform panelRt, WinRewardPanel panel)
    {
        var glowSprite = Single("Assets/Sprites/reward-glow.png");
        // The rays are clipped to the inside of the panel (a mask under the frame), like the reference.
        var clip = new GameObject("GlowClip", typeof(RectTransform), typeof(RectMask2D));
        clip.transform.SetParent(panelRt, false);
        var crt = (RectTransform)clip.transform;
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = new Vector2(10f, 10f); crt.offsetMax = new Vector2(-10f, -10f);
        crt.SetAsFirstSibling();
        var g = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        g.transform.SetParent(crt, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(75.6f - 10f, -80f + 10f); rt.sizeDelta = new Vector2(300f, 300f);
        var img = g.GetComponent<Image>(); img.sprite = glowSprite; img.preserveAspect = true; img.raycastTarget = false;
        panel.glow = rt;
    }

    [MenuItem("Tools/Beach Tennis GO/Add Win Reward Glow")]
    public static void AddGlow()
    {
        var active = EditorSceneManager.GetActiveScene();
        var scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath);
        var panel = Object.FindFirstObjectByType<WinRewardPanel>(FindObjectsInactive.Include);
        if (panel == null || panel.glow != null) return;
        EnsureGlow((RectTransform)panel.transform, panel);
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void Ensure()
    {
        var active = EditorSceneManager.GetActiveScene();
        var scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath);
        var view = Object.FindFirstObjectByType<HudView>();
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (view == null || view.winTitle == null || gm == null) return;
        if (Object.FindFirstObjectByType<WinRewardPanel>(FindObjectsInactive.Include) != null) return;

        if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
        var config = AssetDatabase.LoadAssetAtPath<RallyRewardConfig>(ConfigPath);
        if (config == null) { config = ScriptableObject.CreateInstance<RallyRewardConfig>(); AssetDatabase.CreateAsset(config, ConfigPath); }

        var ui = SliceSheet("Assets/Sprites/reward-ui.png", new[] { "reward-panel", "reward-ribbon", "reward-box" }, 128, 44);
        var coinSprite = Single("Assets/Sprites/rally-coin.png");
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        var navy = new Color(0.03f, 0.1f, 0.34f, 1f);

        TMPro.TMP_Text Label(string name, Transform parent, int size, TMPro.TextAlignmentOptions align, Color color, float outline = 3f, Color? gradBottom = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.color = color; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false; t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.text = name;
            var o = go.GetComponent<Outline>(); o.effectColor = navy; o.effectDistance = new Vector2(outline, -outline);
            if (gradBottom.HasValue) { var g = go.AddComponent<UiTextGradient>(); g.top = color; g.bottom = gradBottom.Value; }
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

        void Frac(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1); rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // The win title becomes a full-screen holder (the pop-in scales it); a Stack inside it holds the parts that WinScreenLayout arranges.
        var winRt = (RectTransform)view.winTitle.transform;
        winRt.anchorMin = Vector2.zero; winRt.anchorMax = Vector2.one; winRt.offsetMin = winRt.offsetMax = Vector2.zero; winRt.pivot = new Vector2(0.5f, 0.5f);
        var stack = Rect("Stack", winRt);
        foreach (var part in new Transform[] { view.winLine1.transform, view.winLine2.transform, view.winContinueButton.transform, view.winBackButton.transform })
            part.SetParent(stack, false);

        // --- the panel ---
        var panelRt = Rect("RewardPanel", stack, typeof(Image), typeof(WinRewardPanel));
        panelRt.sizeDelta = new Vector2(360f, 244f);
        var pimg = panelRt.GetComponent<Image>(); pimg.sprite = ui[0]; pimg.type = Image.Type.Sliced; pimg.raycastTarget = false;
        var panel = panelRt.GetComponent<WinRewardPanel>();

        EnsureGlow(panelRt, panel);
        var coin = Rect("Coin", panelRt, typeof(Image));
        coin.anchorMin = coin.anchorMax = new Vector2(0.21f, 1f); coin.pivot = new Vector2(0.5f, 0.5f);
        coin.anchoredPosition = new Vector2(0f, -80f); coin.sizeDelta = new Vector2(112f, 112f);
        var cimg = coin.GetComponent<Image>(); cimg.sprite = coinSprite; cimg.preserveAspect = true; cimg.raycastTarget = false;
        panel.coin = coin;

        var ribbon = Rect("Ribbon", panelRt, typeof(Image));
        ribbon.anchorMin = new Vector2(0.40f, 1f); ribbon.anchorMax = new Vector2(0.97f, 1f); ribbon.pivot = new Vector2(0.5f, 1f);
        ribbon.anchoredPosition = new Vector2(0f, -12f); ribbon.sizeDelta = new Vector2(0f, 34f);
        var rimg = ribbon.GetComponent<Image>(); rimg.sprite = ui[1]; rimg.type = Image.Type.Sliced; rimg.raycastTarget = false;
        panel.ribbonText = Label("RibbonText", ribbon, 24, TMPro.TextAlignmentOptions.Center, Color.white, 2f);
        Frac((RectTransform)panel.ribbonText.transform, 0.04f, 0.04f, 0.96f, 0.96f);

        panel.amountText = Label("Amount", panelRt, 64, TMPro.TextAlignmentOptions.Center, new Color(1f, 0.93f, 0.4f), 5f, new Color(1f, 0.68f, 0.08f));
        var art = (RectTransform)panel.amountText.transform;
        art.anchorMin = new Vector2(0.40f, 1f); art.anchorMax = new Vector2(0.97f, 1f); art.pivot = new Vector2(0.5f, 1f);
        art.anchoredPosition = new Vector2(0f, -44f); art.sizeDelta = new Vector2(0f, 62f);

        panel.coinsLabel = Label("CoinsLabel", panelRt, 28, TMPro.TextAlignmentOptions.Center, Color.white, 3f);
        var clt = (RectTransform)panel.coinsLabel.transform;
        clt.anchorMin = new Vector2(0.40f, 1f); clt.anchorMax = new Vector2(0.97f, 1f); clt.pivot = new Vector2(0.5f, 1f);
        clt.anchoredPosition = new Vector2(0f, -104f); clt.sizeDelta = new Vector2(0f, 30f);

        var list = Rect("List", panelRt, typeof(Image));
        list.anchorMin = new Vector2(0f, 0f); list.anchorMax = new Vector2(1f, 0f); list.pivot = new Vector2(0.5f, 0f);
        list.anchoredPosition = new Vector2(0f, 14f); list.sizeDelta = new Vector2(-28f, 88f);
        var limg = list.GetComponent<Image>(); limg.sprite = ui[2]; limg.type = Image.Type.Sliced; limg.raycastTarget = false;
        panel.listBox = list;

        var row = Rect("RowTemplate", list);
        row.anchorMin = new Vector2(0f, 1f); row.anchorMax = new Vector2(1f, 1f); row.pivot = new Vector2(0.5f, 1f); row.sizeDelta = new Vector2(0f, 32f);
        var rlabel = Label("Label", row, 24, TMPro.TextAlignmentOptions.Left, new Color(0.86f, 0.94f, 1f), 2f);
        Frac((RectTransform)rlabel.transform, 0.03f, 0f, 0.66f, 1f);
        var rvalue = Label("Value", row, 28, TMPro.TextAlignmentOptions.Right, new Color(1f, 0.85f, 0.25f), 2.5f);
        Frac((RectTransform)rvalue.transform, 0.66f, 0f, 0.97f, 1f);
        var rline = Rect("Line", row, typeof(Image));
        rline.anchorMin = new Vector2(0.02f, 0f); rline.anchorMax = new Vector2(0.98f, 0f); rline.pivot = new Vector2(0.5f, 0.5f); rline.anchoredPosition = Vector2.zero; rline.sizeDelta = new Vector2(0f, 1.5f);
        var lineImg = rline.GetComponent<Image>(); lineImg.color = new Color(0.45f, 0.6f, 0.95f, 0.6f); lineImg.raycastTarget = false;
        panel.rowTemplate = row;
        row.gameObject.SetActive(false);

        // --- layout of the whole screen ---
        var layout = view.winTitle.AddComponent<WinScreenLayout>();
        layout.stack = stack; layout.line1 = (RectTransform)view.winLine1.transform; layout.line2 = (RectTransform)view.winLine2.transform;
        layout.panel = panel; layout.continueButton = (RectTransform)view.winContinueButton.transform; layout.backButton = (RectTransform)view.winBackButton.transform;

        gm.rewardConfig = config; gm.rewardPanel = panel;
        EditorUtility.SetDirty(gm);
        EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[BeachTennisGO] Win reward panel added. The values are in " + ConfigPath);
    }
}
