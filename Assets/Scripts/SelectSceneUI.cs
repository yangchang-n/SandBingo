using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SelectSceneUI : MonoBehaviour
{
    [Header("Stage Buttons")]
    public Button easyButton;
    public Button normalButton;
    public Button hardButton;

    [Header("Navigation")]
    public Button backButton;

    void Start()
    {
        SetupButtons();
        UpdateButtonStates();
    }

    void SetupButtons()
    {
        if (easyButton != null)
            easyButton.onClick.AddListener(OnEasyButtonClick);

        if (normalButton != null)
            normalButton.onClick.AddListener(OnNormalButtonClick);

        if (hardButton != null)
            hardButton.onClick.AddListener(OnHardButtonClick);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackButtonClick);
    }

    void UpdateButtonStates()
    {
        if (GlobalManager.Instance == null)
        {
            Debug.LogWarning("GlobalManager not found! All buttons enabled by default.");
            EnableAllButtons();
            return;
        }

        // Easy는 항상 활성화
        if (easyButton != null)
        {
            easyButton.interactable = true;
        }

        // Normal은 Stage 1 클리어 시 활성화
        if (normalButton != null)
        {
            bool stage1Cleared = GlobalManager.Instance.IsStageCleared(1);
            normalButton.interactable = stage1Cleared;
        }

        // Hard는 Stage 2 클리어 시 활성화
        if (hardButton != null)
        {
            bool stage2Cleared = GlobalManager.Instance.IsStageCleared(2);
            hardButton.interactable = stage2Cleared;
        }
    }

    void EnableAllButtons()
    {
        if (easyButton != null)
            easyButton.interactable = true;

        if (normalButton != null)
            normalButton.interactable = true;

        if (hardButton != null)
            hardButton.interactable = true;
    }

    public void OnEasyButtonClick()
    {
        Debug.Log("Easy difficulty selected (Stage 1)");
        PlayerPrefs.SetInt("BotMode", 1);
        PlayerPrefs.SetInt("BotDifficulty", 1);
        SceneManager.LoadScene("GameScene");
    }

    public void OnNormalButtonClick()
    {
        Debug.Log("Normal difficulty selected (Stage 2)");
        PlayerPrefs.SetInt("BotMode", 1);
        PlayerPrefs.SetInt("BotDifficulty", 2);
        SceneManager.LoadScene("GameScene");
    }

    public void OnHardButtonClick()
    {
        Debug.Log("Hard difficulty selected (Stage 3)");
        PlayerPrefs.SetInt("BotMode", 1);
        PlayerPrefs.SetInt("BotDifficulty", 3);
        SceneManager.LoadScene("GameScene");
    }

    public void OnBackButtonClick()
    {
        Debug.Log("Returning to Title Scene");
        SceneManager.LoadScene("TitleScene");
    }

#if UNITY_EDITOR
    [ContextMenu("Unlock All Stages (Debug)")]
    void DebugUnlockAll()
    {
        if (GlobalManager.Instance != null)
        {
            GlobalManager.Instance.stage1Cleared = true;
            GlobalManager.Instance.stage2Cleared = true;
            GlobalManager.Instance.stage3Cleared = true;
            UpdateButtonStates();
            Debug.Log("All stages unlocked for testing!");
        }
    }

    [ContextMenu("Lock All Stages (Debug)")]
    void DebugLockAll()
    {
        if (GlobalManager.Instance != null)
        {
            GlobalManager.Instance.stage1Cleared = false;
            GlobalManager.Instance.stage2Cleared = false;
            GlobalManager.Instance.stage3Cleared = false;
            UpdateButtonStates();
            Debug.Log("All stages locked for testing!");
        }
    }
#endif
}