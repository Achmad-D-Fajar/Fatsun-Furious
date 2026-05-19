// =============================================================================
// LevelFinishLine.cs
// Place this on an invisible trigger at the very end (top) of each Level
// Layout Prefab. When the player's collider enters it, the level is won.
//
// SETUP:
//  1. Create an empty child GameObject at the top of your Level Layout Prefab.
//  2. Name it "FinishLine".
//  3. Add a BoxCollider2D, set IsTrigger = true, resize to span all 3 lanes.
//  4. Attach this script.
//  5. The player must have a Collider2D with the tag "Player".
// =============================================================================

using UnityEngine;

public class LevelFinishLine : MonoBehaviour
{
    [Header("─── Optional Visual ──────────────────────────────────")]
    [Tooltip("Optional particle effect or animation to play when the player crosses the line.")]
    [SerializeField] private ParticleSystem finishParticles;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Only fire for the player.
        if (!other.CompareTag("Player")) return;

        // Only trigger once, and only during active gameplay.
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameState.Playing) return;

        // Play optional finish particles.
        if (finishParticles != null) finishParticles.Play();

        // Notify GameManager — it handles time recording, unlocking, and UI.
        GameManager.Instance.TriggerLevelComplete();

        Debug.Log("[LevelFinishLine] Finish line crossed!");
    }
}