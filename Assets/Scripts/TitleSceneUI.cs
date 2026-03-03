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

    private OptionsUI optionsUI;

    void Start()
    {
        // OptionsUI 찾기
        optionsUI = FindObjectOfType<OptionsUI>();

        // 버튼 이벤트 설정
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        if (optionsButton != null)
            optionsButton.onClick.AddListener(OnOptionsClicked);

        if (creditsButton != null)
            creditsButton.onClick.AddListener(OnCreditsClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);
    }

    void Update()
    {
        // ESC 키로 Options 패널 닫기
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (optionsUI != null)
            {
                optionsUI.HandleEscapeKey();
            }
        }
    }

    void OnStartClicked()
    {
        SceneManager.LoadScene("SelectScene");
    }

    void OnOptionsClicked()
    {
        if (optionsUI != null)
        {
            optionsUI.OpenOptions();
        }
        else
        {
            Debug.LogWarning("OptionsUI not found!");
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