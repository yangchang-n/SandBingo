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
        AudioManager.Play(Sfx.Click);
        GlobalManager.Instance.LoadScene(SceneNames.Select);
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
        AudioManager.Play(Sfx.Click);
        GlobalManager.Instance.LoadScene(SceneNames.Credits);
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
