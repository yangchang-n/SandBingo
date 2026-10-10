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

    // 튜토리얼 진입 여부
    public bool stage1TutorialSeen;

    // 소리 설정
    public int volumePercentage;
    public bool isMuted;
    public int sfxVolumePercentage;
    public bool isSfxMuted;

    // 해상도 설정
    public int screenWidth;
    public int screenHeight;
    public bool isFullscreen;

    // C# 기본값과 다른 항목만 초기화
    public SaveData()
    {
        volumePercentage = 50;
        sfxVolumePercentage = 50;
        isFullscreen = true;
    }
}
