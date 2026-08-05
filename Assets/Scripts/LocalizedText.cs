using UnityEngine;
using UnityEngine.UI;

// 각 Text 오브젝트에 부착하는 다국어 컴포넌트
// - EN/KR 텍스트와 폰트 크기를 Inspector에서 직접 입력
// - 폰트는 GlobalManager에서 언어별로 공유
// - 언어 변경 시 GlobalManager 이벤트를 받아 자동 갱신
[RequireComponent(typeof(Text))]
public class LocalizedText : MonoBehaviour
{
    [Header("Text")]
    [TextArea(2, 5)]
    public string textEN;
    [TextArea(2, 5)]
    public string textKR;

    [Header("Font Size (0 = 원본 크기 유지)")]
    public int fontSizeEN = 0;
    public int fontSizeKR = 0;

    private Text _text;
    private string _originalText;
    private int _originalFontSize;

    void Awake()
    {
        _text = GetComponent<Text>();
        // 컴포넌트 부착 당시의 원본 값 저장 (비어있거나 0인 필드의 폴백용)
        _originalText = _text.text;
        _originalFontSize = _text.fontSize;
    }

    void OnEnable()
    {
        if (GlobalManager.Instance != null)
            GlobalManager.Instance.OnLanguageChanged += Apply;

        // 비활성 상태에서 언어가 바뀐 채로 활성화될 수 있으므로 즉시 적용
        Apply();
    }

    void OnDisable()
    {
        if (GlobalManager.Instance != null)
            GlobalManager.Instance.OnLanguageChanged -= Apply;
    }

    void Apply()
    {
        if (_text == null) return;

        string lang = GlobalManager.Instance != null
            ? GlobalManager.Instance.GetCurrentLanguage()
            : "EN";

        ApplyText(lang);
        ApplyFont();
        ApplyFontSize(lang);
    }

    void ApplyText(string lang)
    {
        if (lang == "KR")
        {
            // KR 텍스트가 있으면 사용, 없으면 EN으로 폴백, EN도 없으면 원본 유지
            if (!string.IsNullOrEmpty(textKR))
                _text.text = textKR;
            else if (!string.IsNullOrEmpty(textEN))
                _text.text = textEN;
            else
                _text.text = _originalText;
        }
        else
        {
            // EN 텍스트가 있으면 사용, 없으면 원본 유지
            if (!string.IsNullOrEmpty(textEN))
                _text.text = textEN;
            else
                _text.text = _originalText;
        }
    }

    void ApplyFont()
    {
        if (GlobalManager.Instance == null) return;

        Font font = GlobalManager.Instance.GetCurrentFont();
        // null이면 폰트 변경 없이 기존 폰트 유지
        if (font != null)
            _text.font = font;
    }

    void ApplyFontSize(string lang)
    {
        if (lang == "KR")
        {
            // KR 크기가 지정되어 있으면 사용, 0이면 EN 크기로 폴백, EN도 0이면 원본 크기 유지
            if (fontSizeKR > 0)
                _text.fontSize = fontSizeKR;
            else if (fontSizeEN > 0)
                _text.fontSize = fontSizeEN;
            else
                _text.fontSize = _originalFontSize;
        }
        else
        {
            // EN 크기가 지정되어 있으면 사용, 0이면 원본 크기 유지
            if (fontSizeEN > 0)
                _text.fontSize = fontSizeEN;
            else
                _text.fontSize = _originalFontSize;
        }
    }
}
