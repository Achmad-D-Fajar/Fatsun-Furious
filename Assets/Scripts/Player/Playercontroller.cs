// =============================================================================
// PlayerController.cs
// Complete rewrite of PlayerMovement.cs using the New Input System.
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
// ── Responsibilities ─────────────────────────────────────────────────────────
//  - 3-playable-lane movement with house-wall border crash detection
//  - Speed state management (Normal / Accelerate / Slow)
//  - State-guarded inputs (no movement during Paused / GameOver / LevelComplete)
//  - Reporting speed state to GameManager every frame
//  - Triggering greeting feedback via UIManager
//  - Tracking whether player is currently in "Slow/Greet" mode (queried by hazards)
//
// SETUP:
//  1. Attach to the Player sprite GameObject.
//  2. Ensure a Rigidbody2D is attached (Body Type: Dynamic, Gravity Scale: 0,
//     Collision Detection: Continuous, Freeze Rotation Z: ✓).
//  3. Ensure a Collider2D is attached (IsTrigger = true).
//  4. Tag the Player GameObject as "Player".
//  5. This script uses direct Keyboard polling (New Input System).
//     No InputActionAsset needed. Just install com.unity.inputsystem.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    // ── Lane Configuration ────────────────────────────────────────────────────

    [Header("─── Lane Configuration (5 Visual Columns) ───────────")]
    [Tooltip("World-space X positions of the 3 PLAYABLE lanes (columns 2, 3, 4).\n\n" +
             "Screen layout:\n" +
             "  [HOUSE] | Lane 0 | Lane 1 | Lane 2 | [HOUSE]\n" +
             "  BORDER  | (Left) |(Center)| (Right)|  BORDER\n\n" +
             "Measure your alley in the Scene view.\n" +
             "Typical narrow gang: { -1.8, 0, 1.8 }")]
    [SerializeField] private float[] laneXPositions = { -1.8f, 0f, 1.8f };

    [Tooltip("How quickly (units/sec) the player snaps between lanes.")]
    [SerializeField] private float laneTransitionSpeed = 14f;

    [Header("─── Border Wall Positions (Columns 1 & 5) ──────────")]
    [Tooltip("World-space X of the LEFT house wall (column 1). Gizmo only. Typical: -2.7")]
    [SerializeField] private float leftWallX  = -2.7f;

    [Tooltip("World-space X of the RIGHT house wall (column 5). Gizmo only. Typical: 2.7")]
    [SerializeField] private float rightWallX =  2.7f;

    // ── Speed State Tint ──────────────────────────────────────────────────────

    [Header("─── Speed State Visual Feedback ─────────────────────")]
    [Tooltip("SpriteRenderer on the player. Used for tinting during speed states.")]
    [SerializeField] private SpriteRenderer playerSprite;

    [Tooltip("Tint color applied to the sprite while Accelerating.")]
    [SerializeField] private Color accelerateTint = new Color(1f, 0.8f, 0.2f);

    [Tooltip("Tint color applied to the sprite while Slowing.")]
    [SerializeField] private Color slowTint       = new Color(0.5f, 0.8f, 1f);

    [Tooltip("Default sprite color (no tint).")]
    [SerializeField] private Color normalTint     = Color.white;

    // ── Greeting Logic ────────────────────────────────────────────────────────

    [Header("─── Greeting Logic ───────────────────────────────────")]
    [Tooltip("Minimum seconds the player must hold 'S' for the greeting to count.")]
    [SerializeField] private float minGreetHoldTime = 0.25f;

    // ── Default Sprite + Size ─────────────────────────────────────────────────

    [Header("─── Default Sprite ───────────────────────────────────")]
    [Tooltip("Sprite default saat player jalan lurus ke depan.")]
    [SerializeField] private Sprite spriteDefault;

    [Tooltip("Ukuran tampilan sprite default dalam WORLD UNITS (lebar × tinggi).\n\n" +
             "Ini adalah ukuran referensi saat player jalan lurus.\n" +
             "Contoh: (0.5, 0.8) artinya sprite default tampil 0.5 unit lebar, 0.8 unit tinggi.")]
    [SerializeField] private Vector2 defaultDisplaySize = new Vector2(0.5f, 0.8f);

    // ── Turn Sprites + Size ───────────────────────────────────────────────────

    [Header("─── Turn Sprites ─────────────────────────────────────")]
    [Tooltip("Sprite ditampilkan saat player belok ke kiri.")]
    [SerializeField] private Sprite spriteTurnLeft;

    [Tooltip("Sprite ditampilkan saat player belok ke kanan.")]
    [SerializeField] private Sprite spriteTurnRight;

    [Tooltip("Ukuran tampilan turn sprite dalam WORLD UNITS (lebar × tinggi).\n\n" +
             "Dipakai untuk spriteTurnLeft dan spriteTurnRight sekaligus.\n" +
             "Set terpisah dari defaultDisplaySize agar bisa lebih lebar/pendek " +
             "tanpa mempengaruhi sprite default.\n" +
             "Contoh: (0.65, 0.75) kalau sprite belok lebih lebar dari default.")]
    [SerializeField] private Vector2 turnDisplaySize = new Vector2(0.65f, 0.75f);

    [Tooltip("Berapa detik sprite belok ditampilkan sebelum kembali ke sprite default.")]
    [SerializeField] private float turnSpriteDuration = 0.2f;

    // ── VFX ───────────────────────────────────────────────────────────────────

    [Header("─── VFX ─────────────────────────────────────────────")]
    [Tooltip("Transform di ujung depan player. Buat child empty GO bernama 'VFXSpawnPoint'.")]
    [SerializeField] private Transform vfxSpawnPoint;

    [Tooltip("Prefab VFX 💥 crash. Muncul saat player nabrak obstacle.")]
    [SerializeField] private GameObject crashVFXPrefab;

    [Tooltip("Prefab VFX 💦 splash. Muncul saat player lewat puddle.")]
    [SerializeField] private GameObject splashVFXPrefab;

    [Tooltip("Sorting order VFX agar muncul di atas semua sprite.")]
    [SerializeField] private int vfxSortingOrder = 10;

    // ── Public Read-Only State ────────────────────────────────────────────────

    public SpeedState CurrentSpeedState { get; private set; } = SpeedState.Normal;
    public bool IsActivelyGreeting      { get; private set; } = false;
    public int  CurrentLaneIndex        { get; private set; } = 1;

    // ── Private State ─────────────────────────────────────────────────────────

    private float       _targetX          = 0f;
    private float       _slowKeyHoldTimer = 0f;
    private bool        _slowKeyHeld      = false;
    private Rigidbody2D _rb;
    private Coroutine   _turnSpriteCoroutine;

    private Keyboard _kb => Keyboard.current;

    // ==========================================================================
    //  Unity Lifecycle
    // ==========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null) Debug.LogError("[PlayerController] Rigidbody2D missing!");

        CurrentLaneIndex = 1;
        _targetX         = laneXPositions[CurrentLaneIndex];
        SetPositionX(_targetX);

        // Terapkan ukuran default sejak awal
        ApplyPlayerSprite(spriteDefault, defaultDisplaySize);
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.SetScrollSpeedForState(SpeedState.Normal);
            return;
        }

        HandleSpeedInput();
        HandleLaneSwitchInput();
        SmoothMoveToTargetLane();
        UpdateSpriteColor();
    }

    // ==========================================================================
    //  Input Handling
    // ==========================================================================

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
    }

    // ==========================================================================
    //  Turn Sprite
    // ==========================================================================

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
        // Tampilkan sprite belok dengan ukuran turnDisplaySize
        ApplyPlayerSprite(turnSprite, turnDisplaySize);

        yield return new WaitForSeconds(turnSpriteDuration);

        // Kembali ke sprite default dengan ukuran defaultDisplaySize
        ApplyPlayerSprite(spriteDefault, defaultDisplaySize);
    }

    // ==========================================================================
    //  Sprite + Size Application
    // ==========================================================================

    /// <summary>
    /// Ganti sprite playerSprite dan set localScale-nya agar tampil
    /// persis sebesar targetSize dalam world units.
    ///
    /// Cara hitung:
    ///   sprite.bounds.size = ukuran sprite dalam world units pada scale (1,1,1).
    ///   localScale target  = targetSize / sprite.bounds.size
    ///
    /// Default sprite → pakai defaultDisplaySize.
    /// Turn sprite    → pakai turnDisplaySize.
    /// Keduanya independen, tidak saling mempengaruhi.
    /// </summary>
    private void ApplyPlayerSprite(Sprite s, Vector2 targetSize)
    {
        if (playerSprite == null || s == null) return;

        playerSprite.sprite = s;

        Vector2 native = s.bounds.size;
        if (native.x <= 0f || native.y <= 0f) return;

        playerSprite.transform.localScale = new Vector3(
            targetSize.x / native.x,
            targetSize.y / native.y,
            playerSprite.transform.localScale.z   // Z tidak diubah
        );
    }

    // ==========================================================================
    //  Movement
    // ==========================================================================

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

    // ==========================================================================
    //  Visual Feedback
    // ==========================================================================

    private void UpdateSpriteColor()
    {
        if (playerSprite == null) return;
        playerSprite.color = CurrentSpeedState switch
        {
            SpeedState.Accelerate => accelerateTint,
            SpeedState.Slow       => slowTint,
            _                     => normalTint
        };
    }

    // ==========================================================================
    //  VFX Spawning
    // ==========================================================================

    public void SpawnCrashVFX()  => SpawnVFX(crashVFXPrefab);

    public void SpawnSplashVFX(bool isDirty)
    {
        GameObject vfx = SpawnVFX(splashVFXPrefab);
        if (vfx == null) return;
        vfx.GetComponent<SplashVFX>()?.Init(isDirty);
    }

    private GameObject SpawnVFX(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[PlayerController] VFX prefab not assigned!");
            return null;
        }

        Vector3 spawnPos = vfxSpawnPoint != null ? vfxSpawnPoint.position : transform.position;
        GameObject vfx   = Instantiate(prefab, spawnPos, Quaternion.identity);

        SpriteRenderer sr = vfx.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = vfxSortingOrder;

        return vfx;
    }

    // ==========================================================================
    //  Editor Gizmos
    // ==========================================================================

    private void OnDrawGizmosSelected()
    {
        float gizmoHeight = 20f;
        Vector3 center    = transform.position;

        // Playable lane centers — hijau
        Gizmos.color = new Color(0f, 1f, 0f, 0.6f);
        foreach (float x in laneXPositions)
        {
            Gizmos.DrawLine(
                new Vector3(x, center.y - gizmoHeight, 0f),
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

        // Current lane indicator — cyan
        if (Application.isPlaying && laneXPositions != null &&
            CurrentLaneIndex < laneXPositions.Length)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(
                new Vector3(laneXPositions[CurrentLaneIndex], center.y, 0f),
                new Vector3(defaultDisplaySize.x, defaultDisplaySize.y, 0f));
        }

        // Visualisasi ukuran default sprite di posisi player — putih
        if (defaultDisplaySize.x > 0f && defaultDisplaySize.y > 0f)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
            Gizmos.DrawWireCube(center,
                new Vector3(defaultDisplaySize.x, defaultDisplaySize.y, 0f));
        }

        // Visualisasi ukuran turn sprite — kuning
        if (turnDisplaySize.x > 0f && turnDisplaySize.y > 0f)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.25f);
            Gizmos.DrawWireCube(center,
                new Vector3(turnDisplaySize.x, turnDisplaySize.y, 0f));
        }
    }
}