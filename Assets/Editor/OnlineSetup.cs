using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds the Online scene: a copy of the Gameplay scene (same court, HUD, sounds, rackets...) with the online mode's own objects, so that the
// training mode and the online mode do not share a scene. In the copy: no mode menu and no tap hint, the CPU's racket gets the fake opponent's
// controller (OnlineRacketController) and the other player's (RemoteRacketController), and the search screen and OnlineMatch are added.
// Run again after changing the Gameplay scene to bring the changes over (the Online scene is made again from scratch).
// Menu: Tools > Beach Tennis GO > Online > Build Online Scene
public static class OnlineSetup
{
    const string GameplayPath = "Assets/Scene/Gameplay.unity";
    const string OnlinePath = "Assets/Scene/Online.unity";
    const string FontPath = "Assets/Fonts/LilitaOne-Regular.ttf";

    [MenuItem("Tools/Beach Tennis GO/Online/Build Online Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[BeachTennisGO] Leave Play mode first."); return; }
        CleanGameplay();

        AssetDatabase.DeleteAsset(OnlinePath);
        if (!AssetDatabase.CopyAsset(GameplayPath, OnlinePath)) { Debug.LogError("[BeachTennisGO] Could not copy the Gameplay scene."); return; }
        var scene = EditorSceneManager.OpenScene(OnlinePath);
        Configure();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // both scenes are in the build; Gameplay first (it is the one that loads)
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(GameplayPath, true),
            new EditorBuildSettingsScene(OnlinePath, true),
        };
        EditorSceneManager.OpenScene(GameplayPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[BeachTennisGO] Online scene built (" + OnlinePath + "); both scenes are in the build settings.");
    }

    // The training scene has no online objects (an earlier version of the fake online mode left some there).
    static void CleanGameplay()
    {
        var scene = EditorSceneManager.OpenScene(GameplayPath);
        foreach (var mm in Resources.FindObjectsOfTypeAll<OnlineMatchmaking>())
        {
            if (!mm.gameObject.scene.IsValid()) continue;
            if (mm.window != null) Object.DestroyImmediate(mm.window);
            Object.DestroyImmediate(mm.gameObject);
        }
        foreach (var o in Resources.FindObjectsOfTypeAll<OnlineRacketController>())
            if (o.gameObject.scene.IsValid()) Object.DestroyImmediate(o);
        foreach (var o in Resources.FindObjectsOfTypeAll<RemoteRacketController>())
            if (o.gameObject.scene.IsValid()) Object.DestroyImmediate(o);
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null) { gm.onlineScene = false; EditorUtility.SetDirty(gm); }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void CopyShared(Component from, Component to)
    {
        var src = new SerializedObject(from);
        var dst = new SerializedObject(to);
        var it = src.GetIterator();
        for (bool enter = true; it.NextVisible(enter); enter = false)
        {
            if (it.name == "m_Script") continue;
            var target = dst.FindProperty(it.propertyPath);
            if (target != null && target.propertyType == it.propertyType) dst.CopyFromSerializedProperty(it);
        }
        dst.ApplyModifiedProperties();
    }

    static void Configure()
    {
        var gm = Object.FindFirstObjectByType<GameManager>();
        var view = Object.FindFirstObjectByType<HudView>();
        gm.onlineScene = true;

        // no mode menu, no tap hint: this scene has one job
        var menu = Object.FindFirstObjectByType<GameModeMenu>(FindObjectsInactive.Include);
        if (menu != null) { Object.DestroyImmediate(menu.gameObject); gm.menu = null; }
        var hint = Object.FindFirstObjectByType<TapHint>(FindObjectsInactive.Include);
        if (hint != null) Object.DestroyImmediate(hint.gameObject);

        // the far racket: the fake opponent's brain (starts as a copy of the training CPU's tuning) and the other player's racket controller
        var farRacket = gm.ai.gameObject;
        var training = gm.ai;
        var fake = farRacket.AddComponent<OnlineRacketController>();
        CopyShared(training, fake);
        var remote = farRacket.AddComponent<RemoteRacketController>();
        CopyShared(gm.player, remote);
        remote.cam = null;
        remote.restPosition = new Vector3(0f, 1f, 4.8f);
        gm.ai = fake;
        Object.DestroyImmediate(training);

        // no rewarded rematch online
        if (view != null && view.retryButton != null) view.retryButton.gameObject.SetActive(false);

        // the objects of the online flow
        var canvas = view.infoText.canvas.transform;
        var screen = BuildScreen(canvas);
        var root = new GameObject("Online");
        var session = root.AddComponent<OnlineSession>();
        var match = root.AddComponent<OnlineMatch>();
        match.game = gm; match.session = session; match.screen = screen; match.remote = remote; match.fake = fake;
        match.rackets = Object.FindFirstObjectByType<RacketSelection>(FindObjectsInactive.Include);
        match.poki = Object.FindFirstObjectByType<PokiService>();
        EditorUtility.SetDirty(gm);
    }

    static OnlineMatchmaking BuildScreen(Transform canvas)
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        var box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var navy = new Color(0.03f, 0.12f, 0.38f, 1f);

        TMPro.TMP_Text Label(string name, Transform parent, int size, TMPro.TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.color = color; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.text = name;
            var o = go.GetComponent<Outline>(); o.effectColor = navy; o.effectDistance = new Vector2(3f, -3f);
            var tmp = TmpConvert.Convert(t);
            tmp.alignment = align; tmp.enableAutoSizing = true; tmp.fontSizeMin = 8; tmp.fontSizeMax = size; tmp.enableWordWrapping = false; tmp.richText = true;
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

        // always-active holder of the component (the window is hidden, and a hidden object never gets its Awake)
        var holder = Rect("OnlineMatchmakingController", canvas);
        holder.sizeDelta = new Vector2(100f, 100f);
        var mm = holder.gameObject.AddComponent<OnlineMatchmaking>();

        var win = Rect("OnlineMatchmaking", canvas);
        Frac(win, 0f, 0f, 1f, 1f);
        mm.window = win.gameObject;
        var bg = Rect("Background", win, typeof(RawImage), typeof(RaysBackground));
        Frac(bg, 0f, 0f, 1f, 1f);
        var bgImg = bg.GetComponent<RawImage>();
        bgImg.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SettingsBackground.mat"); bgImg.raycastTarget = true;

        mm.title = Label("Title", win, 64, TMPro.TextAlignmentOptions.Center, Color.white);
        var trt = (RectTransform)mm.title.transform;
        trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -40f); trt.sizeDelta = new Vector2(-40f, 90f);

        // the ball that spins while it searches
        var spin = Rect("Spinner", win, typeof(Image));
        spin.anchorMin = spin.anchorMax = new Vector2(0.5f, 0.62f); spin.pivot = new Vector2(0.5f, 0.5f); spin.sizeDelta = new Vector2(110f, 110f);
        var simg = spin.GetComponent<Image>(); simg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ball2D.png"); simg.preserveAspect = true; simg.raycastTarget = false;
        mm.spinner = spin;

        mm.searchText = Label("SearchText", win, 46, TMPro.TextAlignmentOptions.Center, Color.white);
        Frac((RectTransform)mm.searchText.transform, 0.04f, 0.38f, 0.96f, 0.5f);

        var found = Rect("Found", win);
        Frac(found, 0f, 0.3f, 1f, 0.8f);
        mm.foundGroup = found.gameObject;
        mm.foundLabel = Label("FoundLabel", found, 44, TMPro.TextAlignmentOptions.Center, new Color(1f, 0.92f, 0.35f));
        Frac((RectTransform)mm.foundLabel.transform, 0.04f, 0.62f, 0.96f, 0.92f);
        mm.opponentName = Label("Name", found, 84, TMPro.TextAlignmentOptions.Center, Color.white);
        Frac((RectTransform)mm.opponentName.transform, 0.04f, 0.18f, 0.96f, 0.6f);
        found.gameObject.SetActive(false);

        var cancel = Rect("Cancel", win, typeof(Image), typeof(Button));
        cancel.anchorMin = cancel.anchorMax = new Vector2(0.5f, 0f); cancel.pivot = new Vector2(0.5f, 0f); cancel.anchoredPosition = new Vector2(0f, 48f); cancel.sizeDelta = new Vector2(260f, 70f);
        var cimg = cancel.GetComponent<Image>(); cimg.sprite = box; cimg.type = Image.Type.Sliced; cimg.color = Color.white;
        var btn = cancel.GetComponent<Button>(); btn.targetGraphic = cimg;
        var color = new Color(0.86f, 0.25f, 0.28f);
        var c = btn.colors; c.normalColor = color; c.highlightedColor = Color.Lerp(color, Color.white, 0.2f); c.pressedColor = Color.Lerp(color, Color.black, 0.25f); c.selectedColor = color;
        btn.colors = c;
        mm.cancelLabel = Label("Label", cancel, 36, TMPro.TextAlignmentOptions.Center, Color.white);
        Frac((RectTransform)mm.cancelLabel.transform, 0.03f, 0.03f, 0.97f, 0.97f);
        mm.cancelButton = btn;

        win.gameObject.SetActive(false);
        win.SetAsLastSibling();
        return mm;
    }
}
