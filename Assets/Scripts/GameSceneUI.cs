using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameSceneUI : MonoBehaviour
{
    [Header("Victory UI")]
    public GameObject victoryPanel;
    public GameObject oasisWinText;
    public GameObject mudWinText;
    public Button resetButton;
    public Button mainMenuButton;

    private GameManager gameManager;

    void Start()
    {
        gameManager = GameManager.Instance;

        // 초기 UI 설정
        if (victoryPanel != null)
            victoryPanel.SetActive(false);

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

    public void ShowVictoryScreen(bool isOasisWin)
    {
        if (victoryPanel != null)
            victoryPanel.SetActive(true);

        if (isOasisWin && oasisWinText != null)
            oasisWinText.SetActive(true);
        else if (!isOasisWin && mudWinText != null)
            mudWinText.SetActive(true);
    }

    public void HideVictoryScreen()
    {
        if (victoryPanel != null)
            victoryPanel.SetActive(false);

        if (oasisWinText != null)
            oasisWinText.SetActive(false);

        if (mudWinText != null)
            mudWinText.SetActive(false);
    }

    void OnResetClicked()
    {
        Debug.Log("Reset button clicked");
        HideVictoryScreen();

        if (gameManager != null)
            gameManager.ResetGame();
    }

    void OnMainMenuClicked()
    {
        Debug.Log("Main Menu button clicked");
        SceneManager.LoadScene("TitleScene");
    }
}