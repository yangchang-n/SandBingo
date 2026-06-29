using UnityEngine;
using UnityEngine.UI;

public class GameSceneUI : MonoBehaviour
{
    [Header("Menu Panel")]
    public GameObject menuPanel;
    public Button resetButton;
    public Button optionsButton;
    public Button mainMenuButton;
    public Button tutorialButton;

    [Header("Victory Panel")]
    public GameObject victoryPanel;
    public GameObject oasisWinText;
    public GameObject mudWinText;
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
    public Button tutorialSkipButton;

    private GameManager gameManager;
    private OptionsUI optionsUI;
    private bool isMenuOpen = false;

    void Start()
    {
        gameManager = GameManager.Instance;
        optionsUI = FindObjectOfType<OptionsUI>();

        SetupBackgrounds();

        if (menuPanel != null)    menuPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (oasisWinText != null) oasisWinText.SetActive(false);
        if (mudWinText != null)   mudWinText.SetActive(false);

        if (resetButton != null)    resetButton.onClick.AddListener(OnResetClicked);
        if (optionsButton != null)  optionsButton.onClick.AddListener(OnOptionsClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        if (tutorialButton != null) tutorialButton.onClick.AddListener(OnTutorialButtonClicked);

        if (continueButton != null)     continueButton.onClick.AddListener(OnContinueClicked);
        if (retryButton != null)        retryButton.onClick.AddListener(OnRetryClicked);
        if (tutorialSkipButton != null) tutorialSkipButton.onClick.AddListener(OnTutorialSkipClicked);

        SetupTutorial();
        InitializeScoreDisplay();
    }

    void SetupTutorial()
    {
        if (tutorialPanel == null) return;

        GlobalManager gm = GlobalManager.Instance;

        bool shouldShow = gm != null
            && gameManager != null
            && gameManager.botDifficulty == 1
            && gm.stage1TutorialChapter != null
            && !gm.IsStorySeen(gm.stage1TutorialChapter);

        tutorialPanel.SetActive(shouldShow);
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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && gameManager != null && !gameManager.isGameOver)
        {
            if (optionsUI != null && optionsUI.HandleEscapeKey())
                return;

            ToggleMenu();
        }
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

    void OnMainMenuClicked()
    {
        GlobalManager.Instance.LoadScene("SelectScene");
    }

    void OnTutorialButtonClicked()
    {
        HideMenu();

        if (tutorialPanel != null)
            tutorialPanel.SetActive(true);
    }

    void OnContinueClicked()
    {
        if (gameManager != null)
            gameManager.LeaveGameScene();
        else
            GlobalManager.Instance.LoadScene("SelectScene");
    }

    void OnTutorialSkipClicked()
    {
        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);

        GlobalManager gm = GlobalManager.Instance;
        if (gm != null && gm.stage1TutorialChapter != null)
            gm.MarkStoryAsSeen(gm.stage1TutorialChapter);
    }
}
