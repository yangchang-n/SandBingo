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
    public GameObject namePanel;
    public Button skipButton;

    [Header("Typing Settings")]
    public float typingSpeed = 0.04f;

    private StoryChapter currentChapter;
    private int currentLineIndex = 0;
    private bool isTyping = false;
    private Coroutine typingCoroutine = null;

    private bool mouseWasDown = false;
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

        GlobalManager.Instance.MarkStoryAsSeen(currentChapter);

        ShowLine(0);
    }

    void Update()
    {
        bool mouseDown = Input.GetMouseButton(0);

        if (mouseWasDown && !mouseDown && !isChapterEnding)
            Advance();

        mouseWasDown = mouseDown;
    }

    // 현재 언어에 따라 speakerName 반환
    // KR이 비어있으면 EN으로 폴백
    string GetSpeakerName(StoryLine line)
    {
        string lang = GlobalManager.Instance != null
            ? GlobalManager.Instance.GetCurrentLanguage()
            : "EN";

        if (lang == "KR")
            return !string.IsNullOrEmpty(line.speakerNameKR) ? line.speakerNameKR : line.speakerNameEN;

        return line.speakerNameEN;
    }

    // 현재 언어에 따라 dialogueText 반환
    // KR이 비어있으면 EN으로 폴백
    string GetDialogueText(StoryLine line)
    {
        string lang = GlobalManager.Instance != null
            ? GlobalManager.Instance.GetCurrentLanguage()
            : "EN";

        if (lang == "KR")
            return !string.IsNullOrEmpty(line.dialogueTextKR) ? line.dialogueTextKR : line.dialogueTextEN;

        return line.dialogueTextEN;
    }

    void ShowLine(int index)
    {
        if (currentChapter == null || index >= currentChapter.lines.Length)
        {
            EndChapter();
            return;
        }

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

        if (namePanel != null)
            namePanel.SetActive(hasSpeaker);
        if (speakerNameText != null)
            speakerNameText.text = hasSpeaker ? speakerName : "";

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeDialogue(GetDialogueText(line)));
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

            // 즉시완성 시에도 현재 언어에 맞는 텍스트 표시
            if (dialogueText != null)
                dialogueText.text = GetDialogueText(currentChapter.lines[currentLineIndex]);
        }
        else
        {
            ShowLine(currentLineIndex + 1);
        }
    }

    void OnSkipClicked()
    {
        EndChapter();
    }

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

        string nextScene = string.IsNullOrEmpty(gm.pendingNextScene) ? "SelectScene" : gm.pendingNextScene;
        gm.pendingNextScene = "";
        SceneManager.LoadScene(nextScene);
    }
}
