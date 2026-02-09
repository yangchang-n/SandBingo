using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GameManager : MonoBehaviour
{
    // Singleton
    public static GameManager Instance { get; private set; }

    // References
    private SandSimulator sandSimulator;
    private BotController botController;

    // Game Settings
    private int sandPerTurn;
    private const int SAND_SPAWN_RATE = 5;

    // Gauge Settings
    private int gaugeWidth;
    private int gaugeHeight;
    private float gaugeYOffset;

    // Game State
    private int currentPlayer;
    private int remainingSand;
    private bool isPlayerTurn;
    private bool waitingForMouseRelease;
    private bool waitingForBotTurn;

    [Header("Game Mode")]
    public bool isBotMode = false;

    [Header("Game Over State")]
    public bool isGameOver = false;
    public bool isOasisWin = false;
    public bool isMudWin = false;

    // Gauge Objects
    private GameObject gaugeObject;
    private SpriteRenderer gaugeRenderer;
    private Texture2D gaugeTexture;

    [Header("UI References")]
    public GameObject victoryPanel;
    public GameObject oasisWinText;
    public GameObject mudWinText;
    public Button resetButton;

    [Header("Player Colors")]
    public Color skyColor = new Color(0.4f, 0.85f, 0.95f);
    public Color brownColor = new Color(0.6f, 0.4f, 0.2f);

    [Header("Board Colors")]
    public Color boardBackgroundColor = new Color(0.85f, 0.75f, 0.6f);
    public Color clickableAreaColor = new Color(0.95f, 0.9f, 0.8f);
    public Color gridLineColor = Color.black;
    public Color wallColor = new Color(0.3f, 0.3f, 0.3f);

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
        InitializeSettings();
        FindSimulator();
        FindBotController();
        SetupCamera();
        SetupGauge();
        SetupUI();
        StartNewTurn();
    }

    void InitializeSettings()
    {
        sandPerTurn = 400;
        gaugeWidth = 200;
        gaugeHeight = 20;
        gaugeYOffset = 25f;

        currentPlayer = 0;
        isPlayerTurn = true;
        waitingForMouseRelease = false;
        waitingForBotTurn = false;

        isGameOver = false;
        isOasisWin = false;
        isMudWin = false;
    }

    void FindSimulator()
    {
        sandSimulator = FindObjectOfType<SandSimulator>();

        if (sandSimulator == null)
        {
            Debug.LogError("SandSimulator not found in scene!");
        }
    }

    void FindBotController()
    {
        botController = FindObjectOfType<BotController>();

        if (botController == null)
        {
            Debug.LogWarning("BotController not found. Bot mode will not work.");
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

    void SetupUI()
    {
        if (victoryPanel != null)
            victoryPanel.SetActive(false);

        if (oasisWinText != null)
            oasisWinText.SetActive(false);

        if (mudWinText != null)
            mudWinText.SetActive(false);

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetGame);
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
            return; // 봇 턴 대기 중

        HandleTurnTransition();
        HandlePlayerInput();
        UpdateSimulation();
    }

    void HandleTurnTransition()
    {
        if (waitingForMouseRelease && !Input.GetMouseButton(0))
        {
            waitingForMouseRelease = false;
            StartCoroutine(SwitchPlayerAfterDelay(1f));
        }
    }

    IEnumerator SwitchPlayerAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SwitchPlayer();
    }

    void HandlePlayerInput()
    {
        // 봇 모드이고 머드 팀(플레이어 1) 턴이면 입력 무시
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
        sandSimulator.SimulatePhysics();
        sandSimulator.SimulatePhysics();
        sandSimulator.UpdateTexture();

        CheckVictoryCondition();
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
        if (victoryPanel != null)
            victoryPanel.SetActive(true);

        if (isOasisWin && oasisWinText != null)
            oasisWinText.SetActive(true);

        if (isMudWin && mudWinText != null)
            mudWinText.SetActive(true);
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
            // 봇 모드이고 오아시스 턴이면 좌표 기록
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
        UpdateGauge();

        string playerName = currentPlayer == 0 ? "Sky (하늘색)" : "Brown (갈색)";
        Debug.Log($"Player {currentPlayer + 1}'s turn ({playerName})");

        // 봇 모드이고 머드 턴이면 봇 실행
        if (isBotMode && currentPlayer == 1 && botController != null)
        {
            StartCoroutine(ExecuteBotTurnAfterDelay(1f));
        }
    }

    IEnumerator ExecuteBotTurnAfterDelay(float delay)
    {
        waitingForBotTurn = true;
        isPlayerTurn = false;

        yield return new WaitForSeconds(delay);

        botController.ExecuteBotTurn();
        remainingSand = 0;
        UpdateGauge();

        yield return new WaitForSeconds(1f);

        waitingForBotTurn = false;
        SwitchPlayer();
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
        currentPlayer = 0;
        waitingForMouseRelease = false;
        waitingForBotTurn = false;
        isGameOver = false;
        isOasisWin = false;
        isMudWin = false;

        if (victoryPanel != null)
            victoryPanel.SetActive(false);

        if (oasisWinText != null)
            oasisWinText.SetActive(false);

        if (mudWinText != null)
            mudWinText.SetActive(false);

        if (botController != null)
            botController.ClearOasisData();

        sandSimulator.ResetBoard();
        StartNewTurn();

        Debug.Log("Game Reset!");
    }

    public int GetCurrentPlayer() => currentPlayer;
}