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

    [Header("Buttons")]
    public Button backButton;
    public Button resetProgressButton;

    private Resolution[] availableResolutions;

    void Start()
    {
        SetupResolutionDropdown();
        SetupAudioControls();
        SetupButtons();

        // 패널 초기 비활성화
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    // Update 제거 - ESC 처리를 외부에서 호출하도록 변경
    // void Update() { ... }

    void SetupResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        Resolution currentMonitor = Screen.currentResolution;

        // 사용 가능한 해상도 목록
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

        // 전체화면 옵션
        options.Add($"Fullscreen ({currentMonitor.width}x{currentMonitor.height})");
        validResolutions.Add(new Resolution { width = -1, height = -1 });  // Fullscreen marker

        // 현재 모니터보다 작거나 같은 해상도만 추가
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

        // 현재 해상도 선택
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

        // 슬라이더 설정
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0;
            volumeSlider.maxValue = 100;
            volumeSlider.wholeNumbers = true;  // 정수 단위로 끊김
            volumeSlider.value = GlobalManager.Instance.GetVolumePercentage();
            volumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);
        }

        // 입력창 설정
        if (volumeInput != null)
        {
            volumeInput.contentType = InputField.ContentType.IntegerNumber;
            volumeInput.characterLimit = 3;  // 최대 3자리 (100)
            volumeInput.text = GlobalManager.Instance.GetVolumePercentage().ToString();
            volumeInput.onEndEdit.AddListener(OnVolumeInputSubmit);  // 엔터 또는 포커스 잃을 때
        }

        // 음소거 토글 설정
        if (muteToggle != null)
        {
            muteToggle.isOn = GlobalManager.Instance.IsMuted();
            muteToggle.onValueChanged.AddListener(OnMuteToggleChanged);
        }
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

    /// <summary>
    /// ESC 키 처리 - Options 패널이 열려있으면 닫고 true 반환
    /// </summary>
    public bool HandleEscapeKey()
    {
        if (optionsPanel != null && optionsPanel.activeSelf)
        {
            CloseOptions();
            return true;  // ESC를 처리했음
        }
        return false;  // ESC를 처리하지 않았음
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

    void OnResolutionChanged(int index)
    {
        if (GlobalManager.Instance == null || index >= availableResolutions.Length) return;

        Resolution selected = availableResolutions[index];

        if (selected.width == -1)  // Fullscreen
        {
            GlobalManager.Instance.SetResolution(0, 0, true);
        }
        else
        {
            GlobalManager.Instance.SetResolution(selected.width, selected.height, false);
        }
    }

    void OnVolumeSliderChanged(float value)
    {
        int intValue = Mathf.RoundToInt(value);

        // 슬라이더 변경 시 즉시 적용
        if (volumeInput != null)
            volumeInput.text = intValue.ToString();

        if (GlobalManager.Instance != null)
            GlobalManager.Instance.SetVolumePercentage(intValue);
    }

    void OnVolumeInputSubmit(string text)
    {
        // 엔터 또는 포커스 잃을 때 호출
        if (string.IsNullOrEmpty(text))
        {
            // 비어있으면 현재 볼륨으로 복구
            if (GlobalManager.Instance != null && volumeInput != null)
            {
                volumeInput.text = GlobalManager.Instance.GetVolumePercentage().ToString();
            }
            return;
        }

        if (int.TryParse(text, out int value))
        {
            // 0~100 범위로 클램핑
            int clampedValue = Mathf.Clamp(value, 0, 100);

            // 슬라이더 업데이트
            if (volumeSlider != null)
                volumeSlider.value = clampedValue;

            // 볼륨 적용
            if (GlobalManager.Instance != null)
                GlobalManager.Instance.SetVolumePercentage(clampedValue);

            // 입력창에 클램핑된 값 표시
            if (volumeInput != null)
                volumeInput.text = clampedValue.ToString();

            // 클램핑되었으면 로그
            if (value != clampedValue)
            {
                Debug.Log($"Volume clamped: {value} -> {clampedValue}");
            }
        }
        else
        {
            // 정수가 아니면 현재 볼륨으로 복구
            if (GlobalManager.Instance != null && volumeInput != null)
            {
                volumeInput.text = GlobalManager.Instance.GetVolumePercentage().ToString();
            }
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