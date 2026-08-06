using System.Collections;
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

    [Header("Backgrounds")]
    public GameObject easyBackground;
    public GameObject mediumBackground;
    public GameObject hardBackground;

    [Header("Tutorial")]
    public GameObject tutorialPanel;

    [Header("Stage Intro")]
    // 씬 페이드인이 끝난 직후(또는 튜토리얼이 끝난 직후) 잠깐 떴다 사라지는 패널
    // stageIntroPanel 루트에 CanvasGroup이 붙어 있어야 페이드가 동작한다
    public GameObject stageIntroPanel;
    public Text stageNameText;
    // 진흙 모양 그림들을 배치할 기준점 - 패널 위 빈 오브젝트의 RectTransform
    public RectTransform mudShapeAnchor;
    public float stageIntroDisplayDuration = 1.5f;
    public float stageIntroFadeDuration = 0.3f;
    // 아래 두 값은 실제로 보면서 조정하는 용도의 기본값
    public float mudShapePixelsPerCell = 120f;
    public float mudShapeSpacing = 40f;

    private GameManager gameManager;
    private OptionsUI optionsUI;
    private bool isMenuOpen = false;

    private CanvasGroup stageIntroCanvasGroup;
    private bool isStageIntroActive = false;

    // 튜토리얼 패널 오브젝트에 붙어 있는 재생 컴포넌트
    private TutorialPanelUI tutorialUI;

    void Start()
    {
        gameManager = GameManager.Instance;
        optionsUI = FindObjectOfType<OptionsUI>();

        SetupBackgrounds();

        if (menuPanel != null)    menuPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (oasisWinText != null) oasisWinText.SetActive(false);
        if (mudWinText != null)   mudWinText.SetActive(false);

        if (menuOpenButton != null)  menuOpenButton.onClick.AddListener(OpenMenu);
        if (menuCloseButton != null) menuCloseButton.onClick.AddListener(HideMenu);
        if (resetButton != null)    resetButton.onClick.AddListener(OnResetClicked);
        if (optionsButton != null)  optionsButton.onClick.AddListener(OnOptionsClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnLeaveGameClicked);
        if (tutorialButton != null) tutorialButton.onClick.AddListener(OnTutorialButtonClicked);

        if (victoryCloseButton != null) victoryCloseButton.onClick.AddListener(HideVictoryPanel);
        if (continueButton != null)     continueButton.onClick.AddListener(OnLeaveGameClicked);
        if (retryButton != null)        retryButton.onClick.AddListener(OnRetryClicked);

        SetupTutorial();
        InitializeScoreDisplay();
        SetupStageIntro();
    }

    void OnDestroy()
    {
        if (GlobalManager.Instance != null)
            GlobalManager.Instance.OnSceneFadeInComplete -= HandleSceneFadeInComplete;
    }

    void SetupTutorial()
    {
        if (tutorialPanel == null) return;

        // 패널이 꺼져 있어도 GetComponent 는 동작하므로 여기서 한 번만 잡아둔다
        tutorialUI = tutorialPanel.GetComponent<TutorialPanelUI>();

        GlobalManager gm = GlobalManager.Instance;

        bool shouldShow = gm != null
            && gameManager != null
            && gameManager.botDifficulty == 1
            && gm.stage1TutorialChapter != null
            && !gm.IsStorySeen(gm.stage1TutorialChapter);

        if (!shouldShow)
        {
            tutorialPanel.SetActive(false);
            return;
        }

        // 재생 컴포넌트가 없으면 스킵 버튼도 동작하지 않아 패널을 닫을 수 없게 된다
        if (tutorialUI == null)
        {
            Debug.LogWarning("TutorialPanelUI not found on tutorialPanel!");
            tutorialPanel.SetActive(false);
            return;
        }

        tutorialUI.Play();
    }

    void SetupBackgrounds()
    {
        if (easyBackground != null)   easyBackground.SetActive(false);
        if (mediumBackground != null) mediumBackground.SetActive(false);
        if (hardBackground != null)   hardBackground.SetActive(false);

        if (gameManager != null && gameManager.isBotMode)
        {
            switch (gameManager.botDifficulty)
            {
                case 0: // 커스텀 스테이지 - 스테이지 1 배경을 그대로 사용
                case 1:
                    if (easyBackground != null)   easyBackground.SetActive(true);
                    break;
                case 2:
                    if (mediumBackground != null) mediumBackground.SetActive(true);
                    break;
                case 3:
                    if (hardBackground != null)   hardBackground.SetActive(true);
                    break;
            }
        }
        else
        {
            if (easyBackground != null) easyBackground.SetActive(true);
        }
    }

    // 씬 페이드인 완료 이벤트를 구독하고, 패널을 미리 꺼둔다
    void SetupStageIntro()
    {
        if (stageIntroPanel == null) return;

        stageIntroCanvasGroup = stageIntroPanel.GetComponent<CanvasGroup>();
        stageIntroPanel.SetActive(false);

        if (GlobalManager.Instance != null)
            GlobalManager.Instance.OnSceneFadeInComplete += HandleSceneFadeInComplete;
        else
            HandleSceneFadeInComplete(); // 에디터에서 GameScene을 바로 재생하는 경우 페이드 이벤트가 없으므로 즉시 시작
    }

    void HandleSceneFadeInComplete()
    {
        StartCoroutine(ShowStageIntroSequence());
    }

    // 튜토리얼이 자동으로 떠 있는 상태라면 닫힐 때까지 기다린 뒤 표시한다
    // 그 동안은 튜토리얼 패널 자체가 이미 입력을 막고 있다
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

    // 스테이지 이름과 진흙 모양을 채운다
    // 진흙 모양은 GameManager.GetMudPatternForDifficulty를 그대로 가져오므로
    // 실제 봇이 떨어뜨릴 모양과 항상 일치하고, 커스텀 스테이지가 생겨도 이 함수는 그대로 쓸 수 있다
    void PopulateStageIntro()
    {
        if (stageNameText != null)
            stageNameText.text = gameManager.botDifficulty == 0 ? "CUSTOM" : $"STAGE {gameManager.botDifficulty}";

        if (mudShapeAnchor == null) return;

        // 에디터에서 미리 넣어둔 참고용 자식이 있을 수 있어 방어적으로 정리
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
            // 앵커를 mudShapeAnchor의 중심에 고정해야 anchoredPosition이 그 중심 기준으로 계산된다
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(chunkWidth, chunkHeight);
            rect.anchoredPosition = new Vector2(startX + i * (chunkWidth + mudShapeSpacing), 0f);

            Image image = chunk.AddComponent<Image>();
            image.color = gameManager.brownColor;
        }
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // 튜토리얼 중에는 대사 넘기기와 겹치고, 스테이지 진입 연출은 곧 사라지므로 무시한다
        if (isStageIntroActive || IsTutorialOpen()) return;

        // 옵션이 열려 있으면 옵션이 먼저 닫힌다
        if (optionsUI != null && optionsUI.HandleEscapeKey()) return;

        // 승리 패널이 떠 있으면 닫기 버튼과 같게 동작한다
        if (victoryPanel != null && victoryPanel.activeSelf)
        {
            HideVictoryPanel();
            return;
        }

        ToggleMenu();
    }

    void InitializeScoreDisplay()
    {
        if (scoreText == null || gameManager == null) return;

        UpdateScoreText(gameManager.GetCurrentStageScore(), gameManager.GetCurrentStageTargetScore());
    }

    public void UpdateScoreDisplay()
    {
        if (scoreText == null || gameManager == null) return;

        UpdateScoreText(gameManager.GetCurrentStageScore(), gameManager.GetCurrentStageTargetScore());
    }

    void UpdateScoreText(int currentScore, int targetScore)
    {
        scoreText.text = $"{currentScore} / {targetScore}";
    }

    void OpenMenu()
    {
        isMenuOpen = true;

        if (menuPanel != null) menuPanel.SetActive(true);
    }

    void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;

        if (menuPanel != null) menuPanel.SetActive(isMenuOpen);
    }

    void HideMenu()
    {
        isMenuOpen = false;

        if (menuPanel != null) menuPanel.SetActive(false);
    }

    void HideVictoryPanel()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (oasisWinText != null) oasisWinText.SetActive(false);
        if (mudWinText != null)   mudWinText.SetActive(false);
    }

    public void ShowVictoryScreen(bool isOasisWin)
    {
        if (victoryPanel != null) victoryPanel.SetActive(true);

        if (isOasisWin && oasisWinText != null)
            oasisWinText.SetActive(true);
        else if (!isOasisWin && mudWinText != null)
            mudWinText.SetActive(true);
    }

    // 옵션, 튜토리얼, 메뉴, 스테이지 진입 패널처럼 화면을 덮는 패널이 열려 있으면 true를 반환한다
    // GameManager에서 이 값을 확인해서 패널이 열려있는 동안 모래 생성을 막는 용도로 쓴다
    public bool IsInputBlocked()
    {
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
        HideMenu();
        HideVictoryPanel();

        if (gameManager != null)
        {
            gameManager.ResetGame();
            InitializeScoreDisplay();
        }
    }

    void OnRetryClicked()
    {
        HideVictoryPanel();

        if (gameManager != null)
        {
            gameManager.ResetGame();
            InitializeScoreDisplay();
        }
    }

    void OnOptionsClicked()
    {
        if (optionsUI != null)
            optionsUI.OpenOptions();
        else
            Debug.LogWarning("OptionsUI not found!");
    }


    void OnTutorialButtonClicked()
    {
        HideMenu();

        if (tutorialUI != null)
            tutorialUI.Play();
    }

    // 승리 패널의 Continue 와 메뉴의 Quit 이 같은 동작을 한다
    // 아직 보지 않은 post 스토리가 있으면 GameManager 가 그쪽으로 보낸다
    void OnLeaveGameClicked()
    {
        if (gameManager != null)
            gameManager.LeaveGameScene();
        else
            GlobalManager.Instance.LoadScene("SelectScene");
    }
}
