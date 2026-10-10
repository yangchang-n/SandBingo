// Steamworks.NET 과 같은 조건으로 Steam 코드를 끈다
#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_ANDROID || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

// Remote Play Together 게스트 관리
// 접속 순서대로 큐에 넣고 맨 앞의 게스트만 모래 생성 입력 권한을 가진다
// 직접 입력 모드를 켜서 게스트 입력이 호스트 입력에 섞이지 않게 하고 권한 게스트의 입력만 골라서 전달
// 권한 게스트가 나가면 이벤트로 알린다. 화면 처리는 GuestLeftPanel 이 담당
// GlobalManager 와 같은 오브젝트에 붙인다. 이 오브젝트는 씬 전환에도 유지된다
public class RemotePlaySessionManager : MonoBehaviour
{
    public static RemotePlaySessionManager Instance { get; private set; }

    // 권한 게스트가 바뀌거나 이름 정보가 도착하면 알린다. 셀렉트씬의 이름 표시와 Versus 버튼이 구독
    public event System.Action OnActiveGuestChanged;

    // 권한 게스트가 나가면 그 게스트의 이름과 같이 알린다. 다음 게스트가 있어도 항상 알림
    public event System.Action<string> OnActiveGuestLeft;

    // 모래 생성 입력 출처. 게임씬의 게스트 슬롯이 이 객체를 계속 참조하고 권한 게스트가 바뀌어도 그대로 사용
    public IPourInputSource GuestInput { get; private set; }

    // 게스트 입력이 호스트 입력과 분리되어 있는지 확인
    public bool IsDirectInputEnabled { get; private set; }

#if DISABLESTEAMWORKS
    public bool HasActiveGuest => false;
#else
    public bool HasActiveGuest => sessions.Count > 0;
#endif

    // 권한 게스트의 입력 상태. RemoteGuestInputSource 가 읽음
    private bool guestPressed;
    private Vector2 guestNormalizedPosition = new Vector2(0.5f, 0.5f);

#if !DISABLESTEAMWORKS
    // 세션과 그 세션의 Steam 계정을 같이 저장
    // 끊긴 세션에서는 계정을 다시 가져올 수 없어서 이탈한 게스트의 이름을 띄우려면 접속할 때 미리 받아둬야 한다
    // 마지막 마우스 위치도 게스트마다 기록
    // 대기 중이던 게스트가 권한을 넘겨받았을 때 이전 게스트의 위치에 모래가 생성되지 않도록 함
    struct GuestSession
    {
        public RemotePlaySessionID_t sessionID;
        public CSteamID steamID;
        public Vector2 lastNormalizedPosition;
    }

    // 접속 순서대로 쌓인 Remote Play Together 세션. 맨 앞이 입력 권한 게스트
    private readonly List<GuestSession> sessions = new List<GuestSession>();

    // 한 프레임에 읽어오는 입력 이벤트 버퍼. 더 쌓여 있으면 반복해서 읽음
    private const int INPUT_BUFFER_SIZE = 32;
    private readonly RemotePlayInput_t[] inputBuffer = new RemotePlayInput_t[INPUT_BUFFER_SIZE];

    // 콜백 객체는 필드로 저장해둬야 가비지 컬렉션으로 사라지지 않는다
    private Callback<SteamRemotePlaySessionConnected_t> sessionConnectedCallback;
    private Callback<SteamRemotePlaySessionDisconnected_t> sessionDisconnectedCallback;
    private Callback<PersonaStateChange_t> personaStateChangeCallback;

    private bool hasWarnedRelativeMotion;

    // 색별로 한 번만 만든 게스트 커서. Steam 에 만든 커서는 게임이 끝날 때까지 유지되므로 다시 만들지 않는다
    private readonly Dictionary<Color32, RemotePlayCursorID_t> guestCursors = new Dictionary<Color32, RemotePlayCursorID_t>();

    // 문서상 매개변수 이름은 BGRA 인데 설명은 빨강 초록 파랑 알파 순서라고 적혀 있어서 서로 맞지 않는다
    // 매개변수 이름을 따랐다. 실제 테스트에서 Oasis 와 Mud 색이 서로 바뀌어 보이면 false 로 변경
    private const bool GUEST_CURSOR_BGRA = true;
#endif

    private bool isActive;

    // ===== 생명주기 =====

    void Awake()
    {
        // 타이틀씬으로 돌아오면 GlobalManager 가 중복 오브젝트를 지우면서 이 컴포넌트도 같이 사라진다
        // 그 경우에는 아무것도 초기화하지 않음
        if (Instance != null && Instance != this) return;

        Instance = this;
        isActive = true;
        GuestInput = new RemoteGuestInputSource(this);
    }

    void Start()
    {
        if (!isActive) return;

        InitializeSteam();
    }

    void OnDestroy()
    {
        if (!isActive) return;

        ShutdownSteam();

        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (!isActive) return;

        ReadGuestInput();
    }

    // ===== 초대 =====

    // 게임이 Remote Play Together 용으로 설정되어 있지 않거나 오버레이를 사용할 수 없으면 false 를 반환
    public bool ShowInviteUI()
    {
#if !DISABLESTEAMWORKS
        if (SteamManager.IsInitialized)
            return SteamRemotePlay.ShowRemotePlayTogetherUI();
#endif
        return false;
    }

    // ===== 게스트 커서 =====

    // 권한 게스트의 커서를 지정한 이미지로 변경
    // 직접 입력 모드에서만 사용 가능
    public void ApplyActiveGuestCursor(Color32[] pixels, int size, Color32 key)
    {
#if !DISABLESTEAMWORKS
        if (!IsDirectInputEnabled || !SteamManager.IsInitialized || sessions.Count == 0) return;

        if (!guestCursors.TryGetValue(key, out RemotePlayCursorID_t cursor))
        {
            cursor = CreateGuestCursor(pixels, size);
            guestCursors[key] = cursor;
        }

        RemotePlaySessionID_t session = sessions[0].sessionID;
        SteamRemotePlay.SetMouseCursor(session, cursor);
        SteamRemotePlay.SetMouseVisibility(session, true);
#endif
    }

    // 기본 커서로 되돌리는 함수는 문서에 없다. 커서 번호 0 을 넘기면 기본 커서로 돌아갈 것으로 보고 시도
    // 효과가 없으면 게임씬을 나간 뒤에도 게스트는 점 커서가 계속 보인다. 실제 테스트에서 확인 필요
    public void ResetActiveGuestCursor()
    {
#if !DISABLESTEAMWORKS
        if (!IsDirectInputEnabled || !SteamManager.IsInitialized || sessions.Count == 0) return;

        SteamRemotePlay.SetMouseCursor(sessions[0].sessionID, new RemotePlayCursorID_t(0));
#endif
    }

#if !DISABLESTEAMWORKS
    RemotePlayCursorID_t CreateGuestCursor(Color32[] pixels, int size)
    {
        byte[] bytes = new byte[size * size * 4];

        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 c = pixels[i];
            int offset = i * 4;

            bytes[offset]     = GUEST_CURSOR_BGRA ? c.b : c.r;
            bytes[offset + 1] = c.g;
            bytes[offset + 2] = GUEST_CURSOR_BGRA ? c.r : c.b;
            bytes[offset + 3] = c.a;
        }

        // Steam 이 복사를 마칠 때까지 배열이 움직이지 않도록 고정한 뒤 넘긴다
        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            int hotspot = size / 2;
            return SteamRemotePlay.CreateMouseCursor(size, size, hotspot, hotspot, handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }
#endif

    // ===== 이름 =====

    public string GetHostName(string fallback)
    {
#if !DISABLESTEAMWORKS
        if (SteamManager.IsInitialized)
        {
            string name = SteamFriends.GetPersonaName();
            if (!string.IsNullOrEmpty(name)) return name;
        }
#endif
        return fallback;
    }

    // 권한 게스트의 Steam 닉네임. 정보가 아직 도착하지 않았으면 fallback 을 반환하고 정보를 요청
    // 정보가 도착하면 OnActiveGuestChanged 로 다시 알림
    public string GetActiveGuestName(string fallback)
    {
#if !DISABLESTEAMWORKS
        if (SteamManager.IsInitialized && sessions.Count > 0)
            return GetGuestName(sessions[0].steamID, fallback);
#endif
        return fallback;
    }

#if !DISABLESTEAMWORKS
    string GetGuestName(CSteamID steamID, string fallback)
    {
        if (!steamID.IsValid()) return fallback;

        // 정보가 이미 있으면 false 를 반환하므로 매번 호출해도 중복 요청이 생기지 않는다
        if (SteamFriends.RequestUserInformation(steamID, true))
            return fallback;

        string name = SteamFriends.GetFriendPersonaName(steamID);
        return string.IsNullOrEmpty(name) ? fallback : name;
    }
#endif

    // ===== Steam 연결 =====

#if DISABLESTEAMWORKS

    void InitializeSteam() { }
    void ShutdownSteam() { }
    void ReadGuestInput() { }

#else

    void InitializeSteam()
    {
        if (!SteamManager.IsInitialized)
        {
            Debug.Log("[RemotePlay] Steam is not initialized. Remote Play is unavailable.");
            return;
        }

        TryEnableDirectInput();

        sessionConnectedCallback = Callback<SteamRemotePlaySessionConnected_t>.Create(OnSessionConnected);
        sessionDisconnectedCallback = Callback<SteamRemotePlaySessionDisconnected_t>.Create(OnSessionDisconnected);
        personaStateChangeCallback = Callback<PersonaStateChange_t>.Create(OnPersonaStateChange);

        // 이 관리자가 생기기 전에 이미 연결된 세션이 있으면 큐에 추가
        uint count = SteamRemotePlay.GetSessionCount();
        for (int i = 0; i < count; i++)
            AddSession(SteamRemotePlay.GetSessionID(i));
    }

    void ShutdownSteam()
    {
        sessionConnectedCallback?.Dispose();
        sessionDisconnectedCallback?.Dispose();
        personaStateChangeCallback?.Dispose();

        // SteamManager 가 먼저 종료되었으면 Steam 호출이 불가능하므로 건너뛴다
        if (IsDirectInputEnabled && SteamManager.IsInitialized)
            SteamRemotePlay.DisableRemotePlayTogetherDirectInput();

        IsDirectInputEnabled = false;
    }

    // 켜지 못하면 게스트 입력이 호스트 입력에 섞인다. 이 경우 Versus 를 막는다
    // 시작할 때 실패하는 조건이 문서에 나와 있지 않아서 게스트가 접속할 때 한 번 더 시도
    void TryEnableDirectInput()
    {
        if (IsDirectInputEnabled) return;

        IsDirectInputEnabled = SteamRemotePlay.BEnableRemotePlayTogetherDirectInput();
        Debug.Log($"[RemotePlay] Direct input enabled: {IsDirectInputEnabled}");
    }

    void OnSessionConnected(SteamRemotePlaySessionConnected_t callback)
    {
        TryEnableDirectInput();
        AddSession(callback.m_unSessionID);
    }

    void OnSessionDisconnected(SteamRemotePlaySessionDisconnected_t callback)
    {
        RemoveSession(callback.m_unSessionID);
    }

    // 권한 게스트의 이름 정보가 도착하면 이름을 표시하는 UI 가 갱신하도록 알림
    void OnPersonaStateChange(PersonaStateChange_t callback)
    {
        if (sessions.Count == 0) return;

        if (sessions[0].steamID.m_SteamID == callback.m_ulSteamID)
            OnActiveGuestChanged?.Invoke();
    }

    // Remote Play 에는 호스트 본인이 다른 기기로 스트리밍하는 세션도 있어서 친구가 들어온 세션만 받는다
    void AddSession(RemotePlaySessionID_t session)
    {
        if (!SteamRemotePlay.BSessionRemotePlayTogether(session)) return;
        if (FindSessionIndex(session) >= 0) return;

        GuestSession guest = new GuestSession
        {
            sessionID = session,
            steamID = SteamRemotePlay.GetSessionSteamID(session),
            lastNormalizedPosition = new Vector2(0.5f, 0.5f)
        };

        sessions.Add(guest);
        Debug.Log($"[RemotePlay] Guest connected. Session {session}, queue {sessions.Count}");

        // 이름 정보를 미리 요청해둔다. 권한이 늦게 넘어와도 바로 이름을 사용할 수 있음
        GetGuestName(guest.steamID, "");

        if (sessions.Count == 1)
        {
            guestNormalizedPosition = guest.lastNormalizedPosition;
            OnActiveGuestChanged?.Invoke();
        }
    }

    void RemoveSession(RemotePlaySessionID_t session)
    {
        int index = FindSessionIndex(session);
        if (index < 0) return;

        string leavingName = index == 0 ? GetGuestName(sessions[0].steamID, "") : "";

        sessions.RemoveAt(index);
        Debug.Log($"[RemotePlay] Guest disconnected. Session {session}, queue {sessions.Count}");

        // 권한 게스트가 아니었으면 큐에서만 빠지고 끝
        if (index != 0) return;

        // 누르던 버튼이 눌린 채로 남지 않도록 입력 상태를 초기화
        // 다음 게스트는 대기 중에 누른 버튼을 이어받지 않고 다시 눌러야 모래가 생성된다
        guestPressed = false;

        if (sessions.Count > 0)
            guestNormalizedPosition = sessions[0].lastNormalizedPosition;

        OnActiveGuestChanged?.Invoke();
        RaiseActiveGuestLeft(leavingName);
    }

    int FindSessionIndex(RemotePlaySessionID_t session)
    {
        for (int i = 0; i < sessions.Count; i++)
        {
            if (sessions[i].sessionID == session) return i;
        }
        return -1;
    }

    // 다른 게스트의 이벤트도 꺼내야 Steam 쪽 대기열이 비워진다
    void ReadGuestInput()
    {
        if (!IsDirectInputEnabled || !SteamManager.IsInitialized) return;

        uint count;
        do
        {
            count = SteamRemotePlay.GetInput(inputBuffer, INPUT_BUFFER_SIZE);

            for (int i = 0; i < count; i++)
                ApplyInputEvent(inputBuffer[i]);
        }
        while (count == INPUT_BUFFER_SIZE);
    }

    // 위치는 모든 게스트에 대해 기록하고 버튼은 권한 게스트의 것만 반영
    void ApplyInputEvent(RemotePlayInput_t input)
    {
        int index = FindSessionIndex(input.m_unSessionID);
        if (index < 0) return;

        bool isActiveGuest = index == 0;

        switch (input.m_eType)
        {
            case ERemotePlayInputType.k_ERemotePlayInputMouseMotion:
                RemotePlayInputMouseMotion_t motion = input.m_val.m_MouseMotion;

                if (motion.m_bAbsolute)
                {
                    Vector2 position = new Vector2(motion.m_flNormalizedX, motion.m_flNormalizedY);

                    GuestSession guest = sessions[index];
                    guest.lastNormalizedPosition = position;
                    sessions[index] = guest;

                    if (isActiveGuest)
                        guestNormalizedPosition = position;
                }
                else if (!hasWarnedRelativeMotion)
                {
                    // 상대 이동만 있는 경우는 확인된 적이 없어서 처리하지 않는다. 실제 테스트에서 이 경고가 보이면 대응 필요
                    hasWarnedRelativeMotion = true;
                    Debug.LogWarning("[RemotePlay] Relative mouse motion received. Only absolute motion is supported.");
                }
                break;

            case ERemotePlayInputType.k_ERemotePlayInputMouseButtonDown:
                if (isActiveGuest && input.m_val.m_eMouseButton == ERemotePlayMouseButton.k_ERemotePlayMouseButtonLeft)
                    guestPressed = true;
                break;

            case ERemotePlayInputType.k_ERemotePlayInputMouseButtonUp:
                if (isActiveGuest && input.m_val.m_eMouseButton == ERemotePlayMouseButton.k_ERemotePlayMouseButtonLeft)
                    guestPressed = false;
                break;
        }
    }

#endif

    // ===== 이탈 알림 =====

    void RaiseActiveGuestLeft(string leavingName)
    {
        if (OnActiveGuestLeft == null)
        {
            Debug.LogWarning("[RemotePlay] No GuestLeftPanel is listening. Check the GuestLeftCanvas in TitleScene.");
            return;
        }

        OnActiveGuestLeft.Invoke(leavingName);
    }

    // 실제 게스트 없이 이탈 처리를 테스트한다. 플레이 중 이 컴포넌트의 우클릭 메뉴에서 실행
    [ContextMenu("Simulate Active Guest Left (Debug)")]
    void DebugSimulateActiveGuestLeft()
    {
        guestPressed = false;
        RaiseActiveGuestLeft("TEST GUEST");
    }

    // ===== 게스트 입력 출처 =====

    // Steam 좌표는 왼쪽 위가 원점인 0에서 1 사이 비율이고 유니티 화면 좌표는 왼쪽 아래가 원점이라 세로축을 뒤집는다
    sealed class RemoteGuestInputSource : IPourInputSource
    {
        private readonly RemotePlaySessionManager manager;

        public RemoteGuestInputSource(RemotePlaySessionManager manager)
        {
            this.manager = manager;
        }

        public bool IsPressed => manager.guestPressed;

        public Vector2 ScreenPosition => new Vector2(
            manager.guestNormalizedPosition.x * Screen.width,
            (1f - manager.guestNormalizedPosition.y) * Screen.height);
    }
}
