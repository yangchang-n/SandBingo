using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;

public class GlobalManager : MonoBehaviour
{
    // Singleton
    public static GlobalManager Instance { get; private set; }

    [Header("Stage Progress")]
    public bool stage1Cleared = false;
    public bool stage2Cleared = false;
    public bool stage3Cleared = false;

    [Header("Best Scores")]
    public int stage1BestScore = 0;
    public int stage2BestScore = 0;
    public int stage3BestScore = 0;

    [Header("Story Chapters")]
    public StoryChapter stage1PreChapter;
    public StoryChapter stage1TutorialChapter;
    public StoryChapter stage1PostChapter;
    public StoryChapter stage2PreChapter;
    public StoryChapter stage2PostChapter;
    public StoryChapter stage3PreChapter;
    public StoryChapter stage3PostChapter;

    [Header("Audio Settings")]
    public AudioClip bgmClip;
    [HideInInspector]
    public float bgmVolume = 0.5f;

    [Header("Game Settings")]
    public bool isMuted = false;
    public int volumePercentage = 50;

    [Header("Language Settings")]
    public Font enFont;
    public Font krFont;

    // 언어 변경 이벤트 - LocalizedText가 구독
    public System.Action OnLanguageChanged;

    private string currentLanguage = "EN";

    private AudioSource audioSource;

    // 스토리 감상 여부
    [HideInInspector] public bool stage1PreSeen = false;
    [HideInInspector] public bool stage1TutorialSeen = false;
    [HideInInspector] public bool stage1PostSeen = false;
    [HideInInspector] public bool stage2PreSeen = false;
    [HideInInspector] public bool stage2PostSeen = false;
    [HideInInspector] public bool stage3PreSeen = false;
    [HideInInspector] public bool stage3PostSeen = false;

    // 씬 간 전달용
    [HideInInspector] public int pendingBotDifficulty = 1;
    [HideInInspector] public StoryChapter pendingStoryChapter = null;
    [HideInInspector] public string pendingNextScene = "";

    private string _saveFilePath;

    // ===== Fade System =====

    [Header("Fade Settings")]
    [Tooltip("씬 전환 페이드 인/아웃 각각에 걸리는 시간 (초)")]
    public float fadeDuration = 0.25f;

    private Image _fadePanel;
    private bool _isTransitioning = false;

    // ===== Lifecycle =====

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSaveFilePath();
            LoadProgress();
            InitializeBGM();
            InitializeFadePanel();
            Debug.Log("GlobalManager initialized");
            Debug.Log($"Save file location: {_saveFilePath}");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ===== Fade Setup =====

    // DontDestroyOnLoad 오브젝트에 Canvas와 Image를 동적으로 생성
    // 씬마다 배치할 필요 없이 항상 최상위에 표시됨
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

    // 모든 씬 전환의 단일 진입점
    // 페이드 아웃 -> 씬 로드 -> 페이드 인 순서로 진행
    public void LoadScene(string sceneName)
    {
        if (_isTransitioning) return;
        StartCoroutine(TransitionCoroutine(sceneName));
    }

    IEnumerator TransitionCoroutine(string sceneName)
    {
        _isTransitioning = true;
        _fadePanel.raycastTarget = true;

        yield return StartCoroutine(FadeTo(1f));

        SceneManager.LoadScene(sceneName);

        // 씬 로드 완료 후 한 프레임 대기
        yield return null;

        yield return StartCoroutine(FadeTo(0f));

        _fadePanel.raycastTarget = false;
        _isTransitioning = false;
    }

    IEnumerator FadeTo(float targetAlpha)
    {
        float startAlpha = _fadePanel.color.a;
        float elapsed = 0f;

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

    void InitializeBGM()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = bgmClip;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = bgmVolume;
        audioSource.mute = isMuted;

        if (bgmClip != null)
        {
            audioSource.Play();
            Debug.Log($"BGM started - Volume: {volumePercentage}%, Muted: {isMuted}");
        }
        else
        {
            Debug.LogWarning("BGM clip not assigned!");
        }
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
            string json = File.ReadAllText(_saveFilePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            stage1Cleared = data.stage1Cleared;
            stage2Cleared = data.stage2Cleared;
            stage3Cleared = data.stage3Cleared;

            stage1BestScore = data.stage1BestScore;
            stage2BestScore = data.stage2BestScore;
            stage3BestScore = data.stage3BestScore;

            stage1PreSeen      = data.stage1PreSeen;
            stage1TutorialSeen = data.stage1TutorialSeen;
            stage1PostSeen     = data.stage1PostSeen;
            stage2PreSeen      = data.stage2PreSeen;
            stage2PostSeen     = data.stage2PostSeen;
            stage3PreSeen      = data.stage3PreSeen;
            stage3PostSeen     = data.stage3PostSeen;

            volumePercentage = data.volumePercentage;
            bgmVolume = volumePercentage / 100f;
            isMuted = data.isMuted;

            currentLanguage = string.IsNullOrEmpty(data.languageCode) ? "EN" : data.languageCode;

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

                stage1PreSeen      = this.stage1PreSeen,
                stage1TutorialSeen = this.stage1TutorialSeen,
                stage1PostSeen     = this.stage1PostSeen,
                stage2PreSeen      = this.stage2PreSeen,
                stage2PostSeen     = this.stage2PostSeen,
                stage3PreSeen      = this.stage3PreSeen,
                stage3PostSeen     = this.stage3PostSeen,

                volumePercentage = this.volumePercentage,
                isMuted = this.isMuted,

                screenWidth  = Screen.width,
                screenHeight = Screen.height,
                isFullscreen = Screen.fullScreen,

                languageCode = this.currentLanguage
            };

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(_saveFilePath, json);
            Debug.Log($"Progress saved to: {_saveFilePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save progress: {e.Message}");
        }
    }

    // ===== Language Control =====

    public string GetCurrentLanguage() => currentLanguage;

    public void SetLanguage(string code)
    {
        if (code != "EN" && code != "KR")
        {
            Debug.LogWarning($"SetLanguage: unknown language code '{code}'. Accepted: EN, KR");
            return;
        }

        currentLanguage = code;
        SaveProgress();
        OnLanguageChanged?.Invoke();
        Debug.Log($"Language set to: {currentLanguage}");
    }

    public Font GetCurrentFont()
    {
        if (currentLanguage == "KR")
            return krFont != null ? krFont : enFont;

        return enFont;
    }

    // ===== Story Control =====

    // 스토리 씬으로 전환 - 페이드 효과 포함
    public void GoToStory(StoryChapter chapter, string nextScene)
    {
        pendingStoryChapter = chapter;
        pendingNextScene    = nextScene;
        LoadScene("StoryScene");
    }

    public void MarkStoryAsSeen(StoryChapter chapter)
    {
        if (chapter == null) return;

        if      (chapter == stage1PreChapter)       stage1PreSeen      = true;
        else if (chapter == stage1TutorialChapter)  stage1TutorialSeen = true;
        else if (chapter == stage1PostChapter)      stage1PostSeen     = true;
        else if (chapter == stage2PreChapter)       stage2PreSeen      = true;
        else if (chapter == stage2PostChapter)      stage2PostSeen     = true;
        else if (chapter == stage3PreChapter)       stage3PreSeen      = true;
        else if (chapter == stage3PostChapter)      stage3PostSeen     = true;
        else
        {
            Debug.LogWarning($"MarkStoryAsSeen: Unknown chapter '{chapter.name}'");
            return;
        }

        SaveProgress();
    }

    public bool IsStorySeen(StoryChapter chapter)
    {
        if (chapter == null) return false;

        if (chapter == stage1PreChapter)      return stage1PreSeen;
        if (chapter == stage1TutorialChapter) return stage1TutorialSeen;
        if (chapter == stage1PostChapter)     return stage1PostSeen;
        if (chapter == stage2PreChapter)      return stage2PreSeen;
        if (chapter == stage2PostChapter)     return stage2PostSeen;
        if (chapter == stage3PreChapter)      return stage3PreSeen;
        if (chapter == stage3PostChapter)     return stage3PostSeen;

        Debug.LogWarning($"IsStorySeen: Unknown chapter '{chapter.name}'");
        return false;
    }

    // ===== Stage Control =====

    public void CompleteStage(int stageNumber, int finalScore)
    {
        switch (stageNumber)
        {
            case 0:
                // 커스텀 스테이지는 클리어 여부/최고 점수를 저장하지 않는다
                // SaveProgress()까지 건너뛰어서 의미 없는 저장이 발생하지 않도록 한다
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

        stage1PreSeen = stage1TutorialSeen = stage1PostSeen = false;
        stage2PreSeen = stage2PostSeen = false;
        stage3PreSeen = stage3PostSeen = false;

        SaveProgress();
        Debug.Log("Progress has been reset! (Settings preserved)");
    }

    public void PrintProgress()
    {
        Debug.Log("===== GAME PROGRESS =====");
        Debug.Log($"Stage 1: {(stage1Cleared ? "CLEARED" : "NOT CLEARED")} - Best: {stage1BestScore}");
        Debug.Log($"Stage 2: {(stage2Cleared ? "CLEARED" : "NOT CLEARED")} - Best: {stage2BestScore}");
        Debug.Log($"Stage 3: {(stage3Cleared ? "CLEARED" : "NOT CLEARED")} - Best: {stage3BestScore}");
        Debug.Log($"Save file: {_saveFilePath}");
        Debug.Log("========================");
    }

    // ===== Audio Control =====

    public void SetVolumePercentage(int percentage)
    {
        volumePercentage = Mathf.Clamp(percentage, 0, 100);
        bgmVolume = volumePercentage / 100f;

        if (audioSource != null)
            audioSource.volume = bgmVolume;

        SaveProgress();
        Debug.Log($"Volume set to: {volumePercentage}%");
    }

    public void SetMute(bool mute)
    {
        isMuted = mute;

        if (audioSource != null)
            audioSource.mute = isMuted;

        SaveProgress();
        Debug.Log($"Mute: {isMuted}");
    }

    public void ToggleMute() => SetMute(!isMuted);
    public bool IsMuted() => isMuted;
    public int GetVolumePercentage() => volumePercentage;

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

    public Resolution GetCurrentMonitorResolution() => Screen.currentResolution;

    // ===== Save File Management =====

    public string GetSaveFilePath() => _saveFilePath;
    public bool SaveFileExists() => File.Exists(_saveFilePath);

    public void DeleteSaveFile()
    {
        if (File.Exists(_saveFilePath))
        {
            File.Delete(_saveFilePath);
            Debug.Log($"Save file deleted: {_saveFilePath}");
        }
    }
}
