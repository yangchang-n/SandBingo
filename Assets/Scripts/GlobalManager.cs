using UnityEngine;

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

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 저장/로드 기능 일시 비활성화
            // LoadProgress();

            Debug.Log("GlobalManager initialized");
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    // 저장/로드 기능 일시 비활성화
    /*
    void LoadProgress()
    {
        stage1Cleared = PlayerPrefs.GetInt("Stage1Cleared", 0) == 1;
        stage2Cleared = PlayerPrefs.GetInt("Stage2Cleared", 0) == 1;
        stage3Cleared = PlayerPrefs.GetInt("Stage3Cleared", 0) == 1;
        
        stage1BestScore = PlayerPrefs.GetInt("Stage1BestScore", 0);
        stage2BestScore = PlayerPrefs.GetInt("Stage2BestScore", 0);
        stage3BestScore = PlayerPrefs.GetInt("Stage3BestScore", 0);
    }

    void SaveProgress()
    {
        PlayerPrefs.SetInt("Stage1Cleared", stage1Cleared ? 1 : 0);
        PlayerPrefs.SetInt("Stage2Cleared", stage2Cleared ? 1 : 0);
        PlayerPrefs.SetInt("Stage3Cleared", stage3Cleared ? 1 : 0);
        
        PlayerPrefs.SetInt("Stage1BestScore", stage1BestScore);
        PlayerPrefs.SetInt("Stage2BestScore", stage2BestScore);
        PlayerPrefs.SetInt("Stage3BestScore", stage3BestScore);
        
        PlayerPrefs.Save();
    }
    */

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

        // 저장 기능 일시 비활성화
        // SaveProgress();
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
        stage1Cleared = false;
        stage2Cleared = false;
        stage3Cleared = false;

        stage1BestScore = 0;
        stage2BestScore = 0;
        stage3BestScore = 0;

        // 저장 기능 일시 비활성화
        // SaveProgress();

        Debug.Log("All progress has been reset!");
    }

    public void PrintProgress()
    {
        Debug.Log("===== GAME PROGRESS =====");
        Debug.Log($"Stage 1: {(stage1Cleared ? "CLEARED" : "NOT CLEARED")} - Best: {stage1BestScore}");
        Debug.Log($"Stage 2: {(stage2Cleared ? "CLEARED" : "NOT CLEARED")} - Best: {stage2BestScore}");
        Debug.Log($"Stage 3: {(stage3Cleared ? "CLEARED" : "NOT CLEARED")} - Best: {stage3BestScore}");
        Debug.Log("========================");
    }
}