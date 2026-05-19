// =============================================================================
// LevelManager.cs
// Lives in the 02_Gameplay scene. On Start(), it reads the selected level
// index from GameManager, spawns the correct Level Layout Prefab, then
// signals GameManager to show the Story screen.
//
// SETUP: Attach to an empty GameObject named "LevelManager" in 02_Gameplay.
//        Set the "Spawn Root" to the Transform where the level prefab will appear
//        (usually the scene root or a dedicated "LevelSpawnRoot" empty object).
// =============================================================================

using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("─── Spawn Configuration ─────────────────────────────")]
    [Tooltip("The Transform at which the Level Layout Prefab will be instantiated. " +
             "Create an empty GameObject named 'LevelSpawnRoot' at world position (0,0,0) " +
             "and drag it here.")]
    [SerializeField] private Transform spawnRoot;

    [Tooltip("Reference to the PlayerController in the scene. " +
             "This allows LevelManager to position the player at the level's start point.")]
    [SerializeField] private PlayerController playerController;

    /// <summary>The currently spawned level prefab instance. Other scripts can query this.</summary>
    public GameObject CurrentLevelInstance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        SpawnCurrentLevel();
    }

    // ── Level Spawning ────────────────────────────────────────────────────────

    private void SpawnCurrentLevel()
    {
        // Safety check — GameManager must exist.
        if (GameManager.Instance == null)
        {
            Debug.LogError("[LevelManager] GameManager.Instance is null! " +
                           "Make sure the GameManager scene is loaded first.");
            return;
        }

        LevelData data = GameManager.Instance.CurrentLevelData;
        if (data == null)
        {
            Debug.LogError("[LevelManager] CurrentLevelData is null! " +
                           "Did you call GameManager.SelectLevel() before loading this scene?");
            return;
        }

        if (data.levelLayoutPrefab == null)
        {
            Debug.LogError($"[LevelManager] LevelData '{data.levelName}' has no levelLayoutPrefab assigned!");
            return;
        }

        // Destroy any existing level instance (useful for Retry without full scene reload).
        if (CurrentLevelInstance != null)
            Destroy(CurrentLevelInstance);

        // Spawn the level at the designated root.
        Transform parent = spawnRoot != null ? spawnRoot : transform;
        CurrentLevelInstance = Instantiate(data.levelLayoutPrefab, parent.position, Quaternion.identity, parent);

        Debug.Log($"[LevelManager] Spawned '{data.levelLayoutPrefab.name}' for Level {data.levelIndex + 1}.");

        // After spawning, hand control to GameManager to show the Story screen.
        GameManager.Instance.StartStory();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Destroys the current level instance and re-spawns it.
    /// Called by the Retry button as an alternative to full scene reload.
    /// NOTE: GameManager.RetryLevel() uses SceneManager.LoadScene() which is
    /// simpler and safer. This method is provided as an optional faster path.
    /// </summary>
    public void RespawnLevel()
    {
        SpawnCurrentLevel();
    }
}