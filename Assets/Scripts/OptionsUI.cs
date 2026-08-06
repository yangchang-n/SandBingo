using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class OptionsUI : MonoBehaviour
{
    [Header("Panel")]
    public GameObject optionsPanel;

    [Header("Resolution")]
    public Dropdown resolutionDropdown;

    [Header("Audio")]
    public Slider volumeSlider;
    public InputField volumeInput;
    public Toggle muteToggle;

    [Header("Language")]
    public Button langENButton;
    public Button langKRButton;

    [Header("Buttons")]
    public Button backButton;
    public Button resetProgressButton;

    // 확인 문구 자체는 각 패널의 Text에 LocalizedText로 직접 달아두면 되므로 여기서는 관리하지 않는다
    // 언어 확인 패널의 문구는 "바꿀 언어"가 아니라 "현재 언어"를 기준으로 EN/KR 텍스트를 채워두면
    // GlobalManager의 현재 언어에 맞는 문구가 LocalizedText에 의해 자동으로 표시된다
    [Header("Language Confirmation")]
    public GameObject languageConfirmPanel;
    public Button languageConfirmButton;
    public Button languageCancelButton;

    [Header("Reset Confirmation")]
    public GameObject resetConfirmPanel;
    public Button resetConfirmButton;
    public Button resetCancelButton;

    private Resolution[] availableResolutions;

    // 아직 저장하지 않은 볼륨 값. 음수면 저장할 변경이 없다는 뜻이다
    private int pendingVolume = -1;

    void Start()
    {
        SetupResolutionDropdown();
        SetupAudioControls();
        SetupVolumeCommitTrigger();
        SetupLanguageButtons();
        SetupButtons();

        if (optionsPanel != null)
            optionsPanel.SetActive(false);

        if (languageConfirmPanel != null)
            languageConfirmPanel.SetActive(false);

        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);
    }

    void SetupResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        Resolution currentMonitor = Screen.currentResolution;

        var resolutionList = new System.Collections.Generic.List<Resolution>
        {
            new Resolution { width = 3840, height = 2160 },
            new Resolution { width = 2560, height = 1440 },
            new Resolution { width = 1920, height = 1080 },
            new Resolution { width = 1600, height = 900 },
            new Resolution { width = 1280, height = 720 }
        };

        resolutionDropdown.ClearOptions();
        var options = new System.Collections.Generic.List<string>();
        var validResolutions = new System.Collections.Generic.List<Resolution>();

        options.Add($"Fullscreen ({currentMonitor.width}x{currentMonitor.height})");
        validResolutions.Add(new Resolution { width = -1, height = -1 });

        foreach (var res in resolutionList)
        {
            if (res.width <= currentMonitor.width && res.height <= currentMonitor.height)
            {
                options.Add($"Windowed {res.width}x{res.height}");
                validResolutions.Add(res);
            }
        }

        resolutionDropdown.AddOptions(options);
        availableResolutions = validResolutions.ToArray();

        int currentIndex = 0;
        if (Screen.fullScreen)
        {
            currentIndex = 0;
        }
        else
        {
            for (int i = 1; i < validResolutions.Count; i++)
            {
                if (validResolutions[i].width == Screen.width &&
                    validResolutions[i].height == Screen.height)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        resolutionDropdown.value = currentIndex;
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    void SetupAudioControls()
    {
        if (GlobalManager.Instance == null) return;

        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0;
            volumeSlider.maxValue = 100;
            volumeSlider.wholeNumbers = true;
            volumeSlider.value = GlobalManager.Instance.GetVolumePercentage();
            volumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);
        }

        if (volumeInput != null)
        {
            volumeInput.contentType = InputField.ContentType.IntegerNumber;
            volumeInput.characterLimit = 3;
            volumeInput.text = GlobalManager.Instance.GetVolumePercentage().ToString();
            volumeInput.onEndEdit.AddListener(OnVolumeInputSubmit);
        }

        if (muteToggle != null)
        {
            muteToggle.isOn = GlobalManager.Instance.IsMuted();
            muteToggle.onValueChanged.AddListener(OnMuteToggleChanged);
        }
    }

    void SetupLanguageButtons()
    {
        if (langENButton != null)
            langENButton.onClick.AddListener(() => OnLanguageButtonClicked("EN"));

        if (langKRButton != null)
            langKRButton.onClick.AddListener(() => OnLanguageButtonClicked("KR"));
    }

    void SetupButtons()
    {
        if (backButton != null)
            backButton.onClick.AddListener(CloseOptions);

        if (resetProgressButton != null)
            resetProgressButton.onClick.AddListener(OnResetProgressClicked);

        if (languageConfirmButton != null)
            languageConfirmButton.onClick.AddListener(OnLanguageConfirmed);

        if (languageCancelButton != null)
            languageCancelButton.onClick.AddListener(OnLanguageCancelClicked);

        if (resetConfirmButton != null)
            resetConfirmButton.onClick.AddListener(OnResetConfirmed);

        if (resetCancelButton != null)
            resetCancelButton.onClick.AddListener(OnResetCancelClicked);
    }

    public void OpenOptions()
    {
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);
            RefreshAudioControls();
        }
    }

    // 확인 패널이 열린 채로 뒤로가기를 눌러도 다음에 옵션을 다시 열었을 때
    // 확인 패널이 그대로 떠 있지 않도록 여기서 같이 정리한다
    public void CloseOptions()
    {
        // 마우스를 떼지 않고 창을 닫는 경우나 키보드로 값을 바꾼 경우를 대비한 안전장치
        CommitVolume();

        if (optionsPanel != null)
            optionsPanel.SetActive(false);

        if (languageConfirmPanel != null)
            languageConfirmPanel.SetActive(false);

        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);
    }

    public bool IsOptionsOpen()
    {
        return optionsPanel != null && optionsPanel.activeSelf;
    }

    public bool HandleEscapeKey()
    {
        if (optionsPanel != null && optionsPanel.activeSelf)
        {
            CloseOptions();
            return true;
        }
        return false;
    }

    void RefreshAudioControls()
    {
        if (GlobalManager.Instance == null) return;

        int volume = GlobalManager.Instance.GetVolumePercentage();
        bool muted = GlobalManager.Instance.IsMuted();

        if (volumeSlider != null)
            volumeSlider.value = volume;

        if (volumeInput != null)
            volumeInput.text = volume.ToString();

        if (muteToggle != null)
            muteToggle.isOn = muted;
    }

    // EN/KR 버튼 클릭 시 바로 언어를 바꾸지 않고 확인 패널을 띄운다
    // 언어가 EN/KR 둘뿐이라 "확인" 시점에 현재 언어의 반대쪽으로 바꾸면 되므로
    // 어떤 버튼을 눌렀는지 따로 기억해둘 필요가 없다
    void OnLanguageButtonClicked(string code)
    {
        if (GlobalManager.Instance == null) return;
        if (GlobalManager.Instance.GetCurrentLanguage() == code) return;

        if (languageConfirmPanel != null)
            languageConfirmPanel.SetActive(true);
    }

    void OnLanguageConfirmed()
    {
        if (GlobalManager.Instance == null) return;

        string newLanguage = GlobalManager.Instance.GetCurrentLanguage() == "EN" ? "KR" : "EN";
        GlobalManager.Instance.SetLanguage(newLanguage);
        GlobalManager.Instance.LoadScene("TitleScene");
    }

    void OnLanguageCancelClicked()
    {
        if (languageConfirmPanel != null)
            languageConfirmPanel.SetActive(false);
    }

    void OnResolutionChanged(int index)
    {
        if (GlobalManager.Instance == null || index >= availableResolutions.Length) return;

        Resolution selected = availableResolutions[index];

        if (selected.width == -1)
            GlobalManager.Instance.SetResolution(0, 0, true);
        else
            GlobalManager.Instance.SetResolution(selected.width, selected.height, false);
    }

    // 슬라이더를 끄는 동안에는 소리에만 즉시 반영하고 저장은 미룬다
    // 저장은 손을 뗄 때(PointerUp)나 옵션 창을 닫을 때 한 번만 이루어진다
    void OnVolumeSliderChanged(float value)
    {
        int intValue = Mathf.RoundToInt(value);

        if (volumeInput != null)
            volumeInput.text = intValue.ToString();

        if (GlobalManager.Instance == null)
            return;

        if (GlobalManager.Instance.GetVolumePercentage() == intValue)
            return;

        GlobalManager.Instance.ApplyVolumePercentage(intValue);
        pendingVolume = intValue;
    }

    // 슬라이더에서 손을 떼는 순간을 잡기 위해 PointerUp 이벤트를 붙인다
    // 슬라이더 오브젝트에 EventTrigger 가 없으면 만들어서 사용한다
    void SetupVolumeCommitTrigger()
    {
        if (volumeSlider == null) return;

        EventTrigger trigger = volumeSlider.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = volumeSlider.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        entry.callback.AddListener(_ => CommitVolume());
        trigger.triggers.Add(entry);
    }

    // 미뤄둔 볼륨 값을 실제로 저장한다. 저장할 변경이 없으면 아무것도 하지 않는다
    void CommitVolume()
    {
        if (pendingVolume < 0) return;

        int value = pendingVolume;
        pendingVolume = -1;

        if (GlobalManager.Instance != null)
            GlobalManager.Instance.SetVolumePercentage(value);
    }

    void OnVolumeInputSubmit(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            if (GlobalManager.Instance != null && volumeInput != null)
                volumeInput.text = GlobalManager.Instance.GetVolumePercentage().ToString();
            return;
        }

        if (int.TryParse(text, out int value))
        {
            int clampedValue = Mathf.Clamp(value, 0, 100);

            if (volumeSlider != null)
                volumeSlider.value = clampedValue;

            // 숫자 입력은 값이 확정된 조작이므로 바로 저장한다
            if (GlobalManager.Instance != null)
            {
                GlobalManager.Instance.ApplyVolumePercentage(clampedValue);
                pendingVolume = clampedValue;
                CommitVolume();
            }

            if (volumeInput != null)
                volumeInput.text = clampedValue.ToString();

            if (value != clampedValue)
                Debug.Log($"Volume clamped: {value} -> {clampedValue}");
        }
        else
        {
            if (GlobalManager.Instance != null && volumeInput != null)
                volumeInput.text = GlobalManager.Instance.GetVolumePercentage().ToString();
        }
    }

    void OnMuteToggleChanged(bool isOn)
    {
        if (GlobalManager.Instance != null)
            GlobalManager.Instance.SetMute(isOn);
    }

    // 초기화 버튼 클릭 시 바로 초기화하지 않고 확인 패널을 띄운다
    void OnResetProgressClicked()
    {
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(true);
    }

    void OnResetConfirmed()
    {
        if (GlobalManager.Instance != null)
        {
            GlobalManager.Instance.ResetAllProgress();
            GlobalManager.Instance.LoadScene("TitleScene");
        }
    }

    void OnResetCancelClicked()
    {
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);
    }
}
