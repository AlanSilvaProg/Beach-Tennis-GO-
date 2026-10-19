using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum RingSide { Left, Right, Far, Near }

// Stadium-style perimeter of ad boards around the court. The ring follows the court dimensions: the boards
// are always the same size and sit side by side. The area is not configured directly: it grows with the
// board counts (sideCount sets the depth, endCount the width), but
// never closer to the court than `minMargin`. Change a count in the inspector and boards are added/removed and
// the ring re-fits immediately.
public class AdPanelRing : MonoBehaviour
{
    [System.Serializable]
    public class Run
    {
        public RingSide side;
        public AdPanel[] panels;
    }

    public Court court;
    [Tooltip("Board used when a side needs more boards.")]
    public AdPanel panelPrefab;

    [Header("Area")]
    [Tooltip("The boards never come closer than this to the sidelines/baselines (m). Beyond that, the area grows with the board counts.")]
    [Min(0f)] public float minMargin = 1f;

    [Header("Boards")]
    [Tooltip("Boards on each long side (left and right).")]
    [Min(0)] public int sideCount = 5;
    [Tooltip("Boards behind each baseline (far and near).")]
    [Min(0)] public int endCount = 3;

    [Header("Board")]
    public float width = 3.97f;
    public float height = 1.1f;
    [Tooltip("Space between neighbouring boards (m).")]
    public float gap = 0.03f;
    [Tooltip("Thickness of a board, used so the corners close up (m).")]
    public float thickness = 0.14f;
    public Run[] runs;

    public int Count(RingSide side)
    {
        return side == RingSide.Left || side == RingSide.Right ? sideCount : endCount;
    }

    void OnValidate()
    {
        Fit();
#if UNITY_EDITOR
        // Creating/destroying objects is not allowed inside OnValidate, so do it right after.
        if (!Application.isPlaying && gameObject.scene.IsValid())
            EditorApplication.delayCall += () => { if (this != null) SyncPanels(); };
#endif
    }

    void Awake() { Fit(); }

    float Step => width + gap;

    // Half extents of the board rectangle (centre of the boards), derived from the counts.
    public Vector2 HalfExtents
    {
        get
        {
            float halfX = (endCount * Step + thickness) * 0.5f;
            float halfZ = (sideCount * Step - thickness) * 0.5f;
            if (court == null) return new Vector2(halfX, halfZ);
            return new Vector2(Mathf.Max(halfX, court.halfWidth + minMargin), Mathf.Max(halfZ, court.halfLength + minMargin));
        }
    }

    // Resulting distance from the court lines to the boards (x: sides, y: ends).
    public Vector2 Margin
    {
        get
        {
            var h = HalfExtents;
            return court == null ? h : new Vector2(h.x - court.halfWidth, h.y - court.halfLength);
        }
    }

    public void Fit()
    {
        if (court == null || runs == null) return;
        Vector3 c = court.transform.position;
        Vector2 half = HalfExtents;
        float halfX = half.x, halfZ = half.y;
        float y = court.groundY + height * 0.5f + 0.05f;

        foreach (var run in runs)
        {
            if (run == null || run.panels == null || run.panels.Length == 0) continue;
            Vector3 centre, along, outward;
            switch (run.side)
            {
                case RingSide.Left: centre = new Vector3(-halfX, 0f, 0f); along = Vector3.forward; outward = Vector3.left; break;
                case RingSide.Right: centre = new Vector3(halfX, 0f, 0f); along = Vector3.forward; outward = Vector3.right; break;
                case RingSide.Far: centre = new Vector3(0f, 0f, halfZ); along = Vector3.right; outward = Vector3.forward; break;
                default: centre = new Vector3(0f, 0f, -halfZ); along = Vector3.right; outward = Vector3.back; break;
            }
            int n = run.panels.Length;
            for (int i = 0; i < n; i++)
            {
                var p = run.panels[i];
                if (p == null) continue;
                Vector3 pos = c + centre + along * ((i - (n - 1) * 0.5f) * Step);
                pos.y = y;
                p.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(outward));
                p.size = new Vector2(width, height);
                p.Fit();
            }
        }
    }

#if UNITY_EDITOR
    static readonly RingSide[] Sides = { RingSide.Left, RingSide.Right, RingSide.Far, RingSide.Near };

    bool InSync()
    {
        if (runs == null || runs.Length != Sides.Length) return false;
        for (int s = 0; s < Sides.Length; s++)
            if (runs[s] == null || runs[s].side != Sides[s] || runs[s].panels == null || runs[s].panels.Length != Count(Sides[s])) return false;
        foreach (var run in runs)
            foreach (var p in run.panels)
                if (p == null) return false;
        return true;
    }

    // Adds or removes boards until every side has its count. Existing boards (and their settings) are kept.
    [ContextMenu("Rebuild boards")]
    public void SyncPanels()
    {
        if (court == null || panelPrefab == null || Application.isPlaying || InSync()) return;

        var old = runs;
        var newRuns = new Run[Sides.Length];
        int made = 0;
        for (int s = 0; s < Sides.Length; s++)
        {
            var side = Sides[s];
            var keep = new System.Collections.Generic.List<AdPanel>();
            if (old != null)
                foreach (var r in old)
                    if (r != null && r.side == side && r.panels != null)
                        foreach (var p in r.panels) if (p != null) keep.Add(p);

            int want = Count(side);
            for (int i = want; i < keep.Count; i++) Undo.DestroyObjectImmediate(keep[i].gameObject);
            if (keep.Count > want) keep.RemoveRange(want, keep.Count - want);

            for (int i = keep.Count; i < want; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab.gameObject, transform);
                Undo.RegisterCreatedObjectUndo(go, "Add ad board");
                var ap = go.GetComponent<AdPanel>();
                if (i % 2 == 0 && panelPrefab.sprites != null && panelPrefab.sprites.Length > 0)
                {
                    ap.mode = AdMode.Loop;
                    ap.sprites = new[] { panelPrefab.sprites[0], panelPrefab.sprites[0] };
                    ap.holdTime = 3f + (made % 3) * 0.5f;
                    ap.startDelay = (made * 0.7f) % 3f;
                }
                keep.Add(ap);
                made++;
            }
            for (int i = 0; i < keep.Count; i++) keep[i].gameObject.name = "AdPanel_" + side + "_" + i;
            newRuns[s] = new Run { side = side, panels = keep.ToArray() };
        }
        Undo.RecordObject(this, "Update ad boards");
        runs = newRuns;
        Fit();
        EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
}
