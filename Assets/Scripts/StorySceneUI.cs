using UnityEngine;

// 스토리씬 전용 동작만 담당한다
// 대사 재생 자체는 DialogueSequenceBase 가 처리한다
public class StorySceneUI : DialogueSequenceBase
{
    void Start()
    {
        if (GlobalManager.Instance == null)
        {
            Debug.LogError("GlobalManager not found! Returning to TitleScene.");
            UnityEngine.SceneManagement.SceneManager.LoadScene("TitleScene");
            return;
        }

        StoryChapter chapter = GlobalManager.Instance.pendingStoryChapter;

        if (chapter == null)
        {
            Debug.LogError("pendingStoryChapter is null! Returning to SelectScene.");
            GlobalManager.Instance.LoadScene("SelectScene");
            return;
        }

        PlayChapter(chapter);
    }

    // 챕터가 끝나면 지정된 다음 씬으로 이동한다
    protected override void OnSequenceFinished()
    {
        if (GlobalManager.Instance == null)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("SelectScene");
            return;
        }

        GlobalManager gm = GlobalManager.Instance;
        string nextScene = string.IsNullOrEmpty(gm.pendingNextScene) ? "SelectScene" : gm.pendingNextScene;
        gm.pendingNextScene = "";
        gm.LoadScene(nextScene);
    }
}
