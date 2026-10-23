using UnityEngine;
using UnityEngine.UI;

// "Tap to play" hint of the menu: a hand that taps on the player's racket, with a ring and a few stars bursting from the fingertip at every tap
// (the same hand, timing and effect as the Match3 playable ad). It follows the racket's place on the screen and is only shown in the menu.
// The hand loops in 1 s: it starts on the racket, moves away down-right while tilting, comes back, and the tap effect fires on arrival.
public class TapHint : MonoBehaviour
{
    [Header("Scene")]
    public GameManager game;
    [Tooltip("Everything that is drawn (hand, ring, stars); switched off outside the menu.")]
    public GameObject content;
    public RectTransform hand;
    public Image ring;
    public Image[] stars;
    [Tooltip("Where the fingertip is, from the middle of the hand picture (canvas units).")]
    public Vector2 fingertip = new Vector2(-1f, 47.3f);

    [Header("Hand loop")]
    [Tooltip("Size of the hand (1 = the picture's own size); the fingertip offset and the travel scale with it.")]
    public float handScale = 0.85f;
    public float loopTime = 1f;
    [Tooltip("How far the hand moves away from the racket and back (canvas units).")]
    public Vector2 travel = new Vector2(50.5f, -44.2f);
    [Tooltip("Tilt (degrees) at the far end of the loop.")]
    public float tilt = -28.9f;

    [Header("Tap effect")]
    public float ringSize = 120f;
    public float ringTime = 0.6f;
    public float starSpeed = 200f;
    public float starSpeedVariance = 50f;
    public float starTangent = 80f;
    public float starTime = 0.4f;

    RectTransform area;
    float loopStart;
    float ringStart = -10f;
    float starStart = -10f;
    Vector2[] starPos, starVel;
    bool wasShown;

    void Awake()
    {
        area = (RectTransform)transform.parent;
        if (ring != null) ring.gameObject.SetActive(false);
        if (stars != null) foreach (var s in stars) if (s != null) s.gameObject.SetActive(false);
        starPos = new Vector2[stars != null ? stars.Length : 0];
        starVel = new Vector2[starPos.Length];
        if (content != null) content.SetActive(false);
    }

    void OnValidate() { if (hand != null) hand.localScale = Vector3.one * handScale; }

    void Update()
    {
        bool show = game != null && game.InMenu;
        if (content != null && content.activeSelf != show) content.SetActive(show);
        if (!show) { wasShown = false; return; }
        if (!wasShown) { wasShown = true; loopStart = Time.unscaledTime; }

        // The fingertip rests on the racket, wherever it is on the screen.
        Vector2 target = RacketOnCanvas();
        float loops = (Time.unscaledTime - loopStart) / Mathf.Max(0.1f, loopTime);
        float k = loops - Mathf.Floor(loops);
        float away = 1f - Mathf.Abs(2f * k - 1f);                 // 0 -> 1 -> 0 over one loop
        hand.localScale = Vector3.one * handScale;
        hand.anchoredPosition = target - fingertip * handScale + travel * (handScale * away);
        hand.localEulerAngles = new Vector3(0f, 0f, tilt * away);

        // The tap lands when the hand is back on the racket: the moment the loop starts again.
        int loopNumber = Mathf.FloorToInt(loops);
        if (loopNumber > lastLoop) Tap(target);
        lastLoop = loopNumber;

        UpdateEffects();
    }

    int lastLoop;

    Vector2 RacketOnCanvas()
    {
        var cam = Camera.main;
        if (cam == null || game == null || game.player == null) return Vector2.zero;
        Vector3 screen = cam.WorldToScreenPoint(game.player.Racket.Position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local);
        return local;
    }

    void Tap(Vector2 at)
    {
        ringStart = starStart = Time.unscaledTime;
        if (ring != null) { ring.rectTransform.anchoredPosition = at; ring.gameObject.SetActive(true); }
        for (int i = 0; i < starPos.Length; i++)
        {
            starPos[i] = at;
            float angle = Random.value * Mathf.PI * 2f;
            starVel[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (starSpeed + Random.Range(-starSpeedVariance, starSpeedVariance));
            if (stars[i] != null) stars[i].gameObject.SetActive(true);
        }
    }

    void UpdateEffects()
    {
        float dt = Time.unscaledDeltaTime;
        if (ring != null && ring.gameObject.activeSelf)
        {
            float k = (Time.unscaledTime - ringStart) / ringTime;
            if (k >= 1f) ring.gameObject.SetActive(false);
            else
            {
                ring.rectTransform.sizeDelta = Vector2.one * (ringSize * k);
                var c = ring.color; c.a = 1f - k; ring.color = c;
            }
        }
        float ks = (Time.unscaledTime - starStart) / starTime;
        for (int i = 0; i < starPos.Length; i++)
        {
            var s = stars[i];
            if (s == null || !s.gameObject.activeSelf) continue;
            if (ks >= 1f) { s.gameObject.SetActive(false); continue; }
            Vector2 radial = starPos[i] - (Vector2)ring.rectTransform.anchoredPosition;
            Vector2 side = radial.sqrMagnitude > 0.01f ? new Vector2(-radial.y, radial.x).normalized : Vector2.zero; // sideways push: the stars curl a little
            starVel[i] += side * starTangent * dt;
            starPos[i] += starVel[i] * dt;
            s.rectTransform.anchoredPosition = starPos[i];
            var c = s.color; c.a = 1f - ks; s.color = c;
        }
    }
}
