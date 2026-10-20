using UnityEngine;

// Direction and length of the fake ground shadows (rackets, net). Plain data: RacketShadow and NetShadow read it.
public class ShadowSun : MonoBehaviour
{
    [Tooltip("Direction the shadows fall, in degrees: 0 = away from the player (+Z), positive turns towards +X (the right of the screen).")]
    [Range(-180f, 180f)] public float angle = 30f;
    [Tooltip("Length of a shadow on the ground per metre of height.")]
    [Min(0f)] public float lengthPerMetre = 0.6f;
    [Tooltip("Shadow colour; alpha is its darkness.")]
    public Color color = new Color(0.22f, 0.14f, 0.04f, 0.45f);
    [Tooltip("Height above the ground (must clear the court lines and sit under the ball shadow's draw order).")]
    public float lift = 0.012f;

    public Vector3 Direction
    {
        get
        {
            float a = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        }
    }

    public int Hash => (angle * 31f + lengthPerMetre * 17f + lift * 7f).GetHashCode();
}
