using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class StorySceneUI : MonoBehaviour
{
    [Header("UI References")]
    public Image backgroundImage;
    public Image portraitImage;
    public Text speakerNameText;
    public Text dialogueText;
    public GameObject namePanel;       // speakerName이 비면 숨김
    public Button skipButton;

    [Header("Typing Settings")]
    public float typingSpeed = 0.04f;  // 글자 하나당 출력 간격 (초)

    // 현재 챕터 진행 상태
    private StoryChapter currentChapter;
    private int currentLineIndex = 0;
    private bool isTyping = false;
    private Coroutine typingCoroutine = null;

    // 클릭 입력 처리: 마우스를 뗄 때 반응 (누르는 순간 X)
    private bool mouseWasDown = false;
    // EndChapter 호출 여부 - 씬 전환 중 Advance() 재진입 방지
    private bool isChapterEnding = false;

    void Start()
    {
        if (GlobalManager.Instance == null)
        {
            Debug.LogError("GlobalManager not found! Returning to TitleScene.");
            SceneManager.LoadScene("TitleScene");
            return;
        }

        currentChapter = GlobalManager.Instance.pendingStoryChapter;

        if (currentChapter == null)
        {
            Debug.LogError("pendingStoryChapter is null! Returning to SelectScene.");
            SceneManager.LoadScene("SelectScene");
            return;
        }

        if (skipButton != null)
            skipButton.onClick.AddListener(OnSkipClicked);

        // 챕터 시작 시점에 감상 완료 기록
        GlobalManager.Instance.MarkStoryAsSeen(currentChapter);

        ShowLine(0);
    }

    void Update()
    {
        // ESC: 추후 옵션 패널 연동 예정

        bool mouseDown = Input.GetMouseButton(0);

        // 마우스를 뗐을 때만 진행, 단 챕터 종료가 이미 시작됐으면 무시
        if (mouseWasDown && !mouseDown && !isChapterEnding)
        {
            Advance();
        }

        mouseWasDown = mouseDown;
    }

    // 지정 인덱스의 대사를 화면에 출력
    void ShowLine(int index)
    {
        if (currentChapter == null || index >= currentChapter.lines.Length)
        {
            EndChapter();
            return;
        }

        currentLineIndex = index;
        StoryLine line = currentChapter.lines[index];

        // 배경 교체 (null이면 이전 배경 유지)
        if (line.background != null && backgroundImage != null)
            backgroundImage.sprite = line.background;

        // 초상화 교체
        if (portraitImage != null)
        {
            if (line.portrait != null)
            {
                portraitImage.sprite = line.portrait;
                portraitImage.gameObject.SetActive(true);
            }
            else
            {
                portraitImage.gameObject.SetActive(false);
            }
        }

        // 이름창 표시
        bool hasSpeaker = !string.IsNullOrEmpty(line.speakerName);
        if (namePanel != null)
            namePanel.SetActive(hasSpeaker);
        if (speakerNameText != null)
            speakerNameText.text = hasSpeaker ? line.speakerName : "";

        // 대사 타이핑 시작
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeDialogue(line.dialogueText));
    }

    IEnumerator TypeDialogue(string fullText)
    {
        isTyping = true;

        if (dialogueText != null)
            dialogueText.text = "";

        foreach (char c in fullText)
        {
            if (dialogueText != null)
                dialogueText.text += c;

            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
        typingCoroutine = null;
    }

    // 클릭 동작: 타이핑 중이면 즉시 완성, 완성 상태면 다음 대사
    void Advance()
    {
        if (currentChapter == null) return;

        if (isTyping)
        {
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }
            isTyping = false;

            if (dialogueText != null)
                dialogueText.text = currentChapter.lines[currentLineIndex].dialogueText;
        }
        else
        {
            ShowLine(currentLineIndex + 1);
        }
    }

    // 스킵 버튼: 현재 챕터 전체 건너뜀
    void OnSkipClicked()
    {
        EndChapter();
    }

    // 챕터 종료 처리
    void EndChapter()
    {
        if (isChapterEnding) return;
        isChapterEnding = true;

        if (GlobalManager.Instance == null)
        {
            SceneManager.LoadScene("SelectScene");
            return;
        }

        GlobalManager gm = GlobalManager.Instance;

        // pendingNextScene으로 이동
        string nextScene = string.IsNullOrEmpty(gm.pendingNextScene) ? "SelectScene" : gm.pendingNextScene;
        gm.pendingNextScene = "";
        SceneManager.LoadScene(nextScene);
    }
}
