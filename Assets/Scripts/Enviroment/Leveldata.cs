// =============================================================================
// LevelData.cs
// ScriptableObject that holds all configuration for a single level.
// HOW TO CREATE: Right-Click in Project window > Create > FatsunFurious > Level Data
// Create one asset per level: LevelData_1, LevelData_2, ... LevelData_5
// =============================================================================

using UnityEngine;

[CreateAssetMenu(fileName = "LevelData_X", menuName = "FatsunFurious/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("─── Identity ───────────────────────────────────")]
    [Tooltip("Human-readable name shown on the Level Select screen (e.g., 'Gang Mawar').")]
    public string levelName = "Level 1";

    [Tooltip("Zero-based index. Level 1 = 0, Level 2 = 1, etc. Must match the SaveSystem key.")]
    public int levelIndex = 0;

    [Header("─── Scene & Prefabs ─────────────────────────────")]
    [Tooltip("The prefab containing the full static level layout " +
             "(road tiles, obstacle placements, NPCs, finish line). " +
             "Drag your 'Level_X_Layout' prefab here.")]
    public GameObject levelLayoutPrefab;

    [Tooltip("Sprite(s) for the one-page story comic shown before this level starts. " +
             "Add each comic page as a separate element.")]
    public Sprite[] storyComicPages;

    [Header("─── Timer & Difficulty ─────────────────────────")]
    [Tooltip("Total countdown time in seconds the player has to finish this level.")]
    public float timeLimitSeconds = 60f;

    [Tooltip("Base world scroll speed (units/second) at Normal speed state.")]
    public float baseScrollSpeed = 5f;

    [Tooltip("Multiplier applied when player holds Accelerate (W / Up Arrow).")]
    [Range(1.1f, 3f)]
    public float accelerateMultiplier = 1.5f;

    [Tooltip("Multiplier applied when player holds Slow (S / Down Arrow).")]
    [Range(0.1f, 0.9f)]
    public float slowMultiplier = 0.5f;

    [Header("─── Audio ───────────────────────────────────────")]
    [Tooltip("Background music clip for this level. Leave null to use the default BGM.")]
    public AudioClip levelBGM;
}