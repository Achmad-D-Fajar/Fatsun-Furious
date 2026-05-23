using UnityEngine;

public class PuddleHazard : MonoBehaviour
{
    [Header("─── Puddle Configuration ──────────────────────────")]
    [SerializeField] [Range(0, 2)] private int puddleLaneIndex = 1;

    [Header("─── Adjacent NPC Splash Check ───────────────────")]
    [SerializeField] private bool npcInLeftLane   = false;
    [SerializeField] private bool npcInCenterLane = false;
    [SerializeField] private bool npcInRightLane  = false;

    [Header("─── Failure Messages ───────────────────────────────")]
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
        if (player.CurrentSpeedState == SpeedState.Accelerate)
        {
            player.ShowSplashVFX(isDirty: true);
            GameManager.Instance.TriggerGameOver(slipFailReason);
            return;
        }

        // ── Condition B: Splash NPC ───────────────────────────────────────────
        if (IsAdjacentToNPC(player.CurrentLaneIndex))
        {
            player.ShowSplashVFX(isDirty: true);
            GameManager.Instance.TriggerGameOver(splashFailReason);
            return;
        }

        // ── Safe Pass ─────────────────────────────────────────────────────────
        player.ShowSplashVFX(isDirty: true);
        Debug.Log("[PuddleHazard] Puddle dilewati dengan aman.");
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private bool IsAdjacentToNPC(int playerLane)
    {
        bool[] npcLanes = { npcInLeftLane, npcInCenterLane, npcInRightLane };
        for (int i = 0; i < npcLanes.Length; i++)
            if (npcLanes[i] && Mathf.Abs(playerLane - i) <= 1) return true;
        return false;
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;
        Gizmos.color = new Color(0f, 0.4f, 1f, 0.35f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}