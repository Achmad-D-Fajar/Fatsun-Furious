// =============================================================================
// AnimalHazard.cs
// Animal obstacle (kucing, ayam, dll) yang diam lalu meloncat sekali
// secara horizontal ketika player memasuki zona deteksi di depannya.
//
// HIERARKI SETUP:
//
//   Animal_Kucing_01                  ← GameObject ini, pasang AnimalHazard.cs
//   ├── Sprite                        ← SpriteRenderer, art kucing/ayam
//   ├── BodyCollider                  ← BoxCollider2D (IsTrigger✓) — hitbox tubuh
//   └── DetectionZone                 ← BoxCollider2D (IsTrigger✓) + AnimalDetectionZone.cs
//
// CARA KERJA:
//   1. Animal diam, menampilkan idleSprite.
//   2. Player memasuki DetectionZone → setelah reactionDelay, animal mulai meloncat.
//   3. Selama meloncat, animasi 2-frame (runSprite1 ↔ runSprite2) berjalan.
//   4. Animal bergerak horizontal menuju jumpTargetX (LOCAL space).
//   5. Sampai di tujuan → berhenti, kembali ke idleSprite.
//   6. Jika player menabrak BodyCollider kapan saja → Game Over.
// =============================================================================

using System.Collections;
using UnityEngine;

public class AnimalHazard : MonoBehaviour
{
    [Header("─── Identity ───────────────────────────────────────")]
    [Tooltip("Label debug di Hierarchy.")]
    [SerializeField] private string animalName = "Kucing";

    [Tooltip("Pesan Game Over ketika player menabrak animal ini.")]
    [SerializeField] private string failureReason = "NABRAK!";

    [Header("─── Reaction ───────────────────────────────────────")]
    [Tooltip("Jeda (detik) setelah player terdeteksi sebelum animal mulai meloncat. " +
             "0.2–0.4 terasa natural (efek 'kaget' sebentar).")]
    [SerializeField] [Range(0f, 1f)] private float reactionDelay = 0.25f;

    [Header("─── Jump Destination ────────────────────────────────")]
    [Tooltip("Posisi X tujuan lompatan dalam LOCAL SPACE (relatif ke parent prefab). " +
             "Animal bergerak horizontal dari posisinya saat ini menuju nilai X ini.\n\n" +
             "Contoh:\n" +
             "  Animal di X = -2.4 (tepi kiri), tujuan X = 2.4 (tepi kanan)\n" +
             "  Animal di X =  1.8 (lane kanan), tujuan X = -1.8 (lane kiri)")]
    [SerializeField] private float jumpTargetX = 2.4f;

    [Tooltip("Kecepatan lompat (units/detik).")]
    [SerializeField] [Range(0.5f, 15f)] private float jumpSpeed = 5f;

    [Header("─── Idle Sprite ─────────────────────────────────────")]
    [Tooltip("Sprite saat diam (sebelum dan sesudah meloncat).")]
    [SerializeField] private Sprite idleSprite;

    [Tooltip("Ukuran tampilan idle sprite dalam WORLD UNITS (lebar × tinggi). " +
             "Set ini secara manual sesuai ukuran yang diinginkan di Scene.\n\n" +
             "Tips: Aktifkan Gizmos lalu lihat Scene view — BoxCollider2D body " +
             "bisa jadi referensi visual ukurannya.")]
    [SerializeField] private Vector2 idleDisplaySize = new Vector2(0.5f, 0.5f);

    [Header("─── Run Sprites ─────────────────────────────────────")]
    [Tooltip("Sprite lari / loncat frame 1.")]
    [SerializeField] private Sprite runSprite1;

    [Tooltip("Sprite lari / loncat frame 2 — bergantian dengan frame 1.")]
    [SerializeField] private Sprite runSprite2;

    [Tooltip("Ukuran tampilan run sprite dalam WORLD UNITS (lebar × tinggi). " +
             "Dipakai untuk runSprite1 dan runSprite2 sekaligus.\n\n" +
             "Set terpisah dari idleDisplaySize agar sprite lari bisa " +
             "lebih lebar/panjang dari sprite idle tanpa saling mempengaruhi.")]
    [SerializeField] private Vector2 runDisplaySize = new Vector2(0.6f, 0.5f);

    [Tooltip("Kecepatan pergantian frame animasi (detik per frame). " +
             "0.08 ≈ 12fps, 0.1 ≈ 10fps.")]
    [SerializeField] [Range(0.04f, 0.5f)] private float runAnimInterval = 0.1f;

    [Header("─── Sprite Renderer ─────────────────────────────────")]
    [Tooltip("SpriteRenderer yang dipakai untuk menampilkan sprite dan flip arah.")]
    [SerializeField] private SpriteRenderer animalSprite;

    [Tooltip("Flip sprite secara horizontal sesuai arah lompat " +
             "(berguna agar animal selalu 'menghadap' arah geraknya).")]
    [SerializeField] private bool flipSpriteWithDirection = true;

    // ── Private State ─────────────────────────────────────────────────────────

    private bool      _hasJumped    = false;
    private Coroutine _jumpCoroutine = null;
    private Coroutine _animCoroutine = null;

    // ==========================================================================
    //  Unity Lifecycle
    // ==========================================================================

    private void Awake()
    {
        ApplySprite(idleSprite, isRunFrame: false);
    }

    // ==========================================================================
    //  Public API — dipanggil oleh AnimalDetectionZone.cs
    // ==========================================================================

    /// <summary>Dipanggil saat player memasuki DetectionZone.</summary>
    public void OnPlayerEnterDetection()
    {
        if (_hasJumped) return;
        if (_jumpCoroutine != null) return;

        _jumpCoroutine = StartCoroutine(JumpSequence());
    }

    /// <summary>Dipanggil saat player keluar DetectionZone. Lompatan tidak dibatalkan.</summary>
    public void OnPlayerExitDetection() { }

    // ==========================================================================
    //  Jump Sequence
    // ==========================================================================

    private IEnumerator JumpSequence()
    {
        // ── 1. Reaction delay ────────────────────────────────────────────────
        if (reactionDelay > 0f)
            yield return new WaitForSeconds(reactionDelay);

        _hasJumped = true;

        // ── 2. Tentukan arah dan flip sprite ─────────────────────────────────
        float startX    = transform.localPosition.x;
        float targetX   = jumpTargetX;
        float direction = Mathf.Sign(targetX - startX); // +1 kanan, -1 kiri

        if (flipSpriteWithDirection && animalSprite != null)
            animalSprite.flipX = direction < 0f;

        // ── 3. Mulai animasi lari ────────────────────────────────────────────
        StartRunAnimation();

        // ── 4. Gerak horizontal sampai tujuan ────────────────────────────────
        while (true)
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.Playing)
            {
                float step     = jumpSpeed * Time.deltaTime;
                float currentX = transform.localPosition.x;
                float newX     = Mathf.MoveTowards(currentX, targetX, step);

                transform.localPosition = new Vector3(
                    newX,
                    transform.localPosition.y,
                    transform.localPosition.z);

                if (Mathf.Approximately(newX, targetX))
                    break;
            }

            yield return null;
        }

        // ── 5. Sampai tujuan — berhenti dan kembali idle ─────────────────────
        StopRunAnimation();
        ApplySprite(idleSprite, isRunFrame: false);

        _jumpCoroutine = null;
        Debug.Log($"[AnimalHazard] {animalName} selesai meloncat ke X={targetX}.");
    }

    // ==========================================================================
    //  Animation Helpers
    // ==========================================================================

    private void StartRunAnimation()
    {
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(RunAnimation());
    }

    private void StopRunAnimation()
    {
        if (_animCoroutine == null) return;
        StopCoroutine(_animCoroutine);
        _animCoroutine = null;
    }

    private IEnumerator RunAnimation()
    {
        bool showFrame1 = true;
        WaitForSeconds wait = new WaitForSeconds(runAnimInterval);

        while (true)
        {
            ApplySprite(showFrame1 ? runSprite1 : runSprite2, isRunFrame: true);
            showFrame1 = !showFrame1;
            yield return wait;
        }
    }

    // ==========================================================================
    //  Sprite + Size Application
    // ==========================================================================

    /// <summary>
    /// Ganti sprite dan set localScale agar ukuran tampilan di Scene
    /// persis sesuai idleDisplaySize (idle) atau runDisplaySize (run).
    ///
    /// Cara hitung:
    ///   sprite.bounds.size = ukuran sprite dalam world units pada scale = 1.
    ///   localScale target  = displaySize / sprite.bounds.size
    ///
    /// Karena idle dan run punya field size masing-masing, keduanya
    /// dikontrol penuh secara manual tanpa heuristik otomatis.
    /// </summary>
    private void ApplySprite(Sprite s, bool isRunFrame)
    {
        if (animalSprite == null || s == null) return;

        animalSprite.sprite = s;

        // Pilih target ukuran sesuai jenis frame.
        Vector2 targetSize = isRunFrame ? runDisplaySize : idleDisplaySize;

        // sprite.bounds.size = ukuran world-unit saat localScale = (1,1,1).
        Vector2 spriteNativeSize = s.bounds.size;

        if (spriteNativeSize.x <= 0f || spriteNativeSize.y <= 0f) return;

        // Hitung scale yang diperlukan untuk mencapai targetSize.
        animalSprite.transform.localScale = new Vector3(
            targetSize.x / spriteNativeSize.x,
            targetSize.y / spriteNativeSize.y,
            animalSprite.transform.localScale.z   // Z tidak diubah
        );
    }

    // ==========================================================================
    //  Body Collision — Game Over
    // ==========================================================================

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameState.Playing) return;

        GameManager.Instance.TriggerGameOver(failureReason);
    }

    // ==========================================================================
    //  Editor Gizmos
    // ==========================================================================

    private void OnDrawGizmosSelected()
    {
        // Body collider — merah
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.45f);
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        }

        // Visualisasi idle display size — putih, di posisi sekarang
        if (idleDisplaySize.x > 0f && idleDisplaySize.y > 0f)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            Gizmos.DrawCube(transform.position,
                new Vector3(idleDisplaySize.x, idleDisplaySize.y, 0.01f));
        }

        // Garis tujuan lompatan + visualisasi run display size — hijau
        Vector3 origin = transform.position;
        Vector3 targetWorld = transform.parent != null
            ? transform.parent.TransformPoint(
                new Vector3(jumpTargetX, transform.localPosition.y, 0f))
            : new Vector3(jumpTargetX, transform.position.y, 0f);

        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.8f);
        Gizmos.DrawLine(origin, targetWorld);
        Gizmos.DrawSphere(targetWorld, 0.12f);

        if (runDisplaySize.x > 0f && runDisplaySize.y > 0f)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.2f);
            Gizmos.DrawCube(targetWorld,
                new Vector3(runDisplaySize.x, runDisplaySize.y, 0.01f));
        }

        // Panah arah
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(origin, (targetWorld - origin).normalized * 0.6f);
    }
}