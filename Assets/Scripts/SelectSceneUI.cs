using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SelectSceneUI : MonoBehaviour
{
    [Header("Difficulty Buttons")]
    public Button easyButton;
    public Button mediumButton;
    public Button hardButton;

    [Header("Navigation")]
    public Button backButton;

    void Start()
    {
        // 난이도 버튼 연결
        if (easyButton != null)
            easyButton.onClick.AddListener(() => OnDifficultySelected(1));

        if (mediumButton != null)
            mediumButton.onClick.AddListener(() => OnDifficultySelected(2));

        if (hardButton != null)
            hardButton.onClick.AddListener(() => OnDifficultySelected(3));

        // 뒤로가기 버튼
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    void OnDifficultySelected(int difficulty)
    {
        Debug.Log($"Difficulty {difficulty} selected");

        // 난이도 정보 저장 (GameScene에서 읽어갈 수 있도록)
        PlayerPrefs.SetInt("BotDifficulty", difficulty);
        PlayerPrefs.SetInt("BotMode", 1); // 봇 모드 활성화
        PlayerPrefs.Save();

        // GameScene으로 이동
        SceneManager.LoadScene("GameScene");
    }

    void OnBackClicked()
    {
        Debug.Log("Back button clicked - Returning to TitleScene");
        SceneManager.LoadScene("TitleScene");
    }
}