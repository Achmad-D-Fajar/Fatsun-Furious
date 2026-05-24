// =============================================================================
// UIManager.cs
// Manages ALL UI panels in the game by subscribing to GameManager events.
// One UIManager exists per scene (MainMenu scene + Gameplay scene each have one).
//
// PANEL NAMING CONVENTION (set in Inspector):
//   mainMenuPanel       → The Level Select / Main Menu panel
//   storyPanel          → The comic strip panel shown before gameplay
//   hudPanel            → The in-game HUD (timer, pause button)
//   pausePanel          → The pause overlay
//   gameOverPanel       → The failure screen
//   levelCompletePanel  → Win screen (mid-game levels)
//   finalCompletePanel  → Win screen (after Level 5, shows all times)
//   exitConfirmPanel    → The exit confirmation popup
//
// SETUP: Attach to an empty GameObject named "UIManager" in each scene.
//        Link all panel GameObjects in the Inspector.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class UIManager : MonoBehaviour
{
    // ── Singleton (scene-scoped, NOT DontDestroyOnLoad) ───────────────────────
    public static UIManager Instance { get; private set; }

    // ── Panel References ──────────────────────────────────────────────────────

    [Header("─── Panels ──────────────────────────────────────────")]
    [Tooltip("Root GameObject of the Main Menu / Level Select panel.")]
    [SerializeField] private GameObject mainMenuPanel;

    [Tooltip("Root GameObject of the Story Comic panel.")]
    [SerializeField] private GameObject storyPanel;

    [Tooltip("Root GameObject of the in-game HUD.")]
    [SerializeField] private GameObject hudPanel;

    [Tooltip("Root GameObject of the Pause overlay.")]
    [SerializeField] private GameObject pausePanel;

    [Tooltip("Root GameObject of the Game Over screen.")]
    [SerializeField] private GameObject gameOverPanel;

    [Tooltip("Root GameObject of the Level Complete screen (Levels 1–4).")]
    [SerializeField] private GameObject levelCompletePanel;

    [Tooltip("Root GameObject of the Final Win screen (Level 5 only).")]
    [SerializeField] private GameObject finalCompletePanel;

    [Tooltip("Root GameObject of the Exit Confirmation popup.")]
    [SerializeField] private GameObject exitConfirmPanel;

    // ── HUD Elements ──────────────────────────────────────────────────────────

    [Header("─── HUD Elements ────────────────────────────────────")]
    [Tooltip("TextMeshPro text that displays the countdown timer.")]
    [SerializeField] private TMP_Text timerText;

    [Tooltip("How many seconds remain before the timer text turns red and pulses.")]
    [SerializeField] private float timerWarningThreshold = 10f;

    [Tooltip("Color of the timer text when time is running low.")]
    [SerializeField] private Color timerWarningColor = Color.red;

    [Tooltip("Normal color of the timer text.")]
    [SerializeField] private Color timerNormalColor  = Color.white;

    // ── Game Over Screen Elements ─────────────────────────────────────────────

    [Header("─── Game Over Screen ───────────────────────────────")]
    [Tooltip("Large header text showing the reason for failure (e.g. 'NABRAK!').")]
    [SerializeField] private TMP_Text gameOverReasonText;

    // ── Level Complete Screen Elements ────────────────────────────────────────

    [Header("─── Level Complete Screen ─────────────────────────")]
    [Tooltip("Text showing the clear time on the mid-level win screen.")]
    [SerializeField] private TMP_Text clearTimeText;

    [Tooltip("Text on the Final Win screen showing each level's best time.")]
    [SerializeField] private TMP_Text[] finalTimeTexts; // Assign 5 TMP_Text elements.

    [Tooltip("Text on the Final Win screen showing the total time across all levels.")]
    [SerializeField] private TMP_Text finalTotalTimeText;

    // ── Level Select (Main Menu) ───────────────────────────────────────────────

    [Header("─── Level Select Buttons ──────────────────────────")]
    [Tooltip("Assign all 5 LevelNodeUI components here in order (Level 1 → Level 5). " +
             "See the LevelNodeUI script for per-button setup.")]
    [SerializeField] private LevelNodeUI[] levelNodes;

    // ── Story Panel ───────────────────────────────────────────────────────────

    [Header("─── Story Panel ────────────────────────────────────")]
    [Tooltip("Image component that displays the current story comic page sprite.")]
    [SerializeField] private Image storyComicImage;

    [Tooltip("'Click anywhere to continue' pulsing text at the bottom of the story panel.")]
    [SerializeField] private TMP_Text storyContinueText;

    // ── Greeting Feedback ─────────────────────────────────────────────────────

    [Header("─── Action Feedback ────────────────────────────────")]
    [Tooltip("The 'Permisi!' speech bubble GameObject. Activated briefly on greet.")]
    [SerializeField] private GameObject greetingBubble;

    [Tooltip("Duration in seconds the greeting bubble stays visible.")]
    [SerializeField] private float greetingBubbleDuration = 0.8f;

    // ── Audio Toggle Buttons ──────────────────────────────────────────────────

    [Header("─── Audio Toggles ──────────────────────────────────")]
    [Tooltip("Drag the BGM Toggle component here. Must be a Unity UI Toggle, not a Button.")]
    [SerializeField] private Toggle bgmToggle;

    [Tooltip("Drag the SFX Toggle component here.")]
    [SerializeField] private Toggle sfxToggle;

    // ── Private State ─────────────────────────────────────────────────────────

    private int      _currentStoryPage    = 0;
    private bool     _timerPulsing        = false;
    private Coroutine _greetingCoroutine  = null; // Stored so we can safely stop it.

    // ==========================================================================
    //  Unity Lifecycle
    // ==========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        // Subscribe to GameManager events.
        GameManager.OnStateChanged  += HandleStateChanged;
        GameManager.OnTimerUpdated  += HandleTimerUpdated;
        GameManager.OnGameOver      += HandleGameOver;
        GameManager.OnLevelComplete += HandleLevelComplete;
    }

    private void OnDisable()
    {
        // Always unsubscribe to prevent memory leaks between scene loads.
        GameManager.OnStateChanged  -= HandleStateChanged;
        GameManager.OnTimerUpdated  -= HandleTimerUpdated;
        GameManager.OnGameOver      -= HandleGameOver;
        GameManager.OnLevelComplete -= HandleLevelComplete;
    }

    private void Start()
    {
        HandleStateChanged(GameManager.Instance != null
            ? GameManager.Instance.CurrentState
            : GameState.MainMenu);

        RefreshLevelSelectUI();

        // Sync immediately, then again next frame as safety net —
        // Unity Toggle visuals can re-draw themselves after Start() and
        // overwrite an immediate sync.
        SyncAudioToggleVisuals();
        StartCoroutine(SyncAudioTogglesNextFrame());
    }

    private IEnumerator SyncAudioTogglesNextFrame()
    {
        yield return null; // Wait one frame for Toggle components to settle.
        SyncAudioToggleVisuals();
    }

    // ==========================================================================
    //  Event Handlers
    // ==========================================================================

    private void HandleStateChanged(GameState newState)
    {
        Debug.Log($"[UIManager] HandleStateChanged({newState}) | " +
                $"levelCompletePanel={levelCompletePanel}");
        
        SetAllPanelsInactive();

        switch (newState)
        {

            case GameState.MainMenu:
                ShowPanel(mainMenuPanel);
                RefreshLevelSelectUI();
                SyncAudioToggleVisuals(); // Re-sync toggles every time the menu opens.
                break;

            case GameState.Story:
                ShowPanel(storyPanel);
                ShowStoryPage(0);
                break;

            case GameState.Playing:
                ShowPanel(hudPanel);
                break;

            case GameState.Paused:
                ShowPanel(hudPanel);    // Keep HUD visible behind pause.
                ShowPanel(pausePanel);
                break;

            case GameState.GameOver:
                ShowPanel(hudPanel);
                ShowPanel(gameOverPanel);
                break;

            case GameState.LevelComplete:
                ShowPanel(hudPanel);
                bool isFinal = GameManager.Instance != null && GameManager.Instance.IsFinalLevel;
                ShowPanel(isFinal ? finalCompletePanel : levelCompletePanel);
                break;
        }
    }

    private void HandleTimerUpdated(float remaining)
    {
        if (timerText == null) return;

        timerText.text = SaveSystem.FormatTime(remaining);

        // Warning color and pulse effect when time is low.
        bool inWarning = remaining <= timerWarningThreshold;
        timerText.color = inWarning ? timerWarningColor : timerNormalColor;

        if (inWarning && !_timerPulsing)
        {
            _timerPulsing = true;
            StartCoroutine(PulseText(timerText));
        }
        else if (!inWarning && _timerPulsing)
        {
            _timerPulsing = false;
            StopCoroutine(nameof(PulseText));
            timerText.transform.localScale = Vector3.one;
        }
    }

    private void HandleGameOver(string reason)
    {
        if (gameOverReasonText != null) gameOverReasonText.text = reason;
    }

    private void HandleLevelComplete(float clearTime)
    {
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.IsFinalLevel)
        {
            // Populate the final record screen with all level times.
            PopulateFinalRecordScreen();
        }
        else
        {
            // Show just this level's clear time.
            if (clearTimeText != null)
                clearTimeText.text = $"Clear Time: {SaveSystem.FormatTime(clearTime)}";
        }
    }

    // ==========================================================================
    //  Story Panel Logic
    // ==========================================================================

    /// <summary>Displays a specific page of the story comic.</summary>
    private void ShowStoryPage(int pageIndex)
    {
        if (GameManager.Instance?.CurrentLevelData == null) return;

        Sprite[] pages = GameManager.Instance.CurrentLevelData.storyComicPages;
        bool hasPages = pages != null && pages.Length > 0;

        _currentStoryPage = hasPages ? Mathf.Clamp(pageIndex, 0, pages.Length - 1) : 0;

        // Set the comic image — show placeholder color if no sprite assigned yet
        if (storyComicImage != null)
        {
            storyComicImage.sprite = hasPages ? pages[_currentStoryPage] : null;
            storyComicImage.color  = hasPages ? Color.white : new Color(0.15f, 0.15f, 0.15f);
        }

        // "Click to continue" text always shows so the player knows to tap
        if (storyContinueText != null)
            storyContinueText.gameObject.SetActive(true);
    }

    /// <summary>
    /// Called when the player clicks anywhere on the story panel.
    /// Advances pages; on the last page, starts actual gameplay.
    /// Hook this to the story panel's Button or EventTrigger in the Inspector.
    /// </summary>
    public void OnStoryPanelClicked()
    {
        GameManager.Instance?.PlayButtonClickSFX();
        if (GameManager.Instance?.CurrentLevelData == null) return;

        Sprite[] pages = GameManager.Instance.CurrentLevelData.storyComicPages;
        int totalPages = pages != null ? pages.Length : 0;

        if (totalPages > 1 && _currentStoryPage < totalPages - 1)
        {
            // More pages to show
            ShowStoryPage(_currentStoryPage + 1);
        }
        else
        {
            // Last page OR no pages at all — start the level
            GameManager.Instance.StartPlaying();
        }
    }

    // ==========================================================================
    //  Greeting Feedback
    // ==========================================================================

    /// <summary>
    /// Called by PlayerController or EtikaZone when a successful greeting occurs.
    /// Briefly shows the "Permisi!" speech bubble.
    /// </summary>
    public void ShowGreetingFeedback()
    {
        if (greetingBubble == null) return;

        // Only stop the hide timer if it's actually running — avoids the
        // "coroutine not running" exception that killed the bubble on first use.
        if (_greetingCoroutine != null)
        {
            StopCoroutine(_greetingCoroutine);
            _greetingCoroutine = null;
        }

        greetingBubble.SetActive(true);
        _greetingCoroutine = StartCoroutine(HideGreetingBubble());
    }

    private IEnumerator HideGreetingBubble()
    {
        yield return new WaitForSeconds(greetingBubbleDuration);
        if (greetingBubble != null) greetingBubble.SetActive(false);
        _greetingCoroutine = null;
    }

    // ==========================================================================
    //  Level Select UI
    // ==========================================================================

    /// <summary>
    /// Refreshes all level node buttons (locked/unlocked/completed state + best time).
    /// Called on Start and whenever returning to the main menu.
    /// </summary>
    public void RefreshLevelSelectUI()
    {
        if (levelNodes == null) return;

        for (int i = 0; i < levelNodes.Length; i++)
        {
            if (levelNodes[i] == null) continue;

            bool unlocked  = SaveSystem.IsLevelUnlocked(i);
            float bestTime = SaveSystem.GetBestTime(i);
            levelNodes[i].SetState(unlocked, bestTime, i);
        }
    }

    // ==========================================================================
    //  Button Callbacks (wire these to UI Buttons in the Inspector)
    // ==========================================================================

    // ── Main Menu / Level Select ───────────────────────────────────────────────

    public void OnLevelNodePressed(int levelIndex)
    {
        GameManager.Instance?.PlayButtonClickSFX();
        GameManager.Instance?.SelectLevel(levelIndex);
    }

    public void OnExitPressed()
    {
        GameManager.Instance?.PlayButtonClickSFX();
        ShowPanel(exitConfirmPanel);
    }

    public void OnExitConfirmed()
    {
        GameManager.Instance?.PlayButtonClickSFX();
    #if UNITY_EDITOR
    UnityEditor.EditorApplication.isPlaying = false; // Stops Play mode in Editor
    #elif UNITY_WEBGL
    Application.ExternalCall("location.reload");
    #else
    Application.Quit(); 
    #endif
    }

    public void OnExitCancelled()
    {
        GameManager.Instance?.PlayButtonClickSFX();
        HidePanel(exitConfirmPanel);
    }

    public void OnToggleBGM()
    {
        GameManager.Instance?.PlayButtonClickSFX();
        GameManager.Instance?.ToggleBGM();
        SyncAudioToggleVisuals();
    }

    public void OnToggleSFX()
    {
        GameManager.Instance?.PlayButtonClickSFX();
        GameManager.Instance?.ToggleSFX();
        SyncAudioToggleVisuals();
    }

    // ── Gameplay / HUD ────────────────────────────────────────────────────────

    public void OnPausePressed()
    {
        GameManager.Instance?.PlayButtonClickSFX();
        GameManager.Instance?.TogglePause();
    }

    // ── Pause Screen ──────────────────────────────────────────────────────────

    public void OnResumePressed()
    {
        GameManager.Instance?.PlayButtonClickSFX();
        GameManager.Instance?.TogglePause();
    }

    public void OnRestartFromPause()
    {
        GameManager.Instance?.PlayButtonClickSFX();
        GameManager.Instance?.RetryLevel();
    }

    public void OnQuitToMenuFromPause()
    {
        GameManager.Instance?.PlayButtonClickSFX();
        GameManager.Instance?.GoToMainMenu();
    }

    // ── Game Over Screen ──────────────────────────────────────────────────────

    public void OnRetryFromGameOver()
    {
        GameManager.Instance?.PlayButtonClickSFX();
        GameManager.Instance?.RetryLevel();
    }

    public void OnMenuFromGameOver()
    {
        GameManager.Instance?.PlayButtonClickSFX();
        GameManager.Instance?.GoToMainMenu();
    }

    // ── Level Complete Screen ─────────────────────────────────────────────────

    public void OnNextLevelPressed()
    {
        GameManager.Instance?.PlayButtonClickSFX();
        GameManager.Instance?.GoToNextLevel();
    }

    public void OnRetryLevelPressed()
    {
        GameManager.Instance?.PlayButtonClickSFX();
        GameManager.Instance?.RetryLevel();
    }

    public void OnMenuFromComplete()
    {
        GameManager.Instance?.PlayButtonClickSFX();
        GameManager.Instance?.GoToMainMenu();
    }

    // ── Final Win Screen ──────────────────────────────────────────────────────

    public void OnRetryFromLevel1()
    {
        GameManager.Instance?.PlayButtonClickSFX();
        GameManager.Instance?.SelectLevel(0);
    }

    // ==========================================================================
    //  Private Helpers
    // ==========================================================================

    /// <summary>
    /// Reads BGMEnabled/SFXEnabled from GameManager and sets the toggle visuals
    /// WITHOUT firing onValueChanged (which would flip the state back).
    /// Called on scene load, on every return to main menu, and after each toggle press.
    /// </summary>
    private void SyncAudioToggleVisuals()
    {
        if (GameManager.Instance == null) return;

        if (bgmToggle != null)
            bgmToggle.SetIsOnWithoutNotify(GameManager.Instance.BGMEnabled);
        else
            Debug.LogWarning("[UIManager] bgmToggle is not assigned in the Inspector. " +
                             "Drag the BGM Toggle component into the 'Bgm Toggle' field.");

        if (sfxToggle != null)
            sfxToggle.SetIsOnWithoutNotify(GameManager.Instance.SFXEnabled);
        else
            Debug.LogWarning("[UIManager] sfxToggle is not assigned in the Inspector.");
    }

    private void SetAllPanelsInactive()
    {
        SetActive(mainMenuPanel, false);
        SetActive(storyPanel, false);
        SetActive(hudPanel, false);
        SetActive(pausePanel, false);
        SetActive(gameOverPanel, false);
        SetActive(levelCompletePanel, false);
        SetActive(finalCompletePanel, false);
        // Note: exitConfirmPanel is handled separately (it's an overlay).
    }

    private void ShowPanel(GameObject panel) => SetActive(panel, true);
    private void HidePanel(GameObject panel) => SetActive(panel, false);
    private void SetActive(GameObject go, bool state) { if (go != null) go.SetActive(state); }

    private void PopulateFinalRecordScreen()
    {
        float[] times = SaveSystem.GetAllBestTimes();
        float   total = 0f;

        for (int i = 0; i < times.Length; i++)
        {
            total += times[i];
            if (finalTimeTexts != null && i < finalTimeTexts.Length && finalTimeTexts[i] != null)
                finalTimeTexts[i].text = $"Level {i + 1}: {SaveSystem.FormatTime(times[i])}";
        }

        if (finalTotalTimeText != null)
            finalTotalTimeText.text = $"Total: {SaveSystem.FormatTime(total)}";
    }

    private IEnumerator PulseText(TMP_Text text)
    {
        float speed = 4f;
        float minScale = 0.9f;
        float maxScale = 1.1f;

        while (_timerPulsing)
        {
            float scale = Mathf.Lerp(minScale, maxScale, (Mathf.Sin(Time.unscaledTime * speed) + 1f) / 2f);
            text.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        text.transform.localScale = Vector3.one;
    }

    private void Update()
    {
        GameState state = GameManager.Instance?.CurrentState ?? GameState.MainMenu;

        // ── Story: any key / click / tap untuk lanjut ─────────────────────────
        if (state == GameState.Story)
        {
            bool anyKey  = Keyboard.current    != null && Keyboard.current.anyKey.wasPressedThisFrame;
            bool clicked = Mouse.current       != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool tapped  = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;

            if (anyKey || clicked || tapped)
                OnStoryPanelClicked();

            return; // Jangan proses input lain selama Story
        }

        // ── Playing / Paused: Spacebar untuk pause/resume ─────────────────────
        if (state == GameState.Playing || state == GameState.Paused)
        {
            bool spacePressed = Keyboard.current != null &&
                                Keyboard.current.spaceKey.wasPressedThisFrame;
            if (spacePressed)
                GameManager.Instance?.TogglePause();
        }
    }

    /// <summary>Called by the Restart/New Game button on the Main Menu.</summary>
    public void OnRestartGamePressed()
    {
        GameManager.Instance?.PlayButtonClickSFX();
        SaveSystem.ResetProgress();
        RefreshLevelSelectUI(); // Immediately updates the level node visuals on screen
    }
}