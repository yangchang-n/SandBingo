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

    // 기본값 생성자
    public SaveData()
    {
        stage1Cleared = false;
        stage2Cleared = false;
        stage3Cleared = false;

        stage1BestScore = 0;
        stage2BestScore = 0;
        stage3BestScore = 0;

        stage1PreSeen = false;
        stage1TutorialSeen = false;
        stage1PostSeen = false;
        stage2PreSeen = false;
        stage2PostSeen = false;
        stage3PreSeen = false;
        stage3PostSeen = false;

        volumePercentage = 50;
        isMuted = false;

        screenWidth = 0;
        screenHeight = 0;
        isFullscreen = true;
    }
}
