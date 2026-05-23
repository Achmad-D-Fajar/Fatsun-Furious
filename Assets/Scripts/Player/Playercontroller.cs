// =============================================================================
// PlayerController.cs
//
// ── LANE LAYOUT ──────────────────────────────────────────────────────────────
//
//   The screen shows 5 vertical columns:
//
//   Col 1  │  Col 2  │  Col 3  │  Col 4  │  Col 5
//  [HOUSE] │ Lane 0  │ Lane 1  │ Lane 2  │ [HOUSE]
//  BORDER  │ (Left)  │(Center) │ (Right) │  BORDER
//
//   Columns 1 and 5 are house walls — the player can NEVER stand there.
//   laneXPositions[0..2] map to the 3 playable columns (2, 3, 4).
//   Attempting to switch left from Lane 0, or right from Lane 2,
//   triggers an immediate "NABRAK!" Game Over (crashed into a house).
//
// ── VFX SYSTEM ───────────────────────────────────────────────────────────────
//   Semua VFX adalah child GameObjects di bawah Player.
//   Tidak ada Instantiate / prefab runtime — hanya SetActive dan sprite swap.
//   Artist atur posisi tiap VFX child langsung di Scene view.
//
//   Child GO yang dibutuhkan:
//     VFX_Crash         → SpriteRenderer, default inactive
//     VFX_Splash_Dirty  → SpriteRenderer, default inactive
//     VFX_Splash_Clean  → SpriteRenderer, default inactive
//     VFX_Accelerate    → SpriteRenderer, default inactive (2-frame loop)
//     VFX_Slow          → SpriteRenderer, default inactive (2-frame loop)
//     VFX_Greet         → SpriteRenderer, default inactive
//
// ── SETUP ────────────────────────────────────────────────────────────────────
//  1. Attach to the Player sprite GameObject.
//  2. Rigidbody2D: Dynamic, Gravity Scale 0, Freeze Rotation Z, Continuous.
//  3. Collider2D: IsTrigger = true.
//  4. Tag Player sebagai "Player".
//  5. Buat semua child VFX GO, set inactive, assign ke fields di Inspector.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    // =========================================================================
    //  INSPECTOR FIELDS
    // =========================================================================

    // ── Lane Configuration ────────────────────────────────────────────────────

    [Header("─── Lane Configuration (5 Visual Columns) ───────────")]
    [Tooltip("World-space X positions of the 3 PLAYABLE lanes (columns 2, 3, 4).\n\n" +
             "Screen layout:\n" +
             "  [HOUSE] | Lane 0 | Lane 1 | Lane 2 | [HOUSE]\n" +
             "  BORDER  | (Left) |(Center)| (Right)|  BORDER\n\n" +
             "Typical narrow gang: { -1.8, 0, 1.8 }")]
    [SerializeField] private float[] laneXPositions = { -1.8f, 0f, 1.8f };

    [Tooltip("How quickly (units/sec) the player snaps between lanes.")]
    [SerializeField] private float laneTransitionSpeed = 14f;

    [Header("─── Border Wall Positions (Columns 1 & 5) ──────────")]
    [Tooltip("World-space X of the LEFT house wall. Gizmo only. Typical: -2.7")]
    [SerializeField] private float leftWallX  = -2.7f;

    [Tooltip("World-space X of the RIGHT house wall. Gizmo only. Typical: 2.7")]
    [SerializeField] private float rightWallX =  2.7f;

    // ── Sprites ───────────────────────────────────────────────────────────────

    [Header("─── Player SpriteRenderer ──────────────────────────")]
    [Tooltip("SpriteRenderer pada body motor player.")]
    [SerializeField] private SpriteRenderer playerSprite;

    [Header("─── Default Sprite ───────────────────────────────────")]
    [Tooltip("Sprite default saat player jalan lurus ke depan.")]
    [SerializeField] private Sprite spriteDefault;

    [Tooltip("Ukuran tampilan sprite default dalam WORLD UNITS (lebar × tinggi).")]
    [SerializeField] private Vector2 defaultDisplaySize = new Vector2(0.5f, 0.8f);

    [Header("─── Turn Sprites ─────────────────────────────────────")]
    [Tooltip("Sprite saat player belok ke kiri.")]
    [SerializeField] private Sprite spriteTurnLeft;

    [Tooltip("Sprite saat player belok ke kanan.")]
    [SerializeField] private Sprite spriteTurnRight;

    [Tooltip("Ukuran turn sprite dalam WORLD UNITS. Independent dari defaultDisplaySize.")]
    [SerializeField] private Vector2 turnDisplaySize = new Vector2(0.65f, 0.75f);

    [Tooltip("Durasi sprite belok sebelum kembali ke sprite default (detik).")]
    [SerializeField] private float turnSpriteDuration = 0.2f;

    [Header("─── Turn Speed Reduction ────────────────────────────")]
    [Tooltip("Multiplier kecepatan scroll saat player berpindah lane.\n\n" +
             "1.0 = tidak ada efek (kecepatan tidak berubah saat belok).\n" +
             "0.7 = kecepatan berkurang 30% selama turnSlowDuration.\n" +
             "0.5 = berkurang 50%, terasa seperti ngerem saat belok.\n\n" +
             "Multiplier ini dikalikan di atas kecepatan yang sudah ada " +
             "(Normal/Accelerate/Slow), jadi berlaku di semua speed state.")]
    [SerializeField] [Range(0f, 1f)] private float turnSpeedMultiplier = 0.75f;

    [Tooltip("Durasi (detik) efek perlambatan saat belok.\n\n" +
             "Direkomendasikan: sama atau sedikit lebih pendek dari turnSpriteDuration " +
             "agar efek visual dan efek kecepatan selesai bersamaan.\n" +
             "Contoh: turnSpriteDuration = 0.2 → turnSlowDuration = 0.15–0.2.")]
    [SerializeField] [Range(0f, 0.5f)] private float turnSlowDuration = 0.15f;

    // ── Greeting ──────────────────────────────────────────────────────────────

    [Header("─── Greeting Logic ───────────────────────────────────")]
    [Tooltip("Minimum detik player harus tahan 'S' agar greeting dihitung.")]
    [SerializeField] private float minGreetHoldTime = 0.25f;

    // ── VFX — One-Shot (Crash, Splash, Greet) ─────────────────────────────────

    [Header("─── VFX: One-Shot (Child GameObjects) ───────────────")]
    [Tooltip("Child GO dengan SpriteRenderer sprite 💥 crash.\n" +
             "Muncul sesaat saat player nabrak, lalu hilang otomatis.\n" +
             "Default: inactive.")]
    [SerializeField] private GameObject vfxCrash;

    [Tooltip("Child GO splash 💦 kotor (nyiprat/terpeleset di puddle).\n" +
             "Default: inactive.")]
    [SerializeField] private GameObject vfxSplashDirty;

    [Tooltip("Child GO splash bersih (safe pass lewat puddle).\n" +
             "Default: inactive.")]
    [SerializeField] private GameObject vfxSplashClean;

    [Tooltip("Child GO bubble 'Permisi!' saat greet NPC berhasil.\n" +
             "Default: inactive.")]
    [SerializeField] private GameObject vfxGreet;

    [Tooltip("Durasi VFX one-shot tampil sebelum hilang (detik).\n" +
             "Pakai unscaled time agar crash VFX tampil walau timeScale = 0.")]
    [SerializeField] private float vfxOneShotDuration = 0.5f;

    // ── VFX — Looping 2-Frame (Accelerate & Slow) ─────────────────────────────

    [Header("─── VFX: Accelerate (2-Frame Loop) ───────────────────")]
    [Tooltip("SpriteRenderer pada child GO 'VFX_Accelerate'.\n" +
             "Aktif selama tombol accelerate ditahan, animasi 2 frame bergantian.\n" +
             "Posisikan child ini di belakang motor di Scene view.")]
    [SerializeField] private SpriteRenderer vfxAccelerateRenderer;

    [Tooltip("Frame 1 animasi accelerate (misal: garis angin frame A).")]
    [SerializeField] private Sprite vfxAccelerateFrame1;

    [Tooltip("Frame 2 animasi accelerate (misal: garis angin frame B).")]
    [SerializeField] private Sprite vfxAccelerateFrame2;

    [Header("─── VFX: Slow / Brake (2-Frame Loop) ─────────────────")]
    [Tooltip("SpriteRenderer pada child GO 'VFX_Slow'.\n" +
             "Aktif selama tombol slow/brake ditahan, animasi 2 frame bergantian.\n" +
             "Posisikan child ini di area ban belakang di Scene view.")]
    [SerializeField] private SpriteRenderer vfxSlowRenderer;

    [Tooltip("Frame 1 animasi brake (misal: skid mark / asap frame A).")]
    [SerializeField] private Sprite vfxSlowFrame1;

    [Tooltip("Frame 2 animasi brake (misal: skid mark / asap frame B).")]
    [SerializeField] private Sprite vfxSlowFrame2;

    [Tooltip("Kecepatan ganti frame dalam detik.\n" +
             "0.1 = animasi cepat | 0.3 = animasi lambat.")]
    [SerializeField] private float vfxFrameInterval = 0.15f;

    // =========================================================================
    //  PUBLIC READ-ONLY STATE
    // =========================================================================

    public SpeedState CurrentSpeedState { get; private set; } = SpeedState.Normal;
    public bool       IsActivelyGreeting { get; private set; } = false;
    public int        CurrentLaneIndex   { get; private set; } = 1;

    /// <summary>
    /// Multiplier sementara yang diterapkan ke scroll speed saat player berpindah lane.
    /// Bernilai turnSpeedMultiplier selama turnSlowDuration detik setelah belok,
    /// lalu kembali ke 1.0. Dibaca oleh WorldScroller setiap frame.
    /// </summary>
    public float CurrentTurnSpeedMultiplier { get; private set; } = 1f;

    // =========================================================================
    //  PRIVATE STATE
    // =========================================================================

    private float       _targetX          = 0f;
    private float       _slowKeyHoldTimer = 0f;
    private bool        _slowKeyHeld      = false;
    private Rigidbody2D _rb;

    // Turn sprite
    private Coroutine _turnSpriteCoroutine;

    // Turn speed slow
    private Coroutine _turnSlowCoroutine;

    // VFX one-shot
    private Coroutine _oneShotCoroutine;

    // VFX 2-frame loop
    private float _vfxFrameTimer  = 0f;
    private bool  _vfxFrameToggle = false;

    private Keyboard _kb => Keyboard.current;

    // =========================================================================
    //  UNITY LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null) Debug.LogError("[PlayerController] Rigidbody2D missing!");

        CurrentLaneIndex = 1;
        _targetX         = laneXPositions[CurrentLaneIndex];
        SetPositionX(_targetX);

        ApplyPlayerSprite(spriteDefault, defaultDisplaySize);
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.SetScrollSpeedForState(SpeedState.Normal);

            // Matikan semua looping VFX saat tidak playing
            SetVFXActive(vfxAccelerateRenderer, false);
            SetVFXActive(vfxSlowRenderer, false);
            return;
        }

        HandleSpeedInput();
        HandleLaneSwitchInput();
        SmoothMoveToTargetLane();
        UpdateSpeedVFX();
    }

    // =========================================================================
    //  INPUT HANDLING
    // =========================================================================

    private void HandleSpeedInput()
    {
        if (_kb == null) return;

        bool accelHeld = _kb.wKey.isPressed || _kb.upArrowKey.isPressed;
        bool slowHeld  = _kb.sKey.isPressed || _kb.downArrowKey.isPressed;

        if (accelHeld && !slowHeld)
        {
            CurrentSpeedState  = SpeedState.Accelerate;
            _slowKeyHeld       = false;
            _slowKeyHoldTimer  = 0f;
            IsActivelyGreeting = false;
        }
        else if (slowHeld && !accelHeld)
        {
            CurrentSpeedState = SpeedState.Slow;

            if (!_slowKeyHeld)
            {
                _slowKeyHeld      = true;
                _slowKeyHoldTimer = 0f;
                IsActivelyGreeting = false;
            }

            _slowKeyHoldTimer  += Time.deltaTime;
            IsActivelyGreeting  = _slowKeyHoldTimer >= minGreetHoldTime;
        }
        else
        {
            CurrentSpeedState  = SpeedState.Normal;
            _slowKeyHeld       = false;
            _slowKeyHoldTimer  = 0f;
            IsActivelyGreeting = false;
        }

        GameManager.Instance.SetScrollSpeedForState(CurrentSpeedState);
    }

    private void HandleLaneSwitchInput()
    {
        if (_kb == null) return;

        bool leftPressed  = _kb.aKey.wasPressedThisFrame || _kb.leftArrowKey.wasPressedThisFrame;
        bool rightPressed = _kb.dKey.wasPressedThisFrame || _kb.rightArrowKey.wasPressedThisFrame;

        if (leftPressed)  TrySwitchLane(-1);
        if (rightPressed) TrySwitchLane(+1);
    }

    private void TrySwitchLane(int direction)
    {
        int newIndex = CurrentLaneIndex + direction;

        if (newIndex < 0 || newIndex >= laneXPositions.Length)
        {
            ShowTurnSprite(direction);
            GameManager.Instance?.TriggerGameOver("NABRAK!");
            return;
        }

        CurrentLaneIndex = newIndex;
        _targetX         = laneXPositions[CurrentLaneIndex];
        ShowTurnSprite(direction);
        StartTurnSlow();
    }

    // =========================================================================
    //  MOVEMENT
    // =========================================================================

    private void SmoothMoveToTargetLane()
    {
        float newX = Mathf.MoveTowards(transform.position.x, _targetX,
                                        laneTransitionSpeed * Time.deltaTime);
        _rb.MovePosition(new Vector2(newX, transform.position.y));
    }

    private void SetPositionX(float x)
    {
        transform.position = new Vector3(x, transform.position.y, transform.position.z);
    }

    // =========================================================================
    //  TURN SPRITE
    // =========================================================================

    private void ShowTurnSprite(int direction)
    {
        if (playerSprite == null) return;

        Sprite turnSprite = direction < 0 ? spriteTurnLeft : spriteTurnRight;
        if (turnSprite == null) return;

        if (_turnSpriteCoroutine != null)
            StopCoroutine(_turnSpriteCoroutine);

        _turnSpriteCoroutine = StartCoroutine(TurnSpriteRoutine(turnSprite));
    }

    private IEnumerator TurnSpriteRoutine(Sprite turnSprite)
    {
        ApplyPlayerSprite(turnSprite, turnDisplaySize);
        yield return new WaitForSeconds(turnSpriteDuration);
        ApplyPlayerSprite(spriteDefault, defaultDisplaySize);
    }

    // ── Turn Speed Slow ───────────────────────────────────────────────────────

    private void StartTurnSlow()
    {
        // Kalau efek belok sebelumnya masih berjalan, cancel dulu
        // (misal: player belok lagi sebelum efek selesai — reset durasi)
        if (_turnSlowCoroutine != null) StopCoroutine(_turnSlowCoroutine);
        _turnSlowCoroutine = StartCoroutine(TurnSlowRoutine());
    }

    private IEnumerator TurnSlowRoutine()
    {
        // Terapkan multiplier langsung saat belok dimulai
        CurrentTurnSpeedMultiplier = turnSpeedMultiplier;
        yield return new WaitForSeconds(turnSlowDuration);
        // Kembalikan ke normal setelah durasi habis
        CurrentTurnSpeedMultiplier = 1f;
        _turnSlowCoroutine = null;
    }

    // =========================================================================
    //  SPRITE + SIZE APPLICATION
    // =========================================================================

    private void ApplyPlayerSprite(Sprite s, Vector2 targetSize)
    {
        if (playerSprite == null || s == null) return;

        playerSprite.sprite = s;

        Vector2 native = s.bounds.size;
        if (native.x <= 0f || native.y <= 0f) return;

        playerSprite.transform.localScale = new Vector3(
            targetSize.x / native.x,
            targetSize.y / native.y,
            playerSprite.transform.localScale.z
        );
    }

    // =========================================================================
    //  VFX — LOOPING 2-FRAME (Accelerate & Slow)
    // =========================================================================

    private void UpdateSpeedVFX()
    {
        bool isAccel = CurrentSpeedState == SpeedState.Accelerate;
        bool isSlow  = CurrentSpeedState == SpeedState.Slow;

        // ── Tick frame timer ──────────────────────────────────────────────────
        if (isAccel || isSlow)
        {
            _vfxFrameTimer += Time.deltaTime;
            if (_vfxFrameTimer >= vfxFrameInterval)
            {
                _vfxFrameTimer  = 0f;
                _vfxFrameToggle = !_vfxFrameToggle;
            }
        }
        else
        {
            _vfxFrameTimer  = 0f;
            _vfxFrameToggle = false;
        }

        // ── Accelerate VFX ────────────────────────────────────────────────────
        SetVFXActive(vfxAccelerateRenderer, isAccel);
        if (isAccel && vfxAccelerateRenderer != null)
        {
            vfxAccelerateRenderer.sprite = _vfxFrameToggle
                ? vfxAccelerateFrame2
                : vfxAccelerateFrame1;
        }

        // ── Slow / Brake VFX ──────────────────────────────────────────────────
        SetVFXActive(vfxSlowRenderer, isSlow);
        if (isSlow && vfxSlowRenderer != null)
        {
            vfxSlowRenderer.sprite = _vfxFrameToggle
                ? vfxSlowFrame2
                : vfxSlowFrame1;
        }
    }

    // =========================================================================
    //  VFX — ONE-SHOT (Crash, Splash, Greet)
    // =========================================================================

    /// <summary>
    /// Tampilkan crash VFX 💥.
    /// Dipanggil oleh GameManager.TriggerGameOver().
    /// </summary>
    public void ShowCrashVFX()
    {
        ShowOneShotVFX(vfxCrash);
    }

    /// <summary>
    /// Tampilkan splash VFX 💦.
    /// Dipanggil oleh PuddleHazard.
    /// isDirty = true  → nyiprat/terpeleset (splash kotor).
    /// isDirty = false → safe pass (splash bersih).
    /// </summary>
    public void ShowSplashVFX(bool isDirty)
    {
        ShowOneShotVFX(isDirty ? vfxSplashDirty : vfxSplashClean);
    }

    /// <summary>
    /// Tampilkan bubble 'Permisi!' saat greeting NPC berhasil.
    /// Dipanggil oleh EtikaZone.
    /// </summary>
    public void ShowGreetVFX()
    {
        ShowOneShotVFX(vfxGreet);
    }

    /// <summary>Aktifkan GO sebentar lalu nonaktifkan otomatis via unscaled time.</summary>
    private void ShowOneShotVFX(GameObject vfx)
    {
        if (vfx == null) return;

        // Stop coroutine sebelumnya untuk VFX yang sama agar tidak overlap
        if (_oneShotCoroutine != null) StopCoroutine(_oneShotCoroutine);
        _oneShotCoroutine = StartCoroutine(OneShotRoutine(vfx));
    }

    private IEnumerator OneShotRoutine(GameObject vfx)
    {
        vfx.SetActive(true);
        // Unscaled time: crash VFX tetap tampil walau timeScale = 0
        yield return new WaitForSecondsRealtime(vfxOneShotDuration);
        vfx.SetActive(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Toggle SpriteRenderer GO aktif/nonaktif, hanya jika state berbeda.</summary>
    private void SetVFXActive(SpriteRenderer sr, bool active)
    {
        if (sr != null && sr.gameObject.activeSelf != active)
            sr.gameObject.SetActive(active);
    }

    // =========================================================================
    //  COLLISION — Generic obstacle fallback
    // =========================================================================

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameState.Playing) return;

        if (other.CompareTag("Obstacle"))
            GameManager.Instance.TriggerGameOver("NABRAK!");
    }

    // =========================================================================
    //  EDITOR GIZMOS
    // =========================================================================

    private void OnDrawGizmosSelected()
    {
        float gizmoHeight = 20f;
        Vector3 center    = transform.position;

        // Playable lane centers — hijau
        Gizmos.color = new Color(0f, 1f, 0f, 0.6f);
        foreach (float x in laneXPositions)
        {
            Gizmos.DrawLine(new Vector3(x, center.y - gizmoHeight, 0f),
                            new Vector3(x, center.y + gizmoHeight, 0f));
        }

        // House walls — merah
        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.8f);
        Gizmos.DrawLine(new Vector3(leftWallX,  center.y - gizmoHeight, 0f),
                        new Vector3(leftWallX,  center.y + gizmoHeight, 0f));
        Gizmos.DrawLine(new Vector3(rightWallX, center.y - gizmoHeight, 0f),
                        new Vector3(rightWallX, center.y + gizmoHeight, 0f));

        // Road fill — putih transparan
        Gizmos.color = new Color(1f, 1f, 1f, 0.06f);
        float roadWidth  = rightWallX - leftWallX;
        float roadCenter = (rightWallX + leftWallX) / 2f;
        Gizmos.DrawCube(new Vector3(roadCenter, center.y, 0f),
                        new Vector3(roadWidth, gizmoHeight * 2f, 0f));

        // Current lane indicator saat play — cyan
        if (Application.isPlaying && laneXPositions != null &&
            CurrentLaneIndex < laneXPositions.Length)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(
                new Vector3(laneXPositions[CurrentLaneIndex], center.y, 0f),
                new Vector3(defaultDisplaySize.x, defaultDisplaySize.y, 0f));
        }

        // Ukuran default sprite — putih
        Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
        Gizmos.DrawWireCube(center,
            new Vector3(defaultDisplaySize.x, defaultDisplaySize.y, 0f));

        // Ukuran turn sprite — kuning
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.25f);
        Gizmos.DrawWireCube(center,
            new Vector3(turnDisplaySize.x, turnDisplaySize.y, 0f));
    }
}