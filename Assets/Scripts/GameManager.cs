using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour
{
    // Singleton
    public static GameManager Instance { get; private set; }

    // References
    private SandSimulator sandSimulator;
    private BotController botController;
    private GameSceneUI gameUI;
    private SandGaugeRenderer sandGaugeRenderer;

    // Game Settings
    private int sandPerTurn;
    private const int SAND_SPAWN_RATE = 10;

    // Physics Settings
    [Header("Physics Settings")]
    [Tooltip("물리 시뮬레이션 목표 프레임레이트 (권장: 60-240)")]
    public int targetPhysicsRate = 240;

    [Range(1, 10)]
    [Tooltip("한 물리 스텝당 시뮬레이션 반복 횟수 (권장: 2-4)")]
    public int simulationsPerStep = 2;

    private float physicsAccumulator = 0f;
    private float PhysicsTimestep => 1f / targetPhysicsRate;

    // Score System
    [Header("Score System")]
    public int stage1CurrentScore = 0;
    public int stage1TargetScore = 300;  // 1000 → 300
    public int stage2CurrentScore = 0;
    public int stage2TargetScore = 300;  // 1000 → 300
    public int stage3CurrentScore = 0;
    public int stage3TargetScore = 300;  // 1000 → 300

    // Game State
    private int currentPlayer;
    private int remainingSand;
    private bool isPlayerTurn;
    private bool waitingForMouseRelease;
    private bool waitingForBotTurn;

    // Settlement Detection
    private bool isSandMoving = false;
    private bool hasCheckedWinCondition = false;

    [Header("Game Mode")]
    public bool isBotMode = false;
    [Range(1, 3)]
    public int botDifficulty = 1;

    [Header("Game Over State")]
    public bool isGameOver = false;
    public bool isOasisWin = false;
    public bool isMudWin = false;

    [Header("Player Colors")]
    public Color skyColor = new Color(0x85 / 255f, 0xBE / 255f, 0xC9 / 255f);
    public Color brownColor = new Color(0x3C / 255f, 0x25 / 255f, 0x16 / 255f);

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        LoadGameSettings();
        InitializeSettings();
        FindReferences();
        SetupCamera();
        SetupGauge();
        StartNewTurn();
    }

    void LoadGameSettings()
    {
        if (PlayerPrefs.HasKey("BotMode"))
        {
            isBotMode = PlayerPrefs.GetInt("BotMode") == 1;
        }

        if (PlayerPrefs.HasKey("BotDifficulty"))
        {
            botDifficulty = PlayerPrefs.GetInt("BotDifficulty");
        }
    }

    void InitializeSettings()
    {
        sandPerTurn = 800;
        currentPlayer = 0;
        isPlayerTurn = true;
        waitingForMouseRelease = false;
        waitingForBotTurn = false;

        isSandMoving = false;
        hasCheckedWinCondition = false;

        isGameOver = false;
        isOasisWin = false;
        isMudWin = false;

        physicsAccumulator = 0f;

        // 씬 진입 시 현재 점수 초기화
        stage1CurrentScore = 0;
        stage2CurrentScore = 0;
        stage3CurrentScore = 0;
    }

    void FindReferences()
    {
        sandSimulator = FindObjectOfType<SandSimulator>();
        if (sandSimulator == null)
        {
            Debug.LogError("SandSimulator not found in scene!");
        }

        botController = FindObjectOfType<BotController>();
        if (botController == null && isBotMode)
        {
            Debug.LogWarning("BotController not found. Bot mode will not work.");
        }

        gameUI = FindObjectOfType<GameSceneUI>();
        if (gameUI == null)
        {
            Debug.LogWarning("GameSceneUI not found in scene!");
        }
    }

    void SetupCamera()
    {
        float boardHeight = sandSimulator.GetHeight();
        Camera.main.orthographicSize = boardHeight / 2f * 1.3f;
        Camera.main.transform.position = new Vector3(0, 15f, -10f);
    }

    void SetupGauge()
    {
        GameObject gaugeHolder = new GameObject("GaugeHolder");
        sandGaugeRenderer = gaugeHolder.AddComponent<SandGaugeRenderer>();
        sandGaugeRenderer.Initialize(sandSimulator.GetHeight());
    }

    void Update()
    {
        // 승리 후에도 물리는 계속 (시각 효과)
        if (isGameOver)
        {
            UpdatePhysicsWithFixedTimestep();
            return;
        }

        if (waitingForBotTurn)
            return;

        HandleTurnTransition();
        HandlePlayerInput();
        UpdatePhysicsWithFixedTimestep();
    }

    void UpdatePhysicsWithFixedTimestep()
    {
        physicsAccumulator += Time.deltaTime;

        if (physicsAccumulator > PhysicsTimestep * 3)
        {
            physicsAccumulator = PhysicsTimestep * 3;
            Debug.LogWarning($"Physics accumulator clamped! FPS too low. Current FPS: {1f / Time.deltaTime:F1}");
        }

        while (physicsAccumulator >= PhysicsTimestep)
        {
            UpdateSimulation();
            physicsAccumulator -= PhysicsTimestep;
        }
    }

    void HandleTurnTransition()
    {
        if (waitingForMouseRelease && !Input.GetMouseButton(0))
        {
            waitingForMouseRelease = false;
            StartCoroutine(WaitForSandSettlement());
        }
    }

    IEnumerator WaitForSandSettlement()
    {
        isSandMoving = false;
        hasCheckedWinCondition = false;

        // 1단계: 첫 안착 대기 (0.5초)
        float settlementTimer = 0f;
        while (settlementTimer < 0.5f)
        {
            if (isSandMoving)
            {
                settlementTimer = 0f;
                isSandMoving = false;
            }
            else
            {
                settlementTimer += Time.deltaTime;
            }
            yield return null;
        }

        // 점수 계산 & 칸 제거 (승리 후에는 실행 안됨)
        if (!isGameOver)
        {
            SandSimulator.ScoreResult scoreResult = sandSimulator.CalculateScoreAndGetCells();

            if (scoreResult.cellsToRemove.Count > 0)
            {
                // 점수 가감
                int currentScore = GetCurrentStageScore();
                int netScore = scoreResult.oasisScore - scoreResult.mudScore;
                int newScore = currentScore + netScore;
                SetCurrentStageScore(newScore);

                Debug.Log($"Score Update: {currentScore} + ({scoreResult.oasisScore} - {scoreResult.mudScore}) = {newScore}");

                // 칸 제거
                sandSimulator.RemoveCells(scoreResult.cellsToRemove);

                // 승리 체크 (칸 제거 직후)
                CheckVictoryCondition();

                if (isGameOver)
                {
                    hasCheckedWinCondition = true;
                }

                // 모래가 떨어질 것임 → 다시 안착 대기 (연쇄)
                StartCoroutine(WaitForSandSettlement());
                yield break;
            }
        }

        // 더 이상 점수 변화 없음
        if (!hasCheckedWinCondition)
        {
            CheckVictoryCondition();
            hasCheckedWinCondition = true;
        }

        if (isGameOver)
        {
            yield break;
        }

        // 2단계: 추가 대기 (0.5초)
        float postCheckTimer = 0f;
        while (postCheckTimer < 0.5f)
        {
            if (isSandMoving)
            {
                isSandMoving = false;
                StartCoroutine(WaitForSandSettlement());
                yield break;
            }
            else
            {
                postCheckTimer += Time.deltaTime;
            }
            yield return null;
        }

        SwitchPlayer();
    }

    void HandlePlayerInput()
    {
        if (isBotMode && currentPlayer == 1)
        {
            return;
        }

        if (isPlayerTurn && Input.GetMouseButton(0) && remainingSand > 0)
        {
            SpawnSandAtMouse();
            UpdateGauge();
        }
    }

    void UpdateSimulation()
    {
        bool movedThisFrame = false;

        for (int i = 0; i < simulationsPerStep; i++)
        {
            movedThisFrame |= sandSimulator.SimulatePhysics();
        }

        if (movedThisFrame)
        {
            isSandMoving = true;
        }

        sandSimulator.UpdateTexture();
    }

    void CheckVictoryCondition()
    {
        if (isGameOver) return;

        int currentScore = GetCurrentStageScore();
        int targetScore = GetCurrentStageTargetScore();

        // 오아시스 승리: 목표 점수 도달
        if (currentScore >= targetScore)
        {
            isGameOver = true;
            isOasisWin = true;

            // GlobalManager에 클리어 전달 및 최고 점수 갱신
            if (GlobalManager.Instance != null)
            {
                GlobalManager.Instance.CompleteStage(botDifficulty, currentScore);
            }

            ShowVictoryScreen();
            Debug.Log($"STAGE {botDifficulty} CLEARED! Final Score: {currentScore}");
        }
        // 머드 승리: -목표 점수 이하
        else if (currentScore <= -targetScore)
        {
            isGameOver = true;
            isMudWin = true;
            ShowVictoryScreen();
            Debug.Log($"GAME OVER - Stage {botDifficulty} Failed. Score: {currentScore}");
        }
    }

    void ShowVictoryScreen()
    {
        if (gameUI != null)
        {
            gameUI.ShowVictoryScreen(isOasisWin);
        }
    }

    void SpawnSandAtMouse()
    {
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int gridPos = WorldToGrid(worldPos);

        if (!sandSimulator.IsInClickableArea(gridPos.x, gridPos.y))
        {
            return;
        }

        SandSimulator.CellType sandType = GetCurrentPlayerSandType();
        int spawnAmount = Mathf.Min(SAND_SPAWN_RATE, remainingSand);

        int actualSpawned = sandSimulator.SpawnSand(gridPos.x, gridPos.y, sandType, spawnAmount);

        if (actualSpawned > 0)
        {
            if (isBotMode && currentPlayer == 0 && botController != null)
            {
                botController.RecordOasisSandPosition(gridPos.x);
            }

            remainingSand -= actualSpawned;

            if (remainingSand <= 0)
            {
                EndTurn();
            }
        }
    }

    Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int width = sandSimulator.GetWidth();
        int height = sandSimulator.GetHeight();

        int gridX = Mathf.RoundToInt(worldPos.x + width / 2f);
        int gridY = Mathf.RoundToInt(worldPos.y + height / 2f);
        return new Vector2Int(gridX, gridY);
    }

    SandSimulator.CellType GetCurrentPlayerSandType()
    {
        return currentPlayer == 0
            ? SandSimulator.CellType.SkySand
            : SandSimulator.CellType.BrownSand;
    }

    void UpdateGauge()
    {
        Color currentColor = currentPlayer == 0 ? skyColor : brownColor;
        sandGaugeRenderer.UpdateGaugeIfNeeded(remainingSand, sandPerTurn, currentColor);
    }

    // 점수 헬퍼 메서드
    int GetCurrentStageScore()
    {
        return botDifficulty switch
        {
            1 => stage1CurrentScore,
            2 => stage2CurrentScore,
            3 => stage3CurrentScore,
            _ => 0
        };
    }

    int GetCurrentStageTargetScore()
    {
        return botDifficulty switch
        {
            1 => stage1TargetScore,
            2 => stage2TargetScore,
            3 => stage3TargetScore,
            _ => 1000
        };
    }

    void SetCurrentStageScore(int score)
    {
        switch (botDifficulty)
        {
            case 1:
                stage1CurrentScore = score;
                break;
            case 2:
                stage2CurrentScore = score;
                break;
            case 3:
                stage3CurrentScore = score;
                break;
        }
    }

    void StartNewTurn()
    {
        remainingSand = sandPerTurn;
        isPlayerTurn = true;
        isSandMoving = false;
        hasCheckedWinCondition = false;

        Color currentColor = currentPlayer == 0 ? skyColor : brownColor;
        sandGaugeRenderer.ForceUpdate(remainingSand, sandPerTurn, currentColor);

        string playerName = currentPlayer == 0 ? "Oasis" : "Mud";
        Debug.Log($"{playerName} Turn - Stage {botDifficulty}");

        if (isBotMode && currentPlayer == 1 && botController != null)
        {
            StartCoroutine(ExecuteBotTurnAfterDelay(0.5f));
        }
    }

    IEnumerator ExecuteBotTurnAfterDelay(float delay)
    {
        waitingForBotTurn = true;
        isPlayerTurn = false;

        yield return new WaitForSeconds(delay);

        botController.ExecuteBotTurn(botDifficulty);
        remainingSand = 0;
        UpdateGauge();

        yield return new WaitForSeconds(0.5f);

        waitingForBotTurn = false;

        StartCoroutine(WaitForSandSettlement());
    }

    void EndTurn()
    {
        isPlayerTurn = false;
        waitingForMouseRelease = true;
        remainingSand = 0;
        UpdateGauge();
    }

    void SwitchPlayer()
    {
        currentPlayer = 1 - currentPlayer;
        StartNewTurn();
    }

    public void ResetGame()
    {
        StopAllCoroutines();

        currentPlayer = 0;
        waitingForMouseRelease = false;
        waitingForBotTurn = false;
        isSandMoving = false;
        hasCheckedWinCondition = false;
        isGameOver = false;
        isOasisWin = false;
        isMudWin = false;
        physicsAccumulator = 0f;

        // 모든 스테이지 현재 점수 초기화
        stage1CurrentScore = 0;
        stage2CurrentScore = 0;
        stage3CurrentScore = 0;

        if (botController != null)
            botController.ClearOasisData();

        sandSimulator.ResetBoard();
        StartNewTurn();

        Debug.Log("Game Reset!");
    }

    public int GetCurrentPlayer() => currentPlayer;
}