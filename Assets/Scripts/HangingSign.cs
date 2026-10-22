using UnityEngine;

// The result board as a sign hung from the top edge of the screen by two chains: it drops in from above with an elastic bounce and then swings,
// and when it is hidden it is pulled back up out of the screen (it is never switched off, it just moves). Pure UI animation, no physics.
// Layout: this object sits at the top centre of the HUD canvas; `board` is the sign (anchored from its own top), the two chains are tiled images
// that are stretched every frame from a fixed point at the top of the screen to the sign's top corners.
// In the editor (not playing) the sign is shown at rest so it can be placed by hand.
[ExecuteAlways]
public class HangingSign : MonoBehaviour
{
    public RectTransform board;
    public RectTransform chainLeft;
    public RectTransform chainRight;

    [Header("Shape")]
    [Tooltip("How far the sign hangs below its resting place of before (canvas units): the visible length of the chains when it is still.")]
    public float chainLength = 44f;
    [Tooltip("Distance of the chains from the sign's centre (canvas units).")]
    public float chainSpacing = 112f;
    [Tooltip("The sign's image has an empty margin above the board; the chains end this far below the top of the image (canvas units).")]
    public float attachDrop = 19f;
    [Tooltip("How high above the screen the sign waits when it is hidden (canvas units).")]
    public float hiddenOffset = 300f;
    [Tooltip("Length of the pendulum, i.e. how far the sign travels sideways per radian of swing.")]
    public float pendulumLength = 150f;

    [Header("Motion")]
    [Tooltip("Strength of the spring that drops / pulls up the sign.")]
    public float dropStiffness = 90f;
    [Tooltip("Lower = bouncier when the chains stretch at the end of the fall.")]
    public float dropDamping = 11.8f;
    [Tooltip("Swing frequency (rad/s) and how fast it dies out.")]
    public float swingFrequency = 6.2f;
    public float swingDamping = 1.1f;
    [Tooltip("Sideways kick (rad/s) when the sign lands.")]
    public float landingKick = 1.7f;
    [Tooltip("How much the sign tilts with the swing (fraction of the swing angle).")]
    [Range(0f, 1f)] public float tilt = 0.35f;

    bool shown;
    bool landed;
    float yOffset, yVelocity;   // + = above the resting place
    float angle, angularVelocity;
    public float YOffset => yOffset;
    public float Angle => angle;
    float restY, restX;
    bool initialised;
    int kickSide = 1;

    public bool IsShown => shown;

    void OnEnable()
    {
        if (board != null)
        {
            restX = board.anchoredPosition.x;
            restY = board.anchoredPosition.y;
        }
        initialised = true;
        if (Application.isPlaying) { yOffset = hiddenOffset; shown = false; }
        else { yOffset = 0f; shown = true; }
        Apply();
    }

    public void Show(bool value)
    {
        if (!Application.isPlaying) return;
        if (shown == value) return;
        shown = value;
        if (value)
        {
            landed = false;
            kickSide = -kickSide; // alternate the side of the swing from one appearance to the next
        }
        else
        {
            angularVelocity += 0.8f * kickSide; // a little sway while it is pulled up
        }
    }

    void Update()
    {
        if (board == null) return;
        if (!Application.isPlaying)
        {
            if (yOffset != 0f || angle != 0f) { yOffset = 0f; angle = 0f; }
            Apply();
            return;
        }
        Step(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
    }

    // One step of the motion (also used by the tests).
    public void Step(float dt)
    {
        // Vertical spring towards "hanging" (0) or "up and out" (hiddenOffset).
        float target = shown ? 0f : hiddenOffset;
        float stiffness = shown ? dropStiffness : dropStiffness * 1.4f;
        float damping = shown ? dropDamping : dropDamping * 2.2f; // pulled up firmly, no bounce
        yVelocity += (-stiffness * (yOffset - target) - damping * yVelocity) * dt;
        yOffset += yVelocity * dt;

        // The chains go taut when the sign reaches its resting place: it swings from there.
        if (shown && !landed && yOffset <= 0f)
        {
            landed = true;
            angularVelocity += landingKick * kickSide;
        }

        // Damped pendulum.
        angularVelocity += (-swingFrequency * swingFrequency * angle - swingDamping * angularVelocity) * dt;
        angle += angularVelocity * dt;
        if (!shown && yOffset > hiddenOffset * 0.8f) { angle *= 0.9f; angularVelocity *= 0.9f; } // out of sight: settle

        Apply();
    }

    // Puts the sign and the chains where the state says. Writes only what changed (keeps the editor quiet).
    void Apply()
    {
        if (board == null || !initialised) return;
        if (!Application.isPlaying) { restX = board.anchoredPosition.x; restY = board.anchoredPosition.y; } // being placed by hand

        Vector2 pos = new Vector2(restX + pendulumLength * Mathf.Sin(angle), restY + yOffset + pendulumLength * (1f - Mathf.Cos(angle)));
        float rotation = -angle * Mathf.Rad2Deg * tilt;
        if (Application.isPlaying)
        {
            board.anchoredPosition = pos;
            board.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        Chain(chainLeft, -chainSpacing, pos, rotation);
        Chain(chainRight, chainSpacing, pos, rotation);
    }

    // One chain: from a fixed point just above the top of the screen to the sign's top corner on that side.
    void Chain(RectTransform chain, float side, Vector2 boardPos, float boardRotationDeg)
    {
        if (chain == null) return;
        float rad = boardRotationDeg * Mathf.Deg2Rad;
        Vector2 attach = boardPos + new Vector2(side * Mathf.Cos(rad) + attachDrop * Mathf.Sin(rad), side * Mathf.Sin(rad) - attachDrop * Mathf.Cos(rad)); // the board's visible top corner
        Vector2 anchorPoint = new Vector2(restX + side, 12f);                                    // fixed, just above the screen
        Vector2 d = attach - anchorPoint;
        attach += d.normalized * 8f; // the chain runs a little behind the sign, so no gap ever shows when it swings
        d = attach - anchorPoint;
        float length = d.magnitude;
        chain.anchoredPosition = anchorPoint + d * 0.5f;
        chain.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 90f);
        chain.sizeDelta = new Vector2(chain.sizeDelta.x, length);
    }
}
