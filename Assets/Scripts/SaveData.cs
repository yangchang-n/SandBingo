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

    // 오디오 설정
    public int volumePercentage;
    public bool isMuted;

    // 해상도 설정
    public int screenWidth;
    public int screenHeight;
    public bool isFullscreen;

    // 기본값 생성자
    public SaveData()
    {
        stage1Cleared = false;
        stage2Cleared = false;
        stage3Cleared = false;

        stage1BestScore = 0;
        stage2BestScore = 0;
        stage3BestScore = 0;

        volumePercentage = 50;
        isMuted = false;

        screenWidth = 0;
        screenHeight = 0;
        isFullscreen = true;
    }
}