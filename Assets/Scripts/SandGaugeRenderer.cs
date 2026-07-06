using UnityEngine;

public class SandGaugeRenderer : MonoBehaviour
{
    [Header("Gauge Settings")]
    public int gaugeWidth = 200;
    public int gaugeHeight = 20;
    public float gaugeYOffset = 25f;

    [Header("Gauge Colors")]
    public Color emptyGaugeColor = Color.black;
    public Color gaugeBorderColor = Color.black;

    // 게이지 프레임 오브젝트 참조 (Inspector에서 직접 원하는 프레임을 넣었다 뺐다 하면서 확인 가능)
    // Initialize 시점에 생성된 게이지 크기와 위치에 맞춰 자동으로 조정된다
    [Header("Frame Overlay")]
    public SpriteRenderer gaugeFrameRenderer;

    // 프레임 테두리가 화면에서 실제로 보이길 원하는 두께이다. 단위는 게이지와 동일한 월드 유닛이다
    // 이 값이 0 이하이면 테두리 두께 보정 없이 원본 이미지를 그냥 늘린다
    public float gaugeFrameBorderThickness = 3f;

    private GameObject gaugeObject;
    private SpriteRenderer gaugeRenderer;
    private Texture2D gaugeTexture;

    // 게이지를 4등분하는 눈금 3개의 x좌표이다
    // gaugeWidth가 고정되어 있는 동안은 항상 같은 값이라 Initialize에서 한 번만 계산해둔다
    private int[] tickPositions;

    private int lastRemainingSand = -1;
    private int lastMaxSand = -1;
    private Color lastColor;

    public void Initialize(float boardHeight)
    {
        CalculateTickPositions();
        CreateGauge();
        PositionGauge(boardHeight);
        SetupGaugeFrame();
    }

    // 눈금 위치 3개를 미리 계산해서 저장한다
    // 채우기가 실제로 시작되는 구간(가장자리 2픽셀을 뺀 안쪽)을 기준으로 4등분한다
    void CalculateTickPositions()
    {
        int fillTrackWidth = gaugeWidth - 4;
        tickPositions = new int[3];

        for (int i = 0; i < 3; i++)
        {
            tickPositions[i] = 2 + Mathf.RoundToInt(fillTrackWidth * (i + 1) / 4f);
        }
    }

    void CreateGauge()
    {
        gaugeObject = new GameObject("SandGauge");
        gaugeObject.transform.SetParent(transform);
        gaugeObject.transform.localPosition = Vector3.zero;

        gaugeRenderer = gaugeObject.AddComponent<SpriteRenderer>();
        gaugeRenderer.sortingOrder = 10;

        gaugeTexture = new Texture2D(gaugeWidth, gaugeHeight);
        gaugeTexture.filterMode = FilterMode.Point;

        Sprite gaugeSprite = Sprite.Create(
            gaugeTexture,
            new Rect(0, 0, gaugeWidth, gaugeHeight),
            new Vector2(0.5f, 0.5f),
            1f
        );
        gaugeRenderer.sprite = gaugeSprite;
    }

    void PositionGauge(float boardHeight)
    {
        float boardTop = boardHeight / 2f;
        gaugeObject.transform.position = new Vector3(0, boardTop + gaugeYOffset, 0);
    }

    // 프레임 오브젝트를 실제 생성된 게이지 크기와 위치에 맞춘다
    // 프레임에 어떤 스프라이트를 쓸지는 Inspector에서 자유롭게 정하면 된다
    // 여러 크기의 프레임 이미지를 왜곡 없이 맞추기 위해 Sliced(9-slice) 방식으로 늘린다
    //
    // Sliced 방식은 전체 크기를 아무리 키워도 테두리 자체의 두께는 원본 이미지의
    // 픽셀 값과 Pixels Per Unit에 따라 고정된 월드 유닛 크기로 그려진다
    // 게이지처럼 원본 이미지보다 훨씬 넓게 늘리면 테두리가 상대적으로 매우 얇아 보이게 된다
    // 그래서 원하는 테두리 두께를 먼저 정하고 그 두께가 나오도록 스케일을 거꾸로 계산한 다음
    // 전체 크기는 스케일로 나눠서 최종 결과가 항상 게이지 크기와 맞도록 만든다
    void SetupGaugeFrame()
    {
        if (gaugeFrameRenderer == null) return;

        gaugeFrameRenderer.drawMode = SpriteDrawMode.Sliced;

        Sprite frameSprite = gaugeFrameRenderer.sprite;
        float scale = 1f;

        if (frameSprite != null && gaugeFrameBorderThickness > 0f)
        {
            Vector4 border = frameSprite.border;
            float averageBorderPixels = (border.x + border.y + border.z + border.w) / 4f;

            if (averageBorderPixels > 0f)
            {
                float spritePixelsPerUnit = frameSprite.pixelsPerUnit;
                scale = gaugeFrameBorderThickness * spritePixelsPerUnit / averageBorderPixels;
            }
        }

        // 프레임 테두리의 중심이 게이지 가장자리에 오도록 전체 크기를 테두리 두께만큼 크게 잡는다
        // 2를 빼는 이유는 직접 확인해서 맞춘 보정값이다
        float targetWidth = gaugeWidth + gaugeFrameBorderThickness - 2;
        float targetHeight = gaugeHeight + gaugeFrameBorderThickness - 2;

        gaugeFrameRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        gaugeFrameRenderer.size = new Vector2(targetWidth / scale, targetHeight / scale);
        gaugeFrameRenderer.transform.position = gaugeObject.transform.position;

        // 게이지 채우기 스프라이트(정렬 순서 10)보다 앞에 그려지도록 설정
        gaugeFrameRenderer.sortingOrder = 20;
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
        int fillWidth = Mathf.RoundToInt((gaugeWidth - 4) * fillRatio);

        for (int x = 0; x < gaugeWidth; x++)
        {
            for (int y = 0; y < gaugeHeight; y++)
            {
                gaugeTexture.SetPixel(x, y, GetPixelColor(x, y, fillWidth, playerColor));
            }
        }

        gaugeTexture.Apply();
    }

    Color GetPixelColor(int x, int y, int fillWidth, Color fillColor)
    {
        if (x == 0 || x == gaugeWidth - 1 || y == 0 || y == gaugeHeight - 1)
        {
            return gaugeBorderColor;
        }

        if (IsTickMarkPixel(x, y))
        {
            return gaugeBorderColor;
        }

        if (x >= 2 && x < fillWidth + 2)
        {
            return fillColor;
        }

        return emptyGaugeColor;
    }

    // 게이지를 4등분하는 눈금 3개를 실점선 형태로 표시하기 위한 픽셀 판정이다
    // 실점선은 짧은 선분이 일정 간격으로 끊겨있는 형태를 말한다
    // 눈금 두께는 보드의 그리드선이나 게이지 테두리와 동일하게 1픽셀로 맞춘다
    // 눈금 위치는 Initialize에서 이미 계산된 값을 그대로 사용한다
    bool IsTickMarkPixel(int x, int y)
    {
        if (y % 6 >= 3) return false;

        for (int i = 0; i < tickPositions.Length; i++)
        {
            if (x == tickPositions[i]) return true;
        }

        return false;
    }
}
