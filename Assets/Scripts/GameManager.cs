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
    private Camera mainCamera;

    // Game Settings
    private int sandPerTurn;

    // Physics Settings
    [Header("Physics Settings")]
    [Tooltip("시뮬레이션 갱신 빈도. 움직임의 부드러움만 결정하며 낙하 속도에는 영향이 없다")]
    [Range(30, 480)]
    public int targetPhysicsRate = 240;

    [Tooltip("모래가 초당 떨어지는 픽셀 수. 낙하 속도와 뿌리는 속도를 함께 결정하는 유일한 값이다")]
    [Range(60, 720)]
    public int sandFallSpeed = 360;

    // 생성 패턴의 가로 폭. SandSimulator.SpawnSand가 dx를 -1에서 1까지 채우므로 3이다
    private const int SPAWN_COLUMNS = 3;

    // 한 스텝에 채울 수 있는 최대 높이
    // SandSimulator.SPAWN_PATTERN_HEIGHT와 같은 값이어야 하며 한쪽만 바꾸면 안 된다
    private const int MAX_FALL_PER_STEP = 3;

    // 프레임당 허용할 최대 물리 스텝 수
    // 240Hz 기준 25ms까지 담을 수 있어 40fps 이상에서는 목표 속도가 유지된다
    // 값을 더 키우면 느린 기기에서 한 프레임의 처리량이 늘어
    // 수직동기화가 프레임레이트를 한 단계 더 떨어뜨리는 역효과가 생긴다
    private const int MAX_STEPS_PER_FRAME = 6;

    private float physicsAccumulator = 0f;

    // 스텝당 낙하 픽셀이 소수일 때 나머지를 다음 스텝으로 넘기는 누적값
    private float fallCarry = 0f;

    // 모래가 완전히 정착하면 false가 되어 보드 순회 자체를 멈춘다
    // 보드 내용을 바꾸는 쪽에서 WakeSimulation을 불러 다시 켜준다
    private bool isSimulationAwake = true;

    private float PhysicsTimestep => 1f / targetPhysicsRate;

    // 한 물리 스텝에서 떨어질 픽셀 수. 정수가 아닐 수 있어 실수로 둔다
    private float FallPerStep => (float)sandFallSpeed / targetPhysicsRate;

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

    // 이 씬에 들어온 뒤 한 번이라도 승리했는지를 기록한다
    // isOasisWin 은 리셋으로 지워지지만 이 값은 남아서, 리셋 후 나가더라도
    // 아직 보지 않은 post 스토리를 놓치지 않게 해준다
    private bool hasWonInThisSession = false;

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
        hasWonInThisSession = false;

        physicsAccumulator = 0f;
        fallCarry = 0f;
        isSimulationAwake = true;

        stage1CurrentScore = 0;
        stage2CurrentScore = 0;
        stage3CurrentScore = 0;
        customCurrentScore = 0;

        // 한 스텝의 낙하 거리가 생성 패턴 높이를 넘으면 그 차이만큼 모래 줄기가 끊긴다
        // 이 경우 낙하 거리가 강제로 잘려서 실제 속도가 설정값보다 느려지기도 한다
        if (FallPerStep > MAX_FALL_PER_STEP)
        {
            Debug.LogWarning($"Sand Fall Speed({sandFallSpeed})가 Target Physics Rate({targetPhysicsRate}) 대비 너무 큽니다. " +
                             $"{targetPhysicsRate * MAX_FALL_PER_STEP} 이하로 낮추세요.");
        }
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

        // 마우스 좌표 변환에 매 프레임 쓰이므로 참조를 한 번만 잡아둔다
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main Camera not found in scene!");
        }
    }

    void SetupCamera()
    {
        if (mainCamera == null) return;

        float boardHeight = sandSimulator.GetHeight();
        mainCamera.orthographicSize = boardHeight / 2f * 1.3f;
        mainCamera.transform.position = new Vector3(0, 15f, -10f);
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
        UpdatePhysicsWithFixedTimestep();
    }

    void UpdatePhysicsWithFixedTimestep()
    {
        physicsAccumulator += Time.deltaTime;

        float accumulatorLimit = PhysicsTimestep * MAX_STEPS_PER_FRAME;

        if (physicsAccumulator > accumulatorLimit)
        {
            physicsAccumulator = accumulatorLimit;

            // 개발 중에만 의미가 있는 경고이다
            // 릴리즈 빌드에서는 매 프레임 호출되어 로그 파일만 불리므로 컴파일 단계에서 제외한다
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"Physics accumulator clamped! FPS too low. Current FPS: {1f / Time.deltaTime:F1}");
#endif
        }

        // 마우스 위치와 패널 상태는 프레임당 한 번만 갱신되므로 생성 대상도 프레임당 한 번만 계산한다
        // 스텝마다 다시 계산하면 좌표 변환과 패널 검사만 중복될 뿐 결과는 같다
        bool isPouring = TryGetPourTarget(out Vector2Int pourCell);

        bool simulatedThisFrame = false;

        while (physicsAccumulator >= PhysicsTimestep)
        {
            simulatedThisFrame |= StepSimulation(isPouring, pourCell);
            physicsAccumulator -= PhysicsTimestep;
        }

        if (!simulatedThisFrame)
        {
            return;
        }

        // 화면은 프레임당 한 번만 갱신되므로 텍스처 업로드도 프레임당 한 번이면 충분하다
        sandSimulator.UpdateTexture();

        // 게이지 갱신은 텍스처 전체를 다시 쓰는 작업이라 스텝마다 부르면 안 된다
        if (isPouring)
        {
            UpdateGauge();
        }
    }

    // 물리 스텝 하나를 진행한다. 실제로 보드를 훑었으면 true를 반환한다
    // 뿌리기를 이 안에서 처리해야 생성 간격이 낙하 간격과 같은 박자를 갖는다
    bool StepSimulation(bool isPouring, Vector2Int pourCell)
    {
        fallCarry += FallPerStep;

        int fallPixels = Mathf.FloorToInt(fallCarry);

        if (fallPixels > MAX_FALL_PER_STEP)
        {
            // 생성 패턴 높이를 넘는 낙하는 어차피 채울 수 없으므로 잘라낸다
            // 이때 남은 이월값을 버리지 않으면 계속 쌓여서 상한에 눌러앉는다
            fallPixels = MAX_FALL_PER_STEP;
            fallCarry = 0f;
        }
        else
        {
            fallCarry -= fallPixels;
        }

        if (fallPixels <= 0)
        {
            return false;
        }

        // 채우는 높이를 떨어지는 거리와 같게 맞춰야 모래 줄기가 끊기지 않는다
        // 생성에 성공하면 PourSand 안에서 시뮬레이션이 깨어난다
        if (isPouring && remainingSand > 0)
        {
            PourSand(pourCell, fallPixels * SPAWN_COLUMNS);
        }

        if (!isSimulationAwake)
        {
            return false;
        }

        for (int i = 0; i < fallPixels; i++)
        {
            if (sandSimulator.SimulatePhysics())
            {
                isSandMoving = true;
            }
            else
            {
                // 한 번의 전체 순회에서 아무것도 움직이지 않았다면 보드는 완전히 정착한 상태이다
                // 같은 상태를 다시 훑어도 결과가 같으므로 다음 변화가 생길 때까지 멈춘다
                isSimulationAwake = false;
                break;
            }
        }

        return true;
    }

    // 코드가 보드 내용을 바꿨을 때 반드시 호출해야 한다
    // 빠뜨리면 새로 생긴 모래가 공중에 멈춘 채로 남는다
    void WakeSimulation()
    {
        isSimulationAwake = true;
    }

    // 이번 프레임에 플레이어가 모래를 쏟고 있는지 판정하고 대상 좌표를 돌려준다
    bool TryGetPourTarget(out Vector2Int cell)
    {
        cell = default;

        if (isGameOver) return false;
        if (!isPlayerTurn) return false;
        if (isBotMode && currentPlayer == 1) return false;
        if (remainingSand <= 0) return false;
        if (!Input.GetMouseButton(0)) return false;

        // 옵션, 튜토리얼, 메뉴, 스테이지 진입 패널이 열려 있는 동안에는 모래를 생성하지 않는다
        if (gameUI != null && gameUI.IsInputBlocked()) return false;

        if (mainCamera == null) return false;

        cell = WorldToGrid(mainCamera.ScreenToWorldPoint(Input.mousePosition));

        return sandSimulator.IsInClickableArea(cell.x, cell.y);
    }

    void PourSand(Vector2Int cell, int amount)
    {
        int spawnAmount = Mathf.Min(amount, remainingSand);
        int actualSpawned = sandSimulator.SpawnSand(cell.x, cell.y, GetCurrentPlayerSandType(), spawnAmount);

        if (actualSpawned <= 0)
        {
            return;
        }

        WakeSimulation();

        if (isBotMode && currentPlayer == 0 && botController != null)
        {
            botController.RecordOasisSandPosition(cell.x);
        }

        remainingSand -= actualSpawned;

        if (remainingSand <= 0)
        {
            EndTurn();
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

                // 칸이 비면 그 위의 모래가 다시 무너져야 하므로 시뮬레이션을 깨운다
                WakeSimulation();

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

    void CheckVictoryCondition()
    {
        if (isGameOver) return;

        int currentScore = GetCurrentStageScore();
        int targetScore = GetCurrentStageTargetScore();

        if (currentScore >= targetScore)
        {
            isGameOver = true;
            isOasisWin = true;
            hasWonInThisSession = true;

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

        // 리셋 여부와 무관하게, 이 씬에서 이겨본 적이 있고 아직 보지 않은 post 스토리가 있으면 그쪽으로 보낸다
        if (hasWonInThisSession)
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
    // public으로 열어둬서 스테이지 진입 정보 패널(GameSceneUI)도 같은 데이터를 그대로 가져다 쓴다
    public BotController.MudPattern GetMudPatternForDifficulty(int difficulty)
    {
        switch (difficulty)
        {
            case 0: return new BotController.MudPattern { heightCells = 1.0f, count = 2 };
            case 1: return new BotController.MudPattern { heightCells = 0.8f, count = 2 };
            case 2: return new BotController.MudPattern { heightCells = 1.0f, count = 2 };
            case 3: return new BotController.MudPattern { heightCells = 0.8f, count = 3 };
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

        // 봇이 진흙을 새로 떨어뜨렸으므로 시뮬레이션을 깨운다
        WakeSimulation();

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
        fallCarry = 0f;

        stage1CurrentScore = 0;
        stage2CurrentScore = 0;
        stage3CurrentScore = 0;
        customCurrentScore = 0;

        if (botController != null)
            botController.ClearOasisData();

        sandSimulator.ResetBoard();

        // 보드를 비웠으므로 시뮬레이션을 깨운다
        WakeSimulation();

        StartNewTurn();

        Debug.Log("Game Reset!");
    }

    public int GetCurrentPlayer() => currentPlayer;
}
