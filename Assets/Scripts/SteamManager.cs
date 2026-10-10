// Steamworks.NET 과 같은 조건으로 Steam 코드를 끈다
// 지원하지 않는 플랫폼이거나 DISABLESTEAMWORKS 심볼이 있으면 Steam 코드가 컴파일되지 않는다
// GitHub 빌드는 빌드 메뉴가 DISABLESTEAMWORKS 를 붙여서 만든다
#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_ANDROID || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

// Steam 클라이언트와의 연결을 게임 전체에서 하나만 관리
// 게임이 시작될 때 스스로 생성되고 씬이 바뀌어도 유지되므로 씬에 배치할 필요는 없음
// 연결에 실패해도 게임은 그대로 진행되고 Steam 기능을 쓰는 코드는 IsInitialized 를 먼저 확인한다
public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance { get; private set; }

    // Steam 연결에 성공했는지 확인. Steam 코드가 빠진 빌드에서는 항상 false
    public static bool IsInitialized => Instance != null && Instance.initialized;

    private bool initialized;

    // 첫 씬이 로드되기 전에 한 번 실행
    // 에디터에서 어느 씬부터 재생해도 매니저가 먼저 생성된다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateInstance()
    {
        if (Instance != null) return;

        GameObject managerObject = new GameObject("SteamManager");
        DontDestroyOnLoad(managerObject);
        managerObject.AddComponent<SteamManager>();
    }

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Initialize();
    }

#if DISABLESTEAMWORKS

    void Initialize()
    {
        Debug.Log("[Steam] Steamworks is disabled in this build.");
    }

#else

    // Steamworks 에 등록된 앱 ID. 프로젝트 루트의 steam_appid.txt 와 같은 값
    private const uint STEAM_APP_ID = 5056380;

    void Initialize()
    {
        // 패키지가 가정한 크기와 실제 플랫폼이 다르면 모든 호출 결과가 잘못되므로 연결 전에 확인
        if (!Packsize.Test())
        {
            Debug.LogError("[Steam] Packsize test failed. Steamworks.NET does not match this platform.");
            return;
        }

#if !UNITY_EDITOR
        // exe 를 Steam 밖에서 직접 실행하면 Steam 을 통해 다시 실행하고 이 프로세스는 종료
        // exe 옆에 steam_appid.txt 가 있으면 이 검사를 건너뛴다
        if (SteamAPI.RestartAppIfNecessary(new AppId_t(STEAM_APP_ID)))
        {
            Application.Quit();
            return;
        }
#endif

        // Steam 이 꺼져 있거나 앱 아이디를 알 수 없거나 계정이 앱을 소유하지 않으면 실패
        // 에디터와 직접 실행한 exe 는 프로젝트 루트 또는 exe 옆의 steam_appid.txt 로 앱 아이디를 확인한다
        ESteamAPIInitResult result = SteamAPI.InitEx(out string errorMessage);

        if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
        {
            Debug.LogWarning($"[Steam] Init failed: {result} {errorMessage}");
            return;
        }

        initialized = true;
        Debug.Log($"[Steam] Initialized. User: {SteamFriends.GetPersonaName()}, AppId: {SteamUtils.GetAppID()}");
    }

    // Steam 이 보낸 알림은 이 호출로만 전달된다. 빠뜨리면 세션 접속이나 닉네임 갱신 알림을 받지 못함
    void Update()
    {
        if (initialized)
            SteamAPI.RunCallbacks();
    }

    void OnDestroy()
    {
        if (Instance != this) return;

        Instance = null;

        if (!initialized) return;

        initialized = false;
        SteamAPI.Shutdown();
    }

#endif
}
