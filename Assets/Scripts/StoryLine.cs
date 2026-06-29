using UnityEngine;
using UnityEngine.Serialization;

// 대사 한 줄의 데이터
[System.Serializable]
public class StoryLine
{
    // EN 필드 - 기존 speakerName, dialogueText에서 이름 변경 (기존 .asset 데이터 자동 마이그레이션)
    [FormerlySerializedAs("speakerName")]
    public string speakerNameEN;
    [FormerlySerializedAs("dialogueText")]
    [TextArea(3, 6)]
    public string dialogueTextEN;

    // KR 필드
    public string speakerNameKR;
    [TextArea(3, 6)]
    public string dialogueTextKR;

    public Sprite portrait;
    public Sprite background;
}
