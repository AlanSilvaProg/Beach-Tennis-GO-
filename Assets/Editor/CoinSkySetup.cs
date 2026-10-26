using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Two scene additions: the sky behind the sea (a skybox with drifting clouds) and the Rally coin holder (menu and Rackets window).
// Safe to run again. Menu: Tools > Beach Tennis GO > Add Sky / Add Rally Coin Holder
public static class CoinSkySetup
{
    const string ScenePath = "Assets/Scene/Gameplay.unity";
    const string FontPath = "Assets/Fonts/LilitaOne-Regular.ttf";

    static UnityEngine.SceneManagement.Scene OpenScene()
    {
        var active = EditorSceneManager.GetActiveScene();
        return active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath);
    }

    // ---------------- sky ----------------

    [MenuItem("Tools/Beach Tennis GO/Add Sky")]
    public static void AddSky()
    {
        var scene = OpenScene();
        const string texPath = "Assets/Skybox/sky-box.jpg";
        var tex = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (tex != null)
        {
            tex.textureType = TextureImporterType.Default;
            tex.alphaSource = TextureImporterAlphaSource.None;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.mipmapEnabled = true;
            tex.maxTextureSize = 2048;
            tex.textureCompression = TextureImporterCompression.Compressed;
            tex.crunchedCompression = true; tex.compressionQuality = 85;
            tex.SaveAndReimport();
        }
        const string matPath = "Assets/Skybox/Sky.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("BeachTennisGO/Sky"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        var picture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        mat.SetTexture("_MainTex", picture);
        mat.SetFloat("_TopElevation", 5.8f);
        mat.SetFloat("_WidthDegrees", 30f);
        EditorUtility.SetDirty(mat);

        // The far sea melts into the colour of the picture's bottom edge (the horizon), so the sky runs down into the sea without a line.
        var sea = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Sea.mat");
        if (sea != null && sea.HasProperty("_HorizonColor")) { sea.SetColor("_HorizonColor", HorizonColor(picture)); EditorUtility.SetDirty(sea); }

        // The default skybox also lit the scene (ambient light); keep exactly that light now that the skybox changes.
        var ambient = RenderSettings.ambientLight;
        RenderSettings.skybox = mat;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.Skybox; EditorUtility.SetDirty(cam); }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[BeachTennisGO] Sky set (Assets/Skybox/Sky.mat).");
    }

    // Average colour of the bottom rows of the picture, read from the file (the imported texture is compressed).
    static Color HorizonColor(Texture2D picture)
    {
        var path = AssetDatabase.GetAssetPath(picture);
        var raw = new Texture2D(2, 2);
        raw.LoadImage(System.IO.File.ReadAllBytes(path));
        Color sum = Color.black; int n = 0;
        for (int x = 0; x < raw.width; x += 8) { sum += raw.GetPixel(x, 2); n++; }
        Object.DestroyImmediate(raw);
        var c = sum / Mathf.Max(1, n);
        c.a = 1f;
        return c;
    }

    // ---------------- rewarded ad rackets ----------------

    [MenuItem("Tools/Beach Tennis GO/Add Rewarded Ad Racket Icon")]
    public static void AddAdIcon()
    {
        var scene = OpenScene();
        var sel = Object.FindFirstObjectByType<RacketSelection>(FindObjectsInactive.Include);
        if (sel == null || sel.slotTemplate == null) return;
        const string path = "Assets/Sprites/racket-icons.png";
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        imp.maxTextureSize = 512; // the sheet is 384 wide
        var names = new[] { "lock", "check", "ad" };
        var rects = new SpriteMetaData[names.Length];
        for (int i = 0; i < names.Length; i++)
            rects[i] = new SpriteMetaData { name = names[i], rect = new Rect(i * 128, 0, 128, 128), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
#pragma warning disable CS0618
        imp.spritesheet = rects;
#pragma warning restore CS0618
        imp.SaveAndReimport();
        Sprite adSprite = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Sprite sp && sp.name == "ad") adSprite = sp;

        var slot = sel.slotTemplate;
        if (slot.adIcon == null)
        {
            var go = new GameObject("AdIcon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(slot.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.14f, 0.01f); rt.anchorMax = new Vector2(0.38f, 0.15f); rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>(); img.sprite = adSprite; img.preserveAspect = true; img.raycastTarget = false;
            slot.adIcon = go;
            go.SetActive(false);
            EditorUtility.SetDirty(slot);
        }

        // Oak is the one that is unlocked by an ad
        var catalog = AssetDatabase.LoadAssetAtPath<RacketCatalog>("Assets/Data/RacketCatalog.asset");
        var oak = catalog != null ? catalog.Find("oak") : null;
        if (oak != null) { oak.unlock.kind = UnlockKind.RewardedAd; oak.unlock.requirementText = ""; EditorUtility.SetDirty(catalog); }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[BeachTennisGO] Rewarded ad racket icon added; Oak now unlocks with an ad.");
    }

    // ---------------- coin holder ----------------

    [MenuItem("Tools/Beach Tennis GO/Add Rally Coin Holder")]
    public static void AddCoinHolder()
    {
        var scene = OpenScene();
        var menu = Object.FindFirstObjectByType<GameModeMenu>(FindObjectsInactive.Include);
        var sel = Object.FindFirstObjectByType<RacketSelection>(FindObjectsInactive.Include);
        if (menu == null || menu.root == null) return;

        var pillSprite = LoadSliced("Assets/Sprites/coin-holder.png", 30);
        var coinSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/rally-coin.png");
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

        if (menu.root.GetComponentInChildren<CoinHolder>(true) == null)
            Make("RallyCoinHolder", menu.root.transform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(150f, 44f), pillSprite, coinSprite, font);

        if (sel != null && sel.window != null && sel.window.GetComponentInChildren<CoinHolder>(true) == null)
        {
            var footer = sel.window.transform.Find("Footer");
            if (footer != null)
            {
                var back = footer.Find("Back");
                if (back != null) ((RectTransform)back).sizeDelta = new Vector2(160f, 56f); // room for the holder on a narrow screen
                var holder = Make("RallyCoinHolder", footer, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(170f, 48f), pillSprite, coinSprite, font);
                holder.pivot = new Vector2(1f, 0.5f);
            }
        }

        // the coin in front of a price on a locked card
        if (sel != null && sel.slotTemplate != null && sel.slotTemplate.coinIcon == null)
        {
            var slot = sel.slotTemplate;
            var go = new GameObject("CoinIcon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(slot.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.14f, 0.01f); rt.anchorMax = new Vector2(0.38f, 0.15f); rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>(); img.sprite = coinSprite; img.preserveAspect = true; img.raycastTarget = false;
            slot.coinIcon = go;
            go.SetActive(false);
            EditorUtility.SetDirty(slot);
        }

        // prices: Oak by level, Golden for coins
        var catalog = AssetDatabase.LoadAssetAtPath<RacketCatalog>("Assets/Data/RacketCatalog.asset");
        if (catalog != null)
        {
            var golden = catalog.Find("golden");
            if (golden != null && golden.unlock.kind == UnlockKind.Manual) { golden.unlock.kind = UnlockKind.RallyCoins; golden.unlock.value = 300; golden.unlock.requirementText = ""; EditorUtility.SetDirty(catalog); }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[BeachTennisGO] Rally coin holder added to the menu and the Rackets window.");
    }

    static Sprite LoadSliced(string path, int border)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.wrapMode = TextureWrapMode.Clamp; imp.filterMode = FilterMode.Bilinear;
            imp.spriteBorder = new Vector4(border, border, border, border);
            imp.textureCompression = TextureImporterCompression.Compressed; imp.crunchedCompression = true; imp.compressionQuality = 90;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // The pill with the coin on its left and the number; anchored at `anchor` of its parent.
    static RectTransform Make(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Sprite pill, Sprite coin, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CoinHolder));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.GetComponent<Image>(); img.sprite = pill; img.type = Image.Type.Sliced; img.raycastTarget = false;
        img.pixelsPerUnitMultiplier = pill.rect.height / size.y; // the rounded ends are as big as the pill is tall, whatever its size

        var c = new GameObject("Coin", typeof(RectTransform), typeof(Image));
        c.transform.SetParent(rt, false);
        var crt = (RectTransform)c.transform;
        crt.anchorMin = crt.anchorMax = new Vector2(0f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = new Vector2(size.y * 0.42f, 0f); crt.sizeDelta = new Vector2(size.y * 1.3f, size.y * 1.3f);
        var cimg = c.GetComponent<Image>(); cimg.sprite = coin; cimg.preserveAspect = true; cimg.raycastTarget = false;

        var t = new GameObject("Amount", typeof(RectTransform), typeof(Text), typeof(Outline));
        t.transform.SetParent(rt, false);
        var trt = (RectTransform)t.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(size.y * 1.15f, 2f); trt.offsetMax = new Vector2(-size.y * 0.4f, -2f);
        var text = t.GetComponent<Text>();
        text.font = font; text.fontSize = Mathf.RoundToInt(size.y * 0.75f); text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow; text.text = "0";
        var o = t.GetComponent<Outline>(); o.effectColor = new Color(0.03f, 0.1f, 0.34f, 1f); o.effectDistance = new Vector2(2f, -2f);
        var tmp = TmpConvert.Convert(text);
        tmp.alignment = TMPro.TextAlignmentOptions.Center; tmp.enableAutoSizing = true; tmp.fontSizeMin = 8; tmp.fontSizeMax = Mathf.RoundToInt(size.y * 0.75f); tmp.enableWordWrapping = false;
        go.GetComponent<CoinHolder>().amountText = tmp;
        return rt;
    }
}
