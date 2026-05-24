// =============================================================================
// ObstacleHazard.cs
// =============================================================================

using System.Collections;
using UnityEngine;

public class ObstacleHazard : MonoBehaviour
{
    [Header("─── Obstacle Type ────────────────────────────────────")]
    [Tooltip("Label debug di Hierarchy.")]
    [SerializeField] private string obstacleName = "Obstacle";

    [Tooltip("Pesan Game Over ketika player menabrak obstacle ini.")]
    [SerializeField] private string failureReason = "NABRAK!";

    [Header("─── Local Movement ──────────────────────────────────")]
    [SerializeField] private bool hasLocalMovement = false;

    [Tooltip("Arah movement di local space. (1,0)=kanan, (0,1)=atas, (1,1)=diagonal.")]
    [SerializeField] private Vector2 localMoveDirection = Vector2.right;

    [SerializeField] private float moveSpeed = 2f;

    // ─────────────────────────────────────────────────────────────────────────
    [Header("─── Horizontal Bounce ────────────────────────────────")]
    [Tooltip("Bounce kiri-kanan di dalam batas alley.")]
    [SerializeField] private bool bounceHorizontal = false;

    [Tooltip("Offset kiri dari posisi spawn — LOCAL X relatif ke titik awal obstacle.\n\n" +
             "Contoh: obstacle diletakkan di X = -1.8, bounceMinX = -0.9\n" +
             "→ batas kiri bounce = -1.8 + (-0.9) = -2.7\n\n" +
             "Gunakan nilai negatif untuk ke kiri dari spawn, positif untuk ke kanan.")]
    [SerializeField] private float bounceMinX = -1.8f;

    [Tooltip("Offset kanan dari posisi spawn — LOCAL X relatif ke titik awal obstacle.\n\n" +
             "Contoh: obstacle diletakkan di X = -1.8, bounceMaxX = 3.6\n" +
             "→ batas kanan bounce = -1.8 + 3.6 = 1.8")]
    [SerializeField] private float bounceMaxX =  1.8f;

    [Header("─── Horizontal Sprites ───────────────────────────────")]
    [Tooltip("SpriteRenderer obstacle (opsional — biarkan kosong jika tidak butuh animasi).")]
    [SerializeField] private SpriteRenderer horizontalSpriteRenderer;

    [Tooltip("Frame animasi 1 untuk gerakan horizontal.")]
    [SerializeField] private Sprite hFrameA;

    [Tooltip("Frame animasi 2 untuk gerakan horizontal (bergantian dengan frame 1).")]
    [SerializeField] private Sprite hFrameB;

    [Tooltip("Kecepatan pergantian frame animasi horizontal (detik per frame).")]
    [SerializeField] [Range(0.04f, 0.5f)] private float hAnimInterval = 0.1f;

    [Tooltip("Flip sprite horizontal di awal (sebelum bounce balik).\n" +
             "Centang ini kalau sprite default menghadap kiri tapi obstacle mulai bergerak ke kanan.")]
    [SerializeField] private bool flipHorizontalOnStart = false;

    // ─────────────────────────────────────────────────────────────────────────
    [Header("─── Vertical Patrol ──────────────────────────────────")]
    [Tooltip("NPC bolak-balik atas-bawah di antara dua titik Y.")]
    [SerializeField] private bool patrolVertical = false;

    [Tooltip("Offset Y ATAS dari posisi spawn obstacle.\n\n" +
             "Contoh: obstacle diletakkan di localY = 30, patrolMaxY = 1.5\n" +
             "→ batas atas patrol = 30 + 1.5 = 31.5\n\n" +
             "Gunakan nilai positif untuk ke atas, negatif jika ingin batas atas " +
             "di bawah posisi spawn (tidak umum).")]
    [SerializeField] private float patrolMaxY =  1.5f;

    [Tooltip("Offset Y BAWAH dari posisi spawn obstacle.\n\n" +
             "Contoh: obstacle diletakkan di localY = 30, patrolMinY = -1.5\n" +
             "→ batas bawah patrol = 30 + (-1.5) = 28.5")]
    [SerializeField] private float patrolMinY = -1.5f;

    [Tooltip("Mulai bergerak ke atas dulu. Uncheck untuk mulai ke bawah.")]
    [SerializeField] private bool startMovingUp = true;

    [Header("─── Vertical Sprites ────────────────────────────────")]
    [Tooltip("SpriteRenderer untuk gerakan vertikal (boleh sama dengan horizontal, atau beda).")]
    [SerializeField] private SpriteRenderer verticalSpriteRenderer;

    [Tooltip("Sprite saat obstacle bergerak KE ATAS.")]
    [SerializeField] private Sprite vSpriteUp;

    [Tooltip("Sprite saat obstacle bergerak KE BAWAH.")]
    [SerializeField] private Sprite vSpriteDown;

    // ─────────────────────────────────────────────────────────────────────────
    [Header("─── Jiggle (Bob) ─────────────────────────────────────")]
    [Tooltip("Aktifkan efek jiggle naik-turun saat obstacle bergerak.\n" +
             "Jiggle diterapkan ke child SpriteRenderer, bukan ke transform root, " +
             "sehingga tidak mempengaruhi collider atau posisi aktual obstacle.")]
    [SerializeField] private bool enableJiggle = true;

    [Tooltip("Seberapa jauh sprite bergerak naik/turun (world units). " +
             "0.05–0.12 terasa natural untuk pixel art.")]
    [SerializeField] [Range(0f, 0.3f)] private float jiggleAmplitude = 0.07f;

    [Tooltip("Seberapa cepat siklus jiggle (cycles/detik). " +
             "8–14 terasa seperti langkah kaki.")]
    [SerializeField] [Range(1f, 30f)] private float jiggleFrequency = 10f;

    [Tooltip("Transform yang dijiggle (biasanya child 'Sprite'). " +
             "Jika kosong, script akan cari child pertama dengan SpriteRenderer.")]
    [SerializeField] private Transform jiggleTarget;

    // ── Private ───────────────────────────────────────────────────────────────

    private Vector2   _currentDirection;
    private bool      _movingUp          = true;
    private bool      _movingRight       = true;
    private Coroutine _hAnimCoroutine    = null;
    private Vector3   _jiggleOrigin;

    // Posisi localPosition obstacle saat di-spawn (di-capture di Awake).
    // Semua bounds patrol dan bounce dihitung sebagai OFFSET dari titik ini,
    // bukan sebagai koordinat absolut — sehingga obstacle yang ditempatkan
    // di Y=30 tidak langsung terkena snap-to-bounds di frame pertama.
    private Vector3   _spawnLocalPos;

    // ==========================================================================
    //  Unity Lifecycle
    // ==========================================================================

    private void Awake()
    {
        // Capture posisi spawn SEBELUM movement apapun.
        // Semua bounds patrol/bounce adalah offset dari titik ini.
        _spawnLocalPos = transform.localPosition;

        _currentDirection = localMoveDirection.normalized;

        // Tentukan arah vertikal awal.
        _movingUp = startMovingUp;
        if (patrolVertical)
            _currentDirection.y = _movingUp
                ? Mathf.Abs(_currentDirection.y)
                : -Mathf.Abs(_currentDirection.y);

        // Arah horizontal awal.
        _movingRight = _currentDirection.x >= 0f;

        // Flip sprite horizontal awal jika diminta.
        if (horizontalSpriteRenderer != null)
            horizontalSpriteRenderer.flipX = flipHorizontalOnStart ? !_movingRight : _movingRight;

        // Cari jiggle target otomatis jika tidak di-assign.
        if (jiggleTarget == null && enableJiggle)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) jiggleTarget = sr.transform;
        }

        if (jiggleTarget != null)
            _jiggleOrigin = jiggleTarget.localPosition;
    }

    private void Start()
    {
        // Mulai animasi horizontal jika aktif.
        if (hasLocalMovement && bounceHorizontal &&
            horizontalSpriteRenderer != null && (hFrameA != null || hFrameB != null))
        {
            _hAnimCoroutine = StartCoroutine(HorizontalAnimation());
        }

        // Set sprite vertikal awal.
        ApplyVerticalSprite();
    }

    private void Update()
    {
        if (!hasLocalMovement) return;
        if (GameManager.Instance == null ||
            GameManager.Instance.CurrentState != GameState.Playing) return;

        // ── CRITICAL FIX ──────────────────────────────────────────────────────
        // Jangan pakai transform.Translate — baik Space.Self maupun Space.World
        // keduanya memodifikasi transform.position (world space) secara internal.
        // Ketika WorldScroller menggerakkan parent di frame yang sama, perubahan
        // world Y parent ikut menambah localPosition.y anak → double addition →
        // bounds terlampaui cepat → snap → terlihat sebagai "lompatan ke atas".
        //
        // Solusi: tulis langsung ke localPosition.
        // localPosition += delta tidak pernah terpengaruh pergerakan parent.
        // ─────────────────────────────────────────────────────────────────────
        transform.localPosition += (Vector3)(_currentDirection * moveSpeed * Time.deltaTime);

        HandleHorizontalBounce();
        HandleVerticalPatrol();
        HandleJiggle();
    }

    // ==========================================================================
    //  Movement Handlers
    // ==========================================================================

    private void HandleHorizontalBounce()
    {
        if (!bounceHorizontal) return;

        float x    = transform.localPosition.x;
        float minX = _spawnLocalPos.x + bounceMinX;
        float maxX = _spawnLocalPos.x + bounceMaxX;

        if (x <= minX)
        {
            _currentDirection.x = Mathf.Abs(_currentDirection.x);
            _movingRight = true;
            transform.localPosition = new Vector3(minX, transform.localPosition.y, 0f);
            UpdateHorizontalFlip();
        }
        else if (x >= maxX)
        {
            _currentDirection.x = -Mathf.Abs(_currentDirection.x);
            _movingRight = false;
            transform.localPosition = new Vector3(maxX, transform.localPosition.y, 0f);
            UpdateHorizontalFlip();
        }
    }

    private void HandleVerticalPatrol()
    {
        if (!patrolVertical) return;

        float y    = transform.localPosition.y;
        float maxY = _spawnLocalPos.y + patrolMaxY;
        float minY = _spawnLocalPos.y + patrolMinY;
        bool directionChanged = false;

        if (y >= maxY)
        {
            _currentDirection.y = -Mathf.Abs(_currentDirection.y);
            _movingUp = false;
            directionChanged = true;
            transform.localPosition = new Vector3(transform.localPosition.x, maxY, 0f);
        }
        else if (y <= minY)
        {
            _currentDirection.y = Mathf.Abs(_currentDirection.y);
            _movingUp = true;
            directionChanged = true;
            transform.localPosition = new Vector3(transform.localPosition.x, minY, 0f);
        }

        if (directionChanged) ApplyVerticalSprite();
    }

    private void HandleJiggle()
    {
        if (!enableJiggle || jiggleTarget == null || jiggleAmplitude <= 0f) return;

        float offset = Mathf.Sin(Time.time * jiggleFrequency * Mathf.PI * 2f) * jiggleAmplitude;
        jiggleTarget.localPosition = new Vector3(
            _jiggleOrigin.x,
            _jiggleOrigin.y + offset,
            _jiggleOrigin.z);
    }

    // ==========================================================================
    //  Sprite Helpers
    // ==========================================================================

    /// <summary>Flip horizontalSpriteRenderer sesuai arah gerak saat ini.</summary>
    private void UpdateHorizontalFlip()
    {
        if (horizontalSpriteRenderer == null) return;

        // Kalau flipHorizontalOnStart = true, sprite default menghadap kiri,
        // jadi kita TIDAK flip saat ke kiri, dan FLIP saat ke kanan (dan sebaliknya).
        bool shouldFlip = flipHorizontalOnStart ? _movingRight : !_movingRight;
        horizontalSpriteRenderer.flipX = shouldFlip;
    }

    /// <summary>Terapkan sprite sesuai arah vertikal saat ini.</summary>
    private void ApplyVerticalSprite()
    {
        if (verticalSpriteRenderer == null) return;

        Sprite target = _movingUp ? vSpriteUp : vSpriteDown;
        if (target != null) verticalSpriteRenderer.sprite = target;
    }

    /// <summary>Loop animasi dua frame untuk gerakan horizontal.</summary>
    private IEnumerator HorizontalAnimation()
    {
        bool showA = true;
        WaitForSeconds wait = new WaitForSeconds(hAnimInterval);

        while (true)
        {
            if (GameManager.Instance?.CurrentState == GameState.Playing &&
                horizontalSpriteRenderer != null)
            {
                Sprite frame = showA ? hFrameA : hFrameB;
                if (frame != null) horizontalSpriteRenderer.sprite = frame;
                showA = !showA;
            }
            yield return wait;
        }
    }

    // ==========================================================================
    //  Collision
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
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        }

        if (hasLocalMovement)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, (Vector3)localMoveDirection * 1.5f);
        }

        // Batas horizontal bounce — hijau
        // Digambar sebagai offset dari localPosition obstacle (= posisi spawn di editor).
        if (bounceHorizontal)
        {
            float spawnX  = transform.localPosition.x;
            float worldMinX = transform.parent != null
                ? transform.parent.TransformPoint(new Vector3(spawnX + bounceMinX, transform.localPosition.y, 0f)).x
                : spawnX + bounceMinX;
            float worldMaxX = transform.parent != null
                ? transform.parent.TransformPoint(new Vector3(spawnX + bounceMaxX, transform.localPosition.y, 0f)).x
                : spawnX + bounceMaxX;
            float worldY = transform.position.y;

            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.6f);
            Gizmos.DrawLine(new Vector3(worldMinX, worldY), new Vector3(worldMaxX, worldY));
            Gizmos.DrawSphere(new Vector3(worldMinX, worldY), 0.1f);
            Gizmos.DrawSphere(new Vector3(worldMaxX, worldY), 0.1f);
        }

        // Batas vertical patrol — cyan
        // Digambar sebagai offset dari localPosition obstacle (= posisi spawn di editor).
        if (patrolVertical)
        {
            float spawnY = transform.localPosition.y;
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
            Vector3 top = transform.parent != null
                ? transform.parent.TransformPoint(new Vector3(transform.localPosition.x, spawnY + patrolMaxY, 0f))
                : new Vector3(transform.position.x, transform.position.y + patrolMaxY, 0f);
            Vector3 bot = transform.parent != null
                ? transform.parent.TransformPoint(new Vector3(transform.localPosition.x, spawnY + patrolMinY, 0f))
                : new Vector3(transform.position.x, transform.position.y + patrolMinY, 0f);
            Gizmos.DrawLine(top, bot);
            Gizmos.DrawSphere(top, 0.1f);
            Gizmos.DrawSphere(bot, 0.1f);
        }

        // Jiggle range — oranye tipis
        if (enableJiggle && jiggleAmplitude > 0f)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Vector3 center = jiggleTarget != null ? jiggleTarget.position : transform.position;
            Gizmos.DrawLine(
                center + Vector3.up    * jiggleAmplitude,
                center + Vector3.down  * jiggleAmplitude);
        }
    }
}