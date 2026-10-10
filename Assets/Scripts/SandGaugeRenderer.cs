using UnityEngine;

public class SandGaugeRenderer : MonoBehaviour
{
    // 이 컴포넌트는 실행 중에 GameManager 가 생성한다
    private const int GAUGE_WIDTH = 200;
    private const int GAUGE_HEIGHT = 20;
    private const float GAUGE_Y_OFFSET = 25f;

    private static readonly Color EMPTY_GAUGE_COLOR = Color.black;
    private static readonly Color GAUGE_BORDER_COLOR = Color.black;

    // 채우기가 시작되는 안쪽 여백. 바깥 테두리 1픽셀과 그 안쪽 빈 줄 1픽셀
    private const int FILL_MARGIN = 2;

    // 4등분 눈금의 점선 모양
    private const int TICK_DASH_PERIOD = 6;
    private const int TICK_DASH_LENGTH = 3;

    // 화면에 보이는 프레임 테두리 두께. 단위는 게이지와 같은 월드 유닛
    // 0 이하이면 두께 보정 없이 원본 이미지를 그대로 늘린다
    private const float GAUGE_FRAME_BORDER_THICKNESS = 3f;

    private const int SORTING_GAUGE = 10;
    private const int SORTING_FRAME = 20;

    // 게이지 프레임 스프라이트. GameManager 가 이 컴포넌트를 생성한 직후 넣어준다
    public SpriteRenderer gaugeFrameRenderer;

    private GameObject gaugeObject;
    private SpriteRenderer gaugeRenderer;
    private Texture2D gaugeTexture;

    // 게이지 전체 픽셀을 한 번에 반영하기 위한 버퍼. 모래를 생성하는 동안 매 프레임 사용하므로 한 번만 만든다
    // 순서는 SetPixels 규칙대로 아래 줄부터, 줄 안에서는 왼쪽부터
    private Color[] pixelBuffer;

    // 게이지를 4등분하는 눈금 3개의 x좌표. 초기화할 때 한 번만 계산
    private int[] tickPositions;

    private int lastRemainingSand = -1;
    private int lastMaxSand = -1;
    private Color lastColor;

    public void Initialize(float boardHeight)
    {
        CalculateTickPositions();
        CreateGauge();
        PositionGauge(boardHeight);
        SlicedFrameFitter.Fit(gaugeFrameRenderer, GAUGE_WIDTH, GAUGE_HEIGHT, gaugeObject.transform.position, GAUGE_FRAME_BORDER_THICKNESS, SORTING_FRAME);
    }

    // 여백을 빼고 채워진 구간을 기준으로 4등분
    void CalculateTickPositions()
    {
        int fillTrackWidth = GAUGE_WIDTH - FILL_MARGIN * 2;
        tickPositions = new int[3];

        for (int i = 0; i < 3; i++)
        {
            tickPositions[i] = FILL_MARGIN + Mathf.RoundToInt(fillTrackWidth * (i + 1) / 4f);
        }
    }

    void CreateGauge()
    {
        gaugeObject = new GameObject("SandGauge");
        gaugeObject.transform.SetParent(transform);
        gaugeObject.transform.localPosition = Vector3.zero;

        gaugeRenderer = gaugeObject.AddComponent<SpriteRenderer>();
        gaugeRenderer.sortingOrder = SORTING_GAUGE;

        gaugeTexture = new Texture2D(GAUGE_WIDTH, GAUGE_HEIGHT);
        gaugeTexture.filterMode = FilterMode.Point;
        pixelBuffer = new Color[GAUGE_WIDTH * GAUGE_HEIGHT];

        Sprite gaugeSprite = Sprite.Create(
            gaugeTexture,
            new Rect(0, 0, GAUGE_WIDTH, GAUGE_HEIGHT),
            new Vector2(0.5f, 0.5f),
            1f
        );
        gaugeRenderer.sprite = gaugeSprite;
    }

    void PositionGauge(float boardHeight)
    {
        float boardTop = boardHeight / 2f;
        gaugeObject.transform.position = new Vector3(0, boardTop + GAUGE_Y_OFFSET, 0);
    }

    public void UpdateGaugeIfNeeded(int remainingSand, int maxSand, Color playerColor)
    {
        if (remainingSand != lastRemainingSand ||
            maxSand != lastMaxSand ||
            playerColor != lastColor)
        {
            UpdateGauge(remainingSand, maxSand, playerColor);
            lastRemainingSand = remainingSand;
            lastMaxSand = maxSand;
            lastColor = playerColor;
        }
    }

    public void ForceUpdate(int remainingSand, int maxSand, Color playerColor)
    {
        lastRemainingSand = -1;
        UpdateGaugeIfNeeded(remainingSand, maxSand, playerColor);
    }

    void UpdateGauge(int remainingSand, int maxSand, Color playerColor)
    {
        float fillRatio = (float)remainingSand / maxSand;
        int fillWidth = Mathf.RoundToInt((GAUGE_WIDTH - FILL_MARGIN * 2) * fillRatio);

        for (int y = 0; y < GAUGE_HEIGHT; y++)
        {
            for (int x = 0; x < GAUGE_WIDTH; x++)
            {
                pixelBuffer[y * GAUGE_WIDTH + x] = GetPixelColor(x, y, fillWidth, playerColor);
            }
        }

        gaugeTexture.SetPixels(pixelBuffer);
        gaugeTexture.Apply();
    }

    Color GetPixelColor(int x, int y, int fillWidth, Color fillColor)
    {
        if (x == 0 || x == GAUGE_WIDTH - 1 || y == 0 || y == GAUGE_HEIGHT - 1)
        {
            return GAUGE_BORDER_COLOR;
        }

        if (IsTickMarkPixel(x, y))
        {
            return GAUGE_BORDER_COLOR;
        }

        if (x >= FILL_MARGIN && x < fillWidth + FILL_MARGIN)
        {
            return fillColor;
        }

        return EMPTY_GAUGE_COLOR;
    }

    bool IsTickMarkPixel(int x, int y)
    {
        if (y % TICK_DASH_PERIOD >= TICK_DASH_LENGTH) return false;

        for (int i = 0; i < tickPositions.Length; i++)
        {
            if (x == tickPositions[i]) return true;
        }

        return false;
    }
}
