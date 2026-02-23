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

    private GameObject gaugeObject;
    private SpriteRenderer gaugeRenderer;
    private Texture2D gaugeTexture;

    private int lastRemainingSand = -1;
    private int lastMaxSand = -1;
    private Color lastColor;

    public void Initialize(float boardHeight)
    {
        CreateGauge();
        PositionGauge(boardHeight);
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

        if (x >= 2 && x < fillWidth + 2)
        {
            return fillColor;
        }

        return emptyGaugeColor;
    }
}