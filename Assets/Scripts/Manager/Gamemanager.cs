// =============================================================================
// GameManager.cs
// The central nervous system of Fatsun Furious.
//
// Responsibilities:
//  - Persistent singleton across scenes (DontDestroyOnLoad)
//  - Game state machine (MainMenu → Story → Playing → Paused/GameOver/LevelComplete)
//  - Countdown timer that drives world scroll speed indirectly
//  - Triggering win and lose states with a reason string
//  - Broadcasting events so UIManager and other systems react without polling
//
// SETUP: Attach to an empty GameObject named "GameManager" in your
//        persistent/bootstrap scene OR in the MainMenu scene.
//        Mark it DontDestroyOnLoad (handled automatically in Awake).
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// ── Game State Enum ───────────────────────────────────────────────────────────
public enum GameState
{
    MainMenu,      // On the main menu / level select screen
    Story,         // Viewing the per-level comic strip
    Playing,       // Active gameplay — timer running, inputs live
    Paused,        // Paused overlay shown — timer frozen, inputs locked
    GameOver,      // Failure state — reason string set
    LevelComplete  // Win state — time recorded
}

public class GameManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static GameManager Instance { get; private set; }

    // ── Inspector Fields ──────────────────────────────────────────────────────

    [Header("─── Level Configuration ─────────────────────────────")]
    [Tooltip("All 5 LevelData ScriptableObjects in order. " +
             "Drag LevelData_1 through LevelData_5 here.")]
    [SerializeField] private LevelData[] levels;

    [Header("─── Scene Names ─────────────────────────────────────")]
    [Tooltip("Exact name of the Main Menu scene as shown in Build Settings.")]
    [SerializeField] private string mainMenuSceneName = "01_MainMenu";

    [Tooltip("Exact name of the Gameplay scene as shown in Build Settings.")]
    [SerializeField] private string gameplaySceneName = "02_Gameplay";

    [Header("─── Audio ───────────────────────────────────────────")]
    [Tooltip("AudioSource used to play background music. " +
             "Attach an AudioSource component and drag it here.")]
    [SerializeField] private AudioSource bgmSource;

    [Tooltip("AudioSource used to play sound effects (SFX).")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("BGM played on the Main Menu and Story screens. " +
             "Drag your menu music clip here.")]
    [SerializeField] private AudioClip menuBGM;

    [Tooltip("Sound played when the player successfully greets an NPC (Permisi!).")]
    [SerializeField] private AudioClip sfxGreeting;

    [Tooltip("Sound played on any collision / failure.")]
    [SerializeField] private AudioClip sfxCrash;

    [Tooltip("Sound played when a level is completed.")]
    [SerializeField] private AudioClip sfxLevelComplete;

    [Tooltip("SFX pendek saat player menekan tombol UI apapun.")]
    [SerializeField] private AudioClip sfxButtonClick;

    // ── Public Read-Only State ────────────────────────────────────────────────

    /// <summary>Current game state. Read-only externally; changed via ChangeState().</summary>
    public GameState CurrentState { get; private set; } = GameState.MainMenu;

    /// <summary>Zero-based index of the level currently loaded or selected.</summary>
    public int CurrentLevelIndex { get; private set; } = 0;

    /// <summary>The LevelData asset for the current level.</summary>
    public LevelData CurrentLevelData => levels != null && CurrentLevelIndex < levels.Length
                                         ? levels[CurrentLevelIndex] : null;

    /// <summary>Remaining time in seconds. Read by UIManager to update the HUD.</summary>
    public float RemainingTime { get; private set; }

    /// <summary>Time elapsed since the level started (used for clear-time recording).</summary>
    public float ElapsedTime { get; private set; }

    /// <summary>Current world scroll speed (units/sec). Read by WorldScroller.</summary>
    public float CurrentScrollSpeed { get; private set; }

    /// <summary>Reason string for the current Game Over (e.g. "NABRAK!", "TELAT!").</summary>
    public string GameOverReason { get; private set; } = "";

    /// <summary>True if this is the final level (index 4 out of 5).</summary>
    public bool IsFinalLevel => CurrentLevelIndex == (levels != null ? levels.Length - 1 : 4);

    // ── Events ────────────────────────────────────────────────────────────────
    // UIManager and other systems subscribe to these. No polling needed.

    /// <summary>Fired whenever the game state changes. Passes the new state.</summary>
    public static event Action<GameState> OnStateChanged;

    /// <summary>Fired every frame during Playing state. Passes remaining seconds.</summary>
    public static event Action<float> OnTimerUpdated;

    /// <summary>Fired when a Game Over is triggered. Passes the reason string.</summary>
    public static event Action<string> OnGameOver;

    /// <summary>Fired when a level is completed. Passes the clear time in seconds.</summary>
    public static event Action<float> OnLevelComplete;


    // ── Audio Volume Preferences (persisted via PlayerPrefs) ──────────────────

    public bool BGMEnabled  { get; private set; } = true;
    public bool SFXEnabled  { get; private set; } = true;

    // ── Private State ─────────────────────────────────────────────────────────

    private bool _timerRunning = false;

    // ==========================================================================
    //  Unity Lifecycle
    // ==========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Fallback — fetch AudioSources from this GameObject if Inspector refs are lost
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sfxSource == null && sources.Length > 0) sfxSource = sources[0];
        if (bgmSource == null && sources.Length > 1) bgmSource = sources[1];

        if (sfxSource != null) sfxSource.ignoreListenerPause = true;
        // bgmSource respects AudioListener.pause (default) — used by TogglePause.
        if (bgmSource != null) bgmSource.ignoreListenerPause = false;

        SaveSystem.InitializeSave();
        BGMEnabled = PlayerPrefs.GetInt("bgm_enabled", 1) == 1;
        SFXEnabled = PlayerPrefs.GetInt("sfx_enabled", 1) == 1;

        // Play menu BGM immediately on boot.
        PlayBGM(menuBGM);
    }

    private void Update()
    {
        if (_timerRunning && CurrentState == GameState.Playing)
        {
            RemainingTime -= Time.deltaTime;
            ElapsedTime   += Time.deltaTime;

            // Broadcast timer update every frame for the HUD.
            OnTimerUpdated?.Invoke(RemainingTime);

            // Check expiry AFTER updating so the UI shows 0:00 briefly.
            if (RemainingTime <= 0f)
            {
                RemainingTime = 0f;
                TriggerGameOver("TELAT!");
            }
        }
    }

    // ==========================================================================
    //  Public API — called by UI buttons, PlayerController, and Hazards
    // ==========================================================================

    // ── Level Selection ────────────────────────────────────────────────────────

    /// <summary>
    /// Called when the player taps a level node on the Level Select screen.
    /// Stores the selection and loads the gameplay scene.
    /// </summary>
    public void SelectLevel(int levelIndex)
    {
        if (levelIndex < 0 || levels == null || levelIndex >= levels.Length)
        {
            Debug.LogError($"[GameManager] Invalid level index: {levelIndex}");
            return;
        }
        if (!SaveSystem.IsLevelUnlocked(levelIndex))
        {
            Debug.LogWarning($"[GameManager] Level {levelIndex + 1} is locked.");
            return;
        }

        CurrentLevelIndex = levelIndex;
        Debug.Log($"[GameManager] Selected Level {levelIndex + 1}. Loading gameplay scene.");

        // LevelManager in the gameplay scene will pick up CurrentLevelIndex on Start().
        SceneManager.LoadScene(gameplaySceneName);
    }

    // ── State Transitions ─────────────────────────────────────────────────────

    /// <summary>Called by LevelManager after the level prefab has been spawned.</summary>
    public void StartStory()
    {
        PlayBGM(CurrentLevelData?.levelBGM ?? menuBGM); // Play menu music during the comic story screen.
        ChangeState(GameState.Story);
    }

    /// <summary>Called by UIManager when the player clicks "Continue" on the story screen.</summary>
    public void StartPlaying()
    {
        if (CurrentLevelData == null)
        {
            Debug.LogError("[GameManager] No LevelData set — cannot start playing.");
            return;
        }

        // Reset timer and speed.
        RemainingTime = CurrentLevelData.timeLimitSeconds;
        ElapsedTime   = 0f;
        SetScrollSpeedForState(SpeedState.Normal);

        _timerRunning = true;
        ChangeState(GameState.Playing);

        // Switch to level BGM (stops menu BGM, starts level track with looping).
        PlayBGM(CurrentLevelData.levelBGM);
    }

    /// <summary>Pauses or resumes the game. Toggle-safe.</summary>
    public void TogglePause()
    {
        if (CurrentState == GameState.Playing)
        {
            _timerRunning = false;
            Time.timeScale = 0f;
            AudioListener.pause = true;   // Pauses bgmSource (ignoreListenerPause=false).
            ChangeState(GameState.Paused);
        }
        else if (CurrentState == GameState.Paused)
        {
            _timerRunning = true;
            Time.timeScale = 1f;
            AudioListener.pause = false;  // Resumes bgmSource from where it stopped.
            ChangeState(GameState.Playing);
        }
    }

    /// <summary>
    /// The main failure entry-point. Call from any hazard or PlayerController.
    /// <param name="reason">Indonesian failure text: "NABRAK!", "KURANG AJAR!", "NYIPRAT!", "TELAT!"</param>
    /// </summary>
    public void TriggerGameOver(string reason)
    {
        if (CurrentState == GameState.GameOver) return;

        _timerRunning  = false;
        GameOverReason = reason;

        StopBGM();          // Silence the level music immediately.
        PlaySFX(sfxCrash);  // Play crash / failure sound effect.

        ChangeState(GameState.GameOver);
        OnGameOver?.Invoke(reason);
    }

    /// <summary>
    /// The win entry-point. Called by the LevelFinishLine trigger.
    /// </summary>
    public void TriggerLevelComplete()
    {
        if (CurrentState == GameState.LevelComplete) return;

        _timerRunning = false;
        float clearTime = ElapsedTime;

        // Save best time and unlock next level.
        SaveSystem.TrySetBestTime(CurrentLevelIndex, clearTime);
        SaveSystem.OnLevelComplete(CurrentLevelIndex);

        PlaySFX(sfxLevelComplete);

        ChangeState(GameState.LevelComplete);
        OnLevelComplete?.Invoke(clearTime);

        Debug.Log($"[GameManager] LEVEL {CurrentLevelIndex + 1} COMPLETE! Clear time: {SaveSystem.FormatTime(clearTime)}");
    }

    // ── In-Level Navigation (called by UI buttons) ────────────────────────────

    /// <summary>Restarts the current level from scratch.</summary>
    public void RetryLevel()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false; // Un-pause the listener if retrying from Paused state.
        ChangeState(GameState.MainMenu); // Reset state before reload.
        SceneManager.LoadScene(gameplaySceneName);
    }

    /// <summary>Returns to the Main Menu scene.</summary>
    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false; // Safety: always un-pause the listener on scene exit.
        ChangeState(GameState.MainMenu);
        PlayBGM(menuBGM);            // Restart menu music.
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>Advances to the next level. Call from the "Next Level" button.</summary>
    public void GoToNextLevel()
    {
        if (IsFinalLevel)
        {
            GoToMainMenu();
            return;
        }
        SelectLevel(CurrentLevelIndex + 1);
    }

    // ── Scroll Speed Management ───────────────────────────────────────────────

    /// <summary>
    /// Called by PlayerController every frame to update the world scroll speed
    /// based on the player's current speed state input.
    /// </summary>
    public void SetScrollSpeedForState(SpeedState state)
    {
        if (CurrentLevelData == null) return;

        float baseSpeed = CurrentLevelData.baseScrollSpeed;
        CurrentScrollSpeed = state switch
        {
            SpeedState.Accelerate => baseSpeed * CurrentLevelData.accelerateMultiplier,
            SpeedState.Slow       => baseSpeed * CurrentLevelData.slowMultiplier,
            _                     => baseSpeed  // Normal
        };
    }

    // ── Audio Controls ────────────────────────────────────────────────────────

    public void ToggleBGM()
    {
        BGMEnabled = !BGMEnabled;
        PlayerPrefs.SetInt("bgm_enabled", BGMEnabled ? 1 : 0);
        if (bgmSource == null) return;
        // Un-pause/pause without restarting the clip from the beginning.
        if (BGMEnabled) bgmSource.UnPause();
        else            bgmSource.Pause();
    }

    public void ToggleSFX()
    {
        SFXEnabled = !SFXEnabled;
        PlayerPrefs.SetInt("sfx_enabled", SFXEnabled ? 1 : 0);
    }

    public void PlaySFX(AudioClip clip)
    {
        Debug.Log($"[GameManager] PlaySFX called | " +
                $"SFXEnabled={SFXEnabled} | " +
                $"sfxSource={sfxSource} | " +
                $"clip={clip}");

        if (SFXEnabled && sfxSource != null && clip != null)
            sfxSource.PlayOneShot(clip);
    }

    public void PlayGreetingSFX() => PlaySFX(sfxGreeting);

    /// <summary>
    /// Stops the current BGM, assigns the new clip, enables looping, and plays it.
    /// Safe to call with a null clip — stops BGM without starting a new track.
    /// </summary>
    private void PlayBGM(AudioClip clip)
    {
        if (bgmSource == null) return;

        // Always stop before switching clips to avoid overlapping audio.
        bgmSource.Stop();

        if (clip == null)
        {
            Debug.Log("[GameManager] PlayBGM called with null clip — BGM stopped.");
            return;
        }

        bgmSource.clip = clip;
        bgmSource.loop = true;

        if (BGMEnabled) bgmSource.Play();
    }

    /// <summary>Stops BGM playback immediately.</summary>
    private void StopBGM()
    {
        if (bgmSource != null) bgmSource.Stop();
    }

    private void ApplyAudioSettings()
    {
        // No-op: BGM state is now fully controlled by PlayBGM/StopBGM/ToggleBGM.
        // This stub is kept for future volume-slider support.
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private void ChangeState(GameState newState)
    {
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
        Debug.Log($"[GameManager] State → {newState}");
    }
}

// ── Speed State Enum ─────────────────────────────────────────────────────────
// Defined here so PlayerController and GameManager share the same type.
public enum SpeedState
{
    Normal,
    Accelerate,
    Slow
}