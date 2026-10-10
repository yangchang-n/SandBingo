using UnityEngine;

// 게임씬에서 원격 플레이 게스트가 있을 때만 호스트와 게스트의 커서를 진영 색 점으로 변경
// 게스트가 없거나 게임씬을 벗어나면 호스트 커서는 기본 화살표로 돌아간다
// 점 이미지는 GameManager 의 진영 색으로 직접 만들어서 별도 에셋이 필요 없음
// 게임씬의 아무 오브젝트에나 붙여서 사용
public class PlayerCursorController : MonoBehaviour
{
    // 커서 이미지의 한 변 픽셀 수. 홀수여야 가운데 픽셀이 생겨서 상하좌우 대칭이 된다
    private const int DOT_SIZE = 13;

    // 채워지는 원의 반지름. 바깥 한 픽셀에 테두리가 둘러져서 테두리 포함 지름은 11픽셀
    // 반지름을 정수에 가깝게 두면 상하좌우 끝이 한 픽셀씩 더 튀어나와서 소수로 둔다
    private const float CIRCLE_RADIUS = 4.3f;

    private static readonly Color32 OUTLINE_COLOR = new Color32(0, 0, 0, 255);
    private static readonly Color32 CLEAR_COLOR = new Color32(0, 0, 0, 0);

    // 게스트 커서에 지정하는 투명 이미지
    // 커서 숨김 기능을 쓰면 마우스 입력이 상대 이동으로 바뀔 가능성이 있어서 투명 이미지로 대신함
    private static readonly Color32[] HIDDEN_CURSOR_PIXELS = new Color32[DOT_SIZE * DOT_SIZE];

    private GameManager gameManager;
    private RemotePlaySessionManager session;

    private Texture2D oasisTexture;
    private Texture2D mudTexture;
    private Texture2D guestTexture;

    private bool isApplied;

    void Start()
    {
        gameManager = GameManager.Instance;
        session = RemotePlaySessionManager.Instance;

        if (gameManager == null) return;

        oasisTexture = CreateTexture(CreateDotPixels(gameManager.GetPlayerColor(0)));
        mudTexture = CreateTexture(CreateDotPixels(gameManager.GetPlayerColor(1)));

        if (session != null)
            session.OnActiveGuestChanged += Refresh;

        Refresh();
    }

    // 게임씬을 나갈 때 커서를 원래대로 되돌린다. 다른 씬에서는 커서를 바꾸지 않음
    void OnDestroy()
    {
        if (session != null)
            session.OnActiveGuestChanged -= Refresh;

        Restore();

        if (oasisTexture != null) Destroy(oasisTexture);
        if (mudTexture != null) Destroy(mudTexture);
    }

    // 게스트의 점 커서는 호스트 화면에 직접 그려서 영상으로 같이 송출
    // 게스트 기기에서의 커서는 동시에 보이면 딜레이로 점이 두 개처럼 보일 수 있어서 투명한 이미지로 대신한다
    void OnGUI()
    {
        if (!isApplied || Event.current.type != EventType.Repaint) return;
        if (session == null || !session.HasActiveGuest || !session.IsDirectInputEnabled) return;

        // GUI 좌표는 왼쪽 위가 원점이라 세로축을 반전한다
        Vector2 position = session.GuestInput.ScreenPosition;
        float x = Mathf.Round(position.x) - DOT_SIZE / 2;
        float y = Mathf.Round(Screen.height - position.y) - DOT_SIZE / 2;

        GUI.DrawTexture(new Rect(x, y, DOT_SIZE, DOT_SIZE), guestTexture);
    }

    void Refresh()
    {
        if (!ShouldApply())
        {
            Restore();
            return;
        }

        bool hostIsOasis = gameManager.HostIsOasis;

        // 하드웨어 커서는 운영체제가 시스템 커서 크기에 맞춰 늘려서 그리므로 커지고 흐릿해진다
        // 소프트웨어 커서는 유니티가 픽셀 그대로 그리므로 정한 크기와 모양이 유지된다
        Cursor.SetCursor(hostIsOasis ? oasisTexture : mudTexture, new Vector2(DOT_SIZE / 2, DOT_SIZE / 2), CursorMode.ForceSoftware);

        // 봇전에서는 둘 다 Oasis 팀이지만 구분을 위해 게스트는 Mud 색 사용
        guestTexture = hostIsOasis ? mudTexture : oasisTexture;

        if (session != null)
            session.ApplyActiveGuestCursor(HIDDEN_CURSOR_PIXELS, DOT_SIZE, CLEAR_COLOR);

        isApplied = true;
    }

    bool ShouldApply()
    {
        if (gameManager == null) return false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 개발용 가짜 게스트가 켜져 있으면 실제 게스트 없이도 호스트 커서 모양을 확인할 수 있음
        if (gameManager.debugFakeGuest) return true;
#endif

        return session != null && session.HasActiveGuest;
    }

    void Restore()
    {
        if (!isApplied) return;

        // null 을 넘기면 기본 커서로 돌아간다
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        if (session != null)
            session.ResetActiveGuestCursor();

        isApplied = false;
    }

    // 채우는 영역을 먼저 정하고 그 영역과 상하좌우로 맞닿은 빈 픽셀을 테두리로 칠한다
    // 거리만으로 테두리를 정하면 작은 크기에서 테두리가 끊기거나 두꺼워지는데 이 방식은 항상 한 픽셀 두께로 이어진다
    static Color32[] CreateDotPixels(Color32 fill)
    {
        bool[] isFill = new bool[DOT_SIZE * DOT_SIZE];
        int center = DOT_SIZE / 2;

        for (int y = 0; y < DOT_SIZE; y++)
        {
            for (int x = 0; x < DOT_SIZE; x++)
                isFill[y * DOT_SIZE + x] = IsInsideFill(x - center, y - center);
        }

        Color32[] pixels = new Color32[DOT_SIZE * DOT_SIZE];

        for (int y = 0; y < DOT_SIZE; y++)
        {
            for (int x = 0; x < DOT_SIZE; x++)
            {
                int index = y * DOT_SIZE + x;

                if (isFill[index])
                    pixels[index] = fill;
                else if (IsFillAt(isFill, x + 1, y) || IsFillAt(isFill, x - 1, y) || IsFillAt(isFill, x, y + 1) || IsFillAt(isFill, x, y - 1))
                    pixels[index] = OUTLINE_COLOR;
                else
                    pixels[index] = CLEAR_COLOR;
            }
        }

        return pixels;
    }

    static bool IsInsideFill(int dx, int dy)
    {
        return dx * dx + dy * dy <= CIRCLE_RADIUS * CIRCLE_RADIUS;
    }

    static bool IsFillAt(bool[] isFill, int x, int y)
    {
        if (x < 0 || x >= DOT_SIZE || y < 0 || y >= DOT_SIZE) return false;
        return isFill[y * DOT_SIZE + x];
    }

    // 커서 텍스처는 읽기 가능한 RGBA32 형식이어야 한다
    // 프로젝트가 linear 색 공간이라 일반 텍스처로 만들면 소프트웨어 커서가 실제 색보다 어둡게 그려진다
    // 색 변환을 거치지 않는 linear 텍스처로 만들어서 지정한 색이 그대로 나오게 함
    static Texture2D CreateTexture(Color32[] pixels)
    {
        Texture2D texture = new Texture2D(DOT_SIZE, DOT_SIZE, TextureFormat.RGBA32, false, true);
        texture.filterMode = FilterMode.Point;
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }
}
