using UnityEngine;

// The other player's racket in an online match. It is a PlayerRacketController (so the shots it makes are exactly the shots of a human player: power,
// angle and drop shots from the swing), but it is not moved by a pointer: it follows the position that arrives over the network.
//  - On the host it is the far-side racket, and the position is the one the guest sends (already in host coordinates).
//  - On the guest it is only drawn, at the position the host reports for the host's racket.
public class RemoteRacketController : PlayerRacketController
{
    [Tooltip("How fast the racket follows the position that arrives (per second): high = snappy, low = smooth but late.")]
    public float follow = 24f;

    Vector3 target;
    bool hasTarget;

    // x, z in the world of the screen this runs on.
    public void SetTarget(Vector2 xz)
    {
        target = new Vector3(xz.x, Racket.height, xz.y);
        if (!hasTarget) { hasTarget = true; Racket.MoveTo(target); Racket.SnapSweep(); }
    }

    public override void ResetPosition()
    {
        hasTarget = false;
        Racket.MoveTo(restPosition);
        Racket.SnapSweep();
    }

    public override void Tick(GameManager gm)
    {
        if (!hasTarget) return;
        float k = 1f - Mathf.Exp(-follow * Time.deltaTime);
        Racket.MoveTo(Vector3.Lerp(Racket.Position, target, k));
    }
}
