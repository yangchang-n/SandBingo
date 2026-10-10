using System.Collections.Generic;
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

    // 커스텀 스테이지 버튼과 설정 패널
    // 버튼은 해금 전에는 보이지 않음
    // 누르면 게임씬으로 바로 가지 않고 패널을 열어서 난이도를 정한다
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

    // Steam 이 연결되지 않았거나 GitHub 빌드처럼 Steam 코드가 빠진 경우 두 버튼은 반투명 비활성 상태
    [Header("Remote Play")]
    public Button inviteButton;
    [Tooltip("권한 게스트가 있고 게스트 입력이 호스트 입력과 분리되어 있을 때만 활성화된다")]
    public Button versusButton;
    [Tooltip("권한 게스트 이름. 다국어 폰트. 게스트가 없으면 숨긴다")]
    public Text guestNameText;

    [Header("Versus Side Panel")]
    public GameObject versusPanel;
    public Button hostFirstButton;
    public Button guestFirstButton;
    public Button versusCloseButton;

    [Header("Debug")]
    [Tooltip("에디터와 개발 빌드에서만 동작한다. 게스트 없이도 Versus 버튼을 켠다. 게임씬의 Debug Fake Guest 와 함께 사용")]
    public bool debugAllowVersusWithoutGuest = false;

    // 버튼 자식 그래픽의 지정된 알파. 씬에서 정한 값이 1 이 아닐 수 있어서 처음에 기록해두고 그 값을 기준으로 투명하게 한다
    private readonly Dictionary<Graphic, float> baseAlphas = new Dictionary<Graphic, float>();

    void Start()
    {
        SetupButtons();

        // 씬에 켜진 채로 저장되어 있어도 항상 닫힌 상태로 시작
        if (customPanel != null)
            customPanel.SetActive(false);
        if (versusPanel != null)
            versusPanel.SetActive(false);

        UpdateButtonStates();

        if (RemotePlaySessionManager.Instance != null)
            RemotePlaySessionManager.Instance.OnActiveGuestChanged += UpdateRemotePlayState;

        UpdateRemotePlayState();
    }

    void OnDestroy()
    {
        if (RemotePlaySessionManager.Instance != null)
            RemotePlaySessionManager.Instance.OnActiveGuestChanged -= UpdateRemotePlayState;
    }

    void SetupButtons()
    {
        if (easyButton != null)   easyButton.onClick.AddListener(() => StartGame(1));
        if (normalButton != null) normalButton.onClick.AddListener(() => StartGame(2));
        if (hardButton != null)   hardButton.onClick.AddListener(() => StartGame(3));
        if (backButton != null)   backButton.onClick.AddListener(OnBackButtonClick);

        SetupCustomButtons();
        SetupRemotePlayButtons();
    }

    void SetupRemotePlayButtons()
    {
        if (inviteButton != null)      inviteButton.onClick.AddListener(OnInviteClick);
        if (versusButton != null)      versusButton.onClick.AddListener(OpenVersusPanel);
        if (hostFirstButton != null)   hostFirstButton.onClick.AddListener(() => StartVersus(true));
        if (guestFirstButton != null)  guestFirstButton.onClick.AddListener(() => StartVersus(false));
        if (versusCloseButton != null) versusCloseButton.onClick.AddListener(OnVersusCloseClick);
    }

    void SetupCustomButtons()
    {
        if (customButton != null)      customButton.onClick.AddListener(OpenCustomPanel);
        if (customStartButton != null) customStartButton.onClick.AddListener(OnCustomStartClick);
        if (customCloseButton != null) customCloseButton.onClick.AddListener(OnCustomCloseClick);

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

        // 커스텀 버튼은 스테이지 3을 클리어해야 보인다
        SetButtonVisible(customButton, gm.IsStageCleared(3));
    }

    // 버튼이 비활성화되면 배경 Image 는 Button 의 Disabled Color 로 자동으로 흐려지지만
    // 자식 Text 나 아이콘 Image 는 적용되지 않는다
    // 그래서 배경을 뺀 자식 그래픽의 원래 알파에 Disabled Color 의 알파를 곱해서 맞춘다
    // 원래 알파는 처음 호출될 때 기록한다. Start 에서 처음 호출하므로 그때는 씬에서 정한 값 그대로
    // 주의: GetComponentsInChildren 은 비활성 오브젝트를 찾지 못하므로
    // 패널 안의 버튼에 쓸 때는 반드시 패널을 먼저 켠 뒤에 호출해야 한다
    void SetButtonInteractable(Button button, bool interactable)
    {
        if (button == null) return;

        button.interactable = interactable;

        float multiplier = interactable ? 1f : button.colors.disabledColor.a;

        foreach (Graphic graphic in button.GetComponentsInChildren<Graphic>())
        {
            if (graphic == button.targetGraphic) continue;

            if (!baseAlphas.TryGetValue(graphic, out float baseAlpha))
            {
                baseAlpha = graphic.color.a;
                baseAlphas[graphic] = baseAlpha;
            }

            Color color = graphic.color;
            color.a = baseAlpha * multiplier;
            graphic.color = color;
        }
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

    void HideAllUnlockableButtons()
    {
        SetButtonVisible(customButton, false);
    }

    // GlobalManager 가 없으면 페이드도 난이도 전달도 불가능하므로 씬만 바로 전환한다
    // 에디터에서 SelectScene 을 바로 재생하는 경우에만 해당
    void StartGame(int difficulty)
    {
        AudioManager.Play(Sfx.Click);

        GlobalManager gm = GlobalManager.Instance;

        if (gm == null)
        {
            Debug.LogWarning("GlobalManager not found! Loading GameScene without fade.");
            UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.Game);
            return;
        }

        gm.pendingVersus = false;
        gm.pendingBotDifficulty = difficulty;
        gm.LoadScene(SceneNames.Game);
    }

    // Versus 는 진영 정보를 넘겨야 하므로 GlobalManager 없이는 진입하지 않음
    void StartVersus(bool hostIsOasis)
    {
        AudioManager.Play(Sfx.Click);

        GlobalManager gm = GlobalManager.Instance;

        if (gm == null)
        {
            Debug.LogWarning("GlobalManager not found! Versus requires GlobalManager.");
            return;
        }

        gm.pendingVersus = true;
        gm.pendingVersusHostIsOasis = hostIsOasis;
        gm.LoadScene(SceneNames.Game);
    }

    // ===== Remote Play =====

    // 게스트가 들어오거나 나가거나 이름 정보가 늦게 도착할 때마다 다시 호출된다
    void UpdateRemotePlayState()
    {
        RemotePlaySessionManager session = RemotePlaySessionManager.Instance;

        SetButtonInteractable(inviteButton, SteamManager.IsInitialized);

        bool hasGuest = session != null && session.HasActiveGuest;

        // 게스트 입력이 호스트 입력에 섞이는 상태에서는 차례를 구분할 수 없으므로 Versus 를 제한한다
        bool canVersus = hasGuest && session.IsDirectInputEnabled;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugAllowVersusWithoutGuest) canVersus = true;
#endif
        SetButtonInteractable(versusButton, canVersus);

        if (!canVersus)
            CloseVersusPanel();

        if (guestNameText != null)
        {
            guestNameText.gameObject.SetActive(hasGuest);
            if (hasGuest)
                guestNameText.text = "Guest : " + session.GetActiveGuestName("Unknown");
        }
    }

    // Steam 이 연결되어 있어도 Remote Play Together 가 설정되지 않았거나 오버레이를 사용할 수 없으면 열리지 않는다
    // 이 경우는 화면에 따로 알리지 않고 로그만 남김
    void OnInviteClick()
    {
        AudioManager.Play(Sfx.Click);

        RemotePlaySessionManager session = RemotePlaySessionManager.Instance;

        if (session == null || !session.ShowInviteUI())
            Debug.LogWarning("[RemotePlay] Could not open the Remote Play Together invite UI.");
    }

    void OpenVersusPanel()
    {
        if (versusPanel == null) return;

        versusPanel.SetActive(true);
        AudioManager.Play(Sfx.HardPanelOpen);
    }

    void OnVersusCloseClick()
    {
        CloseVersusPanel();
        AudioManager.Play(Sfx.HardPanelClose);
    }

    // 게스트가 나가서 Versus 를 할 수 없게 되었을 때도 호출되므로 소리를 재생하지 않는다
    void CloseVersusPanel()
    {
        if (versusPanel != null)
            versusPanel.SetActive(false);
    }

    // ===== Custom Stage Panel =====

    // 패널을 먼저 켜야 한다. RefreshCustomPanel 이 사용하는 GetComponentsInChildren 은 비활성 오브젝트를 찾지 못한다
    // Back 버튼은 패널에 완전히 가려지지 않고 시작 버튼 바로 아래에 걸쳐 있어서 잘못 누르지 않도록 패널이 열린 동안 숨긴다
    void OpenCustomPanel()
    {
        if (customPanel == null || GlobalManager.Instance == null) return;

        customPanel.SetActive(true);
        SetButtonVisible(backButton, false);
        RefreshCustomPanel();

        AudioManager.Play(Sfx.HardPanelOpen);
    }

    void OnCustomCloseClick()
    {
        CloseCustomPanel();
        AudioManager.Play(Sfx.HardPanelClose);
    }

    void CloseCustomPanel()
    {
        if (customPanel != null)
            customPanel.SetActive(false);

        SetButtonVisible(backButton, true);
    }

    // 호출하는 쪽에서 GlobalManager 를 이미 확인했으므로 여기서는 검사하지 않음
    void RefreshCustomPanel()
    {
        GlobalManager gm = GlobalManager.Instance;

        int tenths = gm.GetCustomMudHeightTenths();
        int count  = gm.GetCustomMudCount();

        // 지역 설정에 따라 소수점이 쉼표로 바뀌지 않도록 직접 문자열로 만든다
        if (customMudHeightText != null)
            customMudHeightText.text = $"{tenths / 10}.{tenths % 10}";

        if (customMudCountText != null)
            customMudCountText.text = count.ToString();

        SetButtonInteractable(customMudHeightUpButton,   tenths < GlobalManager.CUSTOM_MUD_HEIGHT_MAX_TENTHS);
        SetButtonInteractable(customMudHeightDownButton, tenths > GlobalManager.CUSTOM_MUD_HEIGHT_MIN_TENTHS);
        SetButtonInteractable(customMudCountUpButton,    count  < GlobalManager.CUSTOM_MUD_COUNT_MAX);
        SetButtonInteractable(customMudCountDownButton,  count  > GlobalManager.CUSTOM_MUD_COUNT_MIN);
    }

    // 범위를 벗어난 값은 GlobalManager 의 setter 가 잘라내므로 여기서는 검사하지 않음
    void AdjustCustomMudHeight(int delta)
    {
        GlobalManager gm = GlobalManager.Instance;
        if (gm == null) return;

        gm.SetCustomMudHeightTenths(gm.GetCustomMudHeightTenths() + delta);
        RefreshCustomPanel();
        AudioManager.Play(Sfx.SoftClick);
    }

    void AdjustCustomMudCount(int delta)
    {
        GlobalManager gm = GlobalManager.Instance;
        if (gm == null) return;

        gm.SetCustomMudCount(gm.GetCustomMudCount() + delta);
        RefreshCustomPanel();
        AudioManager.Play(Sfx.SoftClick);
    }

    // 커스텀 스테이지는 난이도 번호 0으로 진입. 진흙 설정값은 이미 GlobalManager 에 있음
    void OnCustomStartClick()
    {
        StartGame(0);
    }

    void OnBackButtonClick()
    {
        AudioManager.Play(Sfx.Click);
        GlobalManager.LoadSceneSafe(SceneNames.Title);
    }

#if UNITY_EDITOR
    [ContextMenu("Unlock All Stages (Debug)")]
    void DebugUnlockAll()
    {
        if (GlobalManager.Instance != null)
        {
            GlobalManager.Instance.DebugSetAllStagesCleared(true);
            UpdateButtonStates();
            Debug.Log("All stages unlocked for testing!");
        }
    }

    [ContextMenu("Lock All Stages (Debug)")]
    void DebugLockAll()
    {
        if (GlobalManager.Instance != null)
        {
            GlobalManager.Instance.DebugSetAllStagesCleared(false);
            UpdateButtonStates();
            Debug.Log("All stages locked for testing!");
        }
    }
#endif
}
