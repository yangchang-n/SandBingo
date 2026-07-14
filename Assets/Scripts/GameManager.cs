using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    [Tooltip("목표 물리 시뮬레이션 프레임레이트 (권장: 60-240)")]
    public int targetPhysicsRate = 240;

    [Range(1, 10)]
    [Tooltip("한 단계 당 실행되는 시뮬레이션 반복 횟수 (권장: 2-4)")]
    public int simulationsPerStep = 2;

    private float physicsAccumulator = 0f;
    private float PhysicsTimestep => 1f / targetPhysicsRate;

    // Score System
    [Header("Score System")]
    public int stage1CurrentScore = 0;
    public int stage1TargetScore = 300;
    public int stage2CurrentScore = 0;
    public int stage2TargetScore = 300;
    public int stage3CurrentScore = 0;
    public int stage3TargetScore = 300;

    // 커스텀 스테이지(botDifficulty == 0)용 점수. 저장 파일에는 남기지 않는다
    public int customCurrentScore = 0;
    public int customTargetScore = 300;

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
    public bool isBotMode = true;
    // 0 = 커스텀 스테이지, 1~3 = 기존 Easy/Normal/Hard
    [Range(0, 3)]
    public int botDifficulty = 1;

    [Header("Game Over State")]
    public bool isGameOver = false;
    public bool isOasisWin = false;
    public bool isMudWin = false;

    [Header("Player Colors")]
    public Color skyColor = new Color(0x85 / 255f, 0xBE / 255f, 0xC9 / 255f);
    public Color brownColor = new Color(0x3C / 255f, 0x25 / 255f, 0x16 / 255f);

    [Header("Visual Effects")]
    public GameObject cellRemovalParticlePrefab;

    // 게이지 프레임 오브젝트 참조
    // 인스펙터에서 원하는 프레임 오브젝트를 넣었다 뺐다 하면서 확인하면 된다
    // SetupGauge에서 생성되는 SandGaugeRenderer에게 그대로 전달된다
    [Header("Frame Overlay")]
    public SpriteRenderer gaugeFrameRenderer;

    // 프레임 테두리가 실제로 보이길 원하는 두께이다. 값이 클수록 테두리가 두껍게 보인다
    // 직접 조정해서 확인한 값으로 고정했다. 더 이상 조정할 필요가 없어서 인스펙터에는 숨긴다
    [HideInInspector]
    public float gaugeFrameBorderThickness = 3f;

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
        if (GlobalManager.Instance != null)
        {
            isBotMode = true;
            botDifficulty = Mathf.Clamp(GlobalManager.Instance.pendingBotDifficulty, 0, 3);
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

        stage1CurrentScore = 0;
        stage2CurrentScore = 0;
        stage3CurrentScore = 0;
        customCurrentScore = 0;
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

        // 인스펙터에서 지정한 프레임 오브젝트와 테두리 두께 값을 새로 생성된 렌더러에게 그대로 전달한다
        sandGaugeRenderer.gaugeFrameRenderer = gaugeFrameRenderer;
        sandGaugeRenderer.gaugeFrameBorderThickness = gaugeFrameBorderThickness;

        sandGaugeRenderer.Initialize(sandSimulator.GetHeight());
    }

    void Update()
    {
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

        float settlementTimer = 0f;
        while (settlementTimer < 0.4f)
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

        if (!isGameOver)
        {
            SandSimulator.ScoreResult scoreResult = sandSimulator.CalculateScoreAndGetCells();

            if (scoreResult.cellsToRemove.Count > 0)
            {
                int currentScore = GetCurrentStageScore();
                int netScore = scoreResult.oasisScore - scoreResult.mudScore;
                int newScore = currentScore + netScore;
                SetCurrentStageScore(newScore);

                Debug.Log($"Score Update: {currentScore} + ({scoreResult.oasisScore} - {scoreResult.mudScore}) = {newScore}");

                SpawnScoreTexts(scoreResult.scoreLines);
                SpawnRemovalParticles(scoreResult.cellsToRemove);

                if (gameUI != null)
                {
                    gameUI.UpdateScoreDisplay();
                }

                yield return new WaitForSeconds(0.6f);

                sandSimulator.RemoveCells(scoreResult.cellsToRemove);

                CheckVictoryCondition();

                if (isGameOver)
                {
                    hasCheckedWinCondition = true;
                }

                StartCoroutine(WaitForSandSettlement());
                yield break;
            }
        }

        if (!hasCheckedWinCondition)
        {
            CheckVictoryCondition();
            hasCheckedWinCondition = true;
        }

        if (isGameOver)
        {
            yield break;
        }

        float postCheckTimer = 0f;
        while (postCheckTimer < 0.4f)
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

    void SpawnRemovalParticles(HashSet<Vector2Int> cellsToRemove)
    {
        if (cellRemovalParticlePrefab == null)
        {
            Debug.LogWarning("Cell removal particle prefab not assigned!");
            return;
        }

        int cellPixelSize = sandSimulator.GetCellPixelSize();
        int boardWidth = sandSimulator.GetWidth();
        int boardHeight = sandSimulator.GetHeight();

        foreach (Vector2Int cell in cellsToRemove)
        {
            int pixelCenterX = 1 + cell.x * cellPixelSize + cellPixelSize / 2;
            int pixelCenterY = 1 + cell.y * cellPixelSize + cellPixelSize / 2;

            float worldX = pixelCenterX - boardWidth / 2f;
            float worldY = pixelCenterY - boardHeight / 2f;
            Vector3 worldPos = new Vector3(worldX, worldY, -1f);

            GameObject particle = Instantiate(cellRemovalParticlePrefab, worldPos, Quaternion.identity);
            Destroy(particle, 1.5f);
        }

        Debug.Log($"Spawned {cellsToRemove.Count} removal particles");
    }

    void SpawnScoreTexts(List<SandSimulator.ScoreLine> scoreLines)
    {
        int cellPixelSize = sandSimulator.GetCellPixelSize();
        int boardWidth = sandSimulator.GetWidth();
        int boardHeight = sandSimulator.GetHeight();

        foreach (var line in scoreLines)
        {
            Vector2Int firstCell = line.cells[0];
            Vector2Int lastCell = line.cells[line.cells.Count - 1];

            int firstCenterX = 1 + firstCell.x * cellPixelSize + cellPixelSize / 2;
            int firstCenterY = 1 + firstCell.y * cellPixelSize + cellPixelSize / 2;
            int lastCenterX = 1 + lastCell.x * cellPixelSize + cellPixelSize / 2;
            int lastCenterY = 1 + lastCell.y * cellPixelSize + cellPixelSize / 2;

            float avgPixelX = (firstCenterX + lastCenterX) / 2f;
            float avgPixelY = (firstCenterY + lastCenterY) / 2f;

            float worldX = avgPixelX - boardWidth / 2f;
            float worldY = avgPixelY - boardHeight / 2f + cellPixelSize;

            Vector3 startPos = new Vector3(worldX, worldY, -1f);

            GameObject textObj = new GameObject("ScoreText");
            textObj.transform.position = startPos;

            TextMesh textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = line.score >= 0 ? $"+{line.score}" : $"{line.score}";
            textMesh.fontSize = 12;
            textMesh.characterSize = 100;
            textMesh.color = Color.white;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontStyle = FontStyle.Bold;

            MeshRenderer meshRenderer = textObj.GetComponent<MeshRenderer>();
            meshRenderer.sortingOrder = 10;

            textObj.transform.localScale = Vector3.one * 0.1f;

            StartCoroutine(AnimateScoreText(textObj, startPos, cellPixelSize));
        }
    }

    IEnumerator AnimateScoreText(GameObject textObj, Vector3 startPos, int cellPixelSize)
    {
        float duration = 1f;
        float elapsed = 0f;
        Vector3 endPos = startPos + new Vector3(0, cellPixelSize * 2, 0);
        TextMesh textMesh = textObj.GetComponent<TextMesh>();
        Color startColor = textMesh.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            textObj.transform.position = Vector3.Lerp(startPos, endPos, t);

            Color newColor = startColor;
            newColor.a = 1f - t;
            textMesh.color = newColor;

            yield return null;
        }

        Destroy(textObj);
    }

    void HandlePlayerInput()
    {
        if (isBotMode && currentPlayer == 1)
        {
            return;
        }

        // 옵션, 튜토리얼, 메뉴 패널이 열려 있는 동안에는 모래를 생성하지 않는다
        if (gameUI != null && gameUI.IsInputBlocked())
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

        if (currentScore >= targetScore)
        {
            isGameOver = true;
            isOasisWin = true;

            if (GlobalManager.Instance != null)
            {
                GlobalManager.Instance.CompleteStage(botDifficulty, currentScore);
            }

            ShowVictoryScreen();
            Debug.Log($"STAGE {botDifficulty} CLEARED! Final Score: {currentScore}");
        }
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

    // 게임 종료 후 씬 전환 - 페이드 효과 포함
    public void LeaveGameScene()
    {
        if (GlobalManager.Instance == null)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("SelectScene");
            return;
        }

        GlobalManager gm = GlobalManager.Instance;

        if (isOasisWin)
        {
            StoryChapter postChapter = botDifficulty switch
            {
                1 => gm.stage1PostChapter,
                2 => gm.stage2PostChapter,
                3 => gm.stage3PostChapter,
                _ => null
            };

            if (postChapter != null && !gm.IsStorySeen(postChapter))
            {
                gm.GoToStory(postChapter, "SelectScene");
                return;
            }
        }

        gm.LoadScene("SelectScene");
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

    public int GetCurrentStageScore()
    {
        return botDifficulty switch
        {
            0 => customCurrentScore,
            1 => stage1CurrentScore,
            2 => stage2CurrentScore,
            3 => stage3CurrentScore,
            _ => 0
        };
    }

    public int GetCurrentStageTargetScore()
    {
        return botDifficulty switch
        {
            0 => customTargetScore,
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
            case 0:
                customCurrentScore = score;
                break;
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

    // 난이도별 진흙 모양 정의 (높이, 개수). 너비는 항상 1칸이라 여기 포함하지 않는다
    // 0번(커스텀 스테이지)은 아직 진입점이 없어 자리표시자 값을 쓴다
    // 커스텀 스테이지 진입 로직이 생기면 이 case만 실제 사용자 지정값으로 교체하면 된다
    BotController.MudPattern GetMudPatternForDifficulty(int difficulty)
    {
        switch (difficulty)
        {
            case 0: return new BotController.MudPattern { heightCells = 1.0f, count = 2 };
            case 1: return new BotController.MudPattern { heightCells = 0.8f, count = 2 };
            case 2: return new BotController.MudPattern { heightCells = 1.2f, count = 2 };
            case 3: return new BotController.MudPattern { heightCells = 1.0f, count = 3 };
            default:
                Debug.LogWarning($"GetMudPatternForDifficulty: 정의되지 않은 난이도 {difficulty}, 기본값(1.0칸 2개)을 사용합니다");
                return new BotController.MudPattern { heightCells = 1.0f, count = 2 };
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

        botController.ExecuteBotTurn(GetMudPatternForDifficulty(botDifficulty));
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

        stage1CurrentScore = 0;
        stage2CurrentScore = 0;
        stage3CurrentScore = 0;
        customCurrentScore = 0;

        if (botController != null)
            botController.ClearOasisData();

        sandSimulator.ResetBoard();
        StartNewTurn();

        Debug.Log("Game Reset!");
    }

    public int GetCurrentPlayer() => currentPlayer;
}
