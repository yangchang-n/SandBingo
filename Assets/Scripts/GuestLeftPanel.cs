using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 권한 게스트가 나갔을 때 어느 씬에서든 띄우는 패널
// TitleScene 의 GlobalManager 아래 GuestLeftCanvas 에 붙인다. 이 오브젝트는 씬 전환에도 유지된다
// 패널 오브젝트가 아니라 항상 켜져 있는 캔버스에 붙여야 Start 가 호출된다
public class GuestLeftPanel : MonoBehaviour
{
    public static GuestLeftPanel Instance { get; private set; }

    // 패널이 열려 있는지 확인. 열려 있는 동안 각 씬은 입력 처리를 멈춘다
    public static bool IsOpen => Instance != null && Instance.isOpen;

    [Tooltip("화면 전체를 덮는 이미지로 뒤쪽 클릭을 막는 패널")]
    public GameObject panel;
    public Button confirmButton;
    [Tooltip("나간 게스트 이름. 다국어 폰트. 비워두면 이름을 표시하지 않는다")]
    public Text nameText;

    private RemotePlaySessionManager session;
    private bool isOpen;
    private bool isActive;

    // 씬 전환 중에 이탈하면 전환이 끝난 뒤에 띄운다
    private bool isPending;
    private string pendingName = "";

    void Awake()
    {
        // 패널이 씬에 켜진 채로 저장되어 있어도 시작할 때는 항상 숨김
        if (panel != null)
            panel.SetActive(false);

        // 타이틀씬으로 돌아오면 GlobalManager 가 중복 오브젝트를 지우면서 이 컴포넌트도 같이 사라진다
        // 그 경우에는 아무것도 초기화하지 않음
        if (Instance != null && Instance != this) return;

        Instance = this;
        isActive = true;

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    void Start()
    {
        if (!isActive) return;

        session = RemotePlaySessionManager.Instance;
        if (session != null)
            session.OnActiveGuestLeft += HandleActiveGuestLeft;

        if (GlobalManager.Instance != null)
            GlobalManager.Instance.OnSceneFadeInComplete += ShowPending;
    }

    void OnDestroy()
    {
        if (!isActive) return;

        if (session != null)
            session.OnActiveGuestLeft -= HandleActiveGuestLeft;

        if (GlobalManager.Instance != null)
            GlobalManager.Instance.OnSceneFadeInComplete -= ShowPending;

        // 패널이 열린 채로 종료되어도 다음 재생에 멈춘 시간이 남지 않도록 함
        if (isOpen)
            Time.timeScale = 1f;

        if (Instance == this)
            Instance = null;
    }

    void HandleActiveGuestLeft(string leavingName)
    {
        if (GlobalManager.Instance != null && GlobalManager.Instance.IsTransitioning)
        {
            isPending = true;
            pendingName = leavingName;
            return;
        }

        Show(leavingName);
    }

    void ShowPending()
    {
        if (!isPending) return;

        isPending = false;
        Show(pendingName);
    }

    void Show(string leavingName)
    {
        // 이미 열려 있으면 연달아 나가도 패널은 하나만 유지
        if (isOpen) return;

        if (panel == null)
        {
            Debug.LogWarning("[GuestLeftPanel] Panel is not assigned.");
            return;
        }

        if (nameText != null)
            nameText.text = leavingName;

        isOpen = true;
        panel.SetActive(true);
        AudioManager.Play(Sfx.PaperOpen);

        // 패널 뒤에서 봇 턴이나 연출이 진행되지 않도록 시간을 멈춘다
        Time.timeScale = 0f;
    }

    // 게임씬은 봇전이면 그대로 이어서 진행하고 Versus 면 셀렉트씬으로 이동
    // 나머지 씬은 현재 씬을 다시 불러와 접속자 표시를 갱신한다
    void OnConfirmClicked()
    {
        if (!isOpen) return;

        isOpen = false;
        panel.SetActive(false);
        Time.timeScale = 1f;
        AudioManager.Play(Sfx.PaperClose);

        string sceneName = SceneManager.GetActiveScene().name;

        if (sceneName == SceneNames.Game)
        {
            bool isVersus = GameManager.Instance != null && GameManager.Instance.isVersusMode;
            if (isVersus)
                GlobalManager.LoadSceneSafe(SceneNames.Select);
            return;
        }

        GlobalManager.LoadSceneSafe(sceneName);
    }
}
