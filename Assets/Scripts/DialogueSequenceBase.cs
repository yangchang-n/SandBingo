using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 대사 챕터를 재생하는 공용 기능
// 챕터를 어디서 가져오는지와 재생이 끝난 뒤 무엇을 하는지만 자식 클래스가 정한다
// StorySceneUI 는 씬 전환, TutorialPanelUI 는 패널 닫기를 담당한다
public class DialogueSequenceBase : MonoBehaviour
{
    [Header("UI References")]
    public Image backgroundImageA;
    public Image backgroundImageB;
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
    public float backgroundFadeDuration = 0.5f;

    private StoryChapter currentChapter;
    private int currentLineIndex = 0;

    private bool isTyping = false;
    private bool isFading = false;

    // showLineCoroutine: 대사 패널 전체 흐름 담당
    // panelFadeCoroutine: 대사 패널 페이드만 별도 추적 (즉시 완성을 위해 분리)
    // backgroundFadeCoroutine: 배경 크로스페이드는 대사 패널과 독립적으로 진행된다
    private Coroutine showLineCoroutine = null;
    private Coroutine panelFadeCoroutine = null;
    private Coroutine typingCoroutine = null;
    private Coroutine backgroundFadeCoroutine = null;
    private bool isBackgroundAVisible = true;

    // 챕터에서 배경이 처음 지정되는 순간에는 이어받을 이전 그림이 없으므로
    // 그 첫 번째 지정만 페이드 없이 즉시 표시한다
    private bool hasShownFirstBackground = false;

    private bool mouseWasDown = false;
    private bool isChapterEnding = false;

    // 직전 줄의 화자와 비교해서 같으면 페이드 없이 내용만 갈아끼운다
    // 나레이션(화자 없음)도 하나의 화자 상태로 취급한다
    private string lastSpeakerKey = null;
    private bool isFirstLine = true;

    private CanvasGroup dialogueCanvasGroup;

    // 한 번만 수행하면 되는 준비 작업을 마쳤는지 표시한다
    private bool isInitialized = false;

    // ===== 자식 클래스가 사용하는 진입점 =====

    // 폰트, CanvasGroup, 스킵 버튼 연결처럼 한 번만 하면 되는 준비 작업
    // PlayChapter 안에서 자동으로 호출되므로 자식이 직접 부를 필요는 없다
    // 오브젝트 활성화 순서와 무관하게 안전하도록 중복 호출을 막아둔다
    protected void EnsureInitialized()
    {
        if (isInitialized) return;
        isInitialized = true;

        ApplyDialogueFonts();

        if (dialoguePanel != null)
            dialogueCanvasGroup = dialoguePanel.GetComponent<CanvasGroup>();

        if (dialogueCanvasGroup == null)
            Debug.LogWarning("CanvasGroup not found on dialoguePanel. Dialogue fade will not work.");

        if (skipButton != null)
            skipButton.onClick.AddListener(OnSkipClicked);
    }

    // 챕터를 처음부터 재생한다. 다시 호출하면 0번 줄부터 재시작된다
    protected void PlayChapter(StoryChapter chapter)
    {
        if (chapter == null) return;

        EnsureInitialized();

        // 재시작을 대비해 대사 흐름과 별개로 도는 배경 전환도 함께 끊는다
        if (backgroundFadeCoroutine != null)
        {
            StopCoroutine(backgroundFadeCoroutine);
            backgroundFadeCoroutine = null;
        }

        currentChapter = chapter;
        currentLineIndex = 0;
        isChapterEnding = false;
        isFirstLine = true;
        lastSpeakerKey = null;
        hasShownFirstBackground = false;
        isBackgroundAVisible = true;
        mouseWasDown = false;

        if (GlobalManager.Instance != null)
            GlobalManager.Instance.MarkStoryAsSeen(chapter);

        // 첫 줄 진입 전 대사 패널을 완전히 투명하게
        if (dialogueCanvasGroup != null)
            dialogueCanvasGroup.alpha = 0f;

        // 배경 두 장 중 A를 기본으로 보이는 상태로 초기화
        if (backgroundImageA != null) SetImageAlpha(backgroundImageA, 1f);
        if (backgroundImageB != null) SetImageAlpha(backgroundImageB, 0f);

        ShowLine(0);
    }

    // 챕터가 끝났을 때 자식이 할 일을 정의한다
    protected virtual void OnSequenceFinished() { }

    // 줄이 바뀔 때마다 호출된다. 배경 전환과 마찬가지로 대사 흐름과 독립적으로 처리된다
    // 튜토리얼의 강조 영역 갱신처럼 자식이 줄에 반응해야 할 때 사용한다
    protected virtual void OnLineChanged(int index, StoryLine line) { }

    // ===== 입력 =====

    // 자식에서 재정의할 경우 반드시 override 로 선언해야 한다
    // 같은 이름으로 새로 선언하면 메서드 숨김이 되어 호출 대상이 불명확해진다
    protected virtual void Update()
    {
        bool mouseDown = Input.GetMouseButton(0);

        if (mouseWasDown && !mouseDown && !isChapterEnding)
            Advance();

        mouseWasDown = mouseDown;
    }

    // ===== 폰트 설정 =====

    // 인물 이름과 대사 텍스트에 GlobalManager가 관리하는 대화창 전용 폰트를 적용한다
    void ApplyDialogueFonts()
    {
        GlobalManager gm = GlobalManager.Instance;
        if (gm == null) return;

        if (speakerNameText != null)
        {
            speakerNameText.font = gm.GetSpeakerNameFont();
            speakerNameText.fontSize = gm.GetSpeakerNameFontSize();
        }

        if (dialogueText != null)
        {
            dialogueText.font = gm.GetDialogueTextFont();
            dialogueText.fontSize = gm.GetDialogueTextFontSize();
        }
    }

    // ===== 줄 전환 =====

    void ShowLine(int index)
    {
        if (currentChapter == null || index >= currentChapter.lines.Length)
        {
            EndChapter();
            return;
        }

        // 진행 중인 대사 흐름 전부 중단 및 핸들 초기화
        if (showLineCoroutine != null) StopCoroutine(showLineCoroutine);
        if (panelFadeCoroutine != null) StopCoroutine(panelFadeCoroutine);
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);

        showLineCoroutine = null;
        panelFadeCoroutine = null;
        typingCoroutine = null;
        isTyping = false;
        isFading = false;

        StoryLine line = currentChapter.lines[index];

        // 배경 변경은 대사 흐름과 독립적으로 시작된다 (클릭으로 넘기는 것을 막지 않음)
        if (line.changeBackground)
            StartBackgroundCrossfade(line.background);

        OnLineChanged(index, line);

        if (dialogueCanvasGroup != null)
        {
            showLineCoroutine = StartCoroutine(ShowLineCoroutine(index, line));
        }
        else
        {
            // CanvasGroup 없으면 페이드 없이 즉시 전환
            currentLineIndex = index;

            if (line.hasDialogue)
            {
                ApplyDialogueContent(line);
                typingCoroutine = StartCoroutine(TypeDialogue(GetDialogueText(line)));
            }
        }
    }

    // 대사 패널 흐름: 화자가 바뀔 때만 페이드를 걸고, 화자가 같으면 페이드 없이 내용만 갈아끼운다
    // 나레이션(화자 없음)과 대사 없는 줄도 화자 없음이라는 하나의 상태로 취급한다
    IEnumerator ShowLineCoroutine(int index, StoryLine line)
    {
        string newSpeakerKey = GetSpeakerKey(line);
        bool shouldFade = isFirstLine || newSpeakerKey != lastSpeakerKey;
        isFirstLine = false;
        lastSpeakerKey = newSpeakerKey;

        // 화자가 바뀌는 경우에만 이전 내용을 페이드아웃
        if (shouldFade && dialogueCanvasGroup.alpha > 0.01f)
        {
            isFading = true;
            panelFadeCoroutine = StartCoroutine(FadePanelTo(0f));
            yield return panelFadeCoroutine;
            isFading = false;
        }

        // 이전 내용이 화면에서 사라진 이후에만 이 줄을 현재 줄로 취급한다
        currentLineIndex = index;

        if (!line.hasDialogue)
            yield break;

        ApplyDialogueContent(line);

        if (shouldFade)
        {
            // 페이드 인과 타이핑 동시 시작
            isFading = true;
            panelFadeCoroutine = StartCoroutine(FadePanelTo(1f));
            typingCoroutine = StartCoroutine(TypeDialogue(GetDialogueText(line)));

            yield return panelFadeCoroutine;
            isFading = false;

            // 타이핑이 페이드 인보다 길면 완료까지 대기
            if (typingCoroutine != null)
                yield return typingCoroutine;
        }
        else
        {
            // 화자가 그대로 유지되므로 패널은 이미 떠 있는 채로 내용만 즉시 갱신하고 타이핑만 진행
            if (dialogueCanvasGroup.alpha < 1f)
                dialogueCanvasGroup.alpha = 1f;

            typingCoroutine = StartCoroutine(TypeDialogue(GetDialogueText(line)));
            yield return typingCoroutine;
        }
    }

    // 이름표, 초상화, 대사 텍스트 초기화
    // 화자 이름이 비어있으면 나레이션 줄로 간주하고 이름표와 초상화를 함께 숨긴다
    void ApplyDialogueContent(StoryLine line)
    {
        string speakerName = GetSpeakerName(line);
        bool isNarration = string.IsNullOrEmpty(speakerName);

        if (namePanel != null) namePanel.SetActive(!isNarration);
        if (speakerNameText != null) speakerNameText.text = isNarration ? "" : speakerName;

        if (portraitImage != null)
        {
            bool showPortrait = !isNarration && line.portrait != null;
            portraitImage.gameObject.SetActive(showPortrait);
            if (showPortrait) portraitImage.sprite = line.portrait;
        }

        // 페이드 아웃 완료 직후 이전 텍스트가 보이지 않도록 미리 초기화
        if (dialogueText != null) dialogueText.text = "";
    }

    // ===== 대사 패널 페이드 =====

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

    // ===== 배경 크로스페이드 =====

    // 배경 이미지 두 장을 겹쳐두고 알파를 교차시켜 컷 전환 없이 배경을 바꾼다
    // 대사 패널 페이드와는 완전히 독립적으로 진행되며 클릭으로 다음 줄 넘기기를 막지 않는다
    void StartBackgroundCrossfade(Sprite newSprite)
    {
        if (backgroundImageA == null || backgroundImageB == null) return;

        if (!hasShownFirstBackground)
        {
            hasShownFirstBackground = true;
            Image target = isBackgroundAVisible ? backgroundImageA : backgroundImageB;
            target.sprite = newSprite;
            SetImageAlpha(target, 1f);
            return;
        }

        if (backgroundFadeCoroutine != null) StopCoroutine(backgroundFadeCoroutine);
        backgroundFadeCoroutine = StartCoroutine(BackgroundCrossfadeCoroutine(newSprite));
    }

    IEnumerator BackgroundCrossfadeCoroutine(Sprite newSprite)
    {
        Image incoming = isBackgroundAVisible ? backgroundImageB : backgroundImageA;
        Image outgoing = isBackgroundAVisible ? backgroundImageA : backgroundImageB;

        incoming.sprite = newSprite;

        // 진행 중이던 크로스페이드가 끝나기 전에 또 끼어들어도 튀지 않도록
        // 하드코딩된 0과 1이 아니라 현재 실제 알파값에서 시작한다
        float startIncomingAlpha = incoming.color.a;
        float startOutgoingAlpha = outgoing.color.a;

        float elapsed = 0f;
        while (elapsed < backgroundFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / backgroundFadeDuration;
            SetImageAlpha(incoming, Mathf.Lerp(startIncomingAlpha, 1f, t));
            SetImageAlpha(outgoing, Mathf.Lerp(startOutgoingAlpha, 0f, t));
            yield return null;
        }

        SetImageAlpha(incoming, 1f);
        SetImageAlpha(outgoing, 0f);
        isBackgroundAVisible = !isBackgroundAVisible;
        backgroundFadeCoroutine = null;
    }

    void SetImageAlpha(Image image, float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
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

    // 우선순위
    // 1. 대사 페이드 또는 타이핑 진행 중이면 모두 즉시 완료하고 현재 줄을 유지한다
    // 2. 아무것도 진행 중이지 않으면 다음 줄로 전환한다
    // 배경 크로스페이드는 이 판단에 영향을 주지 않는다
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

        OnSequenceFinished();
    }

    // ===== 언어 유틸 =====

    // 화자 비교용 키. 나레이션과 대사 없는 줄은 전부 null(화자 없음)로 통일한다
    string GetSpeakerKey(StoryLine line)
    {
        if (!line.hasDialogue) return null;
        string name = GetSpeakerName(line);
        return string.IsNullOrEmpty(name) ? null : name;
    }

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
