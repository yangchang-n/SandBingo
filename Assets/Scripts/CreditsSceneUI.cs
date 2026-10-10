using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CreditsSceneUI : MonoBehaviour
{
    [Header("Scroll")]
    [Tooltip("위로 올라가는 텍스트들의 부모 오브젝트")]
    public RectTransform scrollContent;
    [Tooltip("가장 아래에 있는 텍스트")]
    public Text lastText;
    public float scrollSpeed = 40f;
    public float startDelay = 1f;
    public float restDelay = 5f;

    [Header("Link")]
    public Button linkButton;
    public string linkUrl = "";

    private RectTransform screenRect;
    private Vector2 contentHome;

    void Start()
    {
        if (linkButton != null)
            linkButton.onClick.AddListener(OnLinkClicked);

        if (scrollContent != null && lastText != null)
        {
            screenRect = (RectTransform)scrollContent.parent;
            contentHome = scrollContent.anchoredPosition;
            StartCoroutine(ScrollLoop());
        }
    }

    void Update()
    {
        // 게스트 이탈 패널의 확인 클릭이 타이틀 이동으로 이어지지 않게 한다
        if (GuestLeftPanel.IsOpen) return;

        // 씬 전환 중의 클릭은 무시되므로 효과음도 내지 않는다
        if (GlobalManager.Instance.IsTransitioning) return;

        bool clicked = Input.GetMouseButtonDown(0) && !IsPointerOverLinkButton();

        if (clicked || Input.GetKeyDown(KeyCode.Return))
        {
            AudioManager.Play(Sfx.Click);
            GlobalManager.Instance.LoadScene(SceneNames.Title);
        }
    }

    // ===== 링크 =====

    void OnLinkClicked()
    {
        AudioManager.Play(Sfx.Click);
        Application.OpenURL(linkUrl);

        // 선택된 상태로 남으면 엔터 키로도 버튼이 눌려서 링크가 다시 열림
        EventSystem.current.SetSelectedGameObject(null);
    }

    bool IsPointerOverLinkButton()
    {
        return linkButton != null && linkButton.isActiveAndEnabled
            && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)linkButton.transform, Input.mousePosition);
    }

    // ===== 스크롤 =====

    // 맨 아래 텍스트까지 화면 위로 사라지면 전체를 화면 아래로 옮기고, 원래 자리까지 올라오면 대기 후 반복
    IEnumerator ScrollLoop()
    {
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            while (GetContentBottom() < screenRect.rect.yMax)
            {
                MoveContent(scrollSpeed * Time.deltaTime);
                yield return null;
            }

            MoveContent(screenRect.rect.yMin - GetContentTop());

            while (scrollContent.anchoredPosition.y < contentHome.y)
            {
                Vector2 position = scrollContent.anchoredPosition;
                position.y = Mathf.Min(contentHome.y, position.y + scrollSpeed * Time.deltaTime);
                scrollContent.anchoredPosition = position;
                yield return null;
            }

            yield return new WaitForSeconds(restDelay);
        }
    }

    void MoveContent(float deltaY)
    {
        scrollContent.anchoredPosition += new Vector2(0f, deltaY);
    }

    float GetContentTop()
    {
        Rect rect = scrollContent.rect;
        return screenRect.InverseTransformPoint(scrollContent.TransformPoint(new Vector3(0f, rect.yMax, 0f))).y;
    }

    // 텍스트는 위쪽 정렬로 영역 밖까지 그려지므로 실제 아래 끝은 preferredHeight 로 계산
    float GetContentBottom()
    {
        RectTransform rt = lastText.rectTransform;
        float localBottom = Mathf.Min(rt.rect.yMin, rt.rect.yMax - lastText.preferredHeight);
        return screenRect.InverseTransformPoint(rt.TransformPoint(new Vector3(0f, localBottom, 0f))).y;
    }
}
