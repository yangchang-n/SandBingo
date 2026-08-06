using UnityEngine;
using UnityEngine.UI;

public class SelectSceneUI : MonoBehaviour
{
    [Header("Stage Buttons")]
    public Button easyButton;
    public Button normalButton;
    public Button hardButton;

    [Header("Navigation")]
    public Button backButton;

    [Header("Story Buttons - Stage 1")]
    public Button stage1PreStoryButton;
    public Button stage1PostStoryButton;

    [Header("Story Buttons - Stage 2")]
    public Button stage2PreStoryButton;
    public Button stage2PostStoryButton;

    [Header("Story Buttons - Stage 3")]
    public Button stage3PreStoryButton;
    public Button stage3PostStoryButton;

    void Start()
    {
        SetupButtons();
        UpdateButtonStates();
    }

    void SetupButtons()
    {
        if (easyButton != null)   easyButton.onClick.AddListener(OnEasyButtonClick);
        if (normalButton != null) normalButton.onClick.AddListener(OnNormalButtonClick);
        if (hardButton != null)   hardButton.onClick.AddListener(OnHardButtonClick);
        if (backButton != null)   backButton.onClick.AddListener(OnBackButtonClick);

        if (stage1PreStoryButton != null)
            stage1PreStoryButton.onClick.AddListener(() => OnStoryButtonClick(1, isPre: true));
        if (stage1PostStoryButton != null)
            stage1PostStoryButton.onClick.AddListener(() => OnStoryButtonClick(1, isPre: false));

        if (stage2PreStoryButton != null)
            stage2PreStoryButton.onClick.AddListener(() => OnStoryButtonClick(2, isPre: true));
        if (stage2PostStoryButton != null)
            stage2PostStoryButton.onClick.AddListener(() => OnStoryButtonClick(2, isPre: false));

        if (stage3PreStoryButton != null)
            stage3PreStoryButton.onClick.AddListener(() => OnStoryButtonClick(3, isPre: true));
        if (stage3PostStoryButton != null)
            stage3PostStoryButton.onClick.AddListener(() => OnStoryButtonClick(3, isPre: false));
    }

    void UpdateButtonStates()
    {
        if (GlobalManager.Instance == null)
        {
            Debug.LogWarning("GlobalManager not found! All buttons enabled by default.");
            EnableAllStageButtons();
            HideAllStoryButtons();
            return;
        }

        GlobalManager gm = GlobalManager.Instance;

        SetStageButtonInteractable(easyButton, true);
        SetStageButtonInteractable(normalButton, gm.IsStageCleared(1));
        SetStageButtonInteractable(hardButton, gm.IsStageCleared(2));

        SetStoryButtonVisible(stage1PreStoryButton,  gm.IsStorySeen(gm.stage1PreChapter));
        SetStoryButtonVisible(stage1PostStoryButton, gm.IsStorySeen(gm.stage1PostChapter));
        SetStoryButtonVisible(stage2PreStoryButton,  gm.IsStorySeen(gm.stage2PreChapter));
        SetStoryButtonVisible(stage2PostStoryButton, gm.IsStorySeen(gm.stage2PostChapter));
        SetStoryButtonVisible(stage3PreStoryButton,  gm.IsStorySeen(gm.stage3PreChapter));
        SetStoryButtonVisible(stage3PostStoryButton, gm.IsStorySeen(gm.stage3PostChapter));
    }

    // 버튼이 비활성화되면 배경 Image는 Button의 Disabled Color로 자동으로 흐려지지만
    // 자식 Text(Legacy)는 Selectable이 아니라서 그 적용을 받지 않는다
    // 그래서 텍스트 알파를 버튼과 같은 값으로 직접 맞춰준다
    void SetStageButtonInteractable(Button button, bool interactable)
    {
        if (button == null) return;

        button.interactable = interactable;

        Text label = button.GetComponentInChildren<Text>();
        if (label == null) return;

        Color color = label.color;
        color.a = interactable ? 1f : button.colors.disabledColor.a;
        label.color = color;
    }

    void SetStoryButtonVisible(Button button, bool visible)
    {
        if (button != null)
            button.gameObject.SetActive(visible);
    }

    void EnableAllStageButtons()
    {
        SetStageButtonInteractable(easyButton, true);
        SetStageButtonInteractable(normalButton, true);
        SetStageButtonInteractable(hardButton, true);
    }

    void HideAllStoryButtons()
    {
        SetStoryButtonVisible(stage1PreStoryButton,  false);
        SetStoryButtonVisible(stage1PostStoryButton, false);
        SetStoryButtonVisible(stage2PreStoryButton,  false);
        SetStoryButtonVisible(stage2PostStoryButton, false);
        SetStoryButtonVisible(stage3PreStoryButton,  false);
        SetStoryButtonVisible(stage3PostStoryButton, false);
    }

    void OnStageButtonClick(int difficulty)
    {
        if (GlobalManager.Instance == null)
        {
            StartGame(difficulty);
            return;
        }

        GlobalManager gm = GlobalManager.Instance;
        StoryChapter preChapter = GetPreChapter(gm, difficulty);
        bool preUnseen = preChapter != null && !gm.IsStorySeen(preChapter);

        if (preUnseen)
        {
            gm.pendingBotDifficulty = difficulty;
            gm.GoToStory(preChapter, "GameScene");
        }
        else
        {
            StartGame(difficulty);
        }
    }

    void StartGame(int difficulty)
    {
        if (GlobalManager.Instance != null)
            GlobalManager.Instance.pendingBotDifficulty = difficulty;
        else
            Debug.LogWarning("GlobalManager not found! Difficulty may not be set correctly.");

        GlobalManager.Instance.LoadScene("GameScene");
    }

    void OnStoryButtonClick(int stageNumber, bool isPre)
    {
        if (GlobalManager.Instance == null) return;

        GlobalManager gm = GlobalManager.Instance;
        StoryChapter chapter = isPre ? GetPreChapter(gm, stageNumber) : GetPostChapter(gm, stageNumber);

        if (chapter != null)
            gm.GoToStory(chapter, "SelectScene");
    }

    StoryChapter GetPreChapter(GlobalManager gm, int difficulty)
    {
        return difficulty switch
        {
            1 => gm.stage1PreChapter,
            2 => gm.stage2PreChapter,
            3 => gm.stage3PreChapter,
            _ => null
        };
    }

    StoryChapter GetPostChapter(GlobalManager gm, int difficulty)
    {
        return difficulty switch
        {
            1 => gm.stage1PostChapter,
            2 => gm.stage2PostChapter,
            3 => gm.stage3PostChapter,
            _ => null
        };
    }

    public void OnEasyButtonClick()
    {
        OnStageButtonClick(1);
    }

    public void OnNormalButtonClick()
    {
        OnStageButtonClick(2);
    }

    public void OnHardButtonClick()
    {
        OnStageButtonClick(3);
    }

    public void OnBackButtonClick()
    {
        GlobalManager.Instance.LoadScene("TitleScene");
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
