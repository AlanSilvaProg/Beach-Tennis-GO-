using UnityEngine;

// Fake shadow of the net on the ground: the band (a slanted strip, softer than the posts because the net is see-through) and
// the two posts, projected along the sun direction. Rebuilt when the net size or the sun changes (also in the editor).
[ExecuteAlways]
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class NetShadow : MonoBehaviour
{
    public Net net;
    public ShadowSun sun;
    public Court court;
    [Tooltip("Width of a post's shadow (m).")]
    public float postWidth = 0.12f;

    static readonly int Tint = Shader.PropertyToID("_Color");

    MeshFilter mf;
    MeshRenderer mr;
    Mesh mesh;
    MaterialPropertyBlock block;
    int builtHash;

    void OnEnable()
    {
        mf = GetComponent<MeshFilter>();
        mr = GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        builtHash = 0;
        LateUpdate();
    }

    void OnDestroy()
    {
        if (mesh != null) DestroyImmediate(mesh);
    }

    void LateUpdate()
    {
        if (net == null || sun == null) return;
        if (mesh == null) { mesh = new Mesh { name = "NetShadow", hideFlags = HideFlags.HideAndDontSave }; mf.sharedMesh = mesh; builtHash = 0; }
        if (court == null) court = FindFirstObjectByType<Court>();
        int hash = sun.Hash ^ (net.width * 13f + net.height * 29f + net.bandHeight * 41f + postWidth * 53f).GetHashCode();
        if (hash != builtHash) { builtHash = hash; Build(); }

        transform.SetPositionAndRotation(new Vector3(net.transform.position.x, (court != null ? court.groundY : 0f) + sun.lift, net.transform.position.z), Quaternion.identity);
        if (block == null) block = new MaterialPropertyBlock();
        mr.GetPropertyBlock(block);
        block.SetColor(Tint, sun.color);
        mr.SetPropertyBlock(block);
    }

    void Build()
    {
        float k = sun.lengthPerMetre;
        Vector3 d = sun.Direction;
        float half = net.width * 0.5f;
        float top = net.height, bottom = net.height - net.bandHeight;
        Vector3 side = Vector3.Cross(Vector3.up, d).normalized * (postWidth * 0.5f); // across a post's shadow

        var v = new System.Collections.Generic.List<Vector3>();
        var uv = new System.Collections.Generic.List<Vector2>();
        var tri = new System.Collections.Generic.List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 e, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ue)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(e);
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ue);
            tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        // Net band: each point (x, height) lands at (x, 0) + d * k * height. uv.y = 0 at the bottom edge, 1 at the top (tape).
        Vector3 b0 = new Vector3(-half, 0f, 0f) + d * (k * bottom), b1 = new Vector3(half, 0f, 0f) + d * (k * bottom);
        Vector3 t0 = new Vector3(-half, 0f, 0f) + d * (k * top), t1 = new Vector3(half, 0f, 0f) + d * (k * top);
        Quad(b0, t0, t1, b1, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
        // Posts (uv.x < 0 marks them: solid in the shader).
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 foot = new Vector3(s * half, 0f, 0f), tip = foot + d * (k * top);
            Quad(foot - side, tip - side, tip + side, foot + side, new Vector2(-1f, 0f), new Vector2(-1f, 1f), new Vector2(-1f, 1f), new Vector2(-1f, 0f));
        }
        mesh.Clear();
        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
    }
}
