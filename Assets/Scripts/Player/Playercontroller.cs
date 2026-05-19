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
using UnityEngine.InputSystem;  // Requires: com.unity.inputsystem package

public class PlayerController : MonoBehaviour
{
    // ── Singleton-ish: other scripts can find the player easily ───────────────
    public static PlayerController Instance { get; private set; }

    // ── Inspector Fields ──────────────────────────────────────────────────────

    [Header("─── Lane Configuration (5 Visual Columns) ───────────")]
    [Tooltip("World-space X positions of the 3 PLAYABLE lanes (columns 2, 3, 4).\n\n" +
             "Screen layout:\n" +
             "  [HOUSE] | Lane 0 | Lane 1 | Lane 2 | [HOUSE]\n" +
             "  BORDER  | (Left) |(Center)| (Right)|  BORDER\n\n" +
             "Measure your alley in the Scene view.\n" +
             "Typical narrow gang: { -1.8, 0, 1.8 }")]
    [SerializeField] private float[] laneXPositions = { -1.8f, 0f, 1.8f };

    [Tooltip("How quickly (units/sec) the player snaps between lanes. " +
             "12–15 feels responsive for a narrow alley.")]
    [SerializeField] private float laneTransitionSpeed = 14f;

    [Header("─── Border Wall Positions (Columns 1 & 5) ──────────")]
    [Tooltip("World-space X of the LEFT house wall (column 1). Used only for Gizmo drawing " +
             "so you can see the full road layout in the Scene view. " +
             "Should be slightly left of laneXPositions[0]. Typical: -2.7")]
    [SerializeField] private float leftWallX  = -2.7f;

    [Tooltip("World-space X of the RIGHT house wall (column 5). " +
             "Should be slightly right of laneXPositions[2]. Typical: 2.7")]
    [SerializeField] private float rightWallX =  2.7f;

    [Header("─── Speed State Visual Feedback ─────────────────────")]
    [Tooltip("SpriteRenderer on the player. Used for tinting during speed states.")]
    [SerializeField] private SpriteRenderer playerSprite;

    [Tooltip("Tint color applied to the sprite while Accelerating.")]
    [SerializeField] private Color accelerateTint = new Color(1f, 0.8f, 0.2f); // Yellow-ish

    [Tooltip("Tint color applied to the sprite while Slowing.")]
    [SerializeField] private Color slowTint       = new Color(0.5f, 0.8f, 1f); // Blue-ish

    [Tooltip("Default sprite color (no tint).")]
    [SerializeField] private Color normalTint     = Color.white;

    [Header("─── Greeting Logic ───────────────────────────────────")]
    [Tooltip("Minimum duration in seconds the player must hold 'S' near an NPC " +
             "for the greeting to count. Prevents instant button taps.")]
    [SerializeField] private float minGreetHoldTime = 0.25f;

    // ── Public Read-Only State ────────────────────────────────────────────────

    /// <summary>Current speed state. Queried by hazard scripts (EtikaZone, PuddleHazard, etc.).</summary>
    public SpeedState CurrentSpeedState { get; private set; } = SpeedState.Normal;

    /// <summary>True while the player holds the Slow key AND has met the minimum hold time.</summary>
    public bool IsActivelyGreeting { get; private set; } = false;

    /// <summary>
    /// Current playable lane index: 0 = Left (col 2), 1 = Center (col 3), 2 = Right (col 4).
    /// Attempting -1 or 3 hits the house wall (cols 1 and 5) and triggers NABRAK!
    /// </summary>
    public int CurrentLaneIndex { get; private set; } = 1; // Start in center lane (col 3).

    // ── Private State ─────────────────────────────────────────────────────────

    private float    _targetX            = 0f;
    private float    _slowKeyHoldTimer   = 0f;
    private bool     _slowKeyHeld        = false;
    private Rigidbody2D _rb;

    // ── Input Actions (New Input System) ─────────────────────────────────────
    // Using Keyboard.current for simplicity. For full InputActionAsset workflow,
    // generate a C# class from your .inputactions file and swap these references.
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

        // Initialize position to center lane.
        CurrentLaneIndex = 1;
        _targetX         = laneXPositions[CurrentLaneIndex];
        SetPositionX(_targetX);
    }

    private void Update()
    {
        // Block ALL input if game is not in Playing state.
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
        {
            // Reset speed to normal so the world doesn't scroll oddly on pause.
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

        // Priority: Accelerate > Slow > Normal.
        if (accelHeld && !slowHeld)
        {
            CurrentSpeedState = SpeedState.Accelerate;
            _slowKeyHeld      = false;
            _slowKeyHoldTimer = 0f;
            IsActivelyGreeting = false;
        }
        else if (slowHeld && !accelHeld)
        {
            CurrentSpeedState = SpeedState.Slow;

            if (!_slowKeyHeld)
            {
                // Key just pressed — start timing.
                _slowKeyHeld      = true;
                _slowKeyHoldTimer = 0f;
                IsActivelyGreeting = false;
            }

            _slowKeyHoldTimer += Time.deltaTime;
            IsActivelyGreeting = _slowKeyHoldTimer >= minGreetHoldTime;
        }
        else
        {
            CurrentSpeedState  = SpeedState.Normal;
            _slowKeyHeld       = false;
            _slowKeyHoldTimer  = 0f;
            IsActivelyGreeting = false;
        }

        // Inform GameManager so WorldScroller picks up the correct speed.
        GameManager.Instance.SetScrollSpeedForState(CurrentSpeedState);
    }

    private void HandleLaneSwitchInput()
    {
        if (_kb == null) return;

        // Only switch on the frame the key is first pressed (not held).
        bool leftPressed  = _kb.aKey.wasPressedThisFrame || _kb.leftArrowKey.wasPressedThisFrame;
        bool rightPressed = _kb.dKey.wasPressedThisFrame || _kb.rightArrowKey.wasPressedThisFrame;

        if (leftPressed)  TrySwitchLane(-1);
        if (rightPressed) TrySwitchLane(+1);
    }

    /// <summary>
    /// Attempts to move to an adjacent playable lane.
    /// If the new index would be -1 (left of col 2) or 3 (right of col 4),
    /// the player has tried to enter a house wall (cols 1 or 5) → instant NABRAK!
    /// </summary>
    private void TrySwitchLane(int direction)
    {
        int newIndex = CurrentLaneIndex + direction;

        // newIndex < 0  → tried to enter left house wall  (col 1 / BORDER)
        // newIndex >= 3 → tried to enter right house wall (col 5 / BORDER)
        if (newIndex < 0 || newIndex >= laneXPositions.Length)
        {
            GameManager.Instance?.TriggerGameOver("NABRAK!");
            return;
        }

        CurrentLaneIndex = newIndex;
        _targetX         = laneXPositions[CurrentLaneIndex];
    }

    // ==========================================================================
    //  Movement
    // ==========================================================================

    /// <summary>Smoothly interpolates the player's X position toward the target lane.</summary>
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
    //  Editor — Scene View Gizmos
    // ==========================================================================

    /// <summary>
    /// Draws the full 5-column road layout in the Scene view when the Player
    /// GameObject is selected. Use this to align your level art precisely.
    ///
    ///   Red   lines = house walls (cols 1 & 5) — instant crash if entered
    ///   Green lines = playable lane centers (cols 2, 3, 4)
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        float gizmoHeight = 20f; // Tall enough to see in a vertical level.
        Vector3 center    = transform.position;

        // ── Playable lane center lines (Green) ───────────────────────────────
        Gizmos.color = new Color(0f, 1f, 0f, 0.6f);
        foreach (float x in laneXPositions)
        {
            Gizmos.DrawLine(
                new Vector3(x, center.y - gizmoHeight, 0f),
                new Vector3(x, center.y + gizmoHeight, 0f)
            );
        }

        // ── House wall lines (Red) ────────────────────────────────────────────
        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.8f);
        Gizmos.DrawLine(
            new Vector3(leftWallX, center.y - gizmoHeight, 0f),
            new Vector3(leftWallX, center.y + gizmoHeight, 0f)
        );
        Gizmos.DrawLine(
            new Vector3(rightWallX, center.y - gizmoHeight, 0f),
            new Vector3(rightWallX, center.y + gizmoHeight, 0f)
        );

        // ── Road boundary fill (semi-transparent white) ───────────────────────
        Gizmos.color = new Color(1f, 1f, 1f, 0.06f);
        float roadWidth  = rightWallX - leftWallX;
        float roadCenter = (rightWallX + leftWallX) / 2f;
        Gizmos.DrawCube(
            new Vector3(roadCenter, center.y, 0f),
            new Vector3(roadWidth, gizmoHeight * 2f, 0f)
        );

        // ── Current lane indicator (Cyan square around player) ───────────────
        if (Application.isPlaying && laneXPositions != null && CurrentLaneIndex < laneXPositions.Length)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(
                new Vector3(laneXPositions[CurrentLaneIndex], center.y, 0f),
                new Vector3(0.6f, 0.8f, 0f)
            );
        }
    }
}