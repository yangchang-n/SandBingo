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

    [Header("Audio Settings")]
    public AudioClip bgmClip;
    [Range(0f, 1f)]
    public float bgmVolume = 0.5f;

    private AudioSource audioSource;

    [Header("Game Settings")]
    public bool isMuted = false;
    public int volumePercentage = 50;

    // 저장 파일 경로
    private string SaveFilePath
    {
        get
        {
#if UNITY_EDITOR
            // 에디터: Assets 폴더와 같은 레벨에 SaveData 폴더 생성
            string saveFolder = Path.Combine(Application.dataPath, "..", "SaveData");
            if (!Directory.Exists(saveFolder))
            {
                Directory.CreateDirectory(saveFolder);
            }
            return Path.Combine(saveFolder, "savedata.json");
#else
            // 빌드: 실행 파일과 같은 폴더에 저장
            string exeFolder = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(exeFolder, "savedata.json");
#endif
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadProgress();
            InitializeBGM();

            Debug.Log("GlobalManager initialized");
            Debug.Log($"Save file location: {SaveFilePath}");
        }
        else
        {
            Destroy(gameObject);
            return;
        }
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
        if (File.Exists(SaveFilePath))
        {
            try
            {
                string json = File.ReadAllText(SaveFilePath);
                SaveData data = JsonUtility.FromJson<SaveData>(json);

                // 진행 상황
                stage1Cleared = data.stage1Cleared;
                stage2Cleared = data.stage2Cleared;
                stage3Cleared = data.stage3Cleared;

                stage1BestScore = data.stage1BestScore;
                stage2BestScore = data.stage2BestScore;
                stage3BestScore = data.stage3BestScore;

                // 오디오 설정
                volumePercentage = data.volumePercentage;
                bgmVolume = volumePercentage / 100f;
                isMuted = data.isMuted;

                // 해상도 설정
                if (data.screenWidth > 0 && data.screenHeight > 0)
                {
                    ApplyResolution(data.screenWidth, data.screenHeight, data.isFullscreen);
                }

                Debug.Log($"Progress loaded from: {SaveFilePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load save file: {e.Message}");
            }
        }
        else
        {
            Debug.Log("No save file found. Using default values.");
        }
    }

    void SaveProgress()
    {
        try
        {
            SaveData data = new SaveData
            {
                // 진행 상황
                stage1Cleared = this.stage1Cleared,
                stage2Cleared = this.stage2Cleared,
                stage3Cleared = this.stage3Cleared,

                stage1BestScore = this.stage1BestScore,
                stage2BestScore = this.stage2BestScore,
                stage3BestScore = this.stage3BestScore,

                // 오디오 설정
                volumePercentage = this.volumePercentage,
                isMuted = this.isMuted,

                // 해상도 설정
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                isFullscreen = Screen.fullScreen
            };

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SaveFilePath, json);

            Debug.Log($"Progress saved to: {SaveFilePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save progress: {e.Message}");
        }
    }

    public void CompleteStage(int stageNumber, int finalScore)
    {
        switch (stageNumber)
        {
            case 1:
                stage1Cleared = true;
                if (finalScore > stage1BestScore)
                {
                    int oldScore = stage1BestScore;
                    stage1BestScore = finalScore;
                    Debug.Log($"Stage 1 Best Score Updated: {oldScore} -> {finalScore}");
                }
                break;

            case 2:
                stage2Cleared = true;
                if (finalScore > stage2BestScore)
                {
                    int oldScore = stage2BestScore;
                    stage2BestScore = finalScore;
                    Debug.Log($"Stage 2 Best Score Updated: {oldScore} -> {finalScore}");
                }
                break;

            case 3:
                stage3Cleared = true;
                if (finalScore > stage3BestScore)
                {
                    int oldScore = stage3BestScore;
                    stage3BestScore = finalScore;
                    Debug.Log($"Stage 3 Best Score Updated: {oldScore} -> {finalScore}");
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
        // 진행 상황만 초기화 (볼륨/해상도는 유지)
        stage1Cleared = false;
        stage2Cleared = false;
        stage3Cleared = false;

        stage1BestScore = 0;
        stage2BestScore = 0;
        stage3BestScore = 0;

        SaveProgress();  // 볼륨/해상도는 현재 값 그대로 저장됨
        Debug.Log("Progress has been reset! (Settings preserved)");
    }

    public void PrintProgress()
    {
        Debug.Log("===== GAME PROGRESS =====");
        Debug.Log($"Stage 1: {(stage1Cleared ? "CLEARED" : "NOT CLEARED")} - Best: {stage1BestScore}");
        Debug.Log($"Stage 2: {(stage2Cleared ? "CLEARED" : "NOT CLEARED")} - Best: {stage2BestScore}");
        Debug.Log($"Stage 3: {(stage3Cleared ? "CLEARED" : "NOT CLEARED")} - Best: {stage3BestScore}");
        Debug.Log($"Save file: {SaveFilePath}");
        Debug.Log("========================");
    }

    // ===== Audio Control =====

    public void SetVolumePercentage(int percentage)
    {
        volumePercentage = Mathf.Clamp(percentage, 0, 100);
        bgmVolume = volumePercentage / 100f;

        if (audioSource != null)
        {
            audioSource.volume = bgmVolume;
        }

        SaveProgress();
        Debug.Log($"Volume set to: {volumePercentage}%");
    }

    public void SetMute(bool mute)
    {
        isMuted = mute;

        if (audioSource != null)
        {
            audioSource.mute = isMuted;
        }

        SaveProgress();
        Debug.Log($"Mute: {isMuted}");
    }

    public void ToggleMute()
    {
        SetMute(!isMuted);
    }

    public bool IsMuted()
    {
        return isMuted;
    }

    public int GetVolumePercentage()
    {
        return volumePercentage;
    }

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

    public Resolution GetCurrentMonitorResolution()
    {
        return Screen.currentResolution;
    }

    // ===== Save File Management =====

    public string GetSaveFilePath()
    {
        return SaveFilePath;
    }

    public bool SaveFileExists()
    {
        return File.Exists(SaveFilePath);
    }

    public void DeleteSaveFile()
    {
        if (File.Exists(SaveFilePath))
        {
            File.Delete(SaveFilePath);
            Debug.Log($"Save file deleted: {SaveFilePath}");
        }
    }
}