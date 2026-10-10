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
    private BoardEffects boardEffects;

    // 모래 소리를 켜고 끄는 데 사용
    private bool isPouringThisFrame;
    private Camera mainCamera;

    // 한 턴에 생성할 수 있는 모래의 최대 양
    private const int SAND_PER_TURN = 800;

    // Physics Settings
    [Header("Physics Settings")]
    [Tooltip("시뮬레이션 갱신 빈도. 움직임의 부드러움만 결정하며 낙하 속도에는 영향이 없다")]
    [Range(30, 480)]
    public int targetPhysicsRate = 240;

    [Tooltip("모래가 초당 떨어지는 픽셀 수. 낙하 속도와 생성 속도를 같이 결정하는 값")]
    [Range(60, 720)]
    public int sandFallSpeed = 360;

    // 생성 패턴의 가로 폭과 한 스텝에 채울 수 있는 최대 높이. SandSimulator 의 생성 패턴을 그대로 따른다
    private const int SPAWN_COLUMNS = SandSimulator.SPAWN_PATTERN_WIDTH;
    private const int MAX_FALL_PER_STEP = SandSimulator.SPAWN_PATTERN_HEIGHT;

    // 프레임당 허용할 최대 물리 스텝 수
    // 240Hz 기준 25ms까지 담을 수 있어 40fps 이상에서는 목표 속도가 유지된다
    // 값을 더 키우면 느린 기기에서 한 프레임의 처리량이 늘어
    // 수직동기화가 프레임레이트를 한 단계 더 떨어뜨리는 역효과가 생긴다
    private const int MAX_STEPS_PER_FRAME = 6;

    private float physicsAccumulator = 0f;

    // 스텝당 낙하 픽셀이 소수일 때 나머지를 다음 스텝으로 넘기는 누적값
    private float fallCarry = 0f;

    // 모래가 완전히 정착하면 false 가 되어 보드 순회를 멈춘다
    // 보드 내용을 바꾸는 쪽에서 WakeSimulation 을 호출해서 다시 켠다
    private bool isSimulationAwake = true;

    private float PhysicsTimestep => 1f / targetPhysicsRate;

    // 한 물리 스텝에서 떨어질 픽셀 수. 정수가 아닐 수 있어 실수로 둔다
    private float FallPerStep => (float)sandFallSpeed / targetPhysicsRate;

    // 목표 점수. botDifficulty 0 은 커스텀 스테이지이다
    [Header("Score System")]
    public int stage1TargetScore = 300;
    public int stage2TargetScore = 350;
    public int stage3TargetScore = 400;
    public int customTargetScore = 300;

    // 스테이지의 현재 점수. 저장 파일에는 남기지 않는다
    private int currentScore;

    // Game State
    private int currentPlayer;
    private int remainingSand;
    private bool isPlayerTurn;
    private bool waitingForMouseRelease;

    // 모래 생성 입력은 이 라우터를 통해서만 읽는다
    private readonly PourInputRouter inputRouter = new PourInputRouter();

    // 턴을 끝낸 입력 출처. 턴 전환은 이 출처가 버튼을 뗄 때까지 기다린다
    // 다른 출처가 계속 누르고 있어도 턴 진행이 막히지 않게 하기 위함이다
    private PourInputSlot lastPouringSlot;
    private bool waitingForBotTurn;

    // Settlement Detection
    private bool isSandMoving = false;
    private bool hasCheckedWinCondition = false;

    [Header("Game Mode")]
    public bool isBotMode = true;
    // 셀렉트씬에서 진입하면 GlobalManager 값으로 덮어쓴다. 씬을 바로 재생해서 테스트할 때만 인스펙터에서 변경
    public bool isVersusMode = false;
    // 0 = 커스텀 스테이지, 1~3 = 기존 Easy/Normal/Hard
    [Range(0, 3)]
    public int botDifficulty = 1;

    // Versus 에서 호스트가 Oasis 진영인지 확인. 아니면 호스트가 Mud 진영이다
    // 봇전에서는 이 값과 관계없이 호스트가 항상 Oasis 이다
    private bool hostIsOasis = true;
    public bool HostIsOasis => !isVersusMode || hostIsOasis;

    // Versus 에서는 모래의 양이 첫 턴의 VERSUS_SAND_STEP 부터 턴마다 그만큼 늘어나서 SAND_PER_TURN 에서 멈춘다
    private const int VERSUS_SAND_STEP = 200;

    // 진행된 턴 수. Versus 의 턴별 모래 양 계산에 쓴다
    private int turnCount;

    // Steam 닉네임을 얻지 못할 때 쓰는 Versus 진영별 표시 이름
    private const string HOST_NAME = "HOST";
    private const string GUEST_NAME = "GUEST";

    [Header("Debug")]
    [Tooltip("에디터와 개발 빌드에서만 동작한다. G 키를 누르는 동안 마우스 위치에 게스트가 모래를 생성하는 것으로 처리")]
    public bool debugFakeGuest = false;

    // 커스텀 스테이지의 진흙 모양. 씬에 진입할 때 GlobalManager 에서 한 번만 가져온다
    // GlobalManager 없이 GameScene 을 단독 재생하면 이 기본값을 쓴다
    private BotController.MudPattern customMudPattern =
        new BotController.MudPattern { heightCells = 1.0f, count = 2 };

    private bool isGameOver;
    private bool isOasisWin;
    private bool isMudWin;

    [Header("Player Colors")]
    public Color skyColor = new Color(0x85 / 255f, 0xBE / 255f, 0xC9 / 255f);
    public Color brownColor = new Color(0x3C / 255f, 0x25 / 255f, 0x16 / 255f);

    [Header("Visual Effects")]
    public GameObject cellRemovalParticlePrefab;

    // 게이지 프레임 스프라이트. 실행 중에 생성되는 SandGaugeRenderer 에게 그대로 전달된다
    [Header("Frame Overlay")]
    public SpriteRenderer gaugeFrameRenderer;

    // 정착 판정. 모래가 이 시간 동안 움직이지 않으면 정착한 것으로 본다
    private const float SETTLEMENT_WAIT = 0.4f;

    // 줄 완성 연출을 보여준 뒤 칸을 지우기까지의 시간
    private const float SCORE_DISPLAY_DELAY = 0.6f;

    // 승부가 난 뒤 결과 패널을 띄우기까지의 시간
    private const float VICTORY_PANEL_DELAY = 0.8f;

    // 봇 차례가 시작된 뒤 진흙을 떨어뜨리기까지, 떨어뜨린 뒤 정착 판정을 시작하기까지의 시간
    private const float BOT_DROP_DELAY = 0.5f;
    private const float BOT_SETTLE_DELAY = 0.3f;

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
        ResetState();
        WarnIfFallTooFast();
        FindReferences();
        SetupInput();
        SetupCamera();
        SetupGauge();
        SetupEffects();
        StartNewTurn();

        // 커스텀 스테이지(0)와 Versus 는 스테이지 곡 중 무작위로 고른다
        AudioManager.PlayStageBgm(isVersusMode ? 0 : botDifficulty);

        // 진입만으로 해금되는 도전과제. 재시작은 Start 를 다시 호출하지 않으므로 진입할 때 한 번만 시도한다
        if (isVersusMode)
            SteamAchievements.Unlock(Achievement.VersusPlayed);
        else if (botDifficulty == 0)
            SteamAchievements.Unlock(Achievement.CustomStagePlayed);
    }

    void LoadGameSettings()
    {
        if (GlobalManager.Instance != null)
        {
            isVersusMode = GlobalManager.Instance.pendingVersus;
            isBotMode = !isVersusMode;
            hostIsOasis = GlobalManager.Instance.pendingVersusHostIsOasis;
            botDifficulty = Mathf.Clamp(GlobalManager.Instance.pendingBotDifficulty, 0, 3);

            // 진입 시점의 설정값을 한 번만 복사해둔다
            // 이후 ResetGame으로 재시도해도 난이도가 바뀌지 않는다
            customMudPattern = GlobalManager.Instance.GetCustomMudPattern();
        }
    }

    // 한 판의 진행 상태를 처음으로 되돌린다. 시작과 재시작이 함께 사용한다
    void ResetState()
    {
        turnCount = 0;
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
        fallCarry = 0f;
        isSimulationAwake = true;

        currentScore = 0;
    }

    void WarnIfFallTooFast()
    {
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

        // 마우스 좌표 변환에 매 프레임 사용하므로 참조를 한 번만 가져온다
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main Camera not found in scene!");
        }
    }

    // 모드별 모래 생성 규칙과 게스트 입력 출처를 연결한다
    // 개발용 가짜 게스트가 켜져 있으면 가짜 게스트를, 아니면 원격 플레이 권한 게스트를 연결한다
    // 원격 플레이 출처는 권한 게스트가 바뀌어도 같은 객체라서 한 번만 연결하면 된다
    void SetupInput()
    {
        PourInputMode mode = isVersusMode ? PourInputMode.Versus
                           : isBotMode ? PourInputMode.Bot
                           : PourInputMode.LocalShared;
        inputRouter.SetMode(mode, hostIsOasis);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugFakeGuest)
        {
            inputRouter.Guest.SetSource(new DebugGuestInputSource());
            return;
        }
#endif

        if (RemotePlaySessionManager.Instance != null)
            inputRouter.Guest.SetSource(RemotePlaySessionManager.Instance.GuestInput);
    }

    void SetupCamera()
    {
        if (mainCamera == null) return;

        float boardHeight = SandSimulator.HEIGHT;
        mainCamera.orthographicSize = boardHeight / 2f * 1.3f;
        mainCamera.transform.position = new Vector3(0, 15f, -10f);
    }

    void SetupEffects()
    {
        boardEffects = gameObject.AddComponent<BoardEffects>();
        boardEffects.Initialize(cellRemovalParticlePrefab);
    }

    void SetupGauge()
    {
        GameObject gaugeHolder = new GameObject("GaugeHolder");
        sandGaugeRenderer = gaugeHolder.AddComponent<SandGaugeRenderer>();

        sandGaugeRenderer.gaugeFrameRenderer = gaugeFrameRenderer;

        sandGaugeRenderer.Initialize(SandSimulator.HEIGHT);
    }

    void Update()
    {
        isPouringThisFrame = false;
        UpdateFrame();
        AudioManager.SetPouring(isPouringThisFrame);
    }

    void UpdateFrame()
    {
        // 입력 상태는 프레임마다 한 번만 갱신한다
        inputRouter.Tick(IsPourBlocked(), currentPlayer);

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
            // 릴리즈 빌드에서는 매 프레임 호출되어 로그 파일만 키우므로 컴파일 단계에서 제외한다
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"Physics accumulator clamped! FPS too low. Current FPS: {1f / Time.deltaTime:F1}");
#endif
        }

        // 마우스 위치와 패널 상태는 프레임당 한 번만 갱신되므로 생성 대상도 프레임당 한 번만 계산한다
        // 스텝마다 다시 계산하면 좌표 변환과 패널 검사만 중복될 뿐이고 결과는 같다
        bool isPouring = TryGetPourTarget(out Vector2Int pourCell);
        isPouringThisFrame = isPouring;

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

    // 물리 스텝 하나를 진행한다
    // 모래 생성을 여기서 처리해야 생성 간격이 낙하 간격과 같은 타이밍을 갖는다
    bool StepSimulation(bool isPouring, Vector2Int pourCell)
    {
        fallCarry += FallPerStep;

        int fallPixels = Mathf.FloorToInt(fallCarry);

        if (fallPixels > MAX_FALL_PER_STEP)
        {
            // 생성 패턴 높이를 넘는 낙하는 어차피 채울 수 없으므로 잘라낸다
            // 이때 남은 이월값을 버리지 않으면 계속 쌓여서 상한에 도달한다
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
        // 생성에 성공하면 PourSand 안에서 시뮬레이션이 재개된다
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
                // 같은 상태를 다시 확인해도 결과가 같으므로 다음 변화가 생길 때까지 멈춘다
                isSimulationAwake = false;
                break;
            }
        }

        return true;
    }

    // 코드가 보드 내용을 바꿨을 때 반드시 호출해야 한다
    // 빠뜨리면 새로 생긴 모래가 공중에 남게 된다
    void WakeSimulation()
    {
        isSimulationAwake = true;
    }

    // 모래를 생성하지 않아야 하는 상태인지 판정한다
    // 게임이 끝났거나 플레이어 차례가 아니거나 화면을 덮는 패널이 열려 있으면 막는다
    // 차례별로 어느 출처가 모래를 생성할 수 있는지는 PourInputRouter 가 따로 판정한다
    bool IsPourBlocked()
    {
        if (isGameOver || !isPlayerTurn) return true;

        return gameUI != null && gameUI.IsInputBlocked();
    }

    // 이번 프레임에 플레이어가 모래를 생성하고 있는지 판정하고 대상 좌표를 반환
    bool TryGetPourTarget(out Vector2Int cell)
    {
        cell = default;

        if (remainingSand <= 0) return false;
        if (mainCamera == null) return false;

        // 막힘, 차례, 새로 누름 여부는 Update 의 Tick 에서 이미 반영되어 있다
        if (!inputRouter.TryGetPouringSlot(out PourInputSlot slot)) return false;

        lastPouringSlot = slot;

        cell = SandSimulator.WorldToPixel(mainCamera.ScreenToWorldPoint(slot.Source.ScreenPosition));

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
        if (waitingForMouseRelease && (lastPouringSlot == null || !lastPouringSlot.IsPressed))
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
        while (settlementTimer < SETTLEMENT_WAIT)
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

            if (isVersusMode && scoreResult.cellsToRemove.Count > 0)
            {
                DeclareVersusWinner(scoreResult);
                yield break;
            }

            if (scoreResult.cellsToRemove.Count > 0)
            {
                int currentScore = GetCurrentStageScore();
                int netScore = scoreResult.oasisScore - scoreResult.mudScore;
                int newScore = currentScore + netScore;
                SetCurrentStageScore(newScore);

                Debug.Log($"Score Update: {currentScore} + ({scoreResult.oasisScore} - {scoreResult.mudScore}) = {newScore}");

                boardEffects.PlayLineClear(scoreResult);

                if (gameUI != null)
                {
                    gameUI.UpdateScoreDisplay();
                }

                yield return new WaitForSeconds(SCORE_DISPLAY_DELAY);

                sandSimulator.RemoveCells(scoreResult.cellsToRemove);

                // 칸이 비면 그 위의 모래가 다시 무너져야 하므로 시뮬레이션을 재개한다
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
        while (postCheckTimer < SETTLEMENT_WAIT)
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

    // Versus 는 한 줄을 먼저 완성한 진영이 즉시 이긴다
    // 점수, 칸 제거, 클리어 저장은 모두 건너뛰고 완성된 줄을 보드에 남겨둔다
    void DeclareVersusWinner(SandSimulator.ScoreResult scoreResult)
    {
        // 한 턴의 첫 완성은 해당 차례인 진영만 가능하다. 모래는 빈 공간으로만 움직여서 상대 칸의 소유권을 바꾸지 못한다
        bool oasisScored = scoreResult.oasisScore > 0;
        bool mudScored = scoreResult.mudScore > 0;
        isOasisWin = oasisScored && (!mudScored || currentPlayer == 0);

        isGameOver = true;
        isMudWin = !isOasisWin;

        boardEffects.StartVictoryLoop(scoreResult.cellsToRemove);
        ShowVictoryScreen();

        Debug.Log($"VERSUS - {(isOasisWin ? "Oasis" : "Mud")} wins");
    }

    // 점수로 승패를 가르는 봇전 전용 판정이다. Versus 는 DeclareVersusWinner 에서 처리한다
    void CheckVictoryCondition()
    {
        if (isGameOver || isVersusMode) return;

        int currentScore = GetCurrentStageScore();
        int targetScore = GetCurrentStageTargetScore();

        if (currentScore >= targetScore)
        {
            isGameOver = true;
            isOasisWin = true;

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

    // 승부가 난 순간 바로 띄우면 마지막 줄이 무너지거나 완성되는 모습을 보기 전에 화면이 가려지므로 잠시 뒤에 띄운다
    // 봇전 클리어 기록과 도전과제도 패널이 뜨는 이 시점에 남긴다
    // 기다리는 동안 재시작하거나 나가면 이 코루틴이 멈춰서 승리로 기록되지 않는다
    void ShowVictoryScreen()
    {
        StartCoroutine(ShowVictoryScreenAfterDelay());
    }

    IEnumerator ShowVictoryScreenAfterDelay()
    {
        yield return new WaitForSeconds(VICTORY_PANEL_DELAY);

        // 승부가 난 뒤에는 점수가 더 바뀌지 않으므로 판정 때와 같은 점수가 기록된다
        // 커스텀 스테이지(0)와 Versus 는 클리어 기록 대상이 아니다
        if (!isVersusMode && botDifficulty != 0 && isOasisWin && GlobalManager.Instance != null)
        {
            GlobalManager.Instance.CompleteStage(botDifficulty, GetCurrentStageScore());
        }

        if (gameUI != null)
        {
            gameUI.ShowVictoryScreen(isOasisWin);
        }
    }

    public void LeaveGameScene()
    {
        GlobalManager.LoadSceneSafe(SceneNames.Select);
    }

    SandSimulator.CellType GetCurrentPlayerSandType()
    {
        return currentPlayer == 0
            ? SandSimulator.CellType.SkySand
            : SandSimulator.CellType.BrownSand;
    }

    void UpdateGauge()
    {
        sandGaugeRenderer.UpdateGaugeIfNeeded(remainingSand, SAND_PER_TURN, GetPlayerColor(currentPlayer));
    }

    public int GetCurrentStageScore() => currentScore;

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

    void SetCurrentStageScore(int score) => currentScore = score;

    // 난이도별 진흙 모양 (높이, 개수). 0 은 커스텀 스테이지로, 진입할 때 받아둔 값이다
    // GameSceneUI 의 진입 패널과 정보 패널도 이 함수를 써서 실제 봇의 패턴과 항상 일치한다
    public BotController.MudPattern GetMudPatternForDifficulty(int difficulty)
    {
        switch (difficulty)
        {
            case 0: return customMudPattern;
            case 1: return new BotController.MudPattern { heightCells = 1.0f, count = 2 };
            case 2: return new BotController.MudPattern { heightCells = 0.8f, count = 3 };
            case 3: return new BotController.MudPattern { heightCells = 0.7f, count = 4 };
            default:
                Debug.LogWarning($"GetMudPatternForDifficulty: 정의되지 않은 난이도 {difficulty}, 기본값(1.0칸 2개)을 사용합니다");
                return new BotController.MudPattern { heightCells = 1.0f, count = 2 };
        }
    }

    void StartNewTurn()
    {
        lastPouringSlot = null;
        turnCount++;
        remainingSand = GetTurnSandAmount();
        isPlayerTurn = true;
        isSandMoving = false;
        hasCheckedWinCondition = false;

        // 게이지 분모는 항상 SAND_PER_TURN 이라 Versus 초반 턴은 일부만 찬 상태로 시작한다
        sandGaugeRenderer.ForceUpdate(remainingSand, SAND_PER_TURN, GetPlayerColor(currentPlayer));

        if (gameUI != null)
            gameUI.UpdateTurnDisplay();

        string playerName = currentPlayer == 0 ? "Oasis" : "Mud";
        Debug.Log($"{playerName} Turn - Stage {botDifficulty}");

        if (isBotMode && currentPlayer == 1 && botController != null)
        {
            StartCoroutine(ExecuteBotTurnAfterDelay(BOT_DROP_DELAY));
        }
    }

    IEnumerator ExecuteBotTurnAfterDelay(float delay)
    {
        waitingForBotTurn = true;
        isPlayerTurn = false;

        yield return new WaitForSeconds(delay);

        botController.ExecuteBotTurn(GetMudPatternForDifficulty(botDifficulty));

        // 봇이 진흙을 새로 떨어뜨렸으므로 시뮬레이션을 재개한다
        WakeSimulation();

        remainingSand = 0;
        UpdateGauge();

        yield return new WaitForSeconds(BOT_SETTLE_DELAY);

        // 진흙은 대기가 끝나고 시뮬레이션이 다시 돌아야 화면에 나타나므로 효과음도 여기서 재생
        AudioManager.Play(Sfx.MudDrop);

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
        // GameManager 의 코루틴과 BoardEffects 의 연출은 따로 멈춰야 한다
        StopAllCoroutines();
        boardEffects.StopAll();

        ResetState();

        if (botController != null)
            botController.ClearOasisData();

        sandSimulator.ResetBoard();

        // 보드를 비웠으므로 시뮬레이션을 재개한다
        WakeSimulation();

        StartNewTurn();

        Debug.Log("Game Reset!");
    }

    public int GetCurrentPlayer() => currentPlayer;

    // player 는 0 이 Oasis, 1 이 Mud 이다
    public Color GetPlayerColor(int player) => player == 0 ? skyColor : brownColor;

    int GetTurnSandAmount()
    {
        if (!isVersusMode) return SAND_PER_TURN;

        return Mathf.Min(SAND_PER_TURN, VERSUS_SAND_STEP * turnCount);
    }

    // player 는 0 이 Oasis, 1 이 Mud 이다
    public string GetPlayerName(int player)
    {
        bool isHost = (player == 0) == hostIsOasis;
        RemotePlaySessionManager session = RemotePlaySessionManager.Instance;

        if (session == null)
            return isHost ? HOST_NAME : GUEST_NAME;

        return isHost ? session.GetHostName(HOST_NAME) : session.GetActiveGuestName(GUEST_NAME);
    }
}
