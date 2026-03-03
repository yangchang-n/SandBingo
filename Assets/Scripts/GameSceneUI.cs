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

        // 초기 UI 상태
        if (menuPanel != null)
            menuPanel.SetActive(false);

        if (menuText != null)
            menuText.SetActive(true);

        if (oasisWinText != null)
            oasisWinText.SetActive(false);

        if (mudWinText != null)
            mudWinText.SetActive(false);

        // 버튼 이벤트 설정
        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetClicked);

        if (optionsButton != null)
            optionsButton.onClick.AddListener(OnOptionsClicked);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }

    void SetupBackgrounds()
    {
        // 모든 배경 비활성화
        if (easyBackground != null)
            easyBackground.SetActive(false);

        if (mediumBackground != null)
            mediumBackground.SetActive(false);

        if (hardBackground != null)
            hardBackground.SetActive(false);

        // 난이도에 맞는 배경 활성화
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
            // 봇 모드가 아닌 경우 기본 배경 (예: Easy)
            if (easyBackground != null)
                easyBackground.SetActive(true);
        }
    }

    void Update()
    {
        // ESC 키 처리
        if (Input.GetKeyDown(KeyCode.Escape) && gameManager != null && !gameManager.isGameOver)
        {
            // 먼저 OptionsUI가 ESC를 처리했는지 확인
            if (optionsUI != null && optionsUI.HandleEscapeKey())
            {
                // OptionsUI가 ESC를 처리했으면 여기서 종료
                return;
            }

            // OptionsUI가 처리하지 않았으면 메뉴 패널 토글
            ToggleMenu();
        }
    }

    void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;

        if (menuPanel != null)
            menuPanel.SetActive(isMenuOpen);

        // 메뉴가 열린 경우 MENU 텍스트만 표시
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

        // 승리 시: MENU 텍스트 비활성화, WIN 텍스트 활성화
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
            gameManager.ResetGame();
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