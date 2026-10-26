using System.Collections.Generic;
using UnityEngine;

// Stands with a moving crowd beside the court (put one on each side). The stands are stepped boxes; the crowd is one mesh of small cards, one per
// person, that the Crowd shader turns to face the camera, draws and animates (bobbing, waving arms). Both meshes are built here, when the object
// is enabled and whenever a value changes, so there is nothing to download and the Scene / Game view shows the result as you tweak it.
//   - The tiers rise away from the court; the first tier starts `startX` metres from the centre line (put it just outside the ad boards).
//   - Bleachers.Cheer(amount) makes the whole crowd jump and raise its arms for a moment (0..1); the game calls it when a point is scored.
[ExecuteAlways]
public class Bleachers : MonoBehaviour
{
    public enum Side { Left, Right }

    [Header("Place")]
    [Tooltip("Left stands are at -x and look towards +x; Right stands are the other way round.")]
    public Side side = Side.Left;
    [Tooltip("Distance of the first tier from the centre line of the court (m). The ad boards are at about 8.15.")]
    public float startX = 8.7f;
    [Tooltip("Half of the length of the stands along the court (m).")]
    public float halfLength = 12.5f;

    [Header("Tiers")]
    [Min(1)] public int tiers = 5;
    public float tierDepth = 1.0f;
    public float tierHeight = 0.55f;
    [Tooltip("The risers (the front of each step) alternate between these two colours.")]
    public Color riserA = new Color(0.10f, 0.58f, 0.78f);
    public Color riserB = new Color(0.97f, 0.88f, 0.66f);
    public Color topColor = new Color(0.86f, 0.78f, 0.60f);

    [Header("Crowd")]
    [Tooltip("Space for each person along the stands (m).")]
    public float spacing = 0.75f;
    [Range(0f, 1f)] public float fill = 0.92f;
    [Tooltip("Where on the step the people stand: 0 = front edge, 1 = back edge.")]
    [Range(0f, 1f)] public float standAt = 0.5f;
    public int seed = 7;
    public Color[] shirts =
    {
        new Color(0.95f, 0.35f, 0.25f), new Color(1.00f, 0.75f, 0.20f), new Color(0.15f, 0.70f, 0.90f), new Color(0.98f, 0.98f, 0.95f),
        new Color(0.30f, 0.80f, 0.45f), new Color(0.90f, 0.35f, 0.65f), new Color(0.20f, 0.35f, 0.80f), new Color(1.00f, 0.55f, 0.15f)
    };

    [Header("Materials")]
    public Material standMaterial;
    public Material crowdMaterial;
    [Tooltip("Seconds the crowd takes to calm down after a Cheer.")]
    public float cheerDecay = 2.5f;

    MeshFilter standFilter, crowdFilter;
    Mesh standMesh, crowdMesh;

    static float cheer;
    static int lastFrame = -1;
    static readonly int CheerId = Shader.PropertyToID("_CrowdCheer");

    // 0..1: how hard the crowd cheers right now; it fades out by itself.
    public static void Cheer(float amount) { cheer = Mathf.Max(cheer, Mathf.Clamp01(amount)); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { cheer = 0f; lastFrame = -1; } // static state survives a Play without domain reload

    void OnEnable() { Build(); }

    // Builds the stands and the crowd again (after the values were set from code).
    public void Rebuild() { Build(); }

    void OnDisable() { Release(); }

    void OnValidate()
    {
        if (isActiveAndEnabled) Build();
    }

    void Update()
    {
        if (lastFrame == Time.frameCount) return; // one instance is enough to run the decay and set the shader value
        lastFrame = Time.frameCount;
        cheer = Mathf.MoveTowards(cheer, 0f, Time.unscaledDeltaTime / Mathf.Max(0.1f, cheerDecay));
        Shader.SetGlobalFloat(CheerId, cheer);
    }

    void Release()
    {
        Destroy(ref standMesh);
        Destroy(ref crowdMesh);
    }

    static void Destroy(ref Mesh m)
    {
        if (m == null) return;
        if (Application.isPlaying) Object.Destroy(m); else Object.DestroyImmediate(m);
        m = null;
    }

    MeshFilter Child(string name, Material material)
    {
        var t = transform.Find(name);
        GameObject go = t != null ? t.gameObject : null;
        if (go == null)
        {
            go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
        }
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer.sharedMaterial != material) renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go.GetComponent<MeshFilter>();
    }

    void Build()
    {
        standFilter = Child("Stand", standMaterial);
        crowdFilter = Child("Crowd", crowdMaterial);
        Release();
        float sign = side == Side.Left ? -1f : 1f;
        standMesh = BuildStand(sign);
        crowdMesh = BuildCrowd(sign);
        standFilter.sharedMesh = standMesh;
        crowdFilter.sharedMesh = crowdMesh;
    }

    // ---- stands ----
    readonly List<Vector3> v = new List<Vector3>();
    readonly List<Color> c = new List<Color>();
    readonly List<int> tri = new List<int>();

    // A flat face; its corners are given in any order, and `outward` says which way it must look (the winding is fixed to match).
    void Face(Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color color, Vector3 outward)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(d); v.Add(e);
        for (int k = 0; k < 4; k++) c.Add(color);
        bool flip = Vector3.Dot(Vector3.Cross(b - a, d - a), outward) < 0f;
        if (!flip) { tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); tri.Add(i); tri.Add(i + 2); tri.Add(i + 3); }
        else { tri.Add(i); tri.Add(i + 2); tri.Add(i + 1); tri.Add(i); tri.Add(i + 3); tri.Add(i + 2); }
    }

    static Color Shade(Color color, float f) { return new Color(color.r * f, color.g * f, color.b * f, 1f); }

    Mesh BuildStand(float sign)
    {
        v.Clear(); c.Clear(); tri.Clear();
        float zMin = -halfLength, zMax = halfLength;
        for (int t = 0; t < tiers; t++)
        {
            float x0 = sign * (startX + t * tierDepth), x1 = sign * (startX + (t + 1) * tierDepth);
            float yLow = t * tierHeight, yTop = (t + 1) * tierHeight;
            Vector3 towardsCourt = new Vector3(-sign, 0f, 0f), away = -towardsCourt;
            Color riser = t % 2 == 0 ? riserA : riserB;
            // top: stripes every 1.5 m so the benches do not look like one flat slab
            int stripes = Mathf.Max(1, Mathf.CeilToInt((zMax - zMin) / 1.5f));
            for (int s = 0; s < stripes; s++)
            {
                float za = Mathf.Lerp(zMin, zMax, s / (float)stripes), zb = Mathf.Lerp(zMin, zMax, (s + 1) / (float)stripes);
                Face(new Vector3(x0, yTop, za), new Vector3(x1, yTop, za), new Vector3(x1, yTop, zb), new Vector3(x0, yTop, zb),
                     Shade(topColor, s % 2 == 0 ? 1f : 0.93f), Vector3.up);
            }
            Face(new Vector3(x0, yLow, zMin), new Vector3(x0, yTop, zMin), new Vector3(x0, yTop, zMax), new Vector3(x0, yLow, zMax), Shade(riser, 0.9f), towardsCourt);
            if (t == tiers - 1) Face(new Vector3(x1, 0f, zMin), new Vector3(x1, yTop, zMin), new Vector3(x1, yTop, zMax), new Vector3(x1, 0f, zMax), Shade(riser, 0.6f), away);
            // the two ends of the step
            Face(new Vector3(x0, 0f, zMin), new Vector3(x1, 0f, zMin), new Vector3(x1, yTop, zMin), new Vector3(x0, yTop, zMin), Shade(riser, 0.7f), Vector3.back);
            Face(new Vector3(x0, 0f, zMax), new Vector3(x1, 0f, zMax), new Vector3(x1, yTop, zMax), new Vector3(x0, yTop, zMax), Shade(riser, 0.7f), Vector3.forward);
        }
        var m = new Mesh { name = "Stand", hideFlags = HideFlags.HideAndDontSave };
        m.SetVertices(v); m.SetColors(c); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return m;
    }

    // ---- crowd ----
    Mesh BuildCrowd(float sign)
    {
        var verts = new List<Vector3>();
        var colors = new List<Color>();
        var uv0 = new List<Vector2>();
        var uv1 = new List<Vector2>();
        var indices = new List<int>();
        var rng = new System.Random(seed + (side == Side.Left ? 0 : 1000));
        System.Func<float> next = () => (float)rng.NextDouble();

        float zMin = -halfLength + 0.5f, zMax = halfLength - 0.5f;
        for (int t = 0; t < tiers; t++)
        {
            float x = sign * (startX + (t + standAt) * tierDepth);
            float y = (t + 1) * tierHeight;
            for (float z = zMin + 0.2f * spacing; z <= zMax; z += spacing)
            {
                if (next() > fill) continue;
                var feet = new Vector3(x + (next() - 0.5f) * 0.25f * tierDepth, y, z + (next() - 0.5f) * 0.3f * spacing);
                Color shirt = shirts.Length > 0 ? shirts[rng.Next(shirts.Length)] : Color.white;
                shirt.a = next();                                       // the person's own phase
                Vector2 look = new Vector2(next(), next());              // skin tone, hair tone
                int i = verts.Count;
                for (int k = 0; k < 4; k++)
                {
                    verts.Add(feet);
                    colors.Add(shirt);
                    uv0.Add(new Vector2(k == 1 || k == 2 ? 1f : 0f, k >= 2 ? 1f : 0f));
                    uv1.Add(look);
                }
                indices.Add(i); indices.Add(i + 2); indices.Add(i + 1); indices.Add(i); indices.Add(i + 3); indices.Add(i + 2);
            }
        }
        var m = new Mesh { name = "Crowd", hideFlags = HideFlags.HideAndDontSave };
        m.SetVertices(verts); m.SetColors(colors); m.SetUVs(0, uv0); m.SetUVs(1, uv1); m.SetTriangles(indices, 0);
        // The cards are opened up by the shader, so the real size is larger than the points: give the bounds some room.
        float height = tiers * tierHeight;
        m.bounds = new Bounds(new Vector3(sign * (startX + tiers * tierDepth * 0.5f), height * 0.5f + 1f, 0f),
                              new Vector3(tiers * tierDepth + 4f, height + 4f, halfLength * 2f + 4f));
        return m;
    }
}
