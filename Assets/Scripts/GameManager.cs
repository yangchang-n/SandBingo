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

    // Game State
    private int currentPlayer;
    private int remainingSand;
    private bool isPlayerTurn;
    private bool waitingForMouseRelease;
    private bool waitingForBotTurn;

    // Settlement Detection
    private bool isSandMoving = false;
    private bool hasCheckedWinCondition = false;
    private bool isWaitingForSettlement = false;

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

        int totalSimulations = targetPhysicsRate * simulationsPerStep;
        Debug.Log($"Physics: {targetPhysicsRate} FPS × {simulationsPerStep} sims = {totalSimulations} simulations/sec");
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

        Debug.Log($"Game loaded with BotMode: {isBotMode}, Difficulty: {botDifficulty}");
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
        isWaitingForSettlement = false;

        isGameOver = false;
        isOasisWin = false;
        isMudWin = false;

        physicsAccumulator = 0f;
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
        if (isGameOver)
            return;

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
        isWaitingForSettlement = true;
        isSandMoving = false;
        hasCheckedWinCondition = false;

        Debug.Log("Waiting for sand to settle...");

        float settlementTimer = 0f;
        while (settlementTimer < 0.25f)
        {
            if (isSandMoving)
            {
                settlementTimer = 0f;
                isSandMoving = false;
                // 디버그 로그 제거
            }
            else
            {
                settlementTimer += Time.deltaTime;
            }
            yield return null;
        }

        Debug.Log("Sand settled - checking win condition");

        CheckVictoryCondition();
        hasCheckedWinCondition = true;

        if (isGameOver)
        {
            isWaitingForSettlement = false;
            yield break;
        }

        float postCheckTimer = 0f;
        while (postCheckTimer < 0.25f)
        {
            if (isSandMoving)
            {
                Debug.Log("Sand moved after check - restarting settlement wait");
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

        Debug.Log("Settlement complete - switching player");
        isWaitingForSettlement = false;

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

        int winner = sandSimulator.CheckWinCondition();

        if (winner == 1)
        {
            isGameOver = true;
            isOasisWin = true;
            ShowVictoryScreen();
            Debug.Log("Oasis (Sky) Wins!");
        }
        else if (winner == 2)
        {
            isGameOver = true;
            isMudWin = true;
            ShowVictoryScreen();
            Debug.Log("Mud (Brown) Wins!");
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

    void StartNewTurn()
    {
        remainingSand = sandPerTurn;
        isPlayerTurn = true;
        isSandMoving = false;
        hasCheckedWinCondition = false;
        isWaitingForSettlement = false;

        Color currentColor = currentPlayer == 0 ? skyColor : brownColor;
        sandGaugeRenderer.ForceUpdate(remainingSand, sandPerTurn, currentColor);

        string playerName = currentPlayer == 0 ? "Sky (하늘색)" : "Brown (갈색)";
        Debug.Log($"Player {currentPlayer + 1}'s turn ({playerName})");

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

        Debug.Log("Turn ended. Release mouse to continue.");
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
        isWaitingForSettlement = false;
        isSandMoving = false;
        hasCheckedWinCondition = false;
        isGameOver = false;
        isOasisWin = false;
        isMudWin = false;
        physicsAccumulator = 0f;

        if (botController != null)
            botController.ClearOasisData();

        sandSimulator.ResetBoard();
        StartNewTurn();

        Debug.Log("Game Reset!");
    }

    public int GetCurrentPlayer() => currentPlayer;
}