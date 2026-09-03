using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameMode { Campaign, Timed, Speed }

    [Header("Mode Configuration")]
    public GameMode activeMode = GameMode.Campaign;
    private int scoreCount; // Used for Timed Mode correct clicks

    [Header("Global Configuration")]
    public GameConfig gameConfig;

    [Header("Level Configuration")]
    public List<LevelData> levels;
    public int currentLevelIndex = 0;
    private LevelData activeLevel;

    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject gameplayPanel;
    public GameObject successPanel;
    public GameObject failPanel;
    public GameObject settingsPanel;
    public List<Button> levelButtons;

    [Header("Gameplay Elements")]
    public RectTransform gridContainer;
    public GameObject squarePrefab;
    public Image flashOverlay;

    [Header("Top Bar Indicators")]
    public TMP_Text levelText;
    public TMP_Text timerText;
    public TMP_Text remainingText;
    public Button homeButton;
    public Button settingsButton;

    [Header("Success/Fail Screens UI")]
    public TMP_Text successLevelText;
    public Button successNextButton;
    public Button successMenuButton;
    
    public TMP_Text failLevelText;
    public TMP_Text failReasonText;
    public Button failRetryButton;
    public Button failMenuButton;

    [Header("Settings Panel UI")]
    public Button settingsCloseButton;
    public Slider sfxVolumeSlider; // Optional extra for settings polish
    public Toggle hapticToggle;    // Optional extra for settings polish

    [Header("Asset References")]
    public Sprite solidSquareSprite;
    public Sprite outlineSquareSprite;

    // Grid tracking
    private List<GameSquare> instantiatedSquares = new List<GameSquare>();
    private float levelTimer;
    private int correctClicksRemaining;
    private bool isGameActive;

    // Reverse modifier tracking
    private bool isReverseActive;
    private int reverseCorrectClicksRemaining;
    private float reverseCooldownRemaining;
    private bool isGameplayTransitionLocked;
    private GameplayFeedbackController feedbackController;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private DebugLevelSelectController debugLevelSelectController;
#endif

    // Active lit square tracking
    private GameSquare smallLitSquare;
    private GameSquare mediumLitSquare;
    private GameSquare largeLitSquare;

    // Colors - made slightly transparent/glowing neon border
    private Color flashGreen = new Color(0f, 1f, 0.4f, 0.8f);
    private Color flashRed = new Color(1f, 0.1f, 0.2f, 0.8f);

    private Coroutine flashCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Add listeners to static buttons
        homeButton.onClick.AddListener(ReturnToMainMenu);
        settingsButton.onClick.AddListener(OpenSettings);
        settingsCloseButton.onClick.AddListener(CloseSettings);
        
        successMenuButton.onClick.AddListener(ReturnToMainMenu);
        failRetryButton.onClick.AddListener(RestartLevel);
        failMenuButton.onClick.AddListener(ReturnToMainMenu);

        // Bind main menu game modes dynamically
        BindMenuButtons();

        // Ensure flash overlay is configured as a gorgeous neon edge frame instead of full-screen solid block
        if (flashOverlay != null)
        {
            flashOverlay.gameObject.SetActive(true);
            flashOverlay.type = Image.Type.Simple;
            flashOverlay.color = Color.clear;
            flashOverlay.raycastTarget = false;
        }

        feedbackController = GetComponent<GameplayFeedbackController>();
        if (feedbackController == null)
        {
            feedbackController = gameObject.AddComponent<GameplayFeedbackController>();
        }
        feedbackController.Initialize(gameplayPanel.GetComponent<RectTransform>());

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (gameConfig != null && gameConfig.enableDebugLevelSelect)
        {
            debugLevelSelectController = GetComponent<DebugLevelSelectController>();
            if (debugLevelSelectController == null)
            {
                debugLevelSelectController = gameObject.AddComponent<DebugLevelSelectController>();
            }
            debugLevelSelectController.Initialize(mainMenuPanel.GetComponent<RectTransform>(), levels, StartLevel);
        }
#endif

        ShowMainMenu();
    }

    private void Update()
    {
        if (!isGameActive) return;
        if (isGameplayTransitionLocked) return;

        RotatePlayArea();

        if (!isReverseActive && reverseCooldownRemaining > 0f)
        {
            reverseCooldownRemaining = Mathf.Max(0f, reverseCooldownRemaining - Time.deltaTime);
        }

        if (activeMode == GameMode.Campaign)
        {
            levelTimer -= Time.deltaTime;
            if (levelTimer <= 0f)
            {
                levelTimer = 0f;
                UpdateTimerUI();
                LevelFailed("TIME'S UP!");
            }
            else
            {
                UpdateTimerUI();
            }
        }
        else if (activeMode == GameMode.Timed)
        {
            levelTimer -= Time.deltaTime;
            if (levelTimer <= 0f)
            {
                levelTimer = 0f;
                UpdateTimerUI();
                TimedModeCompleted();
            }
            else
            {
                UpdateTimerUI();
            }
        }
        else if (activeMode == GameMode.Speed)
        {
            levelTimer += Time.deltaTime;
            UpdateTimerUI();
        }
    }

    private void BindMenuButtons()
    {
        // Campaign Mode Button
        Transform btn1 = mainMenuPanel.transform.Find("PlayLevel1Button");
        if (btn1 != null)
        {
            Button btn = btn1.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(StartCampaignMode);
            }
        }

        // Timed Mode Button
        Transform btn2 = mainMenuPanel.transform.Find("PlayLevel2Button");
        if (btn2 != null)
        {
            Button btn = btn2.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(StartTimedMode);
            }
        }

        // Speed Mode Button
        Transform btn3 = mainMenuPanel.transform.Find("PlayLevel3Button");
        if (btn3 != null)
        {
            Button btn = btn3.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(StartSpeedMode);
            }
        }
    }

    public void ShowMainMenu()
    {
        isGameActive = false;
        isGameplayTransitionLocked = false;
        if (feedbackController != null) feedbackController.ResetImmediate();
        mainMenuPanel.SetActive(true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugLevelSelectController != null) debugLevelSelectController.Close();
#endif
        gameplayPanel.SetActive(false);
        successPanel.SetActive(false);
        failPanel.SetActive(false);
        settingsPanel.SetActive(false);

        // Update button texts dynamically based on player persistent data
        int savedCampaignLevel = GetSavedCampaignLevelIndex();
        int timedHighScore = PlayerPrefs.GetInt("TimedHighScore", 0);
        float speedBestTime = PlayerPrefs.GetFloat("SpeedBestTime", 9999f);

        // Update Campaign Button Text
        Transform btn1 = mainMenuPanel.transform.Find("PlayLevel1Button/Text");
        if (btn1 != null)
        {
            TMP_Text txt = btn1.GetComponent<TMP_Text>();
            if (txt != null)
            {
                txt.text = levels != null && levels.Count > 0 && levels[savedCampaignLevel] != null
                    ? $"CONTINUE LEVEL {levels[savedCampaignLevel].levelNumber}"
                    : "CAMPAIGN";
            }
        }

        // Update Timed Button Text
        Transform btn2 = mainMenuPanel.transform.Find("PlayLevel2Button/Text");
        if (btn2 != null)
        {
            TMP_Text txt = btn2.GetComponent<TMP_Text>();
            if (txt != null) txt.text = $"TIMED MODE (BEST: {timedHighScore})";
        }

        // Update Speed Button Text
        Transform btn3 = mainMenuPanel.transform.Find("PlayLevel3Button/Text");
        if (btn3 != null)
        {
            TMP_Text txt = btn3.GetComponent<TMP_Text>();
            if (txt != null) txt.text = $"SPEED MODE (BEST: {(speedBestTime < 9998f ? speedBestTime.ToString("F2") + "s" : "--")})";
        }
    }

    public void StartCampaignMode()
    {
        activeMode = GameMode.Campaign;
        currentLevelIndex = GetSavedCampaignLevelIndex();

        StartActiveSetup();
    }

    public void StartTimedMode()
    {
        activeMode = GameMode.Timed;
        scoreCount = 0;
        
        // Use active campaign level configuration for colors/style consistency, or fallback to Level 1
        currentLevelIndex = GetSavedCampaignLevelIndex();

        StartActiveSetup();
    }

    public void StartSpeedMode()
    {
        activeMode = GameMode.Speed;
        
        currentLevelIndex = GetSavedCampaignLevelIndex();

        StartActiveSetup();
    }

    private void StartActiveSetup()
    {
        if (levels == null || levels.Count == 0)
        {
            Debug.LogError("No levels configured in GameManager.");
            return;
        }

        activeLevel = levels[currentLevelIndex];
        ResetReverseState();

        // Panel Activations
        mainMenuPanel.SetActive(false);
        gameplayPanel.SetActive(true);
        successPanel.SetActive(false);
        failPanel.SetActive(false);
        settingsPanel.SetActive(false);

        ConfigurePlayAreaRotation();

        // Camera styling
        Camera.main.backgroundColor = activeLevel.backgroundColor;
        Camera.main.clearFlags = CameraClearFlags.SolidColor;
        gameplayPanel.GetComponent<Image>().color = activeLevel.backgroundColor;

        // Custom start states per mode
        if (activeMode == GameMode.Campaign)
        {
            levelTimer = activeLevel.timeLimit;
            correctClicksRemaining = activeLevel.requiredCorrectClicks;
            levelText.text = $"LEVEL {activeLevel.levelNumber}";
        }
        else if (activeMode == GameMode.Timed)
        {
            levelTimer = 60f; // 1-minute fixed
            correctClicksRemaining = 0; // Not used
            levelText.text = "TIMED MODE";
        }
        else if (activeMode == GameMode.Speed)
        {
            levelTimer = 0f; // Starts at zero, counts up
            correctClicksRemaining = 100; // Click 100 squares as fast as possible
            levelText.text = "SPEED MODE";
        }

        levelText.color = activeLevel.textPrimaryColor;
        timerText.color = activeLevel.textPrimaryColor;
        remainingText.color = activeLevel.textPrimaryColor;

        UpdateTimerUI();
        UpdateRemainingUI();

        // Build playfield grid
        BuildGrid();

        // Illuminate neon squares
        InitializeLitSquares();

        isGameActive = true;
    }

    private void BuildGrid()
    {
        foreach (var sq in instantiatedSquares)
        {
            Destroy(sq.gameObject);
        }
        instantiatedSquares.Clear();

        int n = activeLevel.gridSize;
        if (n < 2) n = 2;

        float containerSize = Mathf.Min(gridContainer.rect.width, gridContainer.rect.height);
        float spacing = containerSize * 0.03f;
        float totalSpacing = spacing * (n - 1);
        float cellSize = (containerSize - totalSpacing) / n;

        GridLayoutGroup gridLayout = gridContainer.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
        {
            gridLayout = gridContainer.gameObject.AddComponent<GridLayoutGroup>();
        }

        gridLayout.cellSize = new Vector2(cellSize, cellSize);
        gridLayout.spacing = new Vector2(spacing, spacing);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = n;
        gridLayout.childAlignment = TextAnchor.MiddleCenter;

        Canvas.ForceUpdateCanvases();

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                GameObject cellObj = Instantiate(squarePrefab, gridContainer);
                cellObj.name = $"Square_{x}_{y}";
                
                GameSquare sq = cellObj.GetComponent<GameSquare>();
                sq.Setup(
                    x, y, 
                    solidSquareSprite, 
                    outlineSquareSprite, 
                    activeLevel.cellColor, 
                    activeLevel.outlineColor,
                    activeLevel.smallScale,
                    activeLevel.mediumScale,
                    activeLevel.fullScale
                );

                sq.onClicked = OnSquareClicked;
                instantiatedSquares.Add(sq);
            }
        }
    }

    private void ConfigurePlayAreaRotation()
    {
        gridContainer.localRotation = Quaternion.identity;

        // A square's largest axis-aligned footprint occurs at 45 degrees. Scaling
        // it by 1/sqrt(2) keeps every cell fully inside the original play area.
        float safeScale = Mathf.Approximately(activeLevel.rotateSpeed, 0f)
            ? 1f
            : 1f / Mathf.Sqrt(2f);
        gridContainer.localScale = Vector3.one * safeScale;

        Canvas.ForceUpdateCanvases();
    }

    private void RotatePlayArea()
    {
        if (activeLevel == null || Mathf.Approximately(activeLevel.rotateSpeed, 0f)) return;

        gridContainer.Rotate(0f, 0f, activeLevel.rotateSpeed * Time.deltaTime, Space.Self);
    }

    private void InitializeLitSquares()
    {
        smallLitSquare = null;
        mediumLitSquare = null;
        largeLitSquare = null;

        if (instantiatedSquares.Count < 3) return;

        List<GameSquare> pool = new List<GameSquare>(instantiatedSquares);
        
        int r1 = Random.Range(0, pool.Count);
        largeLitSquare = pool[r1];
        pool.RemoveAt(r1);

        int r2 = Random.Range(0, pool.Count);
        mediumLitSquare = pool[r2];
        pool.RemoveAt(r2);

        int r3 = Random.Range(0, pool.Count);
        smallLitSquare = pool[r3];

        largeLitSquare.SetLitSize(GameSquare.LitSize.Large, false);
        mediumLitSquare.SetLitSize(GameSquare.LitSize.Medium, false);
        smallLitSquare.SetLitSize(GameSquare.LitSize.Small, false);
    }

    private void OnSquareClicked(GameSquare sq)
    {
        if (!isGameActive || isGameplayTransitionLocked) return;

        bool isCorrect = isReverseActive ? (sq == smallLitSquare) : (sq == largeLitSquare);

        if (isCorrect)
        {
            TriggerScreenFlash(true);

            if (activeMode == GameMode.Campaign)
            {
                correctClicksRemaining--;
                UpdateRemainingUI();
                if (correctClicksRemaining <= 0)
                {
                    CampaignLevelCompleted();
                    return;
                }
            }
            else if (activeMode == GameMode.Timed)
            {
                scoreCount++;
                UpdateRemainingUI();
            }
            else if (activeMode == GameMode.Speed)
            {
                correctClicksRemaining--;
                UpdateRemainingUI();
                if (correctClicksRemaining <= 0)
                {
                    SpeedModeCompleted();
                    return;
                }
            }

            if (isReverseActive)
            {
                CycleLitSquaresReverse();
                if (feedbackController != null)
                {
                    feedbackController.PlayReverseCorrect(largeLitSquare);
                }
                reverseCorrectClicksRemaining--;

                if (reverseCorrectClicksRemaining <= 0)
                {
                    EndReverse();
                }

                return;
            }
        }
        else
        {
            TriggerScreenFlash(false);

            // A miss during Reverse does not advance the sequence.
            if (isReverseActive)
            {
                if (feedbackController != null) feedbackController.PlayReverseMistake();
                return;
            }
        }

        CycleLitSquares();

        if (isCorrect)
        {
            TryTriggerReverse();
        }
    }

    private void CycleLitSquares()
    {
        largeLitSquare.SetLitSize(GameSquare.LitSize.None, true);

        largeLitSquare = mediumLitSquare;
        largeLitSquare.SetLitSize(GameSquare.LitSize.Large, true);

        mediumLitSquare = smallLitSquare;
        mediumLitSquare.SetLitSize(GameSquare.LitSize.Medium, true);

        List<GameSquare> unlitSquares = new List<GameSquare>();
        foreach (var sq in instantiatedSquares)
        {
            if (sq != largeLitSquare && sq != mediumLitSquare)
            {
                unlitSquares.Add(sq);
            }
        }

        if (unlitSquares.Count > 0)
        {
            smallLitSquare = unlitSquares[Random.Range(0, unlitSquares.Count)];
            smallLitSquare.SetLitSize(GameSquare.LitSize.Small, true);
        }
        else
        {
            smallLitSquare = null;
        }
    }

    private void CycleLitSquaresReverse()
    {
        GameSquare clickedSmallSquare = smallLitSquare;
        clickedSmallSquare.SetLitSize(GameSquare.LitSize.None, true);

        smallLitSquare = mediumLitSquare;
        smallLitSquare.SetLitSize(GameSquare.LitSize.Small, true);

        mediumLitSquare = largeLitSquare;
        mediumLitSquare.SetLitSize(GameSquare.LitSize.Medium, true);

        List<GameSquare> candidates = new List<GameSquare>();
        foreach (var sq in instantiatedSquares)
        {
            if (sq != clickedSmallSquare && sq != smallLitSquare && sq != mediumLitSquare)
            {
                candidates.Add(sq);
            }
        }

        // Every supported grid has at least four cells, but retain a safe fallback.
        if (candidates.Count == 0) candidates.Add(clickedSmallSquare);

        largeLitSquare = candidates[Random.Range(0, candidates.Count)];
        largeLitSquare.SetLitSize(GameSquare.LitSize.Large, true);
    }

    private void TryTriggerReverse()
    {
        if (isReverseActive || activeLevel == null || !activeLevel.reverseEnabled || gameConfig == null) return;
        if (reverseCooldownRemaining > 0f) return;
        if (Random.value > gameConfig.reverseTriggerChancePerCorrectClick) return;

        int minClicks = Mathf.Max(1, gameConfig.reverseMinCorrectClicks);
        int maxClicks = Mathf.Max(minClicks, gameConfig.reverseMaxCorrectClicks);
        reverseCorrectClicksRemaining = Random.Range(minClicks, maxClicks + 1);
        isReverseActive = true;
        isGameplayTransitionLocked = true;

        if (feedbackController != null)
        {
            feedbackController.PlayReverseEntrance(smallLitSquare, () => isGameplayTransitionLocked = false);
        }
        else
        {
            isGameplayTransitionLocked = false;
        }
    }

    private void EndReverse()
    {
        isReverseActive = false;
        reverseCorrectClicksRemaining = 0;
        reverseCooldownRemaining = gameConfig != null ? Mathf.Max(0f, gameConfig.reverseCooldownSeconds) : 0f;
        isGameplayTransitionLocked = true;

        if (feedbackController != null)
        {
            feedbackController.PlayReverseExit(() => isGameplayTransitionLocked = false);
        }
        else
        {
            isGameplayTransitionLocked = false;
        }
    }

    private void ResetReverseState()
    {
        isReverseActive = false;
        reverseCorrectClicksRemaining = 0;
        reverseCooldownRemaining = 0f;
        isGameplayTransitionLocked = false;
        if (feedbackController != null) feedbackController.ResetImmediate();
    }

    private void TriggerScreenFlash(bool success)
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }
        flashCoroutine = StartCoroutine(AnimateFlash(success ? flashGreen : flashRed));
    }

    private IEnumerator AnimateFlash(Color flashColor)
    {
        float inDuration = 0.05f;
        float outDuration = 0.25f;
        float elapsed = 0f;

        flashOverlay.color = flashColor;

        while (elapsed < inDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / inDuration;
            flashOverlay.color = Color.Lerp(Color.clear, flashColor, t);
            yield return null;
        }

        flashOverlay.color = flashColor;
        elapsed = 0f;

        while (elapsed < outDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / outDuration;
            flashOverlay.color = Color.Lerp(flashColor, Color.clear, t);
            yield return null;
        }

        flashOverlay.color = Color.clear;
        flashCoroutine = null;
    }

    private void UpdateTimerUI()
    {
        if (activeMode == GameMode.Speed)
        {
            timerText.text = $"{levelTimer:F2}s";
            timerText.color = activeLevel.textPrimaryColor;
        }
        else
        {
            timerText.text = $"{Mathf.CeilToInt(levelTimer)}s";
            if (levelTimer <= 5f)
            {
                timerText.color = new Color(1f, 0.1f, 0.2f);
            }
            else
            {
                timerText.color = activeLevel.textPrimaryColor;
            }
        }
    }

    private void UpdateRemainingUI()
    {
        if (activeMode == GameMode.Campaign)
        {
            remainingText.text = $"{correctClicksRemaining} LEFT";
        }
        else if (activeMode == GameMode.Timed)
        {
            remainingText.text = $"SCORE: {scoreCount}";
        }
        else if (activeMode == GameMode.Speed)
        {
            remainingText.text = $"{correctClicksRemaining} LEFT";
        }
    }

    private void CampaignLevelCompleted()
    {
        isGameActive = false;
        isGameplayTransitionLocked = false;
        if (feedbackController != null) feedbackController.ResetImmediate();
        successPanel.SetActive(true);
        successLevelText.text = $"LEVEL {activeLevel.levelNumber} COMPLETE";

        // Save progress
        int currentSaved = GetSavedCampaignLevelIndex();
        int nextUnlockedLevel = Mathf.Min(currentLevelIndex + 1, levels.Count - 1);
        if (nextUnlockedLevel > currentSaved)
        {
            PlayerPrefs.SetInt("CampaignLevel", nextUnlockedLevel);
            PlayerPrefs.Save();
        }

        // Re-wire Next button dynamically
        successNextButton.onClick.RemoveAllListeners();
        successNextButton.GetComponentInChildren<TMP_Text>().text = "NEXT LEVEL";
        
        if (levels != null && currentLevelIndex < levels.Count - 1)
        {
            successNextButton.gameObject.SetActive(true);
            successNextButton.onClick.AddListener(() => StartLevel(currentLevelIndex + 1));
        }
        else
        {
            successNextButton.gameObject.SetActive(false); // Loop or end reached
        }
    }

    private void TimedModeCompleted()
    {
        isGameActive = false;
        isGameplayTransitionLocked = false;
        if (feedbackController != null) feedbackController.ResetImmediate();
        successPanel.SetActive(true);

        int best = PlayerPrefs.GetInt("TimedHighScore", 0);
        bool isNewBest = scoreCount > best;
        if (isNewBest)
        {
            PlayerPrefs.SetInt("TimedHighScore", scoreCount);
            PlayerPrefs.Save();
            best = scoreCount;
        }

        successLevelText.text = isNewBest ? $"NEW BEST SCORE!\nSCORE: {scoreCount}" : $"TIMED COMPLETED\nSCORE: {scoreCount}\nBEST: {best}";

        // Configure "Play Again" button instead of next level
        successNextButton.gameObject.SetActive(true);
        successNextButton.onClick.RemoveAllListeners();
        successNextButton.GetComponentInChildren<TMP_Text>().text = "PLAY AGAIN";
        successNextButton.onClick.AddListener(StartTimedMode);
    }

    private void SpeedModeCompleted()
    {
        isGameActive = false;
        isGameplayTransitionLocked = false;
        if (feedbackController != null) feedbackController.ResetImmediate();
        successPanel.SetActive(true);

        float best = PlayerPrefs.GetFloat("SpeedBestTime", 9999f);
        bool isNewBest = levelTimer < best;
        if (isNewBest)
        {
            PlayerPrefs.SetFloat("SpeedBestTime", levelTimer);
            PlayerPrefs.Save();
            best = levelTimer;
        }

        successLevelText.text = isNewBest ? $"NEW BEST TIME!\nTIME: {levelTimer:F2}s" : $"SPEED COMPLETED\nTIME: {levelTimer:F2}s\nBEST: {best:F2}s";

        // Configure "Play Again" button
        successNextButton.gameObject.SetActive(true);
        successNextButton.onClick.RemoveAllListeners();
        successNextButton.GetComponentInChildren<TMP_Text>().text = "PLAY AGAIN";
        successNextButton.onClick.AddListener(StartSpeedMode);
    }

    private void LevelFailed(string reason)
    {
        isGameActive = false;
        isGameplayTransitionLocked = false;
        if (feedbackController != null) feedbackController.ResetImmediate();
        failPanel.SetActive(true);
        failLevelText.text = "LEVEL FAILED";
        failReasonText.text = reason;
    }

    // Called from button bindings
    public void StartLevel(int levelIndex)
    {
        if (levels == null || levels.Count == 0)
        {
            Debug.LogError("Cannot start a campaign level because no levels are configured.");
            return;
        }

        activeMode = GameMode.Campaign;
        currentLevelIndex = Mathf.Clamp(levelIndex, 0, levels.Count - 1);
        StartActiveSetup();
    }

    public void RestartLevel()
    {
        if (activeMode == GameMode.Campaign) StartLevel(currentLevelIndex);
        else if (activeMode == GameMode.Timed) StartTimedMode();
        else if (activeMode == GameMode.Speed) StartSpeedMode();
    }

    private int GetSavedCampaignLevelIndex()
    {
        if (levels == null || levels.Count == 0) return 0;

        int savedIndex = PlayerPrefs.GetInt("CampaignLevel", 0);
        int validIndex = Mathf.Clamp(savedIndex, 0, levels.Count - 1);

        // Migrate stale progress left by older builds that allowed the value to
        // grow beyond the configured level list and then wrapped with modulo.
        if (savedIndex != validIndex)
        {
            PlayerPrefs.SetInt("CampaignLevel", validIndex);
            PlayerPrefs.Save();
        }

        return validIndex;
    }

    public void ReturnToMainMenu()
    {
        ShowMainMenu();
    }

    public void OpenSettings()
    {
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
    }
}
