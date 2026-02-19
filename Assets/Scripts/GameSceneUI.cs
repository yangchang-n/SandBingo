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

    private GameManager gameManager;
    private bool isMenuOpen = false;

    void Start()
    {
        gameManager = GameManager.Instance;

        // 초기 UI 설정
        if (menuPanel != null)
            menuPanel.SetActive(false);

        if (menuText != null)
            menuText.SetActive(true); // MENU 텍스트는 기본 활성화

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