using UnityEngine;
using UnityEngine.UI;

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
        optionsUI = FindObjectOfType<OptionsUI>();

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
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (optionsUI != null)
                optionsUI.HandleEscapeKey();
        }
    }

    void OnStartClicked()
    {
        GlobalManager.Instance.LoadScene("SelectScene");
    }

    void OnOptionsClicked()
    {
        if (optionsUI != null)
            optionsUI.OpenOptions();
        else
            Debug.LogWarning("OptionsUI not found!");
    }

    void OnCreditsClicked()
    {
        GlobalManager.Instance.LoadScene("CreditsScene");
    }

    void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
