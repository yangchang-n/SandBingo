using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StorySceneUI : MonoBehaviour
{
    [Header("UI References")]
    public Image backgroundImage;
    public Image portraitImage;
    public Text speakerNameText;
    public Text dialogueText;
    public GameObject namePanel;
    public Button skipButton;

    // DialoguePanel 루트 오브젝트 - CanvasGroup 컴포넌트가 붙어 있어야 함
    public GameObject dialoguePanel;

    [Header("Typing Settings")]
    public float typingSpeed = 0.04f;

    [Header("Fade Settings")]
    public float dialogueFadeDuration = 0.25f;

    private StoryChapter currentChapter;
    private int currentLineIndex = 0;

    private bool isTyping = false;
    private bool isFading = false;

    // showLineCoroutine: 전체 흐름 (아웃 -> 교체 -> 인 + 타이핑) 담당
    // panelFadeCoroutine: 페이드만 별도 추적 (즉시 중단을 위해 분리)
    private Coroutine showLineCoroutine = null;
    private Coroutine panelFadeCoroutine = null;
    private Coroutine typingCoroutine = null;

    private bool mouseWasDown = false;
    private bool isChapterEnding = false;

    private CanvasGroup dialogueCanvasGroup;

    void Start()
    {
        if (GlobalManager.Instance == null)
        {
            Debug.LogError("GlobalManager not found! Returning to TitleScene.");
            UnityEngine.SceneManagement.SceneManager.LoadScene("TitleScene");
            return;
        }

        currentChapter = GlobalManager.Instance.pendingStoryChapter;

        if (currentChapter == null)
        {
            Debug.LogError("pendingStoryChapter is null! Returning to SelectScene.");
            GlobalManager.Instance.LoadScene("SelectScene");
            return;
        }

        if (dialoguePanel != null)
            dialogueCanvasGroup = dialoguePanel.GetComponent<CanvasGroup>();

        if (dialogueCanvasGroup == null)
            Debug.LogWarning("CanvasGroup not found on dialoguePanel. Dialogue fade will not work.");

        if (skipButton != null)
            skipButton.onClick.AddListener(OnSkipClicked);

        GlobalManager.Instance.MarkStoryAsSeen(currentChapter);

        // 첫 줄 진입 전 패널을 완전히 투명하게
        if (dialogueCanvasGroup != null)
            dialogueCanvasGroup.alpha = 0f;

        ShowLine(0);
    }

    void Update()
    {
        bool mouseDown = Input.GetMouseButton(0);

        if (mouseWasDown && !mouseDown && !isChapterEnding)
            Advance();

        mouseWasDown = mouseDown;
    }

    // ===== 대사 전환 =====

    void ShowLine(int index)
    {
        if (currentChapter == null || index >= currentChapter.lines.Length)
        {
            EndChapter();
            return;
        }

        // 진행 중인 흐름 전부 중단 및 핸들 초기화
        if (showLineCoroutine != null) StopCoroutine(showLineCoroutine);
        if (panelFadeCoroutine != null) StopCoroutine(panelFadeCoroutine);
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);

        showLineCoroutine = null;
        panelFadeCoroutine = null;
        typingCoroutine = null;
        isTyping = false;
        isFading = false;

        if (dialogueCanvasGroup != null)
        {
            showLineCoroutine = StartCoroutine(ShowLineCoroutine(index));
        }
        else
        {
            // CanvasGroup 없으면 즉시 전환
            ApplyLineContent(index);
            typingCoroutine = StartCoroutine(TypeDialogue(GetDialogueText(currentChapter.lines[index])));
        }
    }

    // 전체 줄 전환 흐름: 아웃 -> 내용 교체 -> 인 + 타이핑
    IEnumerator ShowLineCoroutine(int index)
    {
        // alpha가 거의 0이면 (첫 줄 또는 즉시완성 후 재진입 등) 아웃 스킵
        if (dialogueCanvasGroup.alpha > 0.01f)
        {
            isFading = true;
            panelFadeCoroutine = StartCoroutine(FadePanelTo(0f));
            yield return panelFadeCoroutine;
            isFading = false;
        }

        ApplyLineContent(index);

        // 페이드 인과 타이핑 동시 시작
        isFading = true;
        panelFadeCoroutine = StartCoroutine(FadePanelTo(1f));
        typingCoroutine = StartCoroutine(TypeDialogue(GetDialogueText(currentChapter.lines[index])));

        yield return panelFadeCoroutine;
        isFading = false;

        // 타이핑이 페이드 인보다 길면 완료까지 대기
        if (typingCoroutine != null)
            yield return typingCoroutine;
    }

    // 내용만 세팅 (alpha 조작 없음)
    void ApplyLineContent(int index)
    {
        currentLineIndex = index;
        StoryLine line = currentChapter.lines[index];

        if (line.background != null && backgroundImage != null)
            backgroundImage.sprite = line.background;

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

        string speakerName = GetSpeakerName(line);
        bool hasSpeaker = !string.IsNullOrEmpty(speakerName);

        if (namePanel != null) namePanel.SetActive(hasSpeaker);
        if (speakerNameText != null) speakerNameText.text = hasSpeaker ? speakerName : "";

        // 페이드 아웃 완료 직후 이전 텍스트가 보이지 않도록 미리 초기화
        if (dialogueText != null) dialogueText.text = "";
    }

    // ===== 페이드 =====

    IEnumerator FadePanelTo(float targetAlpha)
    {
        float startAlpha = dialogueCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < dialogueFadeDuration)
        {
            elapsed += Time.deltaTime;
            dialogueCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / dialogueFadeDuration);
            yield return null;
        }

        dialogueCanvasGroup.alpha = targetAlpha;
    }

    // ===== 타이핑 =====

    IEnumerator TypeDialogue(string fullText)
    {
        isTyping = true;

        foreach (char c in fullText)
        {
            if (dialogueText != null)
                dialogueText.text += c;

            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
        typingCoroutine = null;
    }

    // ===== 클릭 처리 =====

    // 우선순위:
    // 1. 페이드 또는 타이핑 진행 중 → 모두 즉시 완료, 현재 줄 유지
    // 2. 아무것도 진행 중이지 않음 → 다음 줄로 전환
    void Advance()
    {
        if (currentChapter == null) return;

        if (isFading || isTyping)
        {
            if (showLineCoroutine != null) StopCoroutine(showLineCoroutine);
            if (panelFadeCoroutine != null) StopCoroutine(panelFadeCoroutine);
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);

            showLineCoroutine = null;
            panelFadeCoroutine = null;
            typingCoroutine = null;

            if (dialogueCanvasGroup != null) dialogueCanvasGroup.alpha = 1f;
            if (dialogueText != null) dialogueText.text = GetDialogueText(currentChapter.lines[currentLineIndex]);

            isFading = false;
            isTyping = false;
        }
        else
        {
            ShowLine(currentLineIndex + 1);
        }
    }

    // ===== 스킵 =====

    void OnSkipClicked()
    {
        EndChapter();
    }

    // ===== 챕터 종료 =====

    void EndChapter()
    {
        if (isChapterEnding) return;
        isChapterEnding = true;

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

    // ===== 언어 유틸 =====

    string GetSpeakerName(StoryLine line)
    {
        if (GlobalManager.Instance.GetCurrentLanguage() == "KR")
            return !string.IsNullOrEmpty(line.speakerNameKR) ? line.speakerNameKR : line.speakerNameEN;

        return line.speakerNameEN;
    }

    string GetDialogueText(StoryLine line)
    {
        if (GlobalManager.Instance.GetCurrentLanguage() == "KR")
            return !string.IsNullOrEmpty(line.dialogueTextKR) ? line.dialogueTextKR : line.dialogueTextEN;

        return line.dialogueTextEN;
    }
}
