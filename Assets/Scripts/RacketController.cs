using UnityEngine;

// Base for whoever drives a Racket (the human player or the AI).
[RequireComponent(typeof(Racket))]
public abstract class RacketController : MonoBehaviour
{
    Racket racket;
    public Racket Racket => racket != null ? racket : (racket = GetComponent<Racket>());

    public virtual void ResetPosition() { }
    public abstract void Tick(GameManager gm);
    // Launch velocity of the ball when this racket hits it at `contact`.
    public abstract Vector3 ComputeShot(GameManager gm, Vector3 contact);
    public virtual void OnOpponentHit(GameManager gm) { }
}
