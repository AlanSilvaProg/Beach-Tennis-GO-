using UnityEngine;

// Visual + state of a racket. Position is the centre of the racket head.
public class Paddle : MonoBehaviour
{
    public const float RacketScale = 0.6f;
    public const float HeadCenter = 0.68f; // head centre along model Y (model is 1 unit tall, pivot at the handle)
    public const float HitRadius = 0.2f;

    public Vector3 Velocity { get; private set; }
    Vector3 lastPos;
    Vector3 smoothVel;
    Transform model;

    public static Paddle Create(GameObject racketPrefab, string name, bool facingPlayer, Color tint)
    {
        var root = new GameObject(name);
        var paddle = root.AddComponent<Paddle>();
        var m = racketPrefab != null ? Instantiate(racketPrefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
        m.transform.SetParent(root.transform, false);
        m.transform.localScale = Vector3.one * RacketScale;
        m.transform.localPosition = new Vector3(0f, -HeadCenter * RacketScale, 0f);
        root.transform.rotation = Quaternion.Euler(0f, facingPlayer ? 0f : 180f, 0f);
        paddle.model = m.transform;
        paddle.lastPos = root.transform.position;
        return paddle;
    }

    public void Move(Vector3 pos)
    {
        transform.position = pos;
    }

    void LateUpdate()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 v = (transform.position - lastPos) / dt;
        smoothVel = Vector3.Lerp(smoothVel, v, 1f - Mathf.Exp(-dt * 25f));
        Velocity = smoothVel;
        lastPos = transform.position;

        // Lean the racket with its motion for a bit of feel.
        float sign = Mathf.Approximately(transform.eulerAngles.y, 180f) ? -1f : 1f;
        float roll = Mathf.Clamp(-Velocity.x * 6f * sign, -35f, 35f);
        float pitch = Mathf.Clamp(Velocity.y * 4f, -25f, 25f) - 12f;
        transform.rotation = Quaternion.Euler(0f, sign > 0 ? 0f : 180f, 0f) * Quaternion.Euler(pitch, 0f, roll);
    }
}
