using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TitleSceneUI : MonoBehaviour
{
    [Header("Buttons")]
    public Button startButton;
    public Button optionsButton;
    public Button creditsButton;
    public Button quitButton;

    [Header("Panels")]
    public GameObject optionsPanel;

    void Start()
    {
        // 버튼 이벤트 설정
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        if (optionsButton != null)
            optionsButton.onClick.AddListener(OnOptionsClicked);

        if (creditsButton != null)
            creditsButton.onClick.AddListener(OnCreditsClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

        // 옵션 패널 초기 비활성화
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    void OnStartClicked()
    {
        SceneManager.LoadScene("SelectScene");
    }

    void OnOptionsClicked()
    {
        Debug.Log("Options button clicked - Feature not implemented yet");

        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);
        }
    }

    void OnCreditsClicked()
    {
        SceneManager.LoadScene("CreditsScene");
    }

    void OnQuitClicked()
    {
        Debug.Log("Quit button clicked - Exiting game");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}