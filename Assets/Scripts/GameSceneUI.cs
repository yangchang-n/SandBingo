using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class GameSceneUI : MonoBehaviour
{
    [Header("Menu Panel")]
    public GameObject menuPanel;
    public Button menuOpenButton;
    public Button menuCloseButton;
    public Button resetButton;
    public Button optionsButton;
    public Button mainMenuButton;
    public Button tutorialButton;

    [Header("Victory Panel")]
    public GameObject victoryPanel;
    public GameObject oasisWinText;
    public GameObject mudWinText;
    public Button victoryCloseButton;
    public Button continueButton;
    public Button retryButton;

    [Header("Score Display")]
    public Text scoreText;

    [Header("Mode Objects")]
    [Tooltip("Versus 에서 숨길 오브젝트. 점수 라벨과 값, 진흙 안내 문구, 진흙 모양 기준점, 진흙 높이 텍스트 등")]
    public GameObject[] botOnlyObjects;
    [Tooltip("봇전에서 숨길 오브젝트. 차례 이름 텍스트, 진입 패널의 이름 그룹 등")]
    public GameObject[] versusOnlyObjects;

    [Header("Info Panel")]
    [Tooltip("봇전은 진흙 블록, Versus 는 차례 색 정사각형. 씬에서 정한 너비를 기준으로 높이만 바뀐다")]
    public Image infoBlock;
    [Tooltip("봇전 진흙 높이와 개수. 메뉴 폰트")]
    public Text infoMudText;
    [Tooltip("Versus 현재 차례 이름. 다국어 폰트")]
    public Text infoTurnNameText;
    [Tooltip("Versus 승리 시 점수 자리에 켜지는 그룹")]
    public GameObject infoWinnerGroup;
    [Tooltip("Versus 승자 이름. 다국어 폰트")]
    public Text infoWinnerNameText;

    [Header("Versus Intro")]
    [Tooltip("진입 패널의 Oasis 쪽 이름. 다국어 폰트")]
    public Text introOasisNameText;
    [Tooltip("진입 패널의 Mud 쪽 이름. 다국어 폰트")]
    public Text introMudNameText;

    [Header("Versus Victory")]
    [Tooltip("승리 패널에서 Versus 승리 시에만 켜지는 그룹")]
    public GameObject versusWinGroup;
    [Tooltip("승리 패널의 승자 이름. 다국어 폰트")]
    public Text versusWinnerNameText;

    [Header("Backgrounds")]
    public GameObject easyBackground;
    public GameObject mediumBackground;
    public GameObject hardBackground;

    [Header("Tutorial")]
    public GameObject tutorialPanel;

    [Header("Stage Intro")]
    // 씬 페이드인이 끝난 직후(또는 튜토리얼이 끝난 직후) 잠깐 보였다 사라지는 패널
    // stageIntroPanel 루트에 CanvasGroup 이 있어야 페이드가 동작한다
    public GameObject stageIntroPanel;
    public Text stageNameText;
    // 진흙 모양을 배치할 기준점. 패널 위 빈 오브젝트의 RectTransform
    public RectTransform mudShapeAnchor;
    public float stageIntroDisplayDuration = 1.5f;
    public float stageIntroFadeDuration = 0.3f;
    // 아래 두 값은 직접 보면서 조정하는 기본값
    public float mudShapePixelsPerCell = 120f;
    public float mudShapeSpacing = 40f;

    private GameManager gameManager;
    private OptionsUI optionsUI;

    // 정보 패널 블록의 기준 너비. 시작할 때 씬에서 정한 값을 한 번 읽어둔다
    private float infoBlockWidth;

    private CanvasGroup stageIntroCanvasGroup;
    private bool isStageIntroActive = false;

    private TutorialOverlayUI tutorialUI;

    void Start()
    {
        gameManager = GameManager.Instance;
        optionsUI = FindObjectOfType<OptionsUI>();

        SetupBackgrounds();

        if (menuPanel != null)    menuPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (oasisWinText != null) oasisWinText.SetActive(false);
        if (mudWinText != null)   mudWinText.SetActive(false);
        if (versusWinGroup != null)  versusWinGroup.SetActive(false);
        if (infoWinnerGroup != null) infoWinnerGroup.SetActive(false);

        ApplyModeObjects();

        if (menuOpenButton != null)  menuOpenButton.onClick.AddListener(OpenMenu);
        if (menuCloseButton != null) menuCloseButton.onClick.AddListener(CloseMenu);
        if (resetButton != null)    resetButton.onClick.AddListener(OnResetClicked);
        if (optionsButton != null)  optionsButton.onClick.AddListener(OnOptionsClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnLeaveGameClicked);
        if (tutorialButton != null) tutorialButton.onClick.AddListener(OnTutorialButtonClicked);

        if (victoryCloseButton != null) victoryCloseButton.onClick.AddListener(CloseVictoryPanel);
        if (continueButton != null)     continueButton.onClick.AddListener(OnLeaveGameClicked);
        if (retryButton != null)        retryButton.onClick.AddListener(OnRetryClicked);

        SetupTutorial();
        UpdateScoreDisplay();
        SetupInfoPanel();
        SetupStageIntro();
    }

    bool IsVersus()
    {
        return gameManager != null && gameManager.isVersusMode;
    }

    // 모드에 맞지 않는 오브젝트를 끈다. 배치와 폰트는 씬에서 정하고 코드는 켜고 끄기만 함
    void ApplyModeObjects()
    {
        bool isVersus = IsVersus();

        SetObjectsActive(botOnlyObjects, !isVersus);
        SetObjectsActive(versusOnlyObjects, isVersus);
    }

    static void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null) return;

        foreach (GameObject obj in objects)
        {
            if (obj != null) obj.SetActive(active);
        }
    }

    // 봇전은 진흙 패턴을 한 번만 그리고 Versus 는 턴마다 UpdateTurnDisplay 로 갱신
    void SetupInfoPanel()
    {
        if (infoBlock != null)
            infoBlockWidth = infoBlock.rectTransform.rect.width;

        if (IsVersus())
        {
            UpdateTurnDisplay();
            return;
        }

        if (gameManager == null) return;

        BotController.MudPattern pattern = gameManager.GetMudPatternForDifficulty(gameManager.botDifficulty);

        SetInfoBlock(gameManager.GetPlayerColor(1), pattern.heightCells);

        // 지역 설정에 따라 소수점이 쉼표로 바뀌지 않도록 고정된 형식 사용
        if (infoMudText != null)
            infoMudText.text = pattern.heightCells.ToString("0.0", CultureInfo.InvariantCulture) + " x " + pattern.count;
    }

    // GameManager 가 턴을 시작할 때마다 호출한다. GameManager 가 먼저 시작되므로 첫 호출은 건너뛰고 SetupInfoPanel 이 대신 그린다
    public void UpdateTurnDisplay()
    {
        if (!IsVersus()) return;

        int player = gameManager.GetCurrentPlayer();

        SetInfoBlock(gameManager.GetPlayerColor(player), 1f);

        if (infoTurnNameText != null)
            infoTurnNameText.text = gameManager.GetPlayerName(player);

        // 새 턴이 시작됐으면 승부가 아직 나지 않은 것이므로 승자 표시를 지운다. Reset 직후에도 여기서 지워진다
        if (infoWinnerGroup != null)
            infoWinnerGroup.SetActive(false);
    }

    // 너비는 씬에서 정한 값을 유지하고 높이만 배율에 맞춤
    void SetInfoBlock(Color color, float heightRatio)
    {
        if (infoBlock == null) return;

        infoBlock.color = color;
        infoBlock.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, infoBlockWidth * heightRatio);
    }

    void OnDestroy()
    {
        if (GlobalManager.Instance != null)
            GlobalManager.Instance.OnSceneFadeInComplete -= HandleSceneFadeInComplete;
    }

    void SetupTutorial()
    {
        if (tutorialPanel == null) return;

        // 패널이 꺼져 있어도 GetComponent 는 동작하므로 여기서 한 번만 가져온다
        tutorialUI = tutorialPanel.GetComponent<TutorialOverlayUI>();

        GlobalManager gm = GlobalManager.Instance;

        bool shouldShow = gm != null
            && gameManager != null
            && !IsVersus()
            && gameManager.botDifficulty == 1
            && !gm.IsTutorialSeen();

        if (!shouldShow)
        {
            tutorialPanel.SetActive(false);
            return;
        }

        // 표시 컴포넌트가 없으면 클릭으로 닫는 처리도 없어서 패널을 닫을 수 없다
        if (tutorialUI == null)
        {
            Debug.LogWarning("TutorialOverlayUI not found on tutorialPanel!");
            tutorialPanel.SetActive(false);
            return;
        }

        tutorialUI.Show();
    }

    // 봇전은 난이도별 배경, 커스텀 스테이지와 Versus 는 스테이지 1 배경 사용
    void SetupBackgrounds()
    {
        if (easyBackground != null)   easyBackground.SetActive(false);
        if (mediumBackground != null) mediumBackground.SetActive(false);
        if (hardBackground != null)   hardBackground.SetActive(false);

        int stage = gameManager != null && gameManager.isBotMode ? gameManager.botDifficulty : 1;

        GameObject background = stage switch
        {
            0 => easyBackground,
            1 => easyBackground,
            2 => mediumBackground,
            3 => hardBackground,
            _ => null
        };

        if (background != null) background.SetActive(true);
    }

    void SetupStageIntro()
    {
        if (stageIntroPanel == null) return;

        stageIntroCanvasGroup = stageIntroPanel.GetComponent<CanvasGroup>();
        stageIntroPanel.SetActive(false);

        if (GlobalManager.Instance != null)
            GlobalManager.Instance.OnSceneFadeInComplete += HandleSceneFadeInComplete;
        else
            HandleSceneFadeInComplete(); // 에디터에서 GameScene 을 바로 재생하면 페이드 이벤트가 없으므로 즉시 시작
    }

    void HandleSceneFadeInComplete()
    {
        StartCoroutine(ShowStageIntroSequence());
    }

    // 튜토리얼이 자동으로 열려 있으면 닫힐 때까지 기다린 뒤 표시
    // 그 동안은 튜토리얼 패널이 입력을 막음
    IEnumerator ShowStageIntroSequence()
    {
        while (IsTutorialOpen())
            yield return null;

        if (gameManager == null) yield break;

        PopulateStageIntro();

        isStageIntroActive = true;
        stageIntroPanel.SetActive(true);

        if (stageIntroCanvasGroup != null)
            yield return StartCoroutine(FadeCanvasGroup(stageIntroCanvasGroup, 0f, 1f, stageIntroFadeDuration));

        yield return new WaitForSeconds(stageIntroDisplayDuration);

        if (stageIntroCanvasGroup != null)
            yield return StartCoroutine(FadeCanvasGroup(stageIntroCanvasGroup, 1f, 0f, stageIntroFadeDuration));

        stageIntroPanel.SetActive(false);
        isStageIntroActive = false;
    }

    IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        group.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        group.alpha = to;
    }

    // 진흙은 실제 봇의 패턴을 그대로 사용
    void PopulateStageIntro()
    {
        if (IsVersus())
        {
            if (stageNameText != null)      stageNameText.text = "VERSUS";
            if (introOasisNameText != null) introOasisNameText.text = gameManager.GetPlayerName(0);
            if (introMudNameText != null)   introMudNameText.text = gameManager.GetPlayerName(1);
            return;
        }

        if (stageNameText != null)
            stageNameText.text = gameManager.botDifficulty == 0 ? "CUSTOM" : $"STAGE {gameManager.botDifficulty}";

        if (mudShapeAnchor == null) return;

        // 에디터에서 미리 넣어둔 참고용 자식이 있을 수 있어서 먼저 정리
        for (int i = mudShapeAnchor.childCount - 1; i >= 0; i--)
            Destroy(mudShapeAnchor.GetChild(i).gameObject);

        BotController.MudPattern pattern = gameManager.GetMudPatternForDifficulty(gameManager.botDifficulty);

        float chunkWidth = mudShapePixelsPerCell;
        float chunkHeight = pattern.heightCells * mudShapePixelsPerCell;
        float totalWidth = pattern.count * chunkWidth + (pattern.count - 1) * mudShapeSpacing;
        float startX = -totalWidth / 2f + chunkWidth / 2f;

        for (int i = 0; i < pattern.count; i++)
        {
            GameObject chunk = new GameObject("MudChunk");
            chunk.transform.SetParent(mudShapeAnchor, false);

            RectTransform rect = chunk.AddComponent<RectTransform>();
            // 앵커를 mudShapeAnchor 의 중심에 고정해야 anchoredPosition 이 그 중심 기준으로 계산된다
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(chunkWidth, chunkHeight);
            rect.anchoredPosition = new Vector2(startX + i * (chunkWidth + mudShapeSpacing), 0f);

            Image image = chunk.AddComponent<Image>();
            image.color = gameManager.GetPlayerColor(1);
        }
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // 게스트 이탈 패널은 확인 버튼으로만 닫힘
        if (GuestLeftPanel.IsOpen) return;

        // 튜토리얼은 클릭으로만 닫고 스테이지 진입 연출은 곧 사라지므로 무시
        if (isStageIntroActive || IsTutorialOpen()) return;

        if (optionsUI != null && optionsUI.HandleEscapeKey()) return;

        if (victoryPanel != null && victoryPanel.activeSelf)
        {
            CloseVictoryPanel();
            return;
        }

        ToggleMenu();
    }

    public void UpdateScoreDisplay()
    {
        if (scoreText == null || gameManager == null) return;

        scoreText.text = $"{gameManager.GetCurrentStageScore()} / {gameManager.GetCurrentStageTargetScore()}";
    }

    // 메뉴가 열려 있는지는 패널의 활성 상태로만 판단한다
    // 소리는 사용자가 메뉴를 직접 열고 닫을 때만 재생. 다른 동작과 함께 닫힐 때는 HideMenu 로 재생 없이 닫는다
    void OpenMenu()
    {
        if (menuPanel == null || menuPanel.activeSelf) return;

        menuPanel.SetActive(true);
        AudioManager.Play(Sfx.HardPanelOpen);
    }

    void CloseMenu()
    {
        if (menuPanel == null || !menuPanel.activeSelf) return;

        menuPanel.SetActive(false);
        AudioManager.Play(Sfx.HardPanelClose);
    }

    void ToggleMenu()
    {
        if (menuPanel == null) return;

        if (menuPanel.activeSelf)
            CloseMenu();
        else
            OpenMenu();
    }

    void HideMenu()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
    }

    // 승리 패널의 닫기 버튼과 ESC 용. 재시작과 함께 닫힐 때는 HideVictoryPanel 로 효과음 없이 닫는다
    void CloseVictoryPanel()
    {
        HideVictoryPanel();
        AudioManager.Play(Sfx.HardPanelClose);
    }

    // 승리 패널을 닫고 보드를 볼 때도 승자를 알 수 있도록 정보 패널의 승자 표시는 남겨둔다
    void HideVictoryPanel()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (oasisWinText != null) oasisWinText.SetActive(false);
        if (mudWinText != null)   mudWinText.SetActive(false);
        if (versusWinGroup != null) versusWinGroup.SetActive(false);
    }

    // 패널이 열리는 소리 대신 승리음이나 패배음만 재생. Versus 는 누가 이기든 승리음을 재생한다
    public void ShowVictoryScreen(bool isOasisWin)
    {
        if (victoryPanel != null) victoryPanel.SetActive(true);

        AudioManager.Play(IsVersus() || isOasisWin ? Sfx.Win : Sfx.Lose);

        if (IsVersus())
        {
            string winnerName = gameManager.GetPlayerName(isOasisWin ? 0 : 1);

            if (versusWinnerNameText != null) versusWinnerNameText.text = winnerName;
            if (versusWinGroup != null)       versusWinGroup.SetActive(true);

            if (infoWinnerNameText != null) infoWinnerNameText.text = winnerName;
            if (infoWinnerGroup != null)    infoWinnerGroup.SetActive(true);
            return;
        }

        if (isOasisWin && oasisWinText != null)
            oasisWinText.SetActive(true);
        else if (!isOasisWin && mudWinText != null)
            mudWinText.SetActive(true);
    }

    // GameManager 가 이 값을 확인해서 패널이 열려 있는 동안 모래 생성을 막는다
    public bool IsInputBlocked()
    {
        if (GuestLeftPanel.IsOpen)
            return true;

        if (optionsUI != null && optionsUI.IsOptionsOpen())
            return true;

        if (IsTutorialOpen())
            return true;

        if (menuPanel != null && menuPanel.activeSelf)
            return true;

        if (isStageIntroActive)
            return true;

        return false;
    }

    bool IsTutorialOpen()
    {
        return tutorialPanel != null && tutorialPanel.activeSelf;
    }

    void OnResetClicked()
    {
        AudioManager.Play(Sfx.Click);
        HideMenu();
        HideVictoryPanel();

        if (gameManager != null)
        {
            gameManager.ResetGame();
            UpdateScoreDisplay();
        }
    }

    void OnRetryClicked()
    {
        AudioManager.Play(Sfx.Click);
        HideVictoryPanel();

        if (gameManager != null)
        {
            gameManager.ResetGame();
            UpdateScoreDisplay();
        }
    }

    void OnOptionsClicked()
    {
        if (optionsUI != null)
            optionsUI.OpenOptions();
        else
            Debug.LogWarning("OptionsUI not found!");
    }


    // 메뉴는 효과음 없이 닫고 튜토리얼이 열리는 효과음만 재생. 처음 진입할 때 자동으로 열리는 튜토리얼은 효과음이 없다
    void OnTutorialButtonClicked()
    {
        HideMenu();

        if (tutorialUI != null)
        {
            tutorialUI.Show();
            AudioManager.Play(Sfx.PaperOpen);
        }
    }

    // 승리 패널의 EXIT 와 메뉴의 QUIT 은 같은 동작
    void OnLeaveGameClicked()
    {
        AudioManager.Play(Sfx.Click);

        if (gameManager != null)
            gameManager.LeaveGameScene();
        else
            GlobalManager.LoadSceneSafe(SceneNames.Select);
    }
}
