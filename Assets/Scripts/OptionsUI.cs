using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class OptionsUI : MonoBehaviour
{
    [Header("Panel")]
    public GameObject optionsPanel;

    [Header("Resolution")]
    public Dropdown resolutionDropdown;

    // 배경음악 쪽 필드는 예전 이름을 그대로 써서 씬의 연결을 유지한다
    [Header("Audio - BGM")]
    public Slider volumeSlider;
    public InputField volumeInput;
    public Toggle muteToggle;

    [Header("Audio - SFX")]
    public Slider sfxVolumeSlider;
    public InputField sfxVolumeInput;
    public Toggle sfxMuteToggle;

    [Header("Buttons")]
    public Button backButton;
    public Button resetProgressButton;

    [Header("Reset Confirmation")]
    public GameObject resetConfirmPanel;
    public Button resetConfirmButton;
    public Button resetCancelButton;

    private Resolution[] availableResolutions;

    private VolumeRow bgmRow;
    private VolumeRow sfxRow;

    void Start()
    {
        SetupResolutionDropdown();
        bgmRow = new VolumeRow(AudioChannel.Bgm, volumeSlider, volumeInput, muteToggle);
        sfxRow = new VolumeRow(AudioChannel.Sfx, sfxVolumeSlider, sfxVolumeInput, sfxMuteToggle);
        SetupButtons();

        if (optionsPanel != null)
            optionsPanel.SetActive(false);

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

    void SetupButtons()
    {
        if (backButton != null)
            backButton.onClick.AddListener(CloseOptions);

        if (resetProgressButton != null)
            resetProgressButton.onClick.AddListener(OnResetProgressClicked);

        if (resetConfirmButton != null)
            resetConfirmButton.onClick.AddListener(OnResetConfirmed);

        if (resetCancelButton != null)
            resetCancelButton.onClick.AddListener(OnResetCancelClicked);
    }

    public void OpenOptions()
    {
        if (optionsPanel == null || optionsPanel.activeSelf) return;

        optionsPanel.SetActive(true);
        bgmRow.Refresh();
        sfxRow.Refresh();

        AudioManager.Play(Sfx.HardPanelOpen);
    }

    // 확인 패널이 열린 채로 뒤로가기를 눌러도 옵션을 다시 열었을 때
    // 확인 패널이 남아 있지 않도록 여기서 같이 닫는다
    public void CloseOptions()
    {
        // 마우스를 떼지 않고 창을 닫거나 키보드로 값을 바꾼 경우를 대비
        bgmRow.Commit();
        sfxRow.Commit();

        if (optionsPanel != null && optionsPanel.activeSelf)
        {
            optionsPanel.SetActive(false);
            AudioManager.Play(Sfx.HardPanelClose);
        }

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

    void OnResolutionChanged(int index)
    {
        if (GlobalManager.Instance == null || index >= availableResolutions.Length) return;

        Resolution selected = availableResolutions[index];

        if (selected.width == -1)
            GlobalManager.Instance.SetResolution(0, 0, true);
        else
            GlobalManager.Instance.SetResolution(selected.width, selected.height, false);

        AudioManager.Play(Sfx.SoftClick);
    }

    void OnResetProgressClicked()
    {
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(true);

        AudioManager.Play(Sfx.PaperOpen);
    }

    void OnResetConfirmed()
    {
        AudioManager.Play(Sfx.PaperClose);

        if (GlobalManager.Instance != null)
        {
            GlobalManager.Instance.ResetAllProgress();
            GlobalManager.Instance.LoadScene(SceneNames.Title);
        }
    }

    void OnResetCancelClicked()
    {
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);

        AudioManager.Play(Sfx.PaperClose);
    }

    // 한 채널의 볼륨 슬라이더, 숫자 입력칸, 음소거 토글을 묶어서 관리
    // 슬라이더를 움직이는 동안에는 소리에만 바로 반영하고 저장은 손을 뗄 때나 옵션 창을 닫을 때 한 번만 한다
    sealed class VolumeRow
    {
        private readonly AudioChannel channel;
        private readonly Slider slider;
        private readonly InputField input;
        private readonly Toggle muteToggle;

        // 아직 저장하지 않은 볼륨 값. 음수면 저장할 변경이 없음
        private int pendingVolume = -1;

        public VolumeRow(AudioChannel channel, Slider slider, InputField input, Toggle muteToggle)
        {
            this.channel = channel;
            this.slider = slider;
            this.input = input;
            this.muteToggle = muteToggle;

            SetupControls();
            SetupCommitTrigger();
        }

        void SetupControls()
        {
            GlobalManager gm = GlobalManager.Instance;
            if (gm == null) return;

            if (slider != null)
            {
                slider.minValue = 0;
                slider.maxValue = 100;
                slider.wholeNumbers = true;
                slider.value = gm.GetVolumePercentage(channel);
                slider.onValueChanged.AddListener(OnSliderChanged);
            }

            if (input != null)
            {
                input.contentType = InputField.ContentType.IntegerNumber;
                input.characterLimit = 3;
                input.text = gm.GetVolumePercentage(channel).ToString();
                input.onEndEdit.AddListener(OnInputSubmit);
            }

            if (muteToggle != null)
            {
                muteToggle.isOn = gm.IsMuted(channel);
                muteToggle.onValueChanged.AddListener(OnMuteChanged);
            }
        }

        // 슬라이더에서 손을 떼는 순간을 알기 위해 PointerUp 이벤트를 추가
        void SetupCommitTrigger()
        {
            if (slider == null) return;

            EventTrigger trigger = slider.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = slider.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            entry.callback.AddListener(_ => Commit());
            trigger.triggers.Add(entry);
        }

        public void Refresh()
        {
            GlobalManager gm = GlobalManager.Instance;
            if (gm == null) return;

            int volume = gm.GetVolumePercentage(channel);

            if (slider != null)
                slider.value = volume;

            if (input != null)
                input.text = volume.ToString();

            if (muteToggle != null)
                muteToggle.isOn = gm.IsMuted(channel);
        }

        public void Commit()
        {
            if (pendingVolume < 0) return;

            int value = pendingVolume;
            pendingVolume = -1;

            if (GlobalManager.Instance != null)
                GlobalManager.Instance.SetVolumePercentage(channel, value);
        }

        void OnSliderChanged(float value)
        {
            int intValue = Mathf.RoundToInt(value);

            if (input != null)
                input.text = intValue.ToString();

            GlobalManager gm = GlobalManager.Instance;
            if (gm == null || gm.GetVolumePercentage(channel) == intValue) return;

            gm.ApplyVolumePercentage(channel, intValue);
            pendingVolume = intValue;
        }

        // 숫자 입력은 값이 확정된 것이므로 바로 저장한다. 값이 실제로 바뀌었을 때만 효과음 재생
        void OnInputSubmit(string text)
        {
            GlobalManager gm = GlobalManager.Instance;

            if (string.IsNullOrEmpty(text) || !int.TryParse(text, out int value))
            {
                if (gm != null && input != null)
                    input.text = gm.GetVolumePercentage(channel).ToString();
                return;
            }

            int clampedValue = Mathf.Clamp(value, 0, 100);
            int previousValue = gm != null ? gm.GetVolumePercentage(channel) : clampedValue;

            if (slider != null)
                slider.value = clampedValue;

            if (gm != null)
            {
                gm.ApplyVolumePercentage(channel, clampedValue);
                pendingVolume = clampedValue;
                Commit();
            }

            if (input != null)
                input.text = clampedValue.ToString();

            if (value != clampedValue)
                Debug.Log($"Volume clamped: {value} -> {clampedValue}");

            if (clampedValue != previousValue)
                AudioManager.Play(Sfx.SoftClick);
        }

        void OnMuteChanged(bool isOn)
        {
            if (GlobalManager.Instance != null)
                GlobalManager.Instance.SetMute(channel, isOn);

            AudioManager.Play(Sfx.SoftClick);
        }
    }
}
