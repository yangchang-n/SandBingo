using UnityEngine;

// 대사 한 줄의 데이터
[System.Serializable]
public class StoryLine
{
    public string speakerName;       // 이름 표시줄에 출력될 이름 (빈 문자열이면 이름창 숨김)
    public Sprite portrait;          // 초상화 이미지 (null이면 초상화 숨김)
    public Sprite background;        // 배경 이미지 (null이면 이전 배경 유지)
    [TextArea(3, 6)]
    public string dialogueText;      // 대사 본문
}
