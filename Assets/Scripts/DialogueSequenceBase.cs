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

    // 배경 위에 서 있는 인물 판넬. 배경과 대사창 사이 순서에 두어야 한다
    // 비워두면 판넬 기능 전체가 꺼진 상태로 동작한다 (튜토리얼 패널이 이 경우에 해당한다)
    public Image characterPanelImage;

    public Image portraitImage;
    public Text speakerNameText;
    public Text dialogueText;
    public GameObject namePanel;
    public Button skipButton;

    // DialoguePanel 루트 오브젝트 - CanvasGroup 컴포넌트가 붙어 있어야 함
    public GameObject dialoguePanel;

    // 대사가 다 나와서 클릭으로 넘길 수 있다는 것을 알리는 화살표
    // 비워두면 화살표 기능만 꺼진 상태로 나머지는 정상 동작한다
    public RectTransform continueArrow;

    [Header("Typing Settings")]
    public float typingSpeed = 0.04f;

    [Header("Fade Settings")]
    public float dialogueFadeDuration = 0.25f;
    public float backgroundFadeDuration = 0.5f;
    public float characterPanelFadeDuration = 0.3f;

    // 판넬이 유지되는 줄에서 화자가 판넬 인물과 같을 때 한 번 움찔하는 연출
    // 아래로 먼저 움직인다. 위로 먼저 움직이면 판넬 아래쪽 빈 공간이 드러난다
    [Header("Nudge Settings")]
    public float panelNudgeDistance = 12f;
    public float panelNudgeDuration = 0.12f;

    // 화살표가 원위치를 기준으로 위아래로 움직이는 거리와 초당 왕복 횟수
    [Header("Continue Arrow Settings")]
    public float arrowBobDistance = 6f;
    public float arrowBobCycles = 1.2f;

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

    // 캐릭터 판넬도 배경과 마찬가지로 대사 흐름과 독립적으로 진행된다
    private Coroutine characterPanelCoroutine = null;
    private Coroutine nudgeCoroutine = null;

    // 현재 떠 있는 판넬 인물의 첫 글자. 판넬이 없으면 null
    // 화자와 비교할 때마다 스프라이트 이름을 다시 파싱하지 않도록 갱신 시점에만 계산해둔다
    private string currentPanelKey = null;

    // 움찔의 기준이 되는 원래 위치. EnsureInitialized 에서 한 번만 기록하고 이후 갱신하지 않는다
    private RectTransform characterPanelRect = null;
    private Vector2 characterPanelHome;

    // 화살표도 같은 이유로 원위치를 한 번만 기록한다
    // arrowElapsed 는 화살표가 다시 켜질 때마다 0으로 되돌려 항상 같은 지점에서 시작하게 한다
    private Vector2 continueArrowHome;
    private float arrowElapsed = 0f;

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

        // 움찔의 기준 위치를 여기서 한 번만 잡는다
        // 움찔로 어긋난 상태에서 다시 기록하면 그 위치가 새 기준이 되어 판넬이 계속 밀린다
        if (characterPanelImage != null)
        {
            characterPanelRect = characterPanelImage.rectTransform;
            characterPanelHome = characterPanelRect.anchoredPosition;
        }

        // 화살표도 같은 이유로 원위치를 여기서 한 번만 잡는다
        if (continueArrow != null)
            continueArrowHome = continueArrow.anchoredPosition;

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

        ResetCharacterPanel();

        // Update 가 첫 판정을 하기 전에 화살표가 한 프레임 비치는 것을 막는다
        HideContinueArrow();

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
        UpdateContinueArrow();

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

        // 다음 줄로 넘어가는 순간 진행 중이던 움찔은 무시하고 원위치로 되돌린다
        StopNudge();

        StoryLine line = currentChapter.lines[index];

        // 배경 변경은 대사 흐름과 독립적으로 시작된다 (클릭으로 넘기는 것을 막지 않음)
        if (line.changeBackground)
            StartBackgroundCrossfade(line.background);

        // 판넬 변경도 같은 원칙으로 독립 진행한다
        if (line.changePanel)
            StartCharacterPanelTransition(line.characterPanel);

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
                TryNudgeCharacterPanel(line);
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
        TryNudgeCharacterPanel(line);

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
    // 화자가 화면의 판넬과 같은 인물이면 초상화가 중복이므로 판넬 쪽에 맡기고 초상화는 숨긴다
    void ApplyDialogueContent(StoryLine line)
    {
        string speakerName = GetSpeakerName(line);
        bool isNarration = string.IsNullOrEmpty(speakerName);

        if (namePanel != null) namePanel.SetActive(!isNarration);
        if (speakerNameText != null) speakerNameText.text = isNarration ? "" : speakerName;

        if (portraitImage != null)
        {
            bool showPortrait = !isNarration && line.portrait != null && !IsSpeakerOnPanel(line);
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

    // ===== 캐릭터 판넬 =====

    // 판넬은 없는 상태가 정상이므로 배경처럼 두 장을 겹칠 필요가 없다
    // 한 장으로 페이드아웃, 스프라이트 교체, 페이드인 순서를 밟는다
    void StartCharacterPanelTransition(Sprite newSprite)
    {
        if (characterPanelImage == null) return;

        // 이 줄의 초상화 판정이 새 판넬을 기준으로 이뤄져야 하므로
        // 페이드 완료를 기다리지 않고 즉시 갱신한다
        currentPanelKey = GetFirstLetterKey(newSprite != null ? newSprite.name : null);

        // 판넬이 바뀌는 줄에서는 움찔하지 않는다. 진행 중이던 것도 원위치로 되돌린다
        StopNudge();

        if (characterPanelCoroutine != null) StopCoroutine(characterPanelCoroutine);
        characterPanelCoroutine = StartCoroutine(CharacterPanelTransitionCoroutine(newSprite));
    }

    IEnumerator CharacterPanelTransitionCoroutine(Sprite newSprite)
    {
        // 보이는 판넬이 있으면 먼저 지운다. 교체든 제거든 항상 이 단계를 거친다
        if (characterPanelImage.color.a > 0.01f)
            yield return FadeCharacterPanelTo(0f);

        characterPanelImage.sprite = newSprite;

        // 필드가 비어 있으면 제거이므로 페이드인 없이 끝낸다
        if (newSprite != null)
            yield return FadeCharacterPanelTo(1f);

        characterPanelCoroutine = null;
    }

    // 배경 크로스페이드와 같은 이유로 현재 실제 알파값에서 시작한다
    // 빠르게 넘겨서 전환이 겹쳐도 튀지 않는다
    IEnumerator FadeCharacterPanelTo(float targetAlpha)
    {
        float startAlpha = characterPanelImage.color.a;
        float elapsed = 0f;

        while (elapsed < characterPanelFadeDuration)
        {
            elapsed += Time.deltaTime;
            SetImageAlpha(characterPanelImage, Mathf.Lerp(startAlpha, targetAlpha, elapsed / characterPanelFadeDuration));
            yield return null;
        }

        SetImageAlpha(characterPanelImage, targetAlpha);
    }

    // 챕터 재시작 시 판넬을 완전히 초기 상태로 되돌린다
    // 스토리씬은 씬이 새로 로드되지만 튜토리얼 패널은 같은 오브젝트를 재사용한다
    void ResetCharacterPanel()
    {
        if (characterPanelCoroutine != null)
        {
            StopCoroutine(characterPanelCoroutine);
            characterPanelCoroutine = null;
        }

        StopNudge();
        currentPanelKey = null;

        if (characterPanelImage != null)
        {
            characterPanelImage.sprite = null;
            SetImageAlpha(characterPanelImage, 0f);
        }
    }

    // ===== 움찔 =====

    // 세 조건이 모두 맞을 때만 움찔한다
    // 1. 화자가 판넬 인물과 같을 것
    // 2. 이번 줄에서 판넬이 바뀌지 않을 것 (등장, 교체, 제거는 모두 페이드만 한다)
    // 3. 판넬이 실제로 떠 있을 것 (IsSpeakerOnPanel 이 판넬 없음을 걸러준다)
    void TryNudgeCharacterPanel(StoryLine line)
    {
        if (line.changePanel) return;
        if (!IsSpeakerOnPanel(line)) return;

        StartNudge();
    }

    void StartNudge()
    {
        if (characterPanelRect == null) return;

        StopNudge();
        nudgeCoroutine = StartCoroutine(NudgeCoroutine());
    }

    // 움찔은 알파가 아니라 위치를 다루므로 중단만 하면 어긋난 좌표에 그대로 멈춘다
    // 정지와 원위치 복귀를 한 곳에 묶어두어 어느 경로에서든 빠뜨리지 않게 한다
    void StopNudge()
    {
        if (nudgeCoroutine != null)
        {
            StopCoroutine(nudgeCoroutine);
            nudgeCoroutine = null;
        }

        if (characterPanelRect != null)
            characterPanelRect.anchoredPosition = characterPanelHome;
    }

    IEnumerator NudgeCoroutine()
    {
        float half = panelNudgeDuration * 0.5f;
        float downY = characterPanelHome.y - panelNudgeDistance;

        yield return MoveCharacterPanelY(characterPanelHome.y, downY, half);
        yield return MoveCharacterPanelY(downY, characterPanelHome.y, half);

        characterPanelRect.anchoredPosition = characterPanelHome;
        nudgeCoroutine = null;
    }

    IEnumerator MoveCharacterPanelY(float fromY, float toY, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            Vector2 position = characterPanelRect.anchoredPosition;
            position.y = Mathf.Lerp(fromY, toY, elapsed / duration);
            characterPanelRect.anchoredPosition = position;

            yield return null;
        }

        Vector2 end = characterPanelRect.anchoredPosition;
        end.y = toY;
        characterPanelRect.anchoredPosition = end;
    }

    // ===== 넘김 안내 화살표 =====

    // 매 프레임 표시 여부를 다시 판정한다
    // 줄 전환, 즉시 완성, 스킵, 챕터 종료가 모두 서로 다른 경로로 상태를 바꾸므로
    // 각 경로에서 켜고 끄는 것보다 결과 상태만 보고 판단하는 편이 빠뜨릴 여지가 없다
    void UpdateContinueArrow()
    {
        // 원위치는 EnsureInitialized 에서만 기록되므로 그 전에는 위치를 건드리지 않는다
        // 여기서 먼저 옮겨두면 나중에 기록되는 원위치가 옮겨진 좌표가 되어버린다
        if (!isInitialized) return;
        if (continueArrow == null) return;

        if (!IsWaitingForAdvance())
        {
            HideContinueArrow();
            return;
        }

        if (!continueArrow.gameObject.activeSelf)
        {
            // 다시 켜질 때마다 항상 같은 지점에서 시작하게 한다
            arrowElapsed = 0f;
            continueArrow.gameObject.SetActive(true);
        }

        arrowElapsed += Time.deltaTime;

        // 아래로 먼저 움직인다. 아래를 가리키는 화살표이므로 내려가는 쪽이 먼저 읽힌다
        Vector2 position = continueArrowHome;
        position.y -= Mathf.Sin(arrowElapsed * arrowBobCycles * Mathf.PI * 2f) * arrowBobDistance;
        continueArrow.anchoredPosition = position;
    }

    // 화살표도 움찔과 마찬가지로 위치를 다루므로 끄는 것과 원위치 복귀를 한 곳에 묶어둔다
    void HideContinueArrow()
    {
        if (continueArrow == null) return;

        // 이미 꺼져 있으면 매 프레임 같은 값을 다시 쓰지 않는다
        if (!continueArrow.gameObject.activeSelf) return;

        continueArrow.anchoredPosition = continueArrowHome;
        continueArrow.gameObject.SetActive(false);
    }

    // 클릭이 다음 줄로 이어지는 상태인지를 뜻한다
    // 타이핑이나 페이드 중에도 클릭은 유효하지만 그때는 다음 줄이 아니라 즉시 완성이므로 띄우지 않는다
    bool IsWaitingForAdvance()
    {
        if (currentChapter == null) return false;
        if (isChapterEnding) return false;

        return !isTyping && !isFading;
    }

    // ===== 화자와 판넬 인물 비교 =====

    // 지금 말하는 인물이 화면의 판넬과 같은 인물인지 판정한다
    // 같으면 대사창 초상화를 숨기고 판넬이 대신 움찔해서 말하는 주체를 나타낸다
    bool IsSpeakerOnPanel(StoryLine line)
    {
        if (currentPanelKey == null) return false;
        if (!line.hasDialogue) return false;

        string speakerKey = GetFirstLetterKey(line.speakerNameEN);
        return speakerKey != null && speakerKey == currentPanelKey;
    }

    // 판넬 파일명과 영어 화자명은 모두 T(Tessa) 또는 P(Piper)로 시작한다
    // 한국어 화자명은 첫 글자가 달라지므로 반드시 EN 쪽을 기준으로 비교해야 한다
    string GetFirstLetterKey(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value.Substring(0, 1).ToUpperInvariant();
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
    // 배경 크로스페이드와 판넬 페이드는 이 판단에 영향을 주지 않는다
    // 움찔은 위치를 다루므로 중단할 때 반드시 원위치까지 되돌린다
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

            StopNudge();

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
