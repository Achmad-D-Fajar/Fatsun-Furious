// =============================================================================
// EtikaZone.cs
// The core "Etika" (ethics) mechanic enforcer.
//
// Attach to each NPC / Pos Ronda character in the Level Layout Prefab.
// This script defines an invisible trigger zone around the NPC. When the
// player enters the zone, they MUST be holding the Slow/Greet key (S / Down).
// If they pass through without slowing, it's an immediate Game Over.
//
// HOW IT WORKS:
//  OnTriggerEnter2D → The player has entered the NPC's greeting zone.
//  OnTriggerStay2D  → Continuously check if the player is slowing. If not, fail.
//  OnTriggerExit2D  → Player cleared the zone safely. Show greeting feedback.
//
// SETUP:
//  1. Add a child GameObject to your NPC prefab named "EtikaZone".
//  2. Add a BoxCollider2D (IsTrigger = true) and resize it to extend
//     a comfortable distance in front of the NPC (in the player's approach direction).
//  3. Attach this script to that child GameObject.
//  4. The player must have the tag "Player".
// =============================================================================

using UnityEngine;

public class EtikaZone : MonoBehaviour
{
    [Header("─── Zone Configuration ────────────────────────────────")]
    [Tooltip("If true, the player must be in SpeedState.Slow AND IsActivelyGreeting == true. " +
             "If false, simply being in SpeedState.Slow is enough. " +
             "Recommend TRUE for strict enforcement as per the GDD.")]
    [SerializeField] private bool requireActiveGreeting = true;

    [Tooltip("The failure message key shown on the Game Over screen when this zone fails.")]
    [SerializeField] private string failureReason = "KURANG AJAR!";

    [Tooltip("A small grace period (seconds) after the player enters before checking. " +
             "Gives the player a tiny reaction window. Keep between 0.0–0.3.")]
    [SerializeField] [Range(0f, 0.5f)] private float entryGracePeriod = 0.15f;

    // ── Private State ─────────────────────────────────────────────────────────

    private bool  _playerInZone    = false;
    private float _graceTimer      = 0f;
    private bool  _failTriggered   = false;

    // ==========================================================================
    //  Trigger Logic
    // ==========================================================================

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (IsGameNotPlaying()) return;

        _playerInZone  = true;
        _failTriggered = false;
        _graceTimer    = 0f;

        // Instant fail if player enters the zone while already accelerating.
        // No grace period — accelerating through an NPC zone is always disrespectful.
        PlayerController player = PlayerController.Instance;
        if (player != null && player.CurrentSpeedState == SpeedState.Accelerate)
        {
            _failTriggered = true;
            GameManager.Instance?.TriggerGameOver(failureReason);
            return;
        }

        Debug.Log($"[EtikaZone] Player entered zone of '{transform.parent?.name ?? gameObject.name}'.");
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (IsGameNotPlaying() || _failTriggered) return;

        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        // Accelerating inside the zone = instant fail, no grace period needed.
        if (player.CurrentSpeedState == SpeedState.Accelerate)
        {
            _failTriggered = true;
            GameManager.Instance?.TriggerGameOver(failureReason);
            return;
        }

        // Count up the grace period timer for the non-accelerating case.
        _graceTimer += Time.deltaTime;
        if (_graceTimer < entryGracePeriod) return;

        // Grace period expired — now enforce the full etika check.
        bool isSlowing  = player.CurrentSpeedState == SpeedState.Slow;
        bool isGreeting = player.IsActivelyGreeting;

        bool etikaMet = requireActiveGreeting
            ? (isSlowing && isGreeting)
            : isSlowing;

        if (!etikaMet)
        {
            _failTriggered = true;
            GameManager.Instance?.TriggerGameOver(failureReason);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInZone = false;

        // If the player exited cleanly (no fail), show the greeting feedback.
        if (!_failTriggered && !IsGameNotPlaying())
        {
            PlayerController.Instance?.ShowGreetVFX();
            GameManager.Instance?.PlayGreetingSFX();
            Debug.Log("[EtikaZone] Greeting completed successfully — Permisi!");
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private bool IsGameNotPlaying()
        => GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing;

    // ── Editor Visualization ──────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f); // Orange, semi-transparent.
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}