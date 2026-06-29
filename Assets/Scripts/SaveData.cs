using System;

[Serializable]
public class SaveData
{
    // 진행 상황
    public bool stage1Cleared;
    public bool stage2Cleared;
    public bool stage3Cleared;

    // 최고 점수
    public int stage1BestScore;
    public int stage2BestScore;
    public int stage3BestScore;

    // 스토리 감상 여부
    public bool stage1PreSeen;
    public bool stage1TutorialSeen;
    public bool stage1PostSeen;
    public bool stage2PreSeen;
    public bool stage2PostSeen;
    public bool stage3PreSeen;
    public bool stage3PostSeen;

    // 오디오 설정
    public int volumePercentage;
    public bool isMuted;

    // 해상도 설정
    public int screenWidth;
    public int screenHeight;
    public bool isFullscreen;

    // 언어 설정
    public string languageCode;

    // C# 기본값과 다른 항목만 명시적으로 초기화
    public SaveData()
    {
        volumePercentage = 50;
        isFullscreen = true;
        languageCode = "EN";
    }
}
