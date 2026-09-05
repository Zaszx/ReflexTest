using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene-facing campaign controller. Persistent data and deterministic rules
/// live in the small testable classes under Scripts/Roguelite.
/// </summary>
public sealed class GameManager : MonoBehaviour
{
    private enum FlowState
    {
        Boot,
        MainMenu,
        LevelIntro,
        Playing,
        ReverseEntrance,
        ReverseExit,
        LevelComplete,
        RunSummary,
        Shop,
        Settings
    }

    public static GameManager Instance { get; private set; }

    [Header("Campaign Configuration")]
    public GameConfig gameConfig;
    [Tooltip("Optional explicit campaign. The generated Resources campaign is used when this is empty.")]
    public CampaignDefinition campaign;

    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject gameplayPanel;
    public GameObject successPanel;
    public GameObject failPanel;
    public GameObject settingsPanel;

    [Header("Main Menu")]
    public Button startContinueButton;
    public Button upgradesButton;
    public Button menuSettingsButton;

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

    [Header("Level Complete / Campaign Complete")]
    public TMP_Text successLevelText;
    public Button successNextButton;
    public Button successMenuButton;

    [Header("Run End Summary")]
    public TMP_Text failLevelText;
    public TMP_Text failReasonText;
    public Button failPrimaryButton;
    public Button failMenuButton;

    [Header("Settings")]
    public Button settingsCloseButton;
    public Slider sfxVolumeSlider;
    public Toggle hapticToggle;

    [Header("Asset References")]
    public Sprite solidSquareSprite;
    public Sprite outlineSquareSprite;

    private readonly List<GameSquare> instantiatedSquares = new List<GameSquare>();
    private SaveEnvelopeData saveData;
    private NeonSaveService saveService;
    private ActiveRunData sessionRun;
    private LevelData activeLevel;
    private DeterministicRandom random;
    private GameplayFeedbackController feedbackController;
    private RogueliteUIController rogueliteUI;
    private FlowState state = FlowState.Boot;
    private FlowState stateBeforeSettings = FlowState.MainMenu;
    private bool isDebugSession;
    private bool terminalRequested;
    private bool applicationSuspended;
    private bool applicationPauseSignal;
    private bool applicationFocusLost;
    private bool saveDirty;
    private float checkpointElapsed;
    private int presentationToken;

    private RectTransform boundsRoot;
    private RectTransform rotationScaleRoot;
    private RectTransform gridContentRoot;
    private float baseGridSide;
    private float currentGridScale = 1f;

    private Coroutine introCoroutine;
    private Coroutine flashCoroutine;
    private readonly Color flashGreen = new Color(0f, 1f, 0.4f, 0.8f);
    private readonly Color flashRed = new Color(1f, 0.1f, 0.2f, 0.8f);
    private readonly Color reservePink = new Color(1f, 0.08f, 0.48f, 1f);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private DebugLevelSelectController debugLevelSelectController;
#endif

    private bool SimulationIsActive =>
        state == FlowState.Playing && !applicationSuspended && sessionRun != null && activeLevel != null && !terminalRequested;

    private bool HasRealRun => saveData != null && saveData.activeRun != null;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            return;
        }

        Destroy(gameObject);
    }

    private void Start()
    {
        if (!InitializeConfiguration() || !ValidateRequiredSceneReferences())
        {
            enabled = false;
            return;
        }

        ConfigureStaticButtons();
        ConfigureFeedback();
        ConfigureRogueliteUI();
        ConfigurePersistence();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (gameConfig.enableDebugLevelSelect)
        {
            debugLevelSelectController = GetComponent<DebugLevelSelectController>();
            if (debugLevelSelectController == null)
                debugLevelSelectController = gameObject.AddComponent<DebugLevelSelectController>();
            debugLevelSelectController.Initialize(mainMenuPanel.GetComponent<RectTransform>(), campaign.levels, StartDebugLevel);
        }
#endif

        ShowMainMenu();
    }

    private bool InitializeConfiguration()
    {
        if (gameConfig == null)
        {
            Debug.LogError("Neon Reflex requires a GameConfig reference.");
            return false;
        }

        UpgradeCatalog.EnsureDefaults(gameConfig);
        if (campaign == null)
            campaign = Resources.Load<CampaignDefinition>("NeonReflexCampaign");

        if (campaign == null || campaign.LevelCount == 0)
        {
            Debug.LogError("Neon Reflex campaign is missing or empty. Generate the fixed campaign from the Neon Reflex editor menu.");
            return false;
        }

        CampaignValidationReport report = CampaignValidation.Validate(campaign, gameConfig.minimumTouchTargetPixels);
        for (int i = 0; i < report.errors.Count; i++)
            Debug.LogError("Campaign validation: " + report.errors[i]);
        for (int i = 0; i < report.warnings.Count; i++)
            Debug.LogWarning("Campaign validation: " + report.warnings[i]);
        return report.IsValid;
    }

    private bool ValidateRequiredSceneReferences()
    {
        bool valid = mainMenuPanel != null && gameplayPanel != null && successPanel != null && failPanel != null &&
                     settingsPanel != null && gridContainer != null && squarePrefab != null && levelText != null &&
                     timerText != null && remainingText != null && startContinueButton != null && upgradesButton != null &&
                     menuSettingsButton != null;
        if (!valid)
            Debug.LogError("GameManager has missing required scene references. Re-open SampleScene after its serialized migration.");
        return valid;
    }

    private void ConfigureStaticButtons()
    {
        BindButton(homeButton, ReturnToMainMenu);
        BindButton(settingsButton, OpenSettings);
        BindButton(settingsCloseButton, CloseSettings);
        BindButton(successMenuButton, ReturnToMainMenu);
        BindButton(failMenuButton, ReturnToMainMenu);

        if (flashOverlay != null)
        {
            flashOverlay.gameObject.SetActive(true);
            flashOverlay.type = Image.Type.Simple;
            flashOverlay.color = Color.clear;
            flashOverlay.raycastTarget = false;
        }
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void ConfigureFeedback()
    {
        feedbackController = GetComponent<GameplayFeedbackController>();
        if (feedbackController == null)
            feedbackController = gameObject.AddComponent<GameplayFeedbackController>();
        feedbackController.Initialize(gameplayPanel.GetComponent<RectTransform>());
    }

    private void ConfigureRogueliteUI()
    {
        rogueliteUI = GetComponent<RogueliteUIController>();
        if (rogueliteUI == null)
            rogueliteUI = gameObject.AddComponent<RogueliteUIController>();

        rogueliteUI.Initialize(
            mainMenuPanel.transform.parent as RectTransform,
            mainMenuPanel.GetComponent<RectTransform>(),
            gameplayPanel.GetComponent<RectTransform>(),
            settingsPanel.GetComponent<RectTransform>(),
            startContinueButton,
            upgradesButton,
            menuSettingsButton,
            hapticToggle,
            levelText,
            timerText,
            remainingText,
            homeButton,
            settingsButton,
            gridContainer,
            solidSquareSprite);
        rogueliteUI.BindCallbacks(
            StartOrContinueRun,
            ConfirmAbandonRun,
            OpenUpgradeShop,
            OpenSettings,
            OnShopClosed,
            SetHapticsEnabled,
            TryPurchaseUpgrade);
        rogueliteUI.SetHaptics(PlayerPrefs.GetInt("HapticsEnabled", 1) == 1);
    }

    private void ConfigurePersistence()
    {
        saveService = new NeonSaveService();
        saveData = saveService.Load(gameConfig) ?? SaveEnvelopeData.CreateDefault();
        saveData.saveVersion = gameConfig.saveVersion;
        if (saveData.profile == null)
            saveData.profile = PlayerProfileData.CreateDefault();
        saveData.profile.saveVersion = gameConfig.saveVersion;
        saveService.MigrateLegacyPlayerPrefs(saveData, gameConfig);
        ValidateSavedRunReference();
    }

    private void ValidateSavedRunReference()
    {
        ActiveRunData run = saveData.activeRun;
        if (run == null)
            return;

        int stableIdIndex = FindLevelIndex(run.currentLevelId);
        bool numericIndexValid = run.currentLevelIndex >= 0 && run.currentLevelIndex < campaign.LevelCount;
        if (stableIdIndex >= 0)
        {
            run.currentLevelIndex = stableIdIndex;
        }
        else if (numericIndexValid)
        {
            LevelData recovered = campaign.GetLevel(run.currentLevelIndex);
            run.currentLevelId = recovered.stableId;
            run.betweenLevels = true;
            run.levelState = new ActiveLevelStateData();
            Debug.LogWarning("Saved level ID no longer exists; kept run resources and will safely restart its clamped configured level.");
        }
        else
        {
            run.currentLevelIndex = Mathf.Clamp(run.currentLevelIndex, 0, campaign.LevelCount - 1);
            run.currentLevelId = campaign.GetLevel(run.currentLevelIndex).stableId;
            run.betweenLevels = true;
            run.levelState = new ActiveLevelStateData();
            Debug.LogWarning("Saved campaign position was invalid; kept run resources and clamped it to a configured level.");
        }

        run.currentHealth = Mathf.Clamp(run.currentHealth, 0, Mathf.Max(1, run.upgrades?.maxHealth ?? gameConfig.baseHealth));
        SaveRealRunCritical();
    }

    private int FindLevelIndex(string stableId)
    {
        if (string.IsNullOrEmpty(stableId))
            return -1;
        for (int i = 0; i < campaign.LevelCount; i++)
        {
            LevelData candidate = campaign.GetLevel(i);
            if (candidate != null && string.Equals(candidate.stableId, stableId, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    private void Update()
    {
        if (!SimulationIsActive)
            return;

        float deltaTime = Mathf.Max(0f, Time.deltaTime);
        float simulationDelta = CalculateAvailableGameplayDelta(deltaTime);
        GameplayTimerTransition timerTransition = GameplayTimerRules.Tick(sessionRun, deltaTime);

        AdvanceReverseCooldown(simulationDelta);
        AdvanceGridMotion(simulationDelta);
        UpdateGameplayUI();
        saveDirty = true;
        checkpointElapsed += simulationDelta;

        if (timerTransition == GameplayTimerTransition.EnteredReserve)
        {
            TriggerScreenFlash(false);
            TriggerHaptic();
            SaveRealRunCritical();
        }
        else if (timerTransition == GameplayTimerTransition.ReserveDepleted)
        {
            FailRun("RESERVE DEPLETED");
            return;
        }

        if (checkpointElapsed >= gameConfig.saveCheckpointIntervalSeconds)
            SaveRealRunCritical();
    }

    private float CalculateAvailableGameplayDelta(float requestedDelta)
    {
        if (sessionRun == null || sessionRun.levelState == null)
            return 0f;
        float available = sessionRun.levelState.reserveActive
            ? sessionRun.currentReserveSeconds
            : sessionRun.levelState.normalTimeRemaining + sessionRun.currentReserveSeconds;
        return Mathf.Min(Mathf.Max(0f, requestedDelta), Mathf.Max(0f, available));
    }

    private void AdvanceReverseCooldown(float deltaTime)
    {
        ActiveLevelStateData levelState = sessionRun.levelState;
        if (!levelState.reverseActive && levelState.reverseCooldownRemaining > 0f)
            levelState.reverseCooldownRemaining = Mathf.Max(0f, levelState.reverseCooldownRemaining - deltaTime);
    }

    private void StartOrContinueRun()
    {
        if (HasRealRun)
        {
            ContinueRun();
            return;
        }
        StartNewRun();
    }

    public void StartNewRun()
    {
        if (HasRealRun)
        {
            rogueliteUI.ShowAbandonConfirmation();
            return;
        }

        UpgradeSnapshotData snapshot = UpgradeCatalog.CaptureSnapshot(gameConfig, saveData.profile);
        Guid runGuid = Guid.NewGuid();
        int seed = BitConverter.ToInt32(runGuid.ToByteArray(), 0);
        if (seed == 0)
            seed = 1;
        random = new DeterministicRandom(seed);

        sessionRun = new ActiveRunData
        {
            runId = runGuid.ToString("N"),
            runSaveVersion = gameConfig.saveVersion,
            runSeed = seed,
            randomState = NeonSaveService.EncodeRandomState(random.State),
            currentLevelIndex = 0,
            currentLevelId = campaign.GetLevel(0).stableId,
            currentHealth = snapshot.maxHealth,
            currentReserveSeconds = snapshot.startingReserveSeconds,
            pendingCoins = 0L,
            levelsCompleted = 0,
            betweenLevels = false,
            upgrades = snapshot
        };
        saveData.activeRun = sessionRun;
        isDebugSession = false;
        terminalRequested = false;
        InitializeCurrentLevelState();
        SaveRealRunCritical();
        EnterCurrentLevel(true, false);
    }

    public void ContinueRun()
    {
        if (!HasRealRun)
        {
            ShowMainMenu();
            return;
        }

        sessionRun = saveData.activeRun;
        isDebugSession = false;
        terminalRequested = false;
        RestoreRandom();

        if (sessionRun.currentHealth <= 0)
        {
            FailRun("HEALTH DEPLETED");
            return;
        }
        if (sessionRun.levelState != null && sessionRun.levelState.reserveActive && sessionRun.currentReserveSeconds <= 0f)
        {
            FailRun("RESERVE DEPLETED");
            return;
        }

        if (sessionRun.betweenLevels)
        {
            InitializeCurrentLevelState();
            SaveRealRunCritical();
            EnterCurrentLevel(true, false);
        }
        else
        {
            EnterCurrentLevel(false, true);
        }
    }

    private void InitializeCurrentLevelState()
    {
        activeLevel = campaign.GetLevel(sessionRun.currentLevelIndex);
        if (activeLevel == null)
            throw new InvalidOperationException("Configured campaign level is missing at index " + sessionRun.currentLevelIndex + ".");

        RestoreRandom();
        int cellCount = Mathf.Max(2, activeLevel.gridSize);
        cellCount *= cellCount;
        if (!GridSequenceRules.Initialize(cellCount, ref random, out GridSequenceState sequence))
            sequence = new GridSequenceState(0, 1, 2);

        ActiveLevelStateData levelState = new ActiveLevelStateData
        {
            normalTimeRemaining = Mathf.Max(0.1f, activeLevel.timeLimit),
            reserveActive = false,
            objectiveProgress = 0,
            smallIndex = sequence.small,
            mediumIndex = sequence.medium,
            largeIndex = sequence.large,
            reverseActive = false,
            reverseCorrectTapsRemaining = 0,
            reverseCooldownRemaining = 0f,
            rotationAngle = 0f,
            scalePhase = activeLevel.scaleEnabled ? random.NextFloat01() : 0f,
            scaleDirection = random.Range(0, 2) == 0 ? -1 : 1,
            movementPosition = Vector2.zero,
            movementDirection = activeLevel.movementEnabled
                ? random.NextValidUnitDirection(gameConfig.minimumMovementAxisComponent)
                : Vector2.one.normalized,
            rewardGranted = false
        };

        sessionRun.currentLevelId = activeLevel.stableId;
        sessionRun.levelState = levelState;
        sessionRun.betweenLevels = false;
        StoreRandom();
    }

    private void EnterCurrentLevel(bool showIntroduction, bool restoring)
    {
        activeLevel = campaign.GetLevel(sessionRun.currentLevelIndex);
        if (activeLevel == null)
        {
            Debug.LogError("Cannot enter a missing campaign level.");
            ReturnToMainMenu();
            return;
        }

        presentationToken++;
        terminalRequested = false;
        feedbackController.ResetImmediate();
        feedbackController.SetPresentationPaused(applicationSuspended);
        ConfigureGameplayPanels();
        ConfigureGridHierarchyAndSize();
        BuildGrid();
        RepairOrRestoreSequence();
        ApplyGridMotionState(false, 0f);
        ApplyLevelTheme();
        UpdateGameplayUI();

        if (restoring && sessionRun.levelState.reverseActive)
            feedbackController.RestoreReverseActive();

        state = showIntroduction ? FlowState.LevelIntro : FlowState.Playing;
        if (showIntroduction)
        {
            if (introCoroutine != null)
                StopCoroutine(introCoroutine);
            int token = presentationToken;
            introCoroutine = StartCoroutine(LevelIntroductionRoutine(token));
        }
        SaveRealRunCritical();
    }

    private void ConfigureGameplayPanels()
    {
        rogueliteUI.HideShop();
        rogueliteUI.HideAbandonConfirmation();
        mainMenuPanel.SetActive(false);
        gameplayPanel.SetActive(true);
        successPanel.SetActive(false);
        failPanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    private void ApplyLevelTheme()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.backgroundColor = activeLevel.backgroundColor;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
        }
        Image gameplayBackground = gameplayPanel.GetComponent<Image>();
        if (gameplayBackground != null)
            gameplayBackground.color = activeLevel.backgroundColor;
        levelText.color = activeLevel.textPrimaryColor;
        remainingText.color = activeLevel.textPrimaryColor;
        rogueliteUI.ApplyGameplayTheme(activeLevel.textPrimaryColor, activeLevel.outlineColor);
    }

    private IEnumerator LevelIntroductionRoutine(int token)
    {
        const float duration = 0.45f;
        float elapsed = 0f;
        Vector3 originalScale = levelText.rectTransform.localScale;
        while (elapsed < duration && token == presentationToken)
        {
            if (!applicationSuspended && state != FlowState.Settings)
                elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            levelText.rectTransform.localScale = originalScale * Mathf.Lerp(1.18f, 1f, t);
            yield return null;
        }
        levelText.rectTransform.localScale = originalScale;
        introCoroutine = null;
        if (token == presentationToken && state == FlowState.LevelIntro)
            state = FlowState.Playing;
    }

    private void ConfigureGridHierarchyAndSize()
    {
        boundsRoot = gridContainer.parent as RectTransform;
        if (boundsRoot == null)
            throw new InvalidOperationException("GridContainer requires a RectTransform parent to define gameplay bounds.");

        AspectRatioFitter fitter = gridContainer.GetComponent<AspectRatioFitter>();
        if (fitter != null)
            fitter.enabled = false;
        GridLayoutGroup legacyLayout = gridContainer.GetComponent<GridLayoutGroup>();
        if (legacyLayout != null)
            Destroy(legacyLayout);

        gridContainer.anchorMin = new Vector2(0.5f, 0.5f);
        gridContainer.anchorMax = new Vector2(0.5f, 0.5f);
        gridContainer.pivot = new Vector2(0.5f, 0.5f);
        gridContainer.sizeDelta = Vector2.zero;
        Canvas.ForceUpdateCanvases();

        if (rotationScaleRoot == null)
        {
            rotationScaleRoot = CreateRectTransform("GridRotationScaleRoot", gridContainer);
            gridContentRoot = CreateRectTransform("GridContent", rotationScaleRoot);
            StretchToParent(gridContentRoot);
        }

        Vector2 boundsSize = boundsRoot.rect.size;
        float shortest = Mathf.Max(1f, Mathf.Min(boundsSize.x, boundsSize.y));
        float globalPadding = shortest * gameConfig.gameplayBoundsPaddingNormalized;
        float levelPadding = shortest * Mathf.Max(0f, activeLevel.movementTravelPaddingNormalized);
        float travel = activeLevel.movementEnabled ? shortest * gameConfig.movementTravelAllowanceNormalized : 0f;
        float maximumScale = activeLevel.scaleEnabled ? Mathf.Max(0.01f, activeLevel.maximumGridScale) : 1f;
        float worstRotation = Mathf.Approximately(activeLevel.rotateSpeed, 0f) ? 1f : Mathf.Sqrt(2f);
        float available = Mathf.Max(1f, shortest - 2f * (globalPadding + levelPadding + travel));
        baseGridSide = Mathf.Max(1f, available / (maximumScale * worstRotation));

        float targetCell = baseGridSide / Mathf.Max(2, activeLevel.gridSize);
        if (targetCell < gameConfig.minimumTouchTargetPixels)
        {
            Debug.LogWarning($"Level {activeLevel.levelNumber} touch target is approximately {targetCell:0}px, below the configured {gameConfig.minimumTouchTargetPixels:0}px target.");
        }
        rotationScaleRoot.sizeDelta = new Vector2(baseGridSide, baseGridSide);
        rotationScaleRoot.anchoredPosition = Vector2.zero;
    }

    private static RectTransform CreateRectTransform(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private void BuildGrid()
    {
        for (int i = 0; i < instantiatedSquares.Count; i++)
        {
            if (instantiatedSquares[i] != null)
                Destroy(instantiatedSquares[i].gameObject);
        }
        instantiatedSquares.Clear();

        int size = Mathf.Max(2, activeLevel.gridSize);
        float spacing = baseGridSide * 0.025f;
        float cellSize = Mathf.Max(1f, (baseGridSide - spacing * (size - 1)) / size);
        GridLayoutGroup layout = gridContentRoot.GetComponent<GridLayoutGroup>();
        if (layout == null)
            layout = gridContentRoot.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(cellSize, cellSize);
        layout.spacing = new Vector2(spacing, spacing);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = size;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.padding = new RectOffset(0, 0, 0, 0);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                GameObject cellObject = Instantiate(squarePrefab, gridContentRoot);
                cellObject.name = $"Square_{x}_{y}";
                GameSquare square = cellObject.GetComponent<GameSquare>();
                square.Setup(
                    x,
                    y,
                    solidSquareSprite,
                    outlineSquareSprite,
                    activeLevel.cellColor,
                    activeLevel.outlineColor,
                    activeLevel.smallScale,
                    activeLevel.mediumScale,
                    activeLevel.fullScale);
                square.onClicked = OnSquareClicked;
                instantiatedSquares.Add(square);
            }
        }
        Canvas.ForceUpdateCanvases();
    }

    private void RepairOrRestoreSequence()
    {
        ActiveLevelStateData levelState = sessionRun.levelState;
        GridSequenceState sequence = new GridSequenceState(levelState.smallIndex, levelState.mediumIndex, levelState.largeIndex);
        if (!GridSequenceRules.Validate(sequence, instantiatedSquares.Count))
        {
            RestoreRandom();
            GridSequenceRules.Initialize(instantiatedSquares.Count, ref random, out sequence);
            levelState.smallIndex = sequence.small;
            levelState.mediumIndex = sequence.medium;
            levelState.largeIndex = sequence.large;
            StoreRandom();
            Debug.LogWarning("Invalid saved target sequence was rebuilt deterministically.");
        }
        ApplySequenceVisuals(false);
    }

    private void ApplySequenceVisuals(bool animate)
    {
        for (int i = 0; i < instantiatedSquares.Count; i++)
            instantiatedSquares[i].SetLitSize(GameSquare.LitSize.None, animate);

        ActiveLevelStateData levelState = sessionRun.levelState;
        if (IsValidSquareIndex(levelState.smallIndex))
            instantiatedSquares[levelState.smallIndex].SetLitSize(GameSquare.LitSize.Small, animate);
        if (IsValidSquareIndex(levelState.mediumIndex))
            instantiatedSquares[levelState.mediumIndex].SetLitSize(GameSquare.LitSize.Medium, animate);
        if (IsValidSquareIndex(levelState.largeIndex))
            instantiatedSquares[levelState.largeIndex].SetLitSize(GameSquare.LitSize.Large, animate);
    }

    private bool IsValidSquareIndex(int index)
    {
        return index >= 0 && index < instantiatedSquares.Count;
    }

    private void OnSquareClicked(GameSquare square)
    {
        if (!SimulationIsActive || square == null)
            return;

        int clickedIndex = square.gridY * Mathf.Max(2, activeLevel.gridSize) + square.gridX;
        if (!IsValidSquareIndex(clickedIndex) || instantiatedSquares[clickedIndex] != square)
            return;

        ActiveLevelStateData levelState = sessionRun.levelState;
        int correctIndex = levelState.reverseActive ? levelState.smallIndex : levelState.largeIndex;
        if (clickedIndex != correctIndex)
        {
            HandleMistake();
            return;
        }

        HandleCorrectTap();
    }

    private void HandleMistake()
    {
        TriggerScreenFlash(false);
        sessionRun.currentHealth = Mathf.Max(0, sessionRun.currentHealth - 1);
        if (sessionRun.levelState.reverseActive)
            feedbackController.PlayReverseMistake();
        UpdateGameplayUI();
        SaveRealRunCritical();

        if (sessionRun.currentHealth <= 0)
            FailRun("HEALTH DEPLETED");
    }

    private void HandleCorrectTap()
    {
        TriggerScreenFlash(true);
        ActiveLevelStateData levelState = sessionRun.levelState;
        GridSequenceState sequence = new GridSequenceState(levelState.smallIndex, levelState.mediumIndex, levelState.largeIndex);
        RestoreRandom();

        bool reverseWasActive = levelState.reverseActive;
        bool advanced = reverseWasActive
            ? GridSequenceRules.AdvanceReverse(ref sequence, instantiatedSquares.Count, ref random)
            : GridSequenceRules.AdvanceNormal(ref sequence, instantiatedSquares.Count, ref random);
        if (!advanced)
        {
            Debug.LogError("Target sequence could not advance; input was ignored safely.");
            return;
        }

        levelState.smallIndex = sequence.small;
        levelState.mediumIndex = sequence.medium;
        levelState.largeIndex = sequence.large;
        levelState.objectiveProgress++;
        if (reverseWasActive)
            levelState.reverseCorrectTapsRemaining = Mathf.Max(0, levelState.reverseCorrectTapsRemaining - 1);
        StoreRandom();
        ApplySequenceVisuals(true);
        if (reverseWasActive && IsValidSquareIndex(levelState.largeIndex))
            feedbackController.PlayReverseCorrect(instantiatedSquares[levelState.largeIndex]);
        UpdateGameplayUI();

        if (levelState.objectiveProgress >= Mathf.Max(1, activeLevel.requiredCorrectClicks))
        {
            CompleteCurrentLevel();
            return;
        }

        if (reverseWasActive && levelState.reverseCorrectTapsRemaining <= 0)
            BeginReverseExit();
        else if (!reverseWasActive)
            TryBeginReverse();

        SaveRealRunCritical();
    }

    private void TryBeginReverse()
    {
        ActiveLevelStateData levelState = sessionRun.levelState;
        if (!activeLevel.reverseEnabled || levelState.reverseActive || levelState.reverseCooldownRemaining > 0f)
            return;

        RestoreRandom();
        float roll = random.NextFloat01();
        if (roll > Mathf.Clamp(gameConfig.reverseTriggerChancePerCorrectClick, 0.01f, 1f))
        {
            StoreRandom();
            return;
        }

        int minimum = Mathf.Max(1, gameConfig.reverseMinCorrectClicks);
        int maximum = Mathf.Max(minimum, gameConfig.reverseMaxCorrectClicks);
        levelState.reverseCorrectTapsRemaining = random.Range(minimum, maximum + 1);
        levelState.reverseActive = true;
        StoreRandom();
        state = FlowState.ReverseEntrance;
        int token = ++presentationToken;
        SaveRealRunCritical();

        feedbackController.PlayReverseEntrance(
            IsValidSquareIndex(levelState.smallIndex) ? instantiatedSquares[levelState.smallIndex] : null,
            () =>
            {
                if (token == presentationToken && state == FlowState.ReverseEntrance && !terminalRequested)
                    state = FlowState.Playing;
            });
    }

    private void BeginReverseExit()
    {
        ActiveLevelStateData levelState = sessionRun.levelState;
        levelState.reverseActive = false;
        levelState.reverseCorrectTapsRemaining = 0;
        levelState.reverseCooldownRemaining = Mathf.Max(
            0f,
            gameConfig.reverseCooldownSeconds + (sessionRun.upgrades?.reverseCooldownBonusSeconds ?? 0f));
        state = FlowState.ReverseExit;
        int token = ++presentationToken;
        SaveRealRunCritical();

        feedbackController.PlayReverseExit(() =>
        {
            if (token == presentationToken && state == FlowState.ReverseExit && !terminalRequested)
                state = FlowState.Playing;
        });
    }

    private void CompleteCurrentLevel()
    {
        if (terminalRequested || state != FlowState.Playing)
            return;
        state = FlowState.LevelComplete;
        terminalRequested = true;
        presentationToken++;
        feedbackController.ResetImmediate();

        if (isDebugSession)
        {
            ShowDebugLevelComplete();
            return;
        }

        ActiveLevelStateData completedState = sessionRun.levelState;
        if (!completedState.rewardGranted)
        {
            completedState.rewardGranted = true;
            sessionRun.pendingCoins = RunEconomyRules.SaturatingAdd(sessionRun.pendingCoins, Math.Max(0L, activeLevel.completionCoinReward));
            sessionRun.levelsCompleted = Math.Max(sessionRun.levelsCompleted, sessionRun.currentLevelIndex + 1);
        }

        int completedLevelNumber = activeLevel.levelNumber;
        if (sessionRun.currentLevelIndex >= campaign.LevelCount - 1)
        {
            EndRealRun("CAMPAIGN COMPLETE", true, Math.Max(0L, campaign.completionBonusCoins));
            return;
        }

        sessionRun.currentLevelIndex++;
        LevelData nextLevel = campaign.GetLevel(sessionRun.currentLevelIndex);
        sessionRun.currentLevelId = nextLevel.stableId;
        sessionRun.betweenLevels = true;
        SaveRealRunCritical();

        gameplayPanel.SetActive(false);
        successPanel.SetActive(true);
        successLevelText.text =
            $"LEVEL {completedLevelNumber} COMPLETE\n" +
            $"HP {sessionRun.currentHealth}/{sessionRun.upgrades.maxHealth}   RESERVE {sessionRun.currentReserveSeconds:0.0}s\n" +
            $"PENDING {sessionRun.pendingCoins:N0}\nNEXT: LEVEL {nextLevel.levelNumber}";
        ConfigureSuccessButton("NEXT LEVEL", StartNextLevelFromSummary, true);
    }

    private void StartNextLevelFromSummary()
    {
        if (!HasRealRun || !saveData.activeRun.betweenLevels)
        {
            ShowMainMenu();
            return;
        }
        sessionRun = saveData.activeRun;
        terminalRequested = false;
        InitializeCurrentLevelState();
        SaveRealRunCritical();
        EnterCurrentLevel(true, false);
    }

    private void ShowDebugLevelComplete()
    {
        gameplayPanel.SetActive(false);
        successPanel.SetActive(true);
        successLevelText.text = $"DEBUG LEVEL {activeLevel.levelNumber} COMPLETE\nNO SAVE OR REWARDS CHANGED";
        ConfigureSuccessButton("RETURN TO MENU", ReturnToMainMenu, true);
    }

    private void ConfigureSuccessButton(string label, UnityEngine.Events.UnityAction action, bool visible)
    {
        if (successNextButton == null)
            return;
        successNextButton.gameObject.SetActive(visible);
        BindButton(successNextButton, action);
        SetButtonText(successNextButton, label);
    }

    private void FailRun(string reason)
    {
        if (terminalRequested)
            return;

        if (isDebugSession)
        {
            terminalRequested = true;
            state = FlowState.RunSummary;
            presentationToken++;
            feedbackController.ResetImmediate();
            gameplayPanel.SetActive(false);
            failPanel.SetActive(true);
            failLevelText.text = "DEBUG RUN ENDED";
            failReasonText.text = reason + "\nNO SAVE OR REWARDS CHANGED";
            ConfigureFailPrimary("RETURN TO MENU", ReturnToMainMenu);
            return;
        }

        EndRealRun(reason, false, 0L);
    }

    private void ConfirmAbandonRun()
    {
        if (!HasRealRun)
        {
            ShowMainMenu();
            return;
        }

        sessionRun = saveData.activeRun;
        isDebugSession = false;
        terminalRequested = false;
        EndRealRun("RUN ABANDONED", false, 0L);
    }

    private void EndRealRun(string reason, bool campaignCompleted, long completionBonus)
    {
        if (terminalRequested && state == FlowState.RunSummary)
            return;
        if (sessionRun == null)
            sessionRun = saveData.activeRun;
        if (sessionRun == null)
            return;

        terminalRequested = true;
        state = FlowState.RunSummary;
        presentationToken++;
        feedbackController.ResetImmediate();
        SetSquareAnimationsPaused(false);

        if (!RunEconomyRules.TryBankAndClearActiveRun(
                saveData,
                sessionRun.runId,
                reason,
                campaignCompleted,
                completionBonus,
                campaign.LevelCount,
                out RunSummaryData summary))
        {
            Debug.LogError("Run terminal transaction was rejected because the active run identity changed.");
            return;
        }
        SaveEnvelopeCritical();
        sessionRun = null;
        activeLevel = null;
        isDebugSession = false;
        gameplayPanel.SetActive(false);

        if (campaignCompleted)
        {
            successPanel.SetActive(true);
            failPanel.SetActive(false);
            successLevelText.text =
                "CAMPAIGN COMPLETE\n" +
                $"LEVEL REWARDS {summary.runLevelRewards:N0}\n" +
                $"COMPLETION BONUS {summary.completionBonus:N0}\n" +
                $"TOTAL EARNED {summary.totalEarned:N0}\n" +
                $"NEW BALANCE {summary.newWalletBalance:N0}";
            ConfigureSuccessButton("UPGRADES", OpenUpgradeShop, true);
        }
        else
        {
            successPanel.SetActive(false);
            failPanel.SetActive(true);
            failLevelText.text = reason == "RUN ABANDONED" ? "RUN ENDED" : "RUN FAILED";
            failReasonText.text =
                $"{reason}\n" +
                $"HIGHEST LEVEL {summary.highestLevelEntered}\n" +
                $"LEVELS COMPLETED {summary.levelsCompleted}\n" +
                $"COINS EARNED {summary.totalEarned:N0}\n" +
                $"NEW BALANCE {summary.newWalletBalance:N0}";
            ConfigureFailPrimary("UPGRADES", OpenUpgradeShop);
        }
    }

    private void ConfigureFailPrimary(string label, UnityEngine.Events.UnityAction action)
    {
        if (failPrimaryButton == null)
            return;
        failPrimaryButton.gameObject.SetActive(true);
        BindButton(failPrimaryButton, action);
        SetButtonText(failPrimaryButton, label);
    }

    private static void SetButtonText(Button button, string text)
    {
        TMP_Text label = button == null ? null : button.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.text = text;
    }

    private void AdvanceGridMotion(float deltaTime)
    {
        if (sessionRun?.levelState == null || rotationScaleRoot == null || boundsRoot == null)
            return;

        ActiveLevelStateData levelState = sessionRun.levelState;
        float stabilizer = Mathf.Clamp(sessionRun.upgrades?.gridStabilizerMultiplier ?? 1f, 0.01f, 1f);
        if (!Mathf.Approximately(activeLevel.rotateSpeed, 0f))
        {
            levelState.rotationAngle = Mathf.Repeat(
                levelState.rotationAngle + activeLevel.rotateSpeed * stabilizer * deltaTime + 180f,
                360f) - 180f;
        }

        if (activeLevel.scaleEnabled)
        {
            currentGridScale = GridMotionMath.AdvanceTrianglePhase(
                ref levelState.scalePhase,
                ref levelState.scaleDirection,
                Mathf.Max(0.01f, activeLevel.minimumGridScale),
                Mathf.Max(activeLevel.minimumGridScale, activeLevel.maximumGridScale),
                Mathf.Max(0.05f, activeLevel.scaleCycleDuration * 0.5f),
                deltaTime * stabilizer);
        }
        else
        {
            currentGridScale = 1f;
        }

        ApplyGridMotionState(true, deltaTime);
    }

    private void ApplyGridMotionState(bool advanceMovement, float movementDelta)
    {
        if (sessionRun?.levelState == null || rotationScaleRoot == null || boundsRoot == null)
            return;

        ActiveLevelStateData levelState = sessionRun.levelState;
        if (!activeLevel.scaleEnabled)
            currentGridScale = 1f;
        else if (!advanceMovement)
            currentGridScale = Mathf.Lerp(
                Mathf.Max(0.01f, activeLevel.minimumGridScale),
                Mathf.Max(activeLevel.minimumGridScale, activeLevel.maximumGridScale),
                Mathf.Clamp01(levelState.scalePhase));

        rotationScaleRoot.localRotation = Quaternion.Euler(0f, 0f, levelState.rotationAngle);
        rotationScaleRoot.localScale = Vector3.one * currentGridScale;

        Vector2 boundsHalf = boundsRoot.rect.size * 0.5f;
        float shortest = Mathf.Max(1f, Mathf.Min(boundsRoot.rect.width, boundsRoot.rect.height));
        float padding = shortest * (gameConfig.gameplayBoundsPaddingNormalized + Mathf.Max(0f, activeLevel.movementTravelPaddingNormalized));
        boundsHalf = Vector2.Max(Vector2.zero, boundsHalf - Vector2.one * padding);
        Vector2 objectHalf = GridMotionMath.TransformedAabbHalfExtents(baseGridSide, currentGridScale, levelState.rotationAngle);
        Vector2 naturalRoom = Vector2.Max(Vector2.zero, boundsHalf - objectHalf);
        float configuredTravel = activeLevel.movementEnabled
            ? shortest * Mathf.Max(0.01f, gameConfig.movementTravelAllowanceNormalized)
            : 0f;
        Vector2 allowedRoom = Vector2.Min(naturalRoom, Vector2.one * configuredTravel);
        Vector2 movementBoundsHalf = objectHalf + allowedRoom;
        Vector2 position = levelState.movementPosition;
        Vector2 direction = levelState.movementDirection;

        if (advanceMovement && activeLevel.movementEnabled)
        {
            float speed = Mathf.Max(0f, activeLevel.movementSpeedNormalized) * shortest *
                          Mathf.Clamp(sessionRun.upgrades?.gridStabilizerMultiplier ?? 1f, 0.01f, 1f);
            GridMotionMath.AdvanceStableMovement(ref position, ref direction, speed, movementDelta, movementBoundsHalf, objectHalf);
        }
        else
        {
            position = GridMotionMath.ClampPosition(position, direction, movementBoundsHalf, objectHalf, out direction);
        }

        levelState.movementPosition = position;
        levelState.movementDirection = direction;
        gridContainer.anchoredPosition = position;
    }

    private void UpdateGameplayUI()
    {
        if (sessionRun == null || activeLevel == null || sessionRun.levelState == null)
            return;

        ActiveLevelStateData levelState = sessionRun.levelState;
        string prefix = isDebugSession ? "DEBUG " : string.Empty;
        levelText.text =
            $"<size=17><color=#8D99BC>{prefix}CAMPAIGN</color></size>\n" +
            $"<b>LEVEL {activeLevel.levelNumber}</b> <size=20><color=#667096>/ {campaign.LevelCount}</color></size>";
        if (levelState.reserveActive)
        {
            timerText.text =
                "<size=17><color=#C07B9D>RESERVE</color></size>\n" +
                $"<b>{sessionRun.currentReserveSeconds:0.0}</b><size=20>s</size>";
            timerText.color = reservePink;
        }
        else
        {
            timerText.text =
                "<size=17><color=#8D99BC>TIME</color></size>\n" +
                $"<b>{Mathf.CeilToInt(levelState.normalTimeRemaining)}</b><size=20>s</size>";
            timerText.color = levelState.normalTimeRemaining <= 5f
                ? new Color(1f, 0.1f, 0.2f, 1f)
                : activeLevel.textPrimaryColor;
        }

        int required = Mathf.Max(1, activeLevel.requiredCorrectClicks);
        remainingText.text =
            "<size=17><color=#8D99BC>OBJECTIVE</color></size>\n" +
            $"<b>{Mathf.Clamp(levelState.objectiveProgress, 0, required)} / {required}</b> " +
            $"<size=17>{(levelState.reverseActive ? "TAP SMALL" : "TAP LARGE")}</size>";
        remainingText.color = levelState.reverseActive ? reservePink : activeLevel.textPrimaryColor;
        rogueliteUI.RefreshHud(
            sessionRun.currentHealth,
            Mathf.Max(1, sessionRun.upgrades?.maxHealth ?? gameConfig.baseHealth),
            sessionRun.currentReserveSeconds,
            Mathf.Max(0f, sessionRun.upgrades?.startingReserveSeconds ?? gameConfig.baseStartingReserveSeconds),
            sessionRun.pendingCoins,
            levelState.objectiveProgress,
            required,
            levelState.normalTimeRemaining,
            activeLevel.timeLimit,
            levelState.reserveActive,
            levelState.reverseActive,
            isDebugSession);
    }

    public void ReturnToMainMenu()
    {
        presentationToken++;
        if (introCoroutine != null)
        {
            StopCoroutine(introCoroutine);
            introCoroutine = null;
        }

        if (!isDebugSession && sessionRun != null && saveData.activeRun == sessionRun)
            SaveRealRunCritical();
        sessionRun = null;
        activeLevel = null;
        isDebugSession = false;
        terminalRequested = false;
        feedbackController.ResetImmediate();
        SetSquareAnimationsPaused(false);
        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        state = FlowState.MainMenu;
        rogueliteUI.HideShop();
        rogueliteUI.HideAbandonConfirmation();
        mainMenuPanel.SetActive(true);
        gameplayPanel.SetActive(false);
        successPanel.SetActive(false);
        failPanel.SetActive(false);
        settingsPanel.SetActive(false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugLevelSelectController != null)
            debugLevelSelectController.Close();
#endif
        RefreshMainMenu();
    }

    private void RefreshMainMenu()
    {
        ActiveRunData run = saveData?.activeRun;
        int levelNumber = 1;
        if (run != null && campaign != null && run.currentLevelIndex >= 0 && run.currentLevelIndex < campaign.LevelCount)
            levelNumber = campaign.GetLevel(run.currentLevelIndex).levelNumber;
        rogueliteUI.RefreshMenu(
            run != null,
            levelNumber,
            saveData?.profile?.coins ?? 0L,
            run?.pendingCoins ?? 0L);
    }

    public void OpenUpgradeShop()
    {
        if (!isDebugSession && sessionRun != null && saveData.activeRun == sessionRun)
            SaveRealRunCritical();

        sessionRun = null;
        activeLevel = null;
        isDebugSession = false;
        terminalRequested = false;
        feedbackController.ResetImmediate();
        mainMenuPanel.SetActive(true);
        gameplayPanel.SetActive(false);
        successPanel.SetActive(false);
        failPanel.SetActive(false);
        settingsPanel.SetActive(false);
        state = FlowState.Shop;
        RefreshMainMenu();
        rogueliteUI.ShowShop(gameConfig, saveData.profile, HasRealRun);
    }

    private void OnShopClosed()
    {
        ShowMainMenu();
    }

    private bool TryPurchaseUpgrade(UpgradeId id)
    {
        if (!UpgradeCatalog.TryPurchase(gameConfig, saveData.profile, id, HasRealRun, out string reason))
        {
            if (!string.IsNullOrEmpty(reason))
                Debug.Log("Upgrade purchase rejected: " + reason);
            return false;
        }

        SaveEnvelopeCritical();
        RefreshMainMenu();
        rogueliteUI.RefreshShop(gameConfig, saveData.profile, HasRealRun);
        return true;
    }

    public void OpenSettings()
    {
        if (state == FlowState.Settings)
            return;
        stateBeforeSettings = state;
        state = FlowState.Settings;
        settingsPanel.SetActive(true);
        settingsPanel.transform.SetAsLastSibling();
        feedbackController.SetPresentationPaused(true);
        SetSquareAnimationsPaused(true);
        rogueliteUI.SetHaptics(PlayerPrefs.GetInt("HapticsEnabled", 1) == 1);
        SaveRealRunCritical();
    }

    public void CloseSettings()
    {
        if (state != FlowState.Settings)
            return;
        settingsPanel.SetActive(false);
        state = stateBeforeSettings;
        bool remainPaused = applicationSuspended;
        feedbackController.SetPresentationPaused(remainPaused);
        SetSquareAnimationsPaused(remainPaused);
    }

    private void SetHapticsEnabled(bool enabled)
    {
        PlayerPrefs.SetInt("HapticsEnabled", enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void SetSquareAnimationsPaused(bool paused)
    {
        for (int i = 0; i < instantiatedSquares.Count; i++)
        {
            if (instantiatedSquares[i] != null)
                instantiatedSquares[i].SetAnimationsPaused(paused);
        }
    }

    private void RestoreRandom()
    {
        uint fallback = sessionRun != null ? unchecked((uint)sessionRun.runSeed) : 1u;
        random = new DeterministicRandom(NeonSaveService.ParseRandomState(sessionRun?.randomState, fallback));
    }

    private void StoreRandom()
    {
        if (sessionRun != null)
            sessionRun.randomState = NeonSaveService.EncodeRandomState(random.State);
    }

    private void SaveRealRunCritical()
    {
        if (isDebugSession || saveData == null || saveData.activeRun == null)
            return;
        if (sessionRun != null)
        {
            StoreRandom();
            saveData.activeRun = sessionRun;
        }
        SaveEnvelopeCritical();
    }

    private void SaveEnvelopeCritical()
    {
        if (saveService == null || saveData == null)
            return;
        saveData.saveVersion = gameConfig.saveVersion;
        saveData.profile.saveVersion = gameConfig.saveVersion;
        try
        {
            saveService.Save(saveData, gameConfig);
            saveDirty = false;
            checkpointElapsed = 0f;
        }
        catch (Exception exception)
        {
            saveDirty = true;
            Debug.LogError("Neon Reflex save failed: " + exception.Message);
        }
    }

    private void TriggerScreenFlash(bool success)
    {
        if (flashOverlay == null)
            return;
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(AnimateFlash(success ? flashGreen : flashRed));
    }

    private IEnumerator AnimateFlash(Color flashColor)
    {
        const float inDuration = 0.05f;
        const float outDuration = 0.25f;
        float elapsed = 0f;
        while (elapsed < inDuration)
        {
            if (!applicationSuspended && state != FlowState.Settings)
                elapsed += Time.unscaledDeltaTime;
            flashOverlay.color = Color.Lerp(Color.clear, flashColor, Mathf.Clamp01(elapsed / inDuration));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < outDuration)
        {
            if (!applicationSuspended && state != FlowState.Settings)
                elapsed += Time.unscaledDeltaTime;
            flashOverlay.color = Color.Lerp(flashColor, Color.clear, Mathf.Clamp01(elapsed / outDuration));
            yield return null;
        }
        flashOverlay.color = Color.clear;
        flashCoroutine = null;
    }

    private static void TriggerHaptic()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (PlayerPrefs.GetInt("HapticsEnabled", 1) == 1)
            Handheld.Vibrate();
#endif
    }

    private void OnApplicationPause(bool paused)
    {
        applicationPauseSignal = paused;
        RefreshApplicationSuspension();
    }

    private void OnApplicationFocus(bool focused)
    {
        applicationFocusLost = !focused;
        RefreshApplicationSuspension();
    }

    private void RefreshApplicationSuspension()
    {
        bool suspended = applicationPauseSignal || applicationFocusLost;
        if (applicationSuspended == suspended)
            return;
        applicationSuspended = suspended;
        feedbackController?.SetPresentationPaused(suspended || state == FlowState.Settings);
        SetSquareAnimationsPaused(suspended || state == FlowState.Settings);
        if (suspended)
        {
            if (saveDirty)
                SaveEnvelopeCritical();
            else
                SaveRealRunCritical();
        }
    }

    private void OnApplicationQuit()
    {
        if (saveDirty)
            SaveEnvelopeCritical();
        else if (HasRealRun)
            SaveRealRunCritical();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void StartDebugLevel(int levelIndex)
    {
        if (campaign == null || campaign.LevelCount == 0)
            return;
        int safeIndex = Mathf.Clamp(levelIndex, 0, campaign.LevelCount - 1);
        UpgradeSnapshotData snapshot = UpgradeCatalog.CaptureSnapshot(gameConfig, saveData.profile);
        int seed = unchecked((int)0x4E524658) ^ (safeIndex + 1) * 397;
        random = new DeterministicRandom(seed);
        sessionRun = new ActiveRunData
        {
            runId = "debug-sandbox",
            runSaveVersion = gameConfig.saveVersion,
            runSeed = seed,
            randomState = NeonSaveService.EncodeRandomState(random.State),
            currentLevelIndex = safeIndex,
            currentLevelId = campaign.GetLevel(safeIndex).stableId,
            currentHealth = snapshot.maxHealth,
            currentReserveSeconds = snapshot.startingReserveSeconds,
            pendingCoins = 0L,
            levelsCompleted = 0,
            betweenLevels = false,
            upgrades = snapshot
        };
        isDebugSession = true;
        terminalRequested = false;
        InitializeCurrentLevelState();
        EnterCurrentLevel(true, false);
    }
#endif
}
