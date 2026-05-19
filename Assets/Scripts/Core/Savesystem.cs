// =============================================================================
// SaveSystem.cs
// Static helper class wrapping PlayerPrefs.
// Handles: which levels are unlocked, and the best clear time per level.
// No MonoBehaviour — call from anywhere without a scene reference.
// =============================================================================

using UnityEngine;

public static class SaveSystem
{
    // ── Key Conventions ──────────────────────────────────────────────────────
    // "level_X_unlocked" → 1 (unlocked) or 0 (locked)
    // "level_X_besttime" → float seconds (0f = never completed)

    private const string KEY_PREFIX        = "level_";
    private const string KEY_UNLOCK_SUFFIX = "_unlocked";
    private const string KEY_TIME_SUFFIX   = "_besttime";
    private const int    TOTAL_LEVELS      = 5;

    // ── Initialization ────────────────────────────────────────────────────────

    /// <summary>
    /// Call once on game boot (from GameManager.Awake).
    /// Ensures Level 1 (index 0) is always unlocked on first run.
    /// </summary>
    public static void InitializeSave()
    {
        if (!IsLevelUnlocked(0))
        {
            UnlockLevel(0);
            Debug.Log("[SaveSystem] First run — Level 1 unlocked.");
        }
    }

    // ── Unlock Status ─────────────────────────────────────────────────────────

    /// <summary>Returns true if the level at zero-based index is unlocked.</summary>
    public static bool IsLevelUnlocked(int levelIndex)
        => PlayerPrefs.GetInt(GetUnlockKey(levelIndex), 0) == 1;

    /// <summary>Unlocks the level at zero-based index and persists immediately.</summary>
    public static void UnlockLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= TOTAL_LEVELS) return;
        PlayerPrefs.SetInt(GetUnlockKey(levelIndex), 1);
        PlayerPrefs.Save();
        Debug.Log($"[SaveSystem] Level {levelIndex + 1} unlocked.");
    }

    /// <summary>
    /// Call when a level completes. Automatically unlocks the next level.
    /// </summary>
    public static void OnLevelComplete(int completedLevelIndex)
    {
        int next = completedLevelIndex + 1;
        if (next < TOTAL_LEVELS) UnlockLevel(next);
    }

    // ── Best Times ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the best clear time for a level in seconds.
    /// Returns 0f if the level has never been cleared.
    /// </summary>
    public static float GetBestTime(int levelIndex)
        => PlayerPrefs.GetFloat(GetTimeKey(levelIndex), 0f);

    /// <summary>
    /// Saves the time ONLY if it beats (is lower than) the current best.
    /// Returns true if a new record was set.
    /// </summary>
    public static bool TrySetBestTime(int levelIndex, float newTime)
    {
        float best = GetBestTime(levelIndex);
        if (best == 0f || newTime < best)
        {
            PlayerPrefs.SetFloat(GetTimeKey(levelIndex), newTime);
            PlayerPrefs.Save();
            Debug.Log($"[SaveSystem] New record for Level {levelIndex + 1}: {FormatTime(newTime)}");
            return true;
        }
        return false;
    }

    /// <summary>Returns best times for ALL levels. Used on the Final Record screen.</summary>
    public static float[] GetAllBestTimes()
    {
        float[] times = new float[TOTAL_LEVELS];
        for (int i = 0; i < TOTAL_LEVELS; i++) times[i] = GetBestTime(i);
        return times;
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    /// <summary>Converts seconds to "MM:SS" display string. Returns "--:--" for zero.</summary>
    public static string FormatTime(float totalSeconds)
    {
        if (totalSeconds <= 0f) return "--:--";
        int m = Mathf.FloorToInt(totalSeconds / 60f);
        int s = Mathf.FloorToInt(totalSeconds % 60f);
        return $"{m:00}:{s:00}";
    }

    /// <summary>⚠ DEBUG ONLY — wipes ALL save data. Remove call before shipping.</summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void DEBUG_ClearAllData()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.LogWarning("[SaveSystem] ⚠ ALL save data cleared!");
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private static string GetUnlockKey(int i) => $"{KEY_PREFIX}{i}{KEY_UNLOCK_SUFFIX}";
    private static string GetTimeKey(int i)   => $"{KEY_PREFIX}{i}{KEY_TIME_SUFFIX}";
}