using UnityEngine;

// 모래 생성 입력의 출처
// 호스트 마우스와 원격 플레이 게스트처럼 입력 장치가 달라도 게임 로직은 이 형태로만 읽음
public interface IPourInputSource
{
    bool IsPressed { get; }

    // 유니티 화면 좌표. 왼쪽 아래가 원점
    Vector2 ScreenPosition { get; }
}

public sealed class LocalMouseInputSource : IPourInputSource
{
    public bool IsPressed => Input.GetMouseButton(0);
    public Vector2 ScreenPosition => Input.mousePosition;
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
// 개발용 가짜 게스트
// Steam 없이 한 PC 에서 게스트 입력을 테스트하기 위한 출처
// G 키를 누르고 있는 동안 현재 마우스 위치에 게스트가 모래를 생성하는 것으로 처리
// 스페이스나 엔터는 선택된 UI 버튼을 누르는 키라서 사용하지 않음
public sealed class DebugGuestInputSource : IPourInputSource
{
    public bool IsPressed => Input.GetKey(KeyCode.G);
    public Vector2 ScreenPosition => Input.mousePosition;
}
#endif

// 입력 출처 하나의 입력 상태와 모래 생성 허용 여부를 관리
// 입력이 막히지 않은 상태에서 새로 누른 경우에만 모래 생성을 허용
// 메뉴나 패널을 닫은 직후 또는 차례가 바뀐 직후에 버튼을 계속 누르고 있어도 모래가 생성되지 않도록 함
public sealed class PourInputSlot
{
    public IPourInputSource Source { get; private set; }

    public bool IsPressed { get; private set; }

    public bool IsArmed { get; private set; }

    // 출처가 바뀌면 이전 출처의 입력 상태를 이어받지 않도록 초기화
    // 출처가 null 이면 이 슬롯은 입력이 없는 것으로 처리
    public void SetSource(IPourInputSource source)
    {
        Source = source;
        IsPressed = false;
        IsArmed = false;
    }

    // 프레임마다 한 번만 호출
    public void Tick(bool isBlocked)
    {
        bool pressed = Source != null && Source.IsPressed;

        if (!pressed)
        {
            IsArmed = false;
        }
        else if (!IsPressed)
        {
            // 이번 프레임에 새로 누른 경우. 막혀 있지 않을 때만 허용
            IsArmed = !isBlocked;
        }
        else if (isBlocked)
        {
            // 누르고 있는 도중에 막히면 허용을 취소하고 다시 누를 때까지 허용하지 않는다
            IsArmed = false;
        }

        IsPressed = pressed;
    }
}

// 모드별 모래 생성 규칙
// Bot: Oasis 차례에 호스트와 게스트가 같이 모래를 생성하고 Mud 차례는 봇이 진행
// Versus: 해당 차례인 진영에 배정된 사람만 모래를 생성
// LocalShared: 호스트 마우스 하나로 양쪽 차례를 모두 조작. 에디터에서 씬을 바로 재생할 때만 사용
public enum PourInputMode
{
    Bot,
    Versus,
    LocalShared
}

// 차례와 모드에 따라 어느 입력 출처가 모래를 생성할 수 있는지 정한다
// 게임 로직은 이 라우터만 확인하고 입력 장치가 몇 개인지는 알 필요가 없다
public sealed class PourInputRouter
{
    public readonly PourInputSlot Host = new PourInputSlot();
    public readonly PourInputSlot Guest = new PourInputSlot();

    private PourInputMode mode = PourInputMode.Bot;

    // Versus 에서 호스트가 Oasis 진영인지 확인. 아니면 호스트가 Mud, 게스트가 Oasis
    private bool hostIsOasis = true;

    public PourInputRouter()
    {
        Host.SetSource(new LocalMouseInputSource());
    }

    public void SetMode(PourInputMode newMode, bool newHostIsOasis)
    {
        mode = newMode;
        hostIsOasis = newHostIsOasis;
    }

    // 게임 로직이 모래 생성을 막아야 하는 상태와 슬롯별 차례 여부를 합쳐서 갱신
    public void Tick(bool isBlocked, int currentPlayer)
    {
        Host.Tick(isBlocked || !CanPour(Host, currentPlayer));
        Guest.Tick(isBlocked || !CanPour(Guest, currentPlayer));
    }

    // 둘이 동시에 누르고 있으면 호스트가 우선
    public bool TryGetPouringSlot(out PourInputSlot slot)
    {
        if (Host.IsArmed)
        {
            slot = Host;
            return true;
        }

        if (Guest.IsArmed)
        {
            slot = Guest;
            return true;
        }

        slot = null;
        return false;
    }

    bool CanPour(PourInputSlot slot, int currentPlayer)
    {
        switch (mode)
        {
            case PourInputMode.Bot:
                return currentPlayer == 0;

            case PourInputMode.Versus:
                bool isHostTurn = (currentPlayer == 0) == hostIsOasis;
                return slot == (isHostTurn ? Host : Guest);

            default:
                return slot == Host;
        }
    }
}
