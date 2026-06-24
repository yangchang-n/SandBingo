using UnityEngine;

// 챕터 하나의 데이터 (ScriptableObject로 에셋 생성)
[CreateAssetMenu(fileName = "NewStoryChapter", menuName = "SandBingo/Story Chapter")]
public class StoryChapter : ScriptableObject
{
    public StoryLine[] lines;
}
