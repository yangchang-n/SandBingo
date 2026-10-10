using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 마우스가 버튼 위에 있는 동안에만 설명 문구를 띄운다
// 설명할 버튼 오브젝트에 직접 붙인다
[RequireComponent(typeof(Button))]
public class ButtonHoverCaption : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("버튼이 활성화되어 있을 때 띄우는 문구")]
    public GameObject availableCaption;
    [Tooltip("버튼이 비활성화되어 있을 때 띄우는 문구")]
    public GameObject unavailableCaption;

    private Button button;
    private bool isHovered;

    void Awake()
    {
        button = GetComponent<Button>();
        Hide();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        Show();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        Hide();
    }

    void Update()
    {
        if (isHovered)
            Show();
    }

    void OnDisable()
    {
        if (!isHovered) return;

        isHovered = false;
        Hide();
    }

    void Show()
    {
        bool interactable = button.interactable;

        SetActive(availableCaption, interactable);
        SetActive(unavailableCaption, !interactable);
    }

    void Hide()
    {
        SetActive(availableCaption, false);
        SetActive(unavailableCaption, false);
    }

    // 상태가 같으면 다시 켜고 끄지 않는다
    static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
