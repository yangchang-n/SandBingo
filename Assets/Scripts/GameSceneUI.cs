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
    public Button mainMenuButton;

    [Header("Backgrounds")]
    public GameObject easyBackground;
    public GameObject mediumBackground;
    public GameObject hardBackground;

    private GameManager gameManager;
    private bool isMenuOpen = false;

    void Start()
    {
        gameManager = GameManager.Instance;

        // 배경화면 설정
        SetupBackgrounds();

        // 초기 UI 설정
        if (menuPanel != null)
            menuPanel.SetActive(false);

        if (menuText != null)
            menuText.SetActive(true);

        if (oasisWinText != null)
            oasisWinText.SetActive(false);

        if (mudWinText != null)
            mudWinText.SetActive(false);

        // 버튼 이벤트 연결
        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetClicked);

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

        // 난이도에 맞는 배경만 활성화
        if (gameManager != null && gameManager.isBotMode)
        {
            switch (gameManager.botDifficulty)
            {
                case 1:
                    if (easyBackground != null)
                    {
                        easyBackground.SetActive(true);
                        Debug.Log("Easy background activated");
                    }
                    break;
                case 2:
                    if (mediumBackground != null)
                    {
                        mediumBackground.SetActive(true);
                        Debug.Log("Medium background activated");
                    }
                    break;
                case 3:
                    if (hardBackground != null)
                    {
                        hardBackground.SetActive(true);
                        Debug.Log("Hard background activated");
                    }
                    break;
            }
        }
        else
        {
            // 봇 모드가 아닐 경우 기본 배경 (예: Easy)
            if (easyBackground != null)
            {
                easyBackground.SetActive(true);
                Debug.Log("Default background activated (Easy)");
            }
        }
    }

    void Update()
    {
        // ESC 키로 메뉴 토글 (게임 오버가 아닐 때만)
        if (Input.GetKeyDown(KeyCode.Escape) && gameManager != null && !gameManager.isGameOver)
        {
            ToggleMenu();
        }
    }

    void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;

        if (menuPanel != null)
            menuPanel.SetActive(isMenuOpen);

        // 메뉴가 열릴 때는 MENU 텍스트만 표시
        if (isMenuOpen)
        {
            if (menuText != null)
                menuText.SetActive(true);

            if (oasisWinText != null)
                oasisWinText.SetActive(false);

            if (mudWinText != null)
                mudWinText.SetActive(false);
        }

        Debug.Log($"Menu {(isMenuOpen ? "opened" : "closed")}");
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

    void OnMainMenuClicked()
    {
        Debug.Log("Main Menu button clicked");
        SceneManager.LoadScene("TitleScene");
    }
}