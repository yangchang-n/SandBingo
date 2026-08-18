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

    // 커스텀 스테이지 버튼과 설정 패널
    // 버튼은 스토리 버튼과 같은 방식으로 해금 전에는 아예 보이지 않는다
    // 누르면 게임씬으로 바로 가지 않고 패널을 열어서 난이도를 정하게 한다
    [Header("Custom Stage")]
    public Button customButton;
    public GameObject customPanel;
    public Button customStartButton;
    public Button customCloseButton;

    [Header("Custom Stage - Mud Height")]
    public Text customMudHeightText;
    public Button customMudHeightUpButton;
    public Button customMudHeightDownButton;

    [Header("Custom Stage - Mud Count")]
    public Text customMudCountText;
    public Button customMudCountUpButton;
    public Button customMudCountDownButton;

    void Start()
    {
        SetupButtons();

        // 씬에 켜진 채로 저장되어 있어도 항상 닫힌 상태에서 시작한다
        if (customPanel != null)
            customPanel.SetActive(false);

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

        SetupCustomButtons();
    }

    void SetupCustomButtons()
    {
        if (customButton != null)      customButton.onClick.AddListener(OpenCustomPanel);
        if (customStartButton != null) customStartButton.onClick.AddListener(OnCustomStartClick);
        if (customCloseButton != null) customCloseButton.onClick.AddListener(CloseCustomPanel);

        if (customMudHeightUpButton != null)
            customMudHeightUpButton.onClick.AddListener(() => AdjustCustomMudHeight(1));
        if (customMudHeightDownButton != null)
            customMudHeightDownButton.onClick.AddListener(() => AdjustCustomMudHeight(-1));

        if (customMudCountUpButton != null)
            customMudCountUpButton.onClick.AddListener(() => AdjustCustomMudCount(1));
        if (customMudCountDownButton != null)
            customMudCountDownButton.onClick.AddListener(() => AdjustCustomMudCount(-1));
    }

    void UpdateButtonStates()
    {
        if (GlobalManager.Instance == null)
        {
            Debug.LogWarning("GlobalManager not found! All buttons enabled by default.");
            EnableAllStageButtons();
            HideAllUnlockableButtons();
            return;
        }

        GlobalManager gm = GlobalManager.Instance;

        SetButtonInteractable(easyButton, true);
        SetButtonInteractable(normalButton, gm.IsStageCleared(1));
        SetButtonInteractable(hardButton, gm.IsStageCleared(2));

        SetButtonVisible(stage1PreStoryButton,  gm.IsStorySeen(gm.stage1PreChapter));
        SetButtonVisible(stage1PostStoryButton, gm.IsStorySeen(gm.stage1PostChapter));
        SetButtonVisible(stage2PreStoryButton,  gm.IsStorySeen(gm.stage2PreChapter));
        SetButtonVisible(stage2PostStoryButton, gm.IsStorySeen(gm.stage2PostChapter));
        SetButtonVisible(stage3PreStoryButton,  gm.IsStorySeen(gm.stage3PreChapter));
        SetButtonVisible(stage3PostStoryButton, gm.IsStorySeen(gm.stage3PostChapter));

        // 커스텀 버튼은 stage3 post 스토리를 본 뒤에만 나타난다
        // 이 스토리는 스테이지 3을 클리어해야만 재생되므로 클리어 여부를 따로 검사할 필요가 없다
        SetButtonVisible(customButton, gm.IsStorySeen(gm.stage3PostChapter));
    }

    // 버튼이 비활성화되면 배경 Image는 Button의 Disabled Color로 자동으로 흐려지지만
    // 자식 Text(Legacy)는 Selectable이 아니라서 그 적용을 받지 않는다
    // 그래서 텍스트 알파를 버튼과 같은 값으로 직접 맞춰준다
    // 주의: GetComponentInChildren은 비활성 오브젝트를 찾지 못하므로
    // 패널 안의 버튼에 쓸 때는 반드시 패널을 먼저 켠 뒤에 호출해야 한다
    void SetButtonInteractable(Button button, bool interactable)
    {
        if (button == null) return;

        button.interactable = interactable;

        Text label = button.GetComponentInChildren<Text>();
        if (label == null) return;

        Color color = label.color;
        color.a = interactable ? 1f : button.colors.disabledColor.a;
        label.color = color;
    }

    void SetButtonVisible(Button button, bool visible)
    {
        if (button != null)
            button.gameObject.SetActive(visible);
    }

    void EnableAllStageButtons()
    {
        SetButtonInteractable(easyButton, true);
        SetButtonInteractable(normalButton, true);
        SetButtonInteractable(hardButton, true);
    }

    // 해금되기 전까지 보이지 않아야 하는 버튼들을 전부 숨긴다
    void HideAllUnlockableButtons()
    {
        SetButtonVisible(stage1PreStoryButton,  false);
        SetButtonVisible(stage1PostStoryButton, false);
        SetButtonVisible(stage2PreStoryButton,  false);
        SetButtonVisible(stage2PostStoryButton, false);
        SetButtonVisible(stage3PreStoryButton,  false);
        SetButtonVisible(stage3PostStoryButton, false);
        SetButtonVisible(customButton,          false);
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

    // GlobalManager가 없으면 페이드도 난이도 전달도 불가능하므로 씬만 직접 넘긴다
    // 에디터에서 SelectScene을 단독 재생하는 경우에만 해당된다
    void StartGame(int difficulty)
    {
        GlobalManager gm = GlobalManager.Instance;

        if (gm == null)
        {
            Debug.LogWarning("GlobalManager not found! Loading GameScene without fade.");
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
            return;
        }

        gm.pendingBotDifficulty = difficulty;
        gm.LoadScene("GameScene");
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

    // ===== Custom Stage Panel =====

    // 커스텀 패널을 연다
    // SetActive를 먼저 호출해야 한다. RefreshCustomPanel이 쓰는 GetComponentInChildren는
    // 비활성 오브젝트를 찾지 못해서 순서가 바뀌면 첫 열림에서 화살표 텍스트 색이 갱신되지 않는다
    // 뒤로가기 버튼은 패널 아래쪽 경계에 걸쳐 있어서 패널에 완전히 가려지지 않는다
    // 삐져나온 부분이 하필 시작 버튼 바로 아래라 오조작으로 타이틀에 나가버리므로 패널이 열린 동안에는 숨긴다
    void OpenCustomPanel()
    {
        if (customPanel == null || GlobalManager.Instance == null) return;

        customPanel.SetActive(true);
        SetButtonVisible(backButton, false);
        RefreshCustomPanel();
    }

    void CloseCustomPanel()
    {
        if (customPanel != null)
            customPanel.SetActive(false);

        SetButtonVisible(backButton, true);
    }

    // 현재 설정값을 숫자와 화살표 상태에 반영한다
    // 값 자체는 GlobalManager가 들고 있으므로 패널을 닫았다 다시 열어도 그대로 유지된다
    // 진입점(OpenCustomPanel, AdjustCustom*)에서 이미 GlobalManager를 확인했으므로 여기서는 검사하지 않는다
    void RefreshCustomPanel()
    {
        GlobalManager gm = GlobalManager.Instance;

        int tenths = gm.GetCustomMudHeightTenths();
        int count  = gm.GetCustomMudCount();

        // 0.1 단위를 정수로 다루므로 소수점 표기를 직접 조립한다
        // ToString("F1")은 지역 설정에 따라 소수점이 쉼표로 나올 수 있다
        if (customMudHeightText != null)
            customMudHeightText.text = $"{tenths / 10}.{tenths % 10}";

        if (customMudCountText != null)
            customMudCountText.text = count.ToString();

        SetButtonInteractable(customMudHeightUpButton,   tenths < GlobalManager.CUSTOM_MUD_HEIGHT_MAX_TENTHS);
        SetButtonInteractable(customMudHeightDownButton, tenths > GlobalManager.CUSTOM_MUD_HEIGHT_MIN_TENTHS);
        SetButtonInteractable(customMudCountUpButton,    count  < GlobalManager.CUSTOM_MUD_COUNT_MAX);
        SetButtonInteractable(customMudCountDownButton,  count  > GlobalManager.CUSTOM_MUD_COUNT_MIN);
    }

    // 화살표 버튼 처리. delta는 항상 1 또는 -1이다
    // 범위를 벗어난 값은 GlobalManager의 setter가 잘라내므로 여기서는 검사하지 않는다
    void AdjustCustomMudHeight(int delta)
    {
        GlobalManager gm = GlobalManager.Instance;
        if (gm == null) return;

        gm.SetCustomMudHeightTenths(gm.GetCustomMudHeightTenths() + delta);
        RefreshCustomPanel();
    }

    void AdjustCustomMudCount(int delta)
    {
        GlobalManager gm = GlobalManager.Instance;
        if (gm == null) return;

        gm.SetCustomMudCount(gm.GetCustomMudCount() + delta);
        RefreshCustomPanel();
    }

    // 커스텀 스테이지는 난이도 번호 0으로 진입한다
    // 진흙 설정값은 이미 GlobalManager에 들어 있어서 따로 넘길 것이 없다
    // 패널을 닫거나 뒤로가기 버튼을 되돌리지 않는다
    // LoadScene이 즉시 페이드 패널로 입력을 막고, 씬이 새로 로드되면서 Start부터 다시 세팅되기 때문이다
    void OnCustomStartClick()
    {
        StartGame(0);
    }

    // ===== Button Handlers =====

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
        if (GlobalManager.Instance != null)
            GlobalManager.Instance.LoadScene("TitleScene");
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("TitleScene");
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
