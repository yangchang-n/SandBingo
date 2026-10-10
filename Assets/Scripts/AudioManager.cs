using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum AudioChannel
{
    Bgm,
    Sfx
}

public enum Sfx
{
    Click,
    SoftClick,
    HardPanelOpen,
    HardPanelClose,
    PaperOpen,
    PaperClose,
    MudDrop,
    LineClear,
    Win,
    Lose
}

// 배경음악, 앰비언트, 효과음, 모래소리 루프를 재생
// 볼륨과 음소거 설정은 GlobalManager 가 저장
// 소리가 지정되지 않은 항목은 재생 없이 진행한다
public class AudioManager : MonoBehaviour
{
    // 소리 파일과 그 소리의 기본 크기. 인스펙터에서 조정한 후 옵션 볼륨과 곱해진다
    [System.Serializable]
    public class Sound
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    public static AudioManager Instance { get; private set; }

    [Header("BGM")]
    [Tooltip("타이틀, 셀렉트, 크레딧 공통. 씬들 사이를 오갈 때는 끊기지 않고 이어진다")]
    public Sound menuBgm = new Sound();
    [Tooltip("커스텀 스테이지와 Versus 는 세 곡 중 지정된 곡에서 무작위로 고른다")]
    public Sound stage1Bgm = new Sound();
    public Sound stage2Bgm = new Sound();
    public Sound stage3Bgm = new Sound();

    [Header("Ambience")]
    [Tooltip("배경음악과 같이 재생. 볼륨과 음소거는 배경음악 설정을 따름")]
    public Sound ambience = new Sound();
    [Tooltip("체크하면 게임씬에서도 끊기지 않고 계속 재생")]
    public bool ambienceInGameScene = false;

    [Header("SFX")]
    public Sound click = new Sound();
    public Sound softClick = new Sound();
    public Sound hardPanelOpen = new Sound();
    public Sound hardPanelClose = new Sound();
    public Sound paperOpen = new Sound();
    public Sound paperClose = new Sound();
    public Sound mudDrop = new Sound();
    public Sound lineClear = new Sound();
    public Sound win = new Sound();
    public Sound lose = new Sound();

    [Header("Loop")]
    [Tooltip("호스트나 게스트가 모래를 생성하는 동안 반복")]
    public Sound pourLoop = new Sound();

    // 모래 소리 루프를 켜고 끌 때의 페이드 시간. 소리가 바로 끊기지 않도록 한다
    private const float POUR_FADE_TIME = 0.08f;

    // 배경음악과 앰비언트의 페이드 시간. 곡이 바뀔 때는 줄어들 때와 커질 때 각각 적용
    private const float BGM_FADE_TIME = 0.5f;

    // 씬 로드 직후의 긴 프레임에서 페이드가 한 번에 끝나지 않도록 제한
    private const float MAX_FADE_FRAME_TIME = 0.05f;

    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private AudioSource pourSource;
    private AudioSource ambienceSource;

    private Sound currentBgm;
    private float bgmChannelVolume;
    private float sfxChannelVolume;

    private Sound nextBgm;
    private bool hasNextBgm;
    private float bgmFade;

    private bool isGameScene;
    private float ambienceFade;

    private bool isPouring;
    private float pourFade;

    private bool isActive;

    public static void Play(Sfx sfx)
    {
        if (Instance != null)
            Instance.PlaySfx(sfx);
    }

    // stage 1, 2, 3 은 각 스테이지 곡, 그 외(커스텀 0, Versus)는 지정된 스테이지 곡 중 무작위
    public static void PlayStageBgm(int stage)
    {
        if (Instance != null)
            Instance.PlayBgm(Instance.PickStageBgm(stage));
    }

    public static void SetPouring(bool pouring)
    {
        if (Instance != null)
            Instance.isPouring = pouring;
    }

    // ===== 생명주기 =====

    void Awake()
    {
        // 타이틀씬으로 돌아오면 GlobalManager 가 중복 오브젝트를 지우면서 이 컴포넌트도 같이 사라진다
        if (Instance != null && Instance != this) return;

        Instance = this;
        isActive = true;

        bgmSource = CreateSource(true);
        sfxSource = CreateSource(false);
        pourSource = CreateSource(true);
        ambienceSource = CreateSource(true);
    }

    void Start()
    {
        if (!isActive) return;

        if (GlobalManager.Instance != null)
            GlobalManager.Instance.OnAudioSettingsChanged += ApplySettings;

        SceneManager.sceneLoaded += OnSceneLoaded;

        ApplySettings();
        PlayBgmForScene(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        if (!isActive) return;

        if (GlobalManager.Instance != null)
            GlobalManager.Instance.OnAudioSettingsChanged -= ApplySettings;

        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (!isActive) return;

        // 실제 시간으로 페이드한다. 게스트 이탈 패널이 시간을 멈춰도 페이드는 계속 진행
        float bgmFadeStep = Mathf.Min(Time.unscaledDeltaTime, MAX_FADE_FRAME_TIME) / BGM_FADE_TIME;

        UpdateBgm(bgmFadeStep);

        bool ambienceOn = !isGameScene || ambienceInGameScene;
        UpdateLoop(ambienceSource, ambience, ambienceOn, bgmChannelVolume, ref ambienceFade, bgmFadeStep);

        UpdateLoop(pourSource, pourLoop, isPouring, sfxChannelVolume, ref pourFade, Time.unscaledDeltaTime / POUR_FADE_TIME);
    }

    AudioSource CreateSource(bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.loop = loop;
        source.playOnAwake = false;
        return source;
    }

    // 게임씬의 곡은 GameManager 가 모드와 난이도를 알고 나서 정하므로 여기서는 건드리지 않는다
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isPouring = false;
        PlayBgmForScene(scene.name);
    }

    void PlayBgmForScene(string sceneName)
    {
        isGameScene = sceneName == SceneNames.Game;

        if (isGameScene) return;

        PlayBgm(menuBgm);
    }

    // ===== 설정 =====

    void ApplySettings()
    {
        GlobalManager gm = GlobalManager.Instance;

        if (gm == null)
        {
            bgmChannelVolume = 0.5f;
            sfxChannelVolume = 0.5f;
            return;
        }

        bgmChannelVolume = gm.GetVolumePercentage(AudioChannel.Bgm) / 100f;
        sfxChannelVolume = gm.GetVolumePercentage(AudioChannel.Sfx) / 100f;

        bool bgmMuted = gm.IsMuted(AudioChannel.Bgm);
        bgmSource.mute = bgmMuted;
        ambienceSource.mute = bgmMuted;

        bool sfxMuted = gm.IsMuted(AudioChannel.Sfx);
        sfxSource.mute = sfxMuted;
        pourSource.mute = sfxMuted;

        sfxSource.volume = sfxChannelVolume;
    }

    // ===== 배경음악 =====

    // 같은 곡이 이미 재생 중이면 처음부터 다시 틀지 않는다
    // 다른 곡으로 바뀌는 도중에 원래 곡을 다시 요청하면 전환을 취소
    void PlayBgm(Sound sound)
    {
        if (sound == currentBgm && bgmSource.isPlaying)
        {
            hasNextBgm = false;
            return;
        }

        nextBgm = sound;
        hasNextBgm = true;
    }

    // 다른 곡을 요청하면 지금 곡을 줄인 뒤 다음 곡으로 교체
    // 인스펙터에서 기본 크기를 조절하면 재생 중에도 바로 반영되도록 매 프레임 계산한다
    void UpdateBgm(float fadeStep)
    {
        if (hasNextBgm)
        {
            if (bgmSource.isPlaying && bgmFade > 0f)
                bgmFade = Mathf.MoveTowards(bgmFade, 0f, fadeStep);
            else
                SwitchBgm();
        }
        else
        {
            bgmFade = Mathf.MoveTowards(bgmFade, 1f, fadeStep);
        }

        if (currentBgm != null)
            bgmSource.volume = bgmChannelVolume * currentBgm.volume * bgmFade;
    }

    void SwitchBgm()
    {
        currentBgm = nextBgm;
        hasNextBgm = false;
        bgmFade = 0f;

        if (currentBgm == null || currentBgm.clip == null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
            return;
        }

        bgmSource.clip = currentBgm.clip;
        bgmSource.volume = 0f;
        bgmSource.Play();
    }

    Sound PickStageBgm(int stage)
    {
        switch (stage)
        {
            case 1: return stage1Bgm;
            case 2: return stage2Bgm;
            case 3: return stage3Bgm;
        }

        List<Sound> assigned = new List<Sound>();
        if (stage1Bgm.clip != null) assigned.Add(stage1Bgm);
        if (stage2Bgm.clip != null) assigned.Add(stage2Bgm);
        if (stage3Bgm.clip != null) assigned.Add(stage3Bgm);

        if (assigned.Count == 0) return null;

        return assigned[Random.Range(0, assigned.Count)];
    }

    // ===== 효과음 =====

    void PlaySfx(Sfx sfx)
    {
        Sound sound = GetSound(sfx);
        if (sound == null || sound.clip == null) return;

        sfxSource.PlayOneShot(sound.clip, sound.volume);
    }

    Sound GetSound(Sfx sfx)
    {
        switch (sfx)
        {
            case Sfx.Click:          return click;
            case Sfx.SoftClick:      return softClick;
            case Sfx.HardPanelOpen:  return hardPanelOpen;
            case Sfx.HardPanelClose: return hardPanelClose;
            case Sfx.PaperOpen:      return paperOpen;
            case Sfx.PaperClose:     return paperClose;
            case Sfx.MudDrop:        return mudDrop;
            case Sfx.LineClear:      return lineClear;
            case Sfx.Win:            return win;
            case Sfx.Lose:           return lose;
        }
        return null;
    }

    // 모래 소리와 앰비언트 루프에 같이 사용. 켜고 끌 때 페이드하고 다 줄어들면 정지
    void UpdateLoop(AudioSource source, Sound sound, bool isOn, float channelVolume, ref float fade, float fadeStep)
    {
        float target = isOn && sound.clip != null ? 1f : 0f;
        fade = Mathf.MoveTowards(fade, target, fadeStep);

        source.volume = channelVolume * sound.volume * fade;

        if (fade > 0f && !source.isPlaying)
        {
            source.clip = sound.clip;
            source.Play();
        }
        else if (fade <= 0f && source.isPlaying)
        {
            source.Stop();
        }
    }
}
