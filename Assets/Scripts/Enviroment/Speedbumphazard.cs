// =============================================================================
// SpeedBumpHazard.cs
// The player must be in SpeedState.Slow when they cross a speed bump.
// Crossing at Normal or Accelerate speed causes a "slip and fall" Game Over.
//
// SETUP:
//  1. Create a SpeedBump prefab (speed bump sprite + BoxCollider2D, IsTrigger = true).
//  2. Attach this script to the prefab.
//  3. The trigger should span all 3 lanes horizontally so any lane triggers it.
// =============================================================================

using UnityEngine;

public class SpeedBumpHazard : MonoBehaviour
{
    [Header("─── Speed Bump Configuration ────────────────────────")]
    [Tooltip("Failure message displayed when the player hits the bump too fast.")]
    [SerializeField] private string failureReason = "JEDUG!";

    [Tooltip("If true, the player can also pass at Normal speed. " +
             "Enable this for tutorial/early levels to be more forgiving. " +
             "Keep false for a strict experience as per the GDD.")]
    [SerializeField] private bool allowNormalSpeed = false;

    [Header("─── Optional Feedback ───────────────────────────────")]
    [Tooltip("Optional bump sound effect. Leave null to use GameManager's crash SFX.")]
    [SerializeField] private AudioClip bumpSFX;

    // ==========================================================================
    //  Trigger Logic
    // ==========================================================================

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameState.Playing) return;

        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        SpeedState state = player.CurrentSpeedState;

        bool isSafe = state == SpeedState.Slow ||
                      (allowNormalSpeed && state == SpeedState.Normal);

        if (!isSafe)
        {
            GameManager.Instance.TriggerGameOver(failureReason);
        }
        else
        {
            // Safe bump — play a softer bump sound if provided.
            if (bumpSFX != null)
                GameManager.Instance.PlaySFX(bumpSFX);

            Debug.Log("[SpeedBumpHazard] Speed bump cleared safely.");
        }
    }

    // ── Editor Visualization ──────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f); // Red.
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}