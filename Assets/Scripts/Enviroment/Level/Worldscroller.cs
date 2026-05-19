// =============================================================================
// WorldScroller.cs
// Moves the entire Level Layout Prefab downward every frame, creating the
// illusion that the player's motorcycle is driving forward (upward).
//
// This is the ONLY script that physically moves the world. All obstacles,
// NPCs, and decorations are children of the Level Layout Prefab and
// move automatically when this script moves the parent.
//
// SETUP:
//  Attach this script to the ROOT of your Level Layout Prefab
//  (e.g., "Level_1_Layout").
//  Do NOT attach it to the LevelSpawnRoot or GameManager.
// =============================================================================

using UnityEngine;

public class WorldScroller : MonoBehaviour
{
    [Header("─── Scroll Configuration ─────────────────────────────")]
    [Tooltip("If true, the world scrolls downward (standard for a top-down upward runner). " +
             "Set to false only if you want to flip the scroll direction.")]
    [SerializeField] private bool scrollDownward = true;

    [Tooltip("Optional: override GameManager's scroll speed with a fixed value. " +
             "Leave at 0 to always use GameManager.Instance.CurrentScrollSpeed (recommended).")]
    [SerializeField] private float overrideSpeed = 0f;

    // ── Private State ─────────────────────────────────────────────────────────

    private float _scrollDirection;

    // ==========================================================================
    //  Unity Lifecycle
    // ==========================================================================

    private void Awake()
    {
        _scrollDirection = scrollDownward ? -1f : 1f;
    }

    private void Update()
    {
        // Only scroll while the game is in the Playing state.
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
            return;

        float speed = overrideSpeed > 0f
            ? overrideSpeed
            : GameManager.Instance.CurrentScrollSpeed;

        // Move the entire level layout downward (negative Y) every frame.
        transform.Translate(Vector2.up * _scrollDirection * speed * Time.deltaTime);
    }
}