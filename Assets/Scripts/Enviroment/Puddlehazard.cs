// =============================================================================
// PuddleHazard.cs
// Implements the two puddle-related fail conditions from the GDD:
//
//  CONDITION A — Slip: Player hits a puddle while in SpeedState.Accelerate.
//  CONDITION B — Splash: Player hits a puddle while an NPC is occupying an
//                adjacent lateral lane (the Jemuran / pedestrian splash scenario).
//
// SETUP:
//  1. Create a Puddle prefab (sprite + BoxCollider2D with IsTrigger = true).
//  2. Attach this script to the prefab.
//  3. For Condition B: Place "NPC Proximity Detectors" in the correct lane
//     positions, OR use the adjacentNPCTags check to scan nearby NPCs.
//     The simplest approach (recommended for your timeline): assign the
//     lane index the puddle occupies, and specify which lanes have adjacent NPCs
//     directly in the Inspector per puddle instance.
// =============================================================================

using UnityEngine;

public class PuddleHazard : MonoBehaviour
{
    [Header("─── Puddle Configuration ──────────────────────────────")]
    [Tooltip("The lane index (0=Left, 1=Center, 2=Right) this puddle sits in. " +
             "Used to determine if an adjacent-lane NPC would be splashed.")]
    [SerializeField] [Range(0, 2)] private int puddleLaneIndex = 1;

    [Header("─── Adjacent NPC Splash Check ───────────────────────")]
    [Tooltip("Check each box for lanes that have an NPC (Jemuran/pedestrian) " +
             "adjacent to this puddle. If the player hits this puddle AND " +
             "the corresponding adjacent lane is checked, it triggers a splash fail.")]
    [SerializeField] private bool npcInLeftLane   = false;
    [SerializeField] private bool npcInCenterLane = false;
    [SerializeField] private bool npcInRightLane  = false;

    [Header("─── Failure Messages ───────────────────────────────────")]
    [SerializeField] private string slipFailReason   = "TERPELESET!";
    [SerializeField] private string splashFailReason = "NYIPRAT!";

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

        // ── Condition A: Slip ────────────────────────────────────────────────
        // Hitting a puddle at full speed causes a slip.
        if (player.CurrentSpeedState == SpeedState.Accelerate)
        {
            GameManager.Instance.TriggerGameOver(slipFailReason);
            return;
        }

        // ── Condition B: Splash ───────────────────────────────────────────────
        // Check if the player's current lane is adjacent to a lane with an NPC.
        // The player's lane is always the puddle's lane when they enter this trigger.
        // We check if any NPC-occupied lane is marked as adjacent.
        if (IsAdjacentToNPC(player.CurrentLaneIndex))
        {
            GameManager.Instance.TriggerGameOver(splashFailReason);
            return;
        }

        // ── Safe Pass ─────────────────────────────────────────────────────────
        // Player slowed down correctly — no consequence. You could add a
        // small visual splash effect here (a particle system) for feedback.
        Debug.Log("[PuddleHazard] Safely navigated the puddle.");
    }

    // ── Helper: Is Player Adjacent to an NPC Lane? ────────────────────────────

    /// <summary>
    /// Returns true if any marked NPC lane is within 1 step of the player's lane.
    /// For a 3-lane system: adjacent means |playerLane - npcLane| == 1 OR same lane.
    /// </summary>
    private bool IsAdjacentToNPC(int playerLane)
    {
        // Build array of lanes that have NPCs.
        bool[] npcLanes = { npcInLeftLane, npcInCenterLane, npcInRightLane };

        for (int i = 0; i < npcLanes.Length; i++)
        {
            if (!npcLanes[i]) continue;
            // Adjacent = same lane or one lane away.
            if (Mathf.Abs(playerLane - i) <= 1)
                return true;
        }
        return false;
    }

    // ── Editor Visualization ──────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;
        Gizmos.color = new Color(0f, 0.4f, 1f, 0.35f); // Blue.
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}