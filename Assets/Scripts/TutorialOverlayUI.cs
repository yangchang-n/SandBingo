using UnityEngine;
using UnityEngine.UI;

// 게임씬의 튜토리얼 패널
// 화면 전체를 덮는 반투명 레이어에 하이라이트 구멍 여러 개를 셰이더로 만든다
public class TutorialOverlayUI : MonoBehaviour
{
    // 셰이더의 _Holes 배열 크기와 같아야 함
    const int MAX_HOLES = 4;

    // 사용하지 않는 칸에 넣는 값
    static readonly Vector4 EMPTY_HOLE = new Vector4(2f, 2f, -1f, -1f);

    // 문자열로 매번 찾지 않도록 Property 번호를 한 번만 구해둔다
    static readonly int HOLES_ID = Shader.PropertyToID("_Holes");

    // 셰이더 파일 첫 줄의 이름과 같아야 함
    const string SHADER_NAME = "SandBingo/UI Hole Overlay";

    [Header("Overlay")]
    [Tooltip("패널을 가득 채우는 반투명 이미지. Source Image 는 비워두고 TutorialHoleOverlay 머티리얼을 지정한다")]
    public Image overlayImage;

    [Tooltip("구멍을 뚫을 영역. 투명한 마커 오브젝트를 영역에 맞게 배치해서 지정한다. 최대 4개")]
    public RectTransform[] holeAreas;

    [Header("Continue Arrow")]
    [Tooltip("우하단에서 위아래로 움직이는 안내 화살표. 비워두면 화살표 기능만 꺼진다")]
    public RectTransform continueArrow;
    public float arrowBobDistance = 6f;
    public float arrowBobCycles = 1.2f;

    [Header("Input")]
    [Tooltip("패널이 켜진 뒤 이 시간 동안은 클릭을 받지 않는다. 씬 페이드인 중 연속 클릭으로 바로 닫히는 것을 방지한다")]
    public float inputLockDuration = 0.5f;

    // ===== 캐시 =====

    // 머티리얼 에셋을 직접 수정하지 않도록 복사본을 한 번만 만들어서 사용
    private Material overlayMaterial;
    private RectTransform overlayRect;

    // 셰이더로 넘길 배열과 좌표 변환용 배열은 한 번만 만들고 재사용
    private readonly Vector4[] holeRects = new Vector4[MAX_HOLES];
    private readonly Vector3[] worldCorners = new Vector3[4];

    // 구멍 좌표는 레이어 기준의 비율이라 캔버스 배율이 바뀌어도 그대로
    // 패널 크기가 바뀌었거나 패널이 다시 켜졌을 때만 새로 계산
    private bool holesDirty = true;

    private Vector2 continueArrowHome;
    private float arrowElapsed = 0f;

    private bool mouseWasDown = false;

    // 패널이 켜진 뒤 흐른 시간. 입력 잠금 판정에만 사용
    private float openElapsed = 0f;

    // 잠금이 풀린 시점에 버튼이 눌려 있으면 한 번 뗄 때까지 무시
    // 잠금 중에 누르기 시작한 클릭을 떼는 것만으로 튜토리얼이 닫히지 않도록 함
    private bool waitForRelease = false;

    private bool isInitialized = false;

    // ===== 진입점 =====

    public void Show()
    {
        gameObject.SetActive(true);
    }

    // ===== 생명주기 =====

    // 패널이 꺼진 채로 시작해도 Show 에서 켜지는 순간 Awake 가 먼저 호출되므로 준비가 보장된다
    void Awake()
    {
        EnsureInitialized();
    }

    void OnEnable()
    {
        // 켜진 프레임에 바로 계산해서 순간 구멍 없이 어두운 화면으로 보이는 것을 방지한다
        // 씬 시작 직후에는 캔버스 크기가 아직 확정되지 않았을 수도 있으므로
        // LateUpdate 에서 한 번 더 계산하도록 표시해둔다
        RefreshHoles();
        holesDirty = true;

        mouseWasDown = false;
        openElapsed = 0f;
        waitForRelease = true;

        arrowElapsed = 0f;
        if (continueArrow != null)
            continueArrow.anchoredPosition = continueArrowHome;
    }

    void OnDestroy()
    {
        if (overlayMaterial != null)
            Destroy(overlayMaterial);
    }

    // 패널 루트가 화면을 가득 채우므로 해상도가 바뀌면 호출된다
    // 계산은 미루고 표시만 해두어서 한 프레임에 여러 번 호출되어도 계산은 한 번만 한다
    void OnRectTransformDimensionsChange()
    {
        holesDirty = true;
    }

    void Update()
    {
        UpdateContinueArrow();
        HandleInput();
    }

    // 모든 스크립트의 Update 가 끝나서 배치가 확정된 뒤에 구멍을 계산
    // 머티리얼 값만 바꾸므로 캔버스를 다시 빌드하지 않음
    void LateUpdate()
    {
        if (holesDirty)
            RefreshHoles();
    }

    // ===== 준비 =====

    void EnsureInitialized()
    {
        if (isInitialized) return;
        isInitialized = true;

        if (continueArrow != null)
            continueArrowHome = continueArrow.anchoredPosition;

        if (overlayImage == null)
        {
            Debug.LogWarning("TutorialOverlayUI: overlayImage is not assigned. Holes will not be drawn.");
            return;
        }

        overlayRect = overlayImage.rectTransform;

        // 배열 프로퍼티는 Properties 블록에 선언할 수 없어서 HasProperty 로 확인되지 않으므로 셰이더 이름으로 확인
        Material source = overlayImage.material;
        if (source == null || source.shader == null || source.shader.name != SHADER_NAME)
        {
            Debug.LogWarning($"TutorialOverlayUI: overlayImage material does not use {SHADER_NAME} shader.");
            return;
        }

        overlayMaterial = new Material(source);
        overlayMaterial.name = source.name + " (Instance)";
        overlayImage.material = overlayMaterial;

        if (holeAreas != null && holeAreas.Length > MAX_HOLES)
            Debug.LogWarning($"TutorialOverlayUI: only the first {MAX_HOLES} hole areas are used.");
    }

    // ===== 구멍 =====

    // 각 영역을 레이어 기준 0 ~ 1 비율로 바꿔서 셰이더에 전달한다
    // 비율이므로 캔버스 배율과 무관하다. 패널을 켤 때와 패널 크기가 바뀔 때만 계산한다
    void RefreshHoles()
    {
        holesDirty = false;

        if (overlayMaterial == null || overlayRect == null) return;

        Rect area = overlayRect.rect;

        // 레이어 크기가 아직 정해지지 않았으면 다음 프레임에 다시 시도
        if (area.width <= 0f || area.height <= 0f)
        {
            holesDirty = true;
            return;
        }

        int count = holeAreas != null ? Mathf.Min(holeAreas.Length, MAX_HOLES) : 0;

        for (int i = 0; i < MAX_HOLES; i++)
        {
            RectTransform target = i < count ? holeAreas[i] : null;
            holeRects[i] = target != null ? GetNormalizedHole(target, area) : EMPTY_HOLE;
        }

        // 배열 길이는 항상 MAX_HOLES 로 고정
        // 머티리얼 배열 크기는 처음 전달한 길이로 정해지므로 길이가 바뀌면 값이 잘린다
        overlayMaterial.SetVectorArray(HOLES_ID, holeRects);
    }

    // 대상의 네 모서리를 레이어 기준의 좌표로 옮긴 뒤 비율로 변환
    // 결과는 x 최소, y 최소, x 최대, y 최대 순서
    Vector4 GetNormalizedHole(RectTransform target, Rect area)
    {
        target.GetWorldCorners(worldCorners);

        Vector2 min = overlayRect.InverseTransformPoint(worldCorners[0]);
        Vector2 max = overlayRect.InverseTransformPoint(worldCorners[2]);

        float x0 = Mathf.Clamp01(Mathf.InverseLerp(area.xMin, area.xMax, min.x));
        float x1 = Mathf.Clamp01(Mathf.InverseLerp(area.xMin, area.xMax, max.x));
        float y0 = Mathf.Clamp01(Mathf.InverseLerp(area.yMin, area.yMax, min.y));
        float y1 = Mathf.Clamp01(Mathf.InverseLerp(area.yMin, area.yMax, max.y));

        return new Vector4(x0, y0, x1, y1);
    }

    // ===== 입력 =====

    // 버튼을 뗄 때 닫는다. 누를 때 닫으면 같은 클릭의 나머지 입력이 뒤에 있는 보드로 이어질 수 있다
    // 잠금 시간이 지나고 버튼이 눌리지 않은 상태에서 새로 누른 클릭만 닫기로 인정
    void HandleInput()
    {
        bool mouseDown = Input.GetMouseButton(0);

        // 게스트 이탈 패널의 확인 클릭으로 튜토리얼이 같이 닫히지 않도록 함
        if (GuestLeftPanel.IsOpen)
        {
            mouseWasDown = false;
            return;
        }

        if (openElapsed < inputLockDuration)
        {
            openElapsed += Time.unscaledDeltaTime;
            mouseWasDown = false;
            return;
        }

        if (waitForRelease)
        {
            if (!mouseDown) waitForRelease = false;
            mouseWasDown = false;
            return;
        }

        if (mouseWasDown && !mouseDown)
        {
            Close();
            return;
        }

        mouseWasDown = mouseDown;
    }

    // GameSceneUI 의 스테이지 진입 연출은 튜토리얼 패널이 꺼질 때까지 대기한다
    void Close()
    {
        if (GlobalManager.Instance != null)
            GlobalManager.Instance.MarkTutorialSeen();

        gameObject.SetActive(false);
        AudioManager.Play(Sfx.PaperClose);
    }

    // ===== 안내 화살표 =====

    // 패널이 켜져 있는 동안 항상 보이므로 위치만 이동시킨다
    void UpdateContinueArrow()
    {
        if (continueArrow == null) return;

        arrowElapsed += Time.deltaTime;

        Vector2 position = continueArrowHome;
        position.y -= Mathf.Sin(arrowElapsed * arrowBobCycles * Mathf.PI * 2f) * arrowBobDistance;
        continueArrow.anchoredPosition = position;
    }
}
