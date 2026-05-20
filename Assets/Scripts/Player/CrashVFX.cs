// =============================================================================
// CrashVFX.cs
// Attach ke Prefab VFX crash. Auto-destroy setelah animasi selesai.
// =============================================================================

using System.Collections;
using UnityEngine;

public class CrashVFX : MonoBehaviour
{
    [Header("─── VFX Configuration ──────────────────────────")]
    [Tooltip("Durasi VFX ditampilkan sebelum auto-destroy (detik).")]
    [SerializeField] private float lifetime = 0.6f;

    [Tooltip("Animasi scale dari kecil ke besar saat muncul.")]
    [SerializeField] private float punchScaleAmount = 1.4f;

    [Tooltip("Kecepatan animasi punch scale.")]
    [SerializeField] private float punchScaleSpeed = 8f;

    [Tooltip("Optional: ParticleSystem child untuk efek percikan.")]
    [SerializeField] private ParticleSystem burstParticles;

    private float _timer;
    private bool  _shrinking;

    private void Start()
    {
        // Mainkan particle burst kalau ada
        if (burstParticles != null)
            burstParticles.Play();

        // Mulai dari scale 0, punch ke besar, lalu shrink
        transform.localScale = Vector3.zero;
        _timer    = 0f;
        _shrinking = false;
    }

    private void Update()
    {
        _timer += Time.unscaledDeltaTime; // unscaled agar jalan walau timeScale = 0

        if (!_shrinking)
        {
            // Fase punch in — scale naik cepat ke punchScaleAmount
            float s = Mathf.MoveTowards(
                transform.localScale.x,
                punchScaleAmount,
                punchScaleSpeed * Time.unscaledDeltaTime
            );
            transform.localScale = Vector3.one * s;

            // Setelah setengah lifetime, mulai shrink
            if (_timer >= lifetime * 0.5f)
                _shrinking = true;
        }
        else
        {
            // Fase shrink out — scale turun ke 0
            float s = Mathf.MoveTowards(
                transform.localScale.x,
                0f,
                punchScaleSpeed * 0.6f * Time.unscaledDeltaTime
            );
            transform.localScale = Vector3.one * s;
        }

        // Destroy setelah lifetime habis
        if (_timer >= lifetime)
            Destroy(gameObject);
    }
}