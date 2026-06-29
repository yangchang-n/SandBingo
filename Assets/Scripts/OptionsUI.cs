using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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

    private Resolution[] availableResolutions;

    void Start()
    {
        SetupResolutionDropdown();
        SetupAudioControls();
        SetupLanguageButtons();
        SetupButtons();

        if (optionsPanel != null)
            optionsPanel.SetActive(false);
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
    }

    public void OpenOptions()
    {
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);
            RefreshAudioControls();
        }
    }

    public void CloseOptions()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
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

    void OnLanguageButtonClicked(string code)
    {
        if (GlobalManager.Instance == null) return;

        // 이미 같은 언어면 아무것도 하지 않음
        if (GlobalManager.Instance.GetCurrentLanguage() == code) return;

        GlobalManager.Instance.SetLanguage(code);

        // 언어 변경 시 TitleScene으로 복귀
        SceneManager.LoadScene("TitleScene");
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

    void OnVolumeSliderChanged(float value)
    {
        int intValue = Mathf.RoundToInt(value);

        if (volumeInput != null)
            volumeInput.text = intValue.ToString();

        if (GlobalManager.Instance != null)
            GlobalManager.Instance.SetVolumePercentage(intValue);
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

            if (GlobalManager.Instance != null)
                GlobalManager.Instance.SetVolumePercentage(clampedValue);

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

    void OnResetProgressClicked()
    {
        if (GlobalManager.Instance != null)
        {
            GlobalManager.Instance.ResetAllProgress();
            SceneManager.LoadScene("TitleScene");
        }
    }
}
