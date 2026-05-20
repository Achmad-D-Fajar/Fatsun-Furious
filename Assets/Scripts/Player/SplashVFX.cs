using UnityEngine;

public class SplashVFX : MonoBehaviour
{
    [Header("─── VFX Configuration ──────────────────────────")]
    [SerializeField] private float lifetime      = 0.5f;
    [SerializeField] private float punchSpeed    = 12f;
    [SerializeField] private float maxScale      = 1.2f;
    [SerializeField] private ParticleSystem splashParticles;

    [SerializeField] private Color safeColor  = new Color(0.4f, 0.8f, 1f, 1f);
    [SerializeField] private Color dirtyColor = new Color(0.6f, 0.4f, 0.2f, 1f);

    private float          _timer;
    private bool           _shrinking;
    private SpriteRenderer _sr;

    public void Init(bool isDirty)
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null) _sr.color = isDirty ? dirtyColor : safeColor;

        if (splashParticles != null)
        {
            var main        = splashParticles.main;
            main.startColor = isDirty ? dirtyColor : safeColor;
            splashParticles.Play();
        }
    }

    private void Start()
    {
        transform.localScale = Vector3.zero;
        _timer     = 0f;
        _shrinking = false;
    }

    private void Update()
    {
        _timer += Time.unscaledDeltaTime;

        if (!_shrinking)
        {
            float s = Mathf.MoveTowards(transform.localScale.x, maxScale,
                                         punchSpeed * Time.unscaledDeltaTime);
            transform.localScale = Vector3.one * s;
            if (_timer >= lifetime * 0.4f) _shrinking = true;
        }
        else
        {
            float s = Mathf.MoveTowards(transform.localScale.x, 0f,
                                         punchSpeed * 0.5f * Time.unscaledDeltaTime);
            transform.localScale = Vector3.one * s;
        }

        if (_timer >= lifetime) Destroy(gameObject);
    }
}