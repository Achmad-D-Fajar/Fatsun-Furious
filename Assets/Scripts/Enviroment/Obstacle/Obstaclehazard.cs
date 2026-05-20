// =============================================================================
// ObstacleHazard.cs
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
    [SerializeField] private bool hasLocalMovement = false;

    [Tooltip("Arah movement di local space. (1,0)=kanan, (0,1)=atas, (1,1)=diagonal.")]
    [SerializeField] private Vector2 localMoveDirection = Vector2.right;

    [SerializeField] private float moveSpeed = 2f;

    [Header("─── Horizontal Bounce ────────────────────────────────")]
    [Tooltip("Bounce kiri-kanan di dalam batas alley.")]
    [SerializeField] private bool bounceHorizontal = false;

    // ── FIX: these are now LOCAL X positions (relative to the parent prefab),
    //         not world X. Because the parent never scrolls horizontally, local X
    //         and world X are equal in practice — but using localPosition is
    //         consistent and safe.
    [SerializeField] private float bounceMinX = -1.8f;
    [SerializeField] private float bounceMaxX =  1.8f;

    [Header("─── Vertical Patrol ──────────────────────────────────")]
    [Tooltip("NPC bolak-balik atas-bawah di antara dua titik Y.")]
    [SerializeField] private bool patrolVertical = false;

    // ── FIX: these are now LOCAL Y positions (relative to the parent prefab).
    //         Set them based on where the obstacle sits inside the Level Layout Prefab,
    //         e.g. if the NPC's spawn localY is 0, use patrolMinY = -1.5, patrolMaxY = 1.5.
    [Tooltip("Titik Y teratas patrol — LOCAL SPACE (relatif ke parent prefab, bukan world).")]
    [SerializeField] private float patrolMaxY =  1.5f;

    [Tooltip("Titik Y terbawah patrol — LOCAL SPACE (relatif ke parent prefab, bukan world).")]
    [SerializeField] private float patrolMinY = -1.5f;

    [Tooltip("Mulai bergerak ke atas dulu. Uncheck untuk mulai ke bawah.")]
    [SerializeField] private bool startMovingUp = true;

    // ── Private ───────────────────────────────────────────────────────────────

    private Vector2 _currentDirection;

    // ==========================================================================
    //  Unity Lifecycle
    // ==========================================================================

    private void Awake()
    {
        _currentDirection = localMoveDirection.normalized;

        if (patrolVertical)
            _currentDirection.y = startMovingUp
                ? Mathf.Abs(_currentDirection.y)
                : -Mathf.Abs(_currentDirection.y);
    }

    private void Update()
    {
        if (!hasLocalMovement) return;
        if (GameManager.Instance == null ||
            GameManager.Instance.CurrentState != GameState.Playing) return;

        // ── FIX: Space.Self moves in the object's OWN local axes, which means
        //         movement is relative to the parent prefab — exactly what we want.
        //         Space.World was fighting the WorldScroller's downward scroll.
        transform.Translate(_currentDirection * moveSpeed * Time.deltaTime, Space.Self);

        HandleHorizontalBounce();
        HandleVerticalPatrol();
    }

    private void HandleHorizontalBounce()
    {
        if (!bounceHorizontal) return;

        // ── FIX: use localPosition.x, not position.x
        float x = transform.localPosition.x;

        if (x <= bounceMinX)
        {
            _currentDirection.x = Mathf.Abs(_currentDirection.x);
            transform.localPosition = new Vector3(bounceMinX, transform.localPosition.y, 0f);
        }
        else if (x >= bounceMaxX)
        {
            _currentDirection.x = -Mathf.Abs(_currentDirection.x);
            transform.localPosition = new Vector3(bounceMaxX, transform.localPosition.y, 0f);
        }
    }

    private void HandleVerticalPatrol()
    {
        if (!patrolVertical) return;

        // ── FIX: use localPosition.y, not position.y.
        //         The parent scrolls in world space; localPosition.y stays stable
        //         relative to the road, so patrol bounds work correctly.
        float y = transform.localPosition.y;

        if (y >= patrolMaxY)
        {
            _currentDirection.y = -Mathf.Abs(_currentDirection.y);
            transform.localPosition = new Vector3(transform.localPosition.x, patrolMaxY, 0f);
        }
        else if (y <= patrolMinY)
        {
            _currentDirection.y = Mathf.Abs(_currentDirection.y);
            transform.localPosition = new Vector3(transform.localPosition.x, patrolMinY, 0f);
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
        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);

        if (hasLocalMovement)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, (Vector3)localMoveDirection * 1.5f);
        }

        // Draw patrol range in local space (visible in Scene view)
        if (patrolVertical)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
            Vector3 top = transform.parent != null
                ? transform.parent.TransformPoint(new Vector3(transform.localPosition.x, patrolMaxY, 0f))
                : new Vector3(transform.position.x, patrolMaxY, 0f);
            Vector3 bot = transform.parent != null
                ? transform.parent.TransformPoint(new Vector3(transform.localPosition.x, patrolMinY, 0f))
                : new Vector3(transform.position.x, patrolMinY, 0f);
            Gizmos.DrawLine(top, bot);
            Gizmos.DrawSphere(top, 0.1f);
            Gizmos.DrawSphere(bot, 0.1f);
        }
    }
}