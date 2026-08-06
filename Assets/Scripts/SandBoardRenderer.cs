using UnityEngine;
using System.Collections.Generic;

public class SandBoardRenderer : MonoBehaviour
{
    // Grid Info
    private int width;
    private int height;
    private int gridSize;
    private int cellPixelSize;

    // Main Texture (background + sand)
    private Texture2D mainTexture;
    private SpriteRenderer mainRenderer;

    // Grid Lines
    private GameObject gridLinesObject;
    private Texture2D gridLinesTexture;
    private SpriteRenderer gridLinesRenderer;

    // Clickable Area Border
    private GameObject clickableBorderObject;
    private Texture2D clickableBorderTexture;
    private SpriteRenderer clickableBorderRenderer;

    // Ownership Texts (O/X)
    private TextMesh[,] ownershipTexts;
    private SandSimulator.CellOwnership[,] previousOwnership;

    // Clickable Area boundaries
    private int clickableMinX;
    private int clickableMaxX;
    private int clickableMinY;
    private int clickableMaxY;

    // 플레이어 색상 (GameManager에서 가져옴)
    private Color skyColor;
    private Color brownColor;

    // 보드 색상 설정 (Inspector에서 설정)
    [Header("Board Colors")]
    public Color boardBackgroundColor = new Color(0xDE / 255f, 0x9E / 255f, 0x4A / 255f);
    public Color clickableAreaColor = new Color(0xFF / 255f, 0xD7 / 255f, 0x98 / 255f);
    public Color gridLineColor = Color.black;
    public Color wallColor = new Color(0f, 0f, 0f, 0f);

    [Header("Clickable Area Visuals")]
    public float borderThickness = 2f;
    public Color borderColor = Color.white;

    [Header("Ownership Text Settings")]
    public Color ownershipTextColor = new Color(1f, 1f, 1f, 0.8f);
    public int ownershipCharacterSize = 100;
    public int ownershipFontSize = 14;

    // 보드 프레임 오브젝트 참조 (Inspector에서 원하는 프레임을 넣었다 뺐다 하면서 확인 가능)
    // Initialize 시점에 생성된 보드 크기에 맞춰 자동으로 크기와 위치가 조정된다
    [Header("Frame Overlay")]
    public SpriteRenderer boardFrameRenderer;

    // 프레임 테두리가 화면에서 실제로 보이길 원하는 두께이다. 단위는 보드와 동일한 월드 유닛이다
    // 이 값이 0 이하이면 테두리 두께 보정 없이 원본 이미지를 그냥 늘린다
    public float boardFrameBorderThickness = 4f;

    public void Initialize(int w, int h, int gs, int cps,
                          int cMinX, int cMaxX, int cMinY, int cMaxY)
    {
        width = w;
        height = h;
        gridSize = gs;
        cellPixelSize = cps;

        clickableMinX = cMinX;
        clickableMaxX = cMaxX;
        clickableMinY = cMinY;
        clickableMaxY = cMaxY;

        // 플레이어 색상은 GameManager에서 가져옴
        skyColor = GameManager.Instance.skyColor;
        brownColor = GameManager.Instance.brownColor;

        previousOwnership = new SandSimulator.CellOwnership[gridSize, gridSize];
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                previousOwnership[x, y] = SandSimulator.CellOwnership.None;
            }
        }

        SetupMainRenderer();
        SetupGridLines();
        SetupClickableAreaVisuals();
        CreateOwnershipTexts();
        SetupBoardFrame();

    }

    void SetupMainRenderer()
    {
        mainTexture = new Texture2D(width, height);
        mainTexture.filterMode = FilterMode.Point;

        mainRenderer = gameObject.AddComponent<SpriteRenderer>();
        mainRenderer.sortingOrder = 0;

        Sprite sprite = Sprite.Create(
            mainTexture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            1f
        );
        mainRenderer.sprite = sprite;
    }

    void SetupGridLines()
    {
        gridLinesObject = new GameObject("GridLines");
        gridLinesObject.transform.SetParent(transform);
        gridLinesObject.transform.localPosition = Vector3.zero;

        gridLinesRenderer = gridLinesObject.AddComponent<SpriteRenderer>();
        gridLinesRenderer.sortingOrder = 5;

        gridLinesTexture = new Texture2D(width, height);
        gridLinesTexture.filterMode = FilterMode.Point;

        Color transparent = new Color(0, 0, 0, 0);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                gridLinesTexture.SetPixel(x, y, transparent);
            }
        }

        DrawGridLinesOnce();

        Sprite gridSprite = Sprite.Create(
            gridLinesTexture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            1f
        );
        gridLinesRenderer.sprite = gridSprite;
    }

    void DrawGridLinesOnce()
    {
        // 세로선
        for (int i = 0; i <= gridSize; i++)
        {
            int x = 1 + i * cellPixelSize;
            for (int y = 1; y < height; y++)
            {
                gridLinesTexture.SetPixel(x, y, gridLineColor);
            }
        }

        // 가로선
        for (int i = 0; i <= gridSize; i++)
        {
            int y = 1 + i * cellPixelSize;
            if (y >= height) break;

            for (int x = 1; x < width - 1; x++)
            {
                gridLinesTexture.SetPixel(x, y, gridLineColor);
            }
        }

        gridLinesTexture.Apply();
    }

    void SetupClickableAreaVisuals()
    {
        clickableBorderObject = new GameObject("ClickableBorder");
        clickableBorderObject.transform.SetParent(transform);
        clickableBorderObject.transform.localPosition = Vector3.zero;

        clickableBorderRenderer = clickableBorderObject.AddComponent<SpriteRenderer>();
        clickableBorderRenderer.sortingOrder = 6;

        clickableBorderTexture = new Texture2D(width, height);
        clickableBorderTexture.filterMode = FilterMode.Point;

        DrawClickableBorder();

        Sprite borderSprite = Sprite.Create(
            clickableBorderTexture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            1f
        );
        clickableBorderRenderer.sprite = borderSprite;
    }

    void DrawClickableBorder()
    {
        Color transparent = new Color(0, 0, 0, 0);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                clickableBorderTexture.SetPixel(x, y, transparent);
            }
        }

        int thickness = Mathf.RoundToInt(borderThickness);

        // 상단 선 (한 픽셀 위쪽)
        for (int t = 0; t < thickness; t++)
        {
            int y = clickableMaxY + 1 - t;
            if (y >= 0 && y < height)
            {
                for (int x = clickableMinX; x <= clickableMaxX; x++)
                {
                    if (x >= 0 && x < width)
                    {
                        clickableBorderTexture.SetPixel(x, y, borderColor);
                    }
                }
            }
        }

        // 하단 선
        for (int t = 0; t < thickness; t++)
        {
            int y = clickableMinY + t;
            if (y >= 0 && y < height)
            {
                for (int x = clickableMinX; x <= clickableMaxX; x++)
                {
                    if (x >= 0 && x < width)
                    {
                        clickableBorderTexture.SetPixel(x, y, borderColor);
                    }
                }
            }
        }

        // 좌측 선
        for (int t = 0; t < thickness; t++)
        {
            int x = clickableMinX + t;
            if (x >= 0 && x < width)
            {
                for (int y = clickableMinY; y <= clickableMaxY; y++)
                {
                    if (y >= 0 && y < height)
                    {
                        clickableBorderTexture.SetPixel(x, y, borderColor);
                    }
                }
            }
        }

        // 우측 선
        for (int t = 0; t < thickness; t++)
        {
            int x = clickableMaxX - t;
            if (x >= 0 && x < width)
            {
                for (int y = clickableMinY; y <= clickableMaxY; y++)
                {
                    if (y >= 0 && y < height)
                    {
                        clickableBorderTexture.SetPixel(x, y, borderColor);
                    }
                }
            }
        }

        clickableBorderTexture.Apply();
    }

    void CreateOwnershipTexts()
    {
        ownershipTexts = new TextMesh[gridSize, gridSize];

        GameObject textsParent = new GameObject("OwnershipTexts");
        textsParent.transform.SetParent(transform);
        textsParent.transform.localPosition = Vector3.zero;

        for (int cellX = 0; cellX < gridSize; cellX++)
        {
            for (int cellY = 0; cellY < gridSize; cellY++)
            {
                GameObject textObj = new GameObject($"Text_{cellX}_{cellY}");
                textObj.transform.SetParent(textsParent.transform);

                TextMesh textMesh = textObj.AddComponent<TextMesh>();

                textMesh.text = "";
                textMesh.characterSize = ownershipCharacterSize;
                textMesh.fontSize = ownershipFontSize;
                textMesh.color = ownershipTextColor;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.fontStyle = FontStyle.Bold;

                MeshRenderer meshRenderer = textObj.GetComponent<MeshRenderer>();
                meshRenderer.sortingOrder = 10;

                float pixelCenterX = 1 + cellX * cellPixelSize + cellPixelSize / 2f;
                float pixelCenterY = 1 + cellY * cellPixelSize + cellPixelSize / 2f;

                float worldX = pixelCenterX - width / 2f;
                float worldY = pixelCenterY - height / 2f;

                textObj.transform.position = new Vector3(worldX, worldY, -1f);
                textObj.transform.localScale = Vector3.one * 0.1f;

                ownershipTexts[cellX, cellY] = textMesh;
            }
        }

    }

    // 프레임 오브젝트를 실제 생성된 보드 크기에 맞춰 늘리고 보드 중앙에 위치시킨다
    // 프레임에 어떤 스프라이트를 쓸지는 Inspector에서 자유롭게 정하면 된다
    // 여러 크기의 프레임 이미지를 왜곡 없이 맞추기 위해 Sliced(9-slice) 방식으로 늘린다
    //
    // Sliced 방식은 전체 크기를 아무리 키워도 테두리 자체의 두께는 원본 이미지의
    // 픽셀 값과 Pixels Per Unit에 따라 고정된 월드 유닛 크기로 그려진다
    // 보드처럼 원본 이미지보다 훨씬 큰 크기로 늘리면 테두리가 상대적으로 매우 얇아 보이게 된다
    // 그래서 원하는 테두리 두께를 먼저 정하고 그 두께가 나오도록 스케일을 거꾸로 계산한 다음
    // 전체 크기는 스케일로 나눠서 최종 결과가 항상 보드 크기와 맞도록 만든다
    void SetupBoardFrame()
    {
        if (boardFrameRenderer == null) return;

        boardFrameRenderer.drawMode = SpriteDrawMode.Sliced;

        Sprite frameSprite = boardFrameRenderer.sprite;
        float scale = 1f;

        if (frameSprite != null && boardFrameBorderThickness > 0f)
        {
            Vector4 border = frameSprite.border;
            float averageBorderPixels = (border.x + border.y + border.z + border.w) / 4f;

            if (averageBorderPixels > 0f)
            {
                float spritePixelsPerUnit = frameSprite.pixelsPerUnit;
                scale = boardFrameBorderThickness * spritePixelsPerUnit / averageBorderPixels;
            }
        }

        // 프레임 테두리의 중심이 보드 가장자리에 오도록 전체 크기를 테두리 두께만큼 크게 잡는다
        // 2를 빼는 이유는 직접 확인해서 맞춘 보정값이다
        float targetWidth = width + boardFrameBorderThickness - 2;
        float targetHeight = height + boardFrameBorderThickness - 2;

        boardFrameRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        boardFrameRenderer.size = new Vector2(targetWidth / scale, targetHeight / scale);
        boardFrameRenderer.transform.position = transform.position;

        // 보드의 다른 요소들(그리드선 5, 테두리 6, 소유권 텍스트 10)보다 앞에 그려지도록 설정
        boardFrameRenderer.sortingOrder = 20;
    }

    public void DrawBackground(HashSet<Vector2Int> dirtyPixels, SandSimulator.CellType[,] grid)
    {
        foreach (Vector2Int pixel in dirtyPixels)
        {
            int x = pixel.x;
            int y = pixel.y;

            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                Color color = GetPixelColor(x, y, grid);
                mainTexture.SetPixel(x, y, color);
            }
        }
    }

    Color GetPixelColor(int x, int y, SandSimulator.CellType[,] grid)
    {
        // 캐시된 색상 사용
        if (grid[x, y] != SandSimulator.CellType.Empty)
        {
            return grid[x, y] switch
            {
                SandSimulator.CellType.SkySand => skyColor,
                SandSimulator.CellType.BrownSand => brownColor,
                SandSimulator.CellType.Wall => wallColor,
                _ => boardBackgroundColor
            };
        }

        bool isInClickableArea = x >= clickableMinX && x <= clickableMaxX &&
                                  y > clickableMinY && y <= clickableMaxY;
        return isInClickableArea ? clickableAreaColor : boardBackgroundColor;
    }

    public void UpdateOwnershipTexts(SandSimulator.CellOwnership[,] ownership)
    {
        for (int cellX = 0; cellX < gridSize; cellX++)
        {
            for (int cellY = 0; cellY < gridSize; cellY++)
            {
                if (ownership[cellX, cellY] != previousOwnership[cellX, cellY])
                {
                    TextMesh textMesh = ownershipTexts[cellX, cellY];

                    switch (ownership[cellX, cellY])
                    {
                        case SandSimulator.CellOwnership.Sky:
                            textMesh.text = "O";
                            textMesh.gameObject.SetActive(true);
                            break;
                        case SandSimulator.CellOwnership.Brown:
                            textMesh.text = "X";
                            textMesh.gameObject.SetActive(true);
                            break;
                        case SandSimulator.CellOwnership.None:
                            textMesh.gameObject.SetActive(false);
                            break;
                    }

                    previousOwnership[cellX, cellY] = ownership[cellX, cellY];
                }
            }
        }
    }

    public void ApplyTexture()
    {
        mainTexture.Apply();
    }

    public void ResetOwnershipTexts()
    {
        for (int cellX = 0; cellX < gridSize; cellX++)
        {
            for (int cellY = 0; cellY < gridSize; cellY++)
            {
                ownershipTexts[cellX, cellY].gameObject.SetActive(false);
                previousOwnership[cellX, cellY] = SandSimulator.CellOwnership.None;
            }
        }
    }
}
