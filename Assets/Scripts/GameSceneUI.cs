using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameSceneUI : MonoBehaviour
{
    [Header("Menu Panel")]
    public GameObject menuPanel;
    public GameObject menuText;
    public GameObject oasisWinText;
    public GameObject mudWinText;
    public Button resetButton;
    public Button optionsButton;
    public Button mainMenuButton;

    [Header("Score Display")]
    public Text scoreText;

    [Header("Backgrounds")]
    public GameObject easyBackground;
    public GameObject mediumBackground;
    public GameObject hardBackground;

    private GameManager gameManager;
    private OptionsUI optionsUI;
    private bool isMenuOpen = false;

    void Start()
    {
        gameManager = GameManager.Instance;
        optionsUI = FindObjectOfType<OptionsUI>();

        SetupBackgrounds();

        if (menuPanel != null)
            menuPanel.SetActive(false);

        if (menuText != null)
            menuText.SetActive(true);

        if (oasisWinText != null)
            oasisWinText.SetActive(false);

        if (mudWinText != null)
            mudWinText.SetActive(false);

        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetClicked);

        if (optionsButton != null)
            optionsButton.onClick.AddListener(OnOptionsClicked);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);

        InitializeScoreDisplay();
    }

    void SetupBackgrounds()
    {
        if (easyBackground != null)
            easyBackground.SetActive(false);

        if (mediumBackground != null)
            mediumBackground.SetActive(false);

        if (hardBackground != null)
            hardBackground.SetActive(false);

        if (gameManager != null && gameManager.isBotMode)
        {
            switch (gameManager.botDifficulty)
            {
                case 1:
                    if (easyBackground != null)
                        easyBackground.SetActive(true);
                    break;
                case 2:
                    if (mediumBackground != null)
                        mediumBackground.SetActive(true);
                    break;
                case 3:
                    if (hardBackground != null)
                        hardBackground.SetActive(true);
                    break;
            }
        }
        else
        {
            if (easyBackground != null)
                easyBackground.SetActive(true);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && gameManager != null && !gameManager.isGameOver)
        {
            if (optionsUI != null && optionsUI.HandleEscapeKey())
            {
                return;
            }

            ToggleMenu();
        }
    }

    void InitializeScoreDisplay()
    {
        if (scoreText == null || gameManager == null)
            return;

        int difficulty = gameManager.isBotMode ? gameManager.botDifficulty : 1;
        int currentScore = gameManager.GetCurrentStageScore();
        int targetScore = gameManager.GetCurrentStageTargetScore();

        UpdateScoreText(currentScore, targetScore);
    }

    public void UpdateScoreDisplay()
    {
        if (scoreText == null || gameManager == null)
            return;

        int currentScore = gameManager.GetCurrentStageScore();
        int targetScore = gameManager.GetCurrentStageTargetScore();

        UpdateScoreText(currentScore, targetScore);
    }

    void UpdateScoreText(int currentScore, int targetScore)
    {
        string scoreString = currentScore >= 0 ? currentScore.ToString() : currentScore.ToString();
        scoreText.text = $"{scoreString} / {targetScore}";
    }

    void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;

        if (menuPanel != null)
            menuPanel.SetActive(isMenuOpen);

        if (isMenuOpen)
        {
            if (menuText != null)
                menuText.SetActive(true);

            if (oasisWinText != null)
                oasisWinText.SetActive(false);

            if (mudWinText != null)
                mudWinText.SetActive(false);
        }
    }

    public void ShowVictoryScreen(bool isOasisWin)
    {
        isMenuOpen = true;

        if (menuPanel != null)
            menuPanel.SetActive(true);

        if (menuText != null)
            menuText.SetActive(false);

        if (isOasisWin && oasisWinText != null)
            oasisWinText.SetActive(true);
        else if (!isOasisWin && mudWinText != null)
            mudWinText.SetActive(true);
    }

    public void HideMenu()
    {
        isMenuOpen = false;

        if (menuPanel != null)
            menuPanel.SetActive(false);

        if (menuText != null)
            menuText.SetActive(true);

        if (oasisWinText != null)
            oasisWinText.SetActive(false);

        if (mudWinText != null)
            mudWinText.SetActive(false);
    }

    void OnResetClicked()
    {
        Debug.Log("Reset button clicked");
        HideMenu();

        if (gameManager != null)
        {
            gameManager.ResetGame();
            InitializeScoreDisplay();
        }
    }

    void OnOptionsClicked()
    {
        Debug.Log("Options button clicked");

        if (optionsUI != null)
        {
            optionsUI.OpenOptions();
        }
        else
        {
            Debug.LogWarning("OptionsUI not found!");
        }
    }

    void OnMainMenuClicked()
    {
        Debug.Log("Main Menu button clicked");
        SceneManager.LoadScene("TitleScene");
    }
}