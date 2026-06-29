using UnityEngine;
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

    // 언어별 폰트 (Inspector에서 할당)
    // EN 폰트가 null이면 폰트 변경 없이 기존 폰트 유지
    // KR 폰트가 null이면 EN 폰트로 폴백
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

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSaveFilePath();
            LoadProgress();
            InitializeBGM();
            Debug.Log("GlobalManager initialized");
            Debug.Log($"Save file location: {_saveFilePath}");
        }
        else
        {
            Destroy(gameObject);
        }
    }

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

            // 기존 저장 파일에 languageCode가 없으면 빈 문자열로 오므로 EN으로 처리
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

    // 언어 변경 - 저장 후 이벤트 발행
    // TitleScene 전환은 호출 측(OptionsUI)에서 담당
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

    // 현재 언어에 맞는 폰트 반환
    // KR 폰트가 없으면 EN 폰트로 폴백, EN도 없으면 null 반환 (LocalizedText에서 기존 폰트 유지)
    public Font GetCurrentFont()
    {
        if (currentLanguage == "KR")
            return krFont != null ? krFont : enFont;

        return enFont;
    }

    // ===== Story Control =====

    public void GoToStory(StoryChapter chapter, string nextScene)
    {
        pendingStoryChapter = chapter;
        pendingNextScene    = nextScene;
        UnityEngine.SceneManagement.SceneManager.LoadScene("StoryScene");
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
