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

    // Game Settings
    private int sandPerTurn;
    private const int SAND_SPAWN_RATE = 10;

    // Gauge Settings
    private int gaugeWidth;
    private int gaugeHeight;
    private float gaugeYOffset;

    // 최적화: 게이지 캐싱
    private int lastRemainingSand = -1;

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

    // Gauge Objects
    private GameObject gaugeObject;
    private SpriteRenderer gaugeRenderer;
    private Texture2D gaugeTexture;

    [Header("Player Colors")]
    public Color skyColor = new Color(0x85 / 255f, 0xBE / 255f, 0xC9 / 255f);
    public Color brownColor = new Color(0x3C / 255f, 0x25 / 255f, 0x16 / 255f);

    [Header("Board Colors")]
    public Color boardBackgroundColor = new Color(0xDE / 255f, 0x9E / 255f, 0x4A / 255f);
    public Color clickableAreaColor = new Color(0xFF / 255f, 0xD7 / 255f, 0x98 / 255f);
    public Color gridLineColor = Color.black;
    public Color wallColor = new Color(0f, 0f, 0f, 0f);

    [Header("Gauge Colors")]
    public Color emptyGaugeColor = new Color(0.2f, 0.2f, 0.2f);
    public Color gaugeBorderColor = new Color(0.6f, 0.6f, 0.6f);

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

        Debug.Log($"Game loaded with BotMode: {isBotMode}, Difficulty: {botDifficulty}");
    }

    void InitializeSettings()
    {
        sandPerTurn = 800;
        gaugeWidth = 200;
        gaugeHeight = 20;
        gaugeYOffset = 25f;

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

        lastRemainingSand = -1;
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
        gaugeObject = new GameObject("SandGauge");
        gaugeRenderer = gaugeObject.AddComponent<SpriteRenderer>();
        gaugeRenderer.sortingOrder = 10;

        gaugeTexture = new Texture2D(gaugeWidth, gaugeHeight);
        gaugeTexture.filterMode = FilterMode.Point;

        Sprite gaugeSprite = Sprite.Create(
            gaugeTexture,
            new Rect(0, 0, gaugeWidth, gaugeHeight),
            new Vector2(0.5f, 0.5f),
            1f
        );
        gaugeRenderer.sprite = gaugeSprite;

        PositionGauge();
        UpdateGauge();
    }

    void PositionGauge()
    {
        float boardTop = sandSimulator.GetHeight() / 2f;
        gaugeObject.transform.position = new Vector3(0, boardTop + gaugeYOffset, 0);
    }

    void Update()
    {
        if (isGameOver)
            return;

        if (waitingForBotTurn)
            return;

        HandleTurnTransition();
        HandlePlayerInput();
        UpdateSimulation();
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

        // 1단계: 모래 안착 대기 (0.25초)
        float settlementTimer = 0f;
        while (settlementTimer < 0.25f)
        {
            if (isSandMoving)
            {
                settlementTimer = 0f;
                isSandMoving = false;
                Debug.Log("Sand moved - resetting settlement timer");
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

        // 2단계: 추가 대기 (0.25초)
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
            UpdateGaugeIfNeeded(); // 최적화: 필요시만 업데이트
        }
    }

    void UpdateSimulation()
    {
        // 최적화: 4번에서 2번으로 감소
        bool movedThisFrame = false;

        movedThisFrame |= sandSimulator.SimulatePhysics();
        movedThisFrame |= sandSimulator.SimulatePhysics();

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

        if (sandSimulator.SpawnSand(gridPos.x, gridPos.y, sandType, spawnAmount))
        {
            if (isBotMode && currentPlayer == 0 && botController != null)
            {
                botController.RecordOasisSandPosition(gridPos.x);
            }

            remainingSand -= spawnAmount;

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

    void UpdateGaugeIfNeeded()
    {
        // 최적화 5: 값이 실제로 바뀌었을 때만 업데이트
        if (remainingSand != lastRemainingSand)
        {
            UpdateGauge();
            lastRemainingSand = remainingSand;
        }
    }

    void UpdateGauge()
    {
        float fillRatio = (float)remainingSand / sandPerTurn;
        int fillWidth = Mathf.RoundToInt((gaugeWidth - 4) * fillRatio);
        Color currentColor = currentPlayer == 0 ? skyColor : brownColor;

        for (int x = 0; x < gaugeWidth; x++)
        {
            for (int y = 0; y < gaugeHeight; y++)
            {
                gaugeTexture.SetPixel(x, y, GetGaugePixelColor(x, y, fillWidth, currentColor));
            }
        }

        gaugeTexture.Apply();
    }

    Color GetGaugePixelColor(int x, int y, int fillWidth, Color fillColor)
    {
        if (x == 0 || x == gaugeWidth - 1 || y == 0 || y == gaugeHeight - 1)
        {
            return gaugeBorderColor;
        }

        if (x >= 2 && x < fillWidth + 2)
        {
            return fillColor;
        }

        return emptyGaugeColor;
    }

    void StartNewTurn()
    {
        remainingSand = sandPerTurn;
        isPlayerTurn = true;
        isSandMoving = false;
        hasCheckedWinCondition = false;
        isWaitingForSettlement = false;
        lastRemainingSand = -1; // 게이지 강제 업데이트
        UpdateGauge();

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
        lastRemainingSand = -1; // 게이지 강제 업데이트
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
        lastRemainingSand = -1; // 게이지 강제 업데이트
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
        lastRemainingSand = -1;

        if (botController != null)
            botController.ClearOasisData();

        sandSimulator.ResetBoard();
        StartNewTurn();

        Debug.Log("Game Reset!");
    }

    public int GetCurrentPlayer() => currentPlayer;
}