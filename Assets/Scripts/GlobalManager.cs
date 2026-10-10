using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;

public class GlobalManager : MonoBehaviour
{
    // Singleton
    public static GlobalManager Instance { get; private set; }

    // 진행도. 세이브 파일에서 읽고 쓰며 외부에서는 함수로만 접근
    private bool stage1Cleared;
    private bool stage2Cleared;
    private bool stage3Cleared;

    private int stage1BestScore;
    private int stage2BestScore;
    private int stage3BestScore;

    // 소리 설정. 세이브 파일에서 읽고 쓰며 재생은 AudioManager 가 담당
    private int bgmVolumePercentage = 50;
    private int sfxVolumePercentage = 50;
    private bool isBgmMuted;
    private bool isSfxMuted;

    public event System.Action OnAudioSettingsChanged;

    // 씬 페이드인이 끝난 직후 호출. 페이드인이 끝난 뒤에 나와야 하는 UI 가 구독
    public System.Action OnSceneFadeInComplete;

    // 튜토리얼 시청 기록. 예전 필드 이름을 그대로 써서 기존 세이브 파일의 기록을 이어받는다
    [System.NonSerialized] public bool stage1TutorialSeen = false;

    // 셀렉트씬에서 정하고 GameManager 가 게임씬에 진입할 때 한 번 읽는 값
    // 봇전으로 진입할 때 pendingVersus 를 반드시 false 로 되돌려야 이전 Versus 설정이 남지 않는다
    [System.NonSerialized] public int pendingBotDifficulty = 1;
    [System.NonSerialized] public bool pendingVersus = false;
    [System.NonSerialized] public bool pendingVersusHostIsOasis = true;

    private string _saveFilePath;

    // ===== Fade System =====

    [Header("Fade Settings")]
    [Tooltip("씬 전환 페이드 인/아웃 각각에 걸리는 시간 (초)")]
    public float fadeDuration = 0.25f;

    private Image _fadePanel;
    private bool _isTransitioning = false;

    // 씬 전환 페이드가 진행 중인지 확인. 전환 중에 띄우면 안 되는 UI 가 사용
    public bool IsTransitioning => _isTransitioning;

    // ===== Lifecycle =====

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSaveFilePath();
            LoadProgress();
            InitializeFadePanel();
            Debug.Log($"Save file location: {_saveFilePath}");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ===== Fade Setup =====

    // 씬마다 배치할 필요 없이 항상 최상위에 표시
    void InitializeFadePanel()
    {
        GameObject canvasObj = new GameObject("FadeCanvas");
        canvasObj.transform.SetParent(transform);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject panelObj = new GameObject("FadePanel");
        panelObj.transform.SetParent(canvasObj.transform, false);

        RectTransform rect = panelObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _fadePanel = panelObj.AddComponent<Image>();
        _fadePanel.color = new Color(0f, 0f, 0f, 0f);
        _fadePanel.raycastTarget = false;
    }

    // ===== Scene Transition =====

    // 모든 씬 전환은 여기를 거친다. 페이드 아웃, 씬 로드, 페이드 인 순서로 진행
    public void LoadScene(string sceneName)
    {
        if (_isTransitioning) return;
        StartCoroutine(TransitionCoroutine(sceneName));
    }

    // GlobalManager 가 없으면 페이드 없이 씬만 연다. 에디터에서 중간 씬을 바로 재생할 때만 해당
    public static void LoadSceneSafe(string sceneName)
    {
        if (Instance != null)
            Instance.LoadScene(sceneName);
        else
            SceneManager.LoadScene(sceneName);
    }

    IEnumerator TransitionCoroutine(string sceneName)
    {
        _isTransitioning = true;
        _fadePanel.raycastTarget = true;

        yield return StartCoroutine(FadeTo(1f));

        SceneManager.LoadScene(sceneName);

        yield return null;

        yield return StartCoroutine(FadeTo(0f));

        OnSceneFadeInComplete?.Invoke();

        _fadePanel.raycastTarget = false;
        _isTransitioning = false;
    }

    IEnumerator FadeTo(float targetAlpha)
    {
        float startAlpha = _fadePanel.color.a;
        float elapsed = 0f;

        // Time.deltaTime 을 써야 한다. 씬을 불러온 직후의 프레임은 로딩 시간 때문에 매우 길어지는데
        // deltaTime 은 그 값을 일정 상한으로 잘라주지만 unscaledDeltaTime 은 그대로라 페이드 인이 한 번에 끝나버린다
        // 시간이 멈춘 채로 씬을 전환하는 곳은 없다. 게스트 이탈 패널은 시간을 먼저 되돌린 뒤 씬을 전환한다
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            _fadePanel.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }

        _fadePanel.color = new Color(0f, 0f, 0f, targetAlpha);
    }

    // ===== Initialization =====

    void InitializeSaveFilePath()
    {
#if UNITY_EDITOR
        string saveFolder = Path.Combine(Application.dataPath, "..", "SaveData");
        if (!Directory.Exists(saveFolder))
            Directory.CreateDirectory(saveFolder);
        _saveFilePath = Path.Combine(saveFolder, "savedata.json");
#else
        string exeFolder = Path.GetDirectoryName(Application.dataPath);
        _saveFilePath = Path.Combine(exeFolder, "savedata.json");
#endif
    }

    // ===== Save/Load =====

    void LoadProgress()
    {
        if (!File.Exists(_saveFilePath))
        {
            Debug.Log("No save file found. Using default values.");
            return;
        }

        try
        {
            // 기본값으로 채운 객체 위에 덮어쓴다. 예전 세이브에 없는 항목(효과음 설정 등)은 기본값을 유지
            string json = File.ReadAllText(_saveFilePath);
            SaveData data = new SaveData();
            JsonUtility.FromJsonOverwrite(json, data);

            stage1Cleared = data.stage1Cleared;
            stage2Cleared = data.stage2Cleared;
            stage3Cleared = data.stage3Cleared;

            stage1BestScore = data.stage1BestScore;
            stage2BestScore = data.stage2BestScore;
            stage3BestScore = data.stage3BestScore;

            stage1TutorialSeen = data.stage1TutorialSeen;

            bgmVolumePercentage = data.volumePercentage;
            isBgmMuted = data.isMuted;
            sfxVolumePercentage = data.sfxVolumePercentage;
            isSfxMuted = data.isSfxMuted;

            if (data.screenWidth > 0 && data.screenHeight > 0)
                ApplyResolution(data.screenWidth, data.screenHeight, data.isFullscreen);

            Debug.Log($"Progress loaded from: {_saveFilePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load save file: {e.Message}");
        }
    }

    void SaveProgress()
    {
        try
        {
            SaveData data = new SaveData
            {
                stage1Cleared = this.stage1Cleared,
                stage2Cleared = this.stage2Cleared,
                stage3Cleared = this.stage3Cleared,

                stage1BestScore = this.stage1BestScore,
                stage2BestScore = this.stage2BestScore,
                stage3BestScore = this.stage3BestScore,

                stage1TutorialSeen = this.stage1TutorialSeen,

                volumePercentage = this.bgmVolumePercentage,
                isMuted = this.isBgmMuted,
                sfxVolumePercentage = this.sfxVolumePercentage,
                isSfxMuted = this.isSfxMuted,

                screenWidth  = Screen.width,
                screenHeight = Screen.height,
                isFullscreen = Screen.fullScreen
            };

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(_saveFilePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save progress: {e.Message}");
        }
    }

    // ===== Tutorial Control =====

    public bool IsTutorialSeen()
    {
        return stage1TutorialSeen;
    }

    // 메뉴에서 다시 볼 때마다 파일을 반복해서 쓰지 않도록 함
    public void MarkTutorialSeen()
    {
        if (stage1TutorialSeen) return;

        stage1TutorialSeen = true;
        SaveProgress();
    }

    // ===== Stage Control =====

    public void CompleteStage(int stageNumber, int finalScore)
    {
        switch (stageNumber)
        {
            case 0:
                // 커스텀 스테이지는 클리어 여부와 최고 점수를 저장하지 않는다
                // SaveProgress 까지 건너뛰어서 불필요한 저장을 막는다
                return;
            case 1:
                stage1Cleared = true;
                if (finalScore > stage1BestScore)
                {
                    Debug.Log($"Stage 1 Best Score Updated: {stage1BestScore} -> {finalScore}");
                    stage1BestScore = finalScore;
                }
                break;
            case 2:
                stage2Cleared = true;
                if (finalScore > stage2BestScore)
                {
                    Debug.Log($"Stage 2 Best Score Updated: {stage2BestScore} -> {finalScore}");
                    stage2BestScore = finalScore;
                }
                break;
            case 3:
                stage3Cleared = true;
                if (finalScore > stage3BestScore)
                {
                    Debug.Log($"Stage 3 Best Score Updated: {stage3BestScore} -> {finalScore}");
                    stage3BestScore = finalScore;
                }
                break;
            default:
                Debug.LogWarning($"Invalid stage number: {stageNumber}");
                break;
        }

        SteamAchievements.UnlockStageClear(stageNumber);
        SaveProgress();
    }

    public int GetBestScore(int stageNumber)
    {
        return stageNumber switch
        {
            1 => stage1BestScore,
            2 => stage2BestScore,
            3 => stage3BestScore,
            _ => 0
        };
    }

    public bool IsStageCleared(int stageNumber)
    {
        return stageNumber switch
        {
            1 => stage1Cleared,
            2 => stage2Cleared,
            3 => stage3Cleared,
            _ => false
        };
    }

    public void ResetAllProgress()
    {
        stage1Cleared = stage2Cleared = stage3Cleared = false;
        stage1BestScore = stage2BestScore = stage3BestScore = 0;

        stage1TutorialSeen = false;

        SaveProgress();
        Debug.Log("Progress has been reset! (Settings preserved)");
    }

#if UNITY_EDITOR
    // 셀렉트씬 디버그 메뉴 전용. 저장하지 않음
    public void DebugSetAllStagesCleared(bool cleared)
    {
        stage1Cleared = cleared;
        stage2Cleared = cleared;
        stage3Cleared = cleared;
    }
#endif

    // ===== Custom Stage Settings =====

    // 커스텀 스테이지의 난이도는 진흙의 높이와 개수로만 정한다
    // 개수 상한은 봇이 배율을 정의해둔 최대 개수를 따름
    public const int CUSTOM_MUD_HEIGHT_MIN_TENTHS = 5;
    public const int CUSTOM_MUD_HEIGHT_MAX_TENTHS = 20;
    public const int CUSTOM_MUD_COUNT_MIN = 1;
    public const int CUSTOM_MUD_COUNT_MAX = BotController.MAX_MUD_COUNT;

    // 높이는 10을 곱해서 정수로 저장한다. float 에 0.1 씩 더하면 오차가 생긴다
    // 직렬화도 저장도 하지 않으므로 앱을 켤 때마다 1.0칸 2개에서 시작
    private int customMudHeightTenths = 10;
    private int customMudCount = 2;

    public int GetCustomMudHeightTenths() => customMudHeightTenths;
    public int GetCustomMudCount() => customMudCount;

    // 범위를 벗어난 값은 여기서 잘라내므로 호출하는 쪽은 검사하지 않아도 된다
    public void SetCustomMudHeightTenths(int tenths)
    {
        customMudHeightTenths = Mathf.Clamp(tenths, CUSTOM_MUD_HEIGHT_MIN_TENTHS, CUSTOM_MUD_HEIGHT_MAX_TENTHS);
    }

    public void SetCustomMudCount(int count)
    {
        customMudCount = Mathf.Clamp(count, CUSTOM_MUD_COUNT_MIN, CUSTOM_MUD_COUNT_MAX);
    }

    public BotController.MudPattern GetCustomMudPattern()
    {
        return new BotController.MudPattern
        {
            heightCells = customMudHeightTenths / 10f,
            count = customMudCount
        };
    }

    // ===== Audio Control =====

    // 볼륨을 소리에만 바로 반영하고 파일에는 쓰지 않는다
    // 슬라이더를 움직이는 동안에는 이쪽만 호출해서 파일을 반복해서 쓰지 않도록 함
    public void ApplyVolumePercentage(AudioChannel channel, int percentage)
    {
        int clamped = Mathf.Clamp(percentage, 0, 100);

        if (channel == AudioChannel.Bgm)
            bgmVolumePercentage = clamped;
        else
            sfxVolumePercentage = clamped;

        OnAudioSettingsChanged?.Invoke();
    }

    // 적용과 저장을 같이 한다. 값이 확정됐을 때만 호출
    public void SetVolumePercentage(AudioChannel channel, int percentage)
    {
        ApplyVolumePercentage(channel, percentage);

        SaveProgress();
        Debug.Log($"{channel} volume set to: {GetVolumePercentage(channel)}%");
    }

    public void SetMute(AudioChannel channel, bool mute)
    {
        if (channel == AudioChannel.Bgm)
            isBgmMuted = mute;
        else
            isSfxMuted = mute;

        OnAudioSettingsChanged?.Invoke();

        SaveProgress();
        Debug.Log($"{channel} mute: {mute}");
    }

    public bool IsMuted(AudioChannel channel) => channel == AudioChannel.Bgm ? isBgmMuted : isSfxMuted;
    public int GetVolumePercentage(AudioChannel channel) => channel == AudioChannel.Bgm ? bgmVolumePercentage : sfxVolumePercentage;

    // ===== Resolution Control =====

    public void SetResolution(int width, int height, bool fullscreen)
    {
        ApplyResolution(width, height, fullscreen);
        SaveProgress();
    }

    void ApplyResolution(int width, int height, bool fullscreen)
    {
        if (fullscreen)
        {
            Screen.SetResolution(
                Screen.currentResolution.width,
                Screen.currentResolution.height,
                FullScreenMode.FullScreenWindow
            );
            Debug.Log($"Resolution: Fullscreen ({Screen.currentResolution.width}x{Screen.currentResolution.height})");
        }
        else
        {
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            Debug.Log($"Resolution: Windowed {width}x{height}");
        }
    }
}
