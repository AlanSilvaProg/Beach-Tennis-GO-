using UnityEngine;

// Fake shadow of a racket: a flat racket-shaped quad on the ground. The handle end and the head centre of the (tilting) racket
// are projected onto the ground along the sun direction, so the shadow leans, stretches and turns with the racket.
// Lives in the scene as its own object (not a child of the racket, which tilts); follows it every frame, after the racket has moved.
[ExecuteAlways]
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RacketShadow : MonoBehaviour
{
    public Racket racket;
    public ShadowSun sun;
    public Court court;
    [Tooltip("Shadow width, i.e. the width of the racket head (m).")]
    public float width = 0.6f;
    [Tooltip("Where the head centre sits along the shadow (0 = handle end, 1 = far tip); matches the shape in the FakeShadow shader.")]
    [Range(0.5f, 0.9f)] public float headAt = 0.7f;
    [Tooltip("The shadow never gets shorter than this (m), e.g. when the racket leans towards the sun.")]
    public float minLength = 0.4f;

    static readonly int Aspect = Shader.PropertyToID("_Aspect");
    static readonly int Tint = Shader.PropertyToID("_Color");

    MeshFilter mf;
    MeshRenderer mr;
    Mesh mesh;
    MaterialPropertyBlock block;

    void OnEnable()
    {
        mf = GetComponent<MeshFilter>();
        mr = GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        LateUpdate();
    }

    void OnDestroy()
    {
        if (mesh != null) DestroyImmediate(mesh);
    }

    // Ground point under p, shifted along the sun direction by the height above the ground.
    Vector3 Project(Vector3 p, float groundY)
    {
        Vector3 d = sun.Direction * (sun.lengthPerMetre * Mathf.Max(0f, p.y - groundY));
        return new Vector3(p.x + d.x, groundY + sun.lift, p.z + d.z);
    }

    void LateUpdate()
    {
        if (racket == null || sun == null) return;
        if (mesh == null) { mesh = BuildUnitQuad(); mf.sharedMesh = mesh; }
        if (court == null) court = FindFirstObjectByType<Court>();
        float groundY = court != null ? court.groundY : 0f;

        // Handle end (model pivot) and head centre (racket pivot) in world space, tilt included.
        Vector3 head = racket.transform.position;
        Vector3 handle = racket.model != null ? racket.model.position : head - racket.transform.up * (racket.headCenter * racket.modelScale);
        Vector3 b = Project(handle, groundY), h = Project(head, groundY);
        Vector3 axis = h - b;
        axis.y = 0f;
        float length = Mathf.Max(minLength, axis.magnitude / headAt);
        Vector3 dir = axis.sqrMagnitude > 1e-6f ? axis.normalized : sun.Direction;

        transform.SetPositionAndRotation(b, Quaternion.LookRotation(dir, Vector3.up));
        transform.localScale = new Vector3(width, 1f, length);
        if (block == null) block = new MaterialPropertyBlock();
        mr.GetPropertyBlock(block);
        block.SetFloat(Aspect, length / width);
        block.SetColor(Tint, sun.color);
        mr.SetPropertyBlock(block);
    }

    // Quad on the ground: x from -0.5 to 0.5 (across), z from 0 to 1 (along); uv in 0..1.
    static Mesh BuildUnitQuad()
    {
        var m = new Mesh { name = "RacketShadow", hideFlags = HideFlags.HideAndDontSave };
        m.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(-0.5f, 0f, 1f), new Vector3(0.5f, 0f, 1f), new Vector3(0.5f, 0f, 0f) };
        m.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        m.bounds = new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 0.1f, 1f));
        return m;
    }
}
