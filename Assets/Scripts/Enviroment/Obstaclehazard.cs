// =============================================================================
// ObstacleHazard.cs
// Replacement for ObstacleMovement.cs.
//
// Handles the generic "hard collision = Game Over" obstacle type.
// Dynamic obstacles (pedestrians walking across, animals, etc.) can
// have their OWN local movement here, INDEPENDENT of the WorldScroller,
// since they move relative to the level layout.
//
// For STATIONARY obstacles (parked motorcycles, poles, etc.): just use this
// script with movement disabled (moveSpeed = 0). They scroll automatically
// because they are children of the Level Layout Prefab.
//
// SETUP:
//  1. Create an obstacle prefab (sprite + BoxCollider2D, IsTrigger = true).
//  2. Attach this script.
//  3. Set tag to "Obstacle" so PlayerController.OnTriggerEnter2D catches it.
//  4. Use the Inspector to configure movement for dynamic obstacles.
// =============================================================================

using UnityEngine;

public class ObstacleHazard : MonoBehaviour
{
    [Header("─── Obstacle Type ────────────────────────────────────")]
    [Tooltip("Purely cosmetic label for debugging in the Scene Hierarchy.")]
    [SerializeField] private string obstacleName = "Obstacle";

    [Tooltip("Failure message displayed on the Game Over screen when this obstacle is hit.")]
    [SerializeField] private string failureReason = "NABRAK!";

    [Header("─── Local Movement (for dynamic obstacles) ──────────")]
    [Tooltip("Enable for obstacles that move independently (e.g., a pedestrian " +
             "crossing the alley). The obstacle moves in LOCAL space, so it " +
             "will also travel with the scrolling world correctly.")]
    [SerializeField] private bool hasLocalMovement = false;

    [Tooltip("Local movement direction (normalized). " +
             "Examples: (1,0) = moves right across the alley; (-1,0) = moves left.")]
    [SerializeField] private Vector2 localMoveDirection = Vector2.right;

    [Tooltip("Speed of local movement in units/second.")]
    [SerializeField] private float moveSpeed = 2f;

    [Tooltip("If true, the obstacle reverses direction when it reaches either edge " +
             "of the alley (for back-and-forth pedestrians).")]
    [SerializeField] private bool bounceAtEdges = false;

    [Tooltip("World-space X boundaries for bouncing. Match your lane edge positions.")]
    [SerializeField] private float bounceMinX = -2.5f;
    [SerializeField] private float bounceMaxX =  2.5f;

    // ── Private ───────────────────────────────────────────────────────────────

    private Vector2 _currentDirection;

    // ==========================================================================
    //  Unity Lifecycle
    // ==========================================================================

    private void Awake()
    {
        _currentDirection = localMoveDirection.normalized;
    }

    private void Update()
    {
        // Only move during active gameplay.
        if (!hasLocalMovement) return;
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing) return;

        // Apply local movement.
        transform.Translate(_currentDirection * moveSpeed * Time.deltaTime);

        // Bounce if enabled.
        if (bounceAtEdges)
        {
            float x = transform.position.x;
            if (x <= bounceMinX || x >= bounceMaxX)
            {
                _currentDirection.x = -_currentDirection.x;
                // Clamp to boundary.
                transform.position = new Vector3(
                    Mathf.Clamp(x, bounceMinX, bounceMaxX),
                    transform.position.y,
                    transform.position.z
                );
            }
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

    // ── Editor Visualization ──────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;
        Gizmos.color = new Color(1f, 0f, 0f, 0.4f); // Red.
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);

        if (hasLocalMovement)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, (Vector3)localMoveDirection * 1.5f);
        }
    }
}