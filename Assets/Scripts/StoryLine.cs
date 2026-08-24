using UnityEngine;
using UnityEngine.Serialization;

// 대사 한 줄의 데이터
// hasDialogue, changeBackground, changePanel, changeBgm 네 체크박스가 각 채널의 반영 여부를 결정한다
// 체크가 꺼진 채널은 이 줄에서 완전히 무시되고 이전 상태가 그대로 유지된다
// changeBgm과 bgm 필드는 데이터 스키마만 미리 추가해둔 상태이며 재생 로직은 아직 연결되어 있지 않다
[System.Serializable]
public class StoryLine
{
    [Header("Dialogue")]
    public bool hasDialogue = true;

    // EN 필드 - 기존 speakerName, dialogueText에서 이름 변경 (기존 asset 데이터 자동 마이그레이션)
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

    [Header("Background")]
    public bool changeBackground = false;
    public Sprite background;

    // 배경 위에 서 있는 인물 판넬
    // changePanel이 꺼져 있으면 현재 판넬이 그대로 유지된다
    // 켜져 있고 characterPanel에 값이 있으면 그 판넬로 교체하고, 비어 있으면 현재 판넬을 제거한다
    // 판넬 파일명의 첫 글자로 인물을 판정하므로 T(Tessa) 또는 P(Piper)로 시작하는 이름을 써야 한다
    [Header("Character Panel")]
    public bool changePanel = false;
    public Sprite characterPanel;

    [Header("BGM")]
    // 아직 재생 로직이 연결되어 있지 않음, 추후 GlobalManager 작업에서 연결 예정
    public bool changeBgm = false;
    public AudioClip bgm;
}
