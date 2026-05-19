// =============================================================================
// LevelNodeUI.cs
// Attach this to each of the 5 Level Node buttons in the Level Select screen.
// It manages its own visual state: Locked, Unlocked, or Completed.
//
// SETUP: For each level node button (Level 1 through 5):
//   1. Add this script to the button's root GameObject.
//   2. Link the child UI elements (lock icon, star icon, best time text, etc.)
//   3. Drag all 5 LevelNodeUI components into UIManager's "Level Nodes" array.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelNodeUI : MonoBehaviour
{
    [Header("─── Visual States ────────────────────────────────────")]
    [Tooltip("GameObject shown when the level is locked (the padlock icon group).")]
    [SerializeField] private GameObject lockedOverlay;

    [Tooltip("GameObject shown when the level is completed (the star/checkmark icon).")]
    [SerializeField] private GameObject completedIcon;

    [Tooltip("Button component — gets disabled when locked.")]
    [SerializeField] private Button buttonComponent;

    [Tooltip("Text showing the level number (e.g., '1').")]
    [SerializeField] private TMP_Text levelNumberText;

    [Tooltip("Text showing the player's best time. Hidden if never completed.")]
    [SerializeField] private TMP_Text bestTimeText;

    [Header("─── Colors ──────────────────────────────────────────")]
    [Tooltip("Image component representing the button background (for color tinting).")]
    [SerializeField] private Image buttonBackground;

    [SerializeField] private Color unlockedColor  = new Color(0.2f, 0.8f, 0.4f);
    [SerializeField] private Color lockedColor    = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color completedColor = new Color(1.0f, 0.85f, 0.2f);

    // ── Private State ─────────────────────────────────────────────────────────

    private int _levelIndex = 0;

    // ==========================================================================
    //  Public API — called by UIManager.RefreshLevelSelectUI()
    // ==========================================================================

    /// <summary>
    /// Configures this node's visual appearance based on save data.
    /// </summary>
    /// <param name="unlocked">Whether the level is accessible.</param>
    /// <param name="bestTime">Best clear time in seconds. 0f = never completed.</param>
    /// <param name="levelIndex">Zero-based level index for the button's OnClick.</param>
    public void SetState(bool unlocked, float bestTime, int levelIndex)
    {
        _levelIndex = levelIndex;

        bool completed = bestTime > 0f;

        // ── Level Number ────────────────────────────────────────────────────
        if (levelNumberText != null)
            levelNumberText.text = (levelIndex + 1).ToString();

        // ── Lock Overlay ────────────────────────────────────────────────────
        SetActive(lockedOverlay, !unlocked);

        // ── Completed Icon ──────────────────────────────────────────────────
        SetActive(completedIcon, completed);

        // ── Best Time Text ──────────────────────────────────────────────────
        if (bestTimeText != null)
        {
            bestTimeText.gameObject.SetActive(completed);
            bestTimeText.text = SaveSystem.FormatTime(bestTime);
        }

        // ── Button Interactability ──────────────────────────────────────────
        if (buttonComponent != null)
        {
            buttonComponent.interactable = unlocked;

            // Re-assign onClick each time to avoid duplicate listeners.
            buttonComponent.onClick.RemoveAllListeners();
            if (unlocked)
                buttonComponent.onClick.AddListener(() => UIManager.Instance?.OnLevelNodePressed(_levelIndex));
        }

        // ── Background Color ────────────────────────────────────────────────
        if (buttonBackground != null)
        {
            buttonBackground.color = !unlocked  ? lockedColor
                                   : completed  ? completedColor
                                   : unlockedColor;
        }
    }

    private void SetActive(GameObject go, bool active) { if (go != null) go.SetActive(active); }
}