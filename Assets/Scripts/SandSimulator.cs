using UnityEngine;
using System.Collections.Generic;

public class SandSimulator : MonoBehaviour
{
    // Grid Settings
    private int gridSize;
    private int cellPixelSize;

    [Header("Clickable Area Settings")]
    [Range(1, 15)]
    public int clickableStartRow = 3;
    [Range(1, 15)]
    public int clickableEndRow = 5;

    private int width;
    private int height;

    public enum CellType
    {
        Empty,
        SkySand,
        BrownSand,
        Wall
    }

    public enum CellOwnership
    {
        None,
        Sky,
        Brown
    }

    private CellType[,] grid;
    private CellOwnership[,] ownership;
    private CellOwnership[,] previousOwnership;
    private TextMesh[,] ownershipTexts;
    private Texture2D texture;
    private SpriteRenderer spriteRenderer;

    // 최적화: 격자선 분리
    private GameObject gridLinesObject;
    private SpriteRenderer gridLinesRenderer;
    private Texture2D gridLinesTexture;

    // 최적화: 더티 플래그
    private HashSet<Vector2Int> dirtyPixels = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> dirtyCells = new HashSet<Vector2Int>();

    private const int SPAWN_PATTERN_HEIGHT = 3;
    private const float OWNERSHIP_THRESHOLD = 0.5f;

    [Header("Ownership Text Settings")]
    public Color ownershipTextColor = new Color(1f, 1f, 1f, 0.8f);
    public int ownershipCharacterSize = 100;
    public int ownershipFontSize = 14;

    void Start()
    {
        InitializeSettings();
        InitializeGrid();
        InitializeOwnership();
        SetupRenderer();
        SetupGridLines(); // 격자선 별도 설정
        CreateOwnershipTexts();

        // 초기 전체 렌더링
        MarkAllDirty();
        UpdateTexture();
    }

    void InitializeSettings()
    {
        gridSize = 15;
        cellPixelSize = 20;

        width = gridSize * cellPixelSize + 2;
        height = gridSize * cellPixelSize + 1;
    }

    void InitializeGrid()
    {
        grid = new CellType[width, height];

        for (int x = 0; x < width; x++)
        {
            grid[x, 0] = CellType.Wall;
        }

        for (int y = 0; y < height; y++)
        {
            grid[0, y] = CellType.Wall;
            grid[width - 1, y] = CellType.Wall;
        }
    }

    void InitializeOwnership()
    {
        ownership = new CellOwnership[gridSize, gridSize];
        previousOwnership = new CellOwnership[gridSize, gridSize]; // 캐시 초기화

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                ownership[x, y] = CellOwnership.None;
                previousOwnership[x, y] = CellOwnership.None;
            }
        }
    }

    void SetupRenderer()
    {
        texture = new Texture2D(width, height);
        texture.filterMode = FilterMode.Point;

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 0;

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            1f
        );
        spriteRenderer.sprite = sprite;
    }

    void SetupGridLines()
    {
        // 격자선용 별도 GameObject 생성
        gridLinesObject = new GameObject("GridLines");
        gridLinesObject.transform.SetParent(transform);
        gridLinesObject.transform.localPosition = Vector3.zero;

        gridLinesRenderer = gridLinesObject.AddComponent<SpriteRenderer>();
        gridLinesRenderer.sortingOrder = 5; // 모래 위에 표시

        gridLinesTexture = new Texture2D(width, height);
        gridLinesTexture.filterMode = FilterMode.Point;

        // 투명 배경으로 초기화
        Color transparent = new Color(0, 0, 0, 0);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                gridLinesTexture.SetPixel(x, y, transparent);
            }
        }

        // 격자선 한 번만 그리기
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
        Color lineColor = GameManager.Instance.gridLineColor;

        // Vertical lines
        for (int i = 0; i <= gridSize; i++)
        {
            int x = 1 + i * cellPixelSize;
            for (int y = 1; y < height; y++)
            {
                gridLinesTexture.SetPixel(x, y, lineColor);
            }
        }

        // Horizontal lines
        for (int i = 0; i <= gridSize; i++)
        {
            int y = 1 + i * cellPixelSize;
            if (y >= height) break;

            for (int x = 1; x < width - 1; x++)
            {
                gridLinesTexture.SetPixel(x, y, lineColor);
            }
        }

        gridLinesTexture.Apply();
        Debug.Log("Grid lines drawn once and cached");
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

    public bool SimulatePhysics()
    {
        bool sandMoved = false;

        for (int y = 1; y < height - 1; y++)
        {
            bool scanLeft = Random.value > 0.5f;

            for (int x = 1; x < width - 1; x++)
            {
                int currentX = scanLeft ? x : width - 1 - x;

                if (IsSand(grid[currentX, y]))
                {
                    if (UpdateSand(currentX, y))
                    {
                        sandMoved = true;
                    }
                }
            }
        }

        return sandMoved;
    }

    bool UpdateSand(int x, int y)
    {
        CellType sandType = grid[x, y];

        // Try to fall down
        if (grid[x, y - 1] == CellType.Empty)
        {
            grid[x, y] = CellType.Empty;
            grid[x, y - 1] = sandType;

            // 최적화: 변경된 픽셀만 마크
            MarkPixelDirty(x, y);
            MarkPixelDirty(x, y - 1);
            MarkCellDirtyByPixel(x, y);
            MarkCellDirtyByPixel(x, y - 1);

            return true;
        }

        // Try to fall diagonally
        int direction = Random.value > 0.5f ? 1 : -1;

        if (grid[x + direction, y - 1] == CellType.Empty)
        {
            grid[x, y] = CellType.Empty;
            grid[x + direction, y - 1] = sandType;

            MarkPixelDirty(x, y);
            MarkPixelDirty(x + direction, y - 1);
            MarkCellDirtyByPixel(x, y);
            MarkCellDirtyByPixel(x + direction, y - 1);

            return true;
        }
        else if (grid[x - direction, y - 1] == CellType.Empty)
        {
            grid[x, y] = CellType.Empty;
            grid[x - direction, y - 1] = sandType;

            MarkPixelDirty(x, y);
            MarkPixelDirty(x - direction, y - 1);
            MarkCellDirtyByPixel(x, y);
            MarkCellDirtyByPixel(x - direction, y - 1);

            return true;
        }

        return false;
    }

    void MarkPixelDirty(int x, int y)
    {
        dirtyPixels.Add(new Vector2Int(x, y));
    }

    void MarkCellDirtyByPixel(int x, int y)
    {
        // 픽셀 좌표를 셀 좌표로 변환
        int cellX = (x - 1) / cellPixelSize;
        int cellY = (y - 1) / cellPixelSize;

        if (cellX >= 0 && cellX < gridSize && cellY >= 0 && cellY < gridSize)
        {
            dirtyCells.Add(new Vector2Int(cellX, cellY));
        }
    }

    void MarkAllDirty()
    {
        dirtyPixels.Clear();
        dirtyCells.Clear();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                dirtyPixels.Add(new Vector2Int(x, y));
            }
        }

        for (int cellX = 0; cellX < gridSize; cellX++)
        {
            for (int cellY = 0; cellY < gridSize; cellY++)
            {
                dirtyCells.Add(new Vector2Int(cellX, cellY));
            }
        }
    }

    public void UpdateTexture()
    {
        UpdateOwnership(); // 최적화: 변경된 칸만
        DrawBackground();  // 최적화: 변경된 픽셀만
        UpdateOwnershipTexts();
        texture.Apply();

        // 다음 프레임을 위해 초기화
        dirtyPixels.Clear();
        dirtyCells.Clear();
    }

    void UpdateOwnership()
    {
        // 최적화: 변경된 칸만 재계산
        foreach (Vector2Int cell in dirtyCells)
        {
            int cellX = cell.x;
            int cellY = cell.y;

            int skyCount = 0;
            int brownCount = 0;

            int startX = 1 + cellX * cellPixelSize;
            int startY = 1 + cellY * cellPixelSize;
            int endX = startX + cellPixelSize;
            int endY = startY + cellPixelSize;

            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    if (grid[x, y] == CellType.SkySand)
                    {
                        skyCount++;
                    }
                    else if (grid[x, y] == CellType.BrownSand)
                    {
                        brownCount++;
                    }
                }
            }

            float skyRatio = (float)skyCount / (cellPixelSize * cellPixelSize);
            float brownRatio = (float)brownCount / (cellPixelSize * cellPixelSize);

            if (skyRatio >= OWNERSHIP_THRESHOLD)
            {
                ownership[cellX, cellY] = CellOwnership.Sky;
            }
            else if (brownRatio >= OWNERSHIP_THRESHOLD)
            {
                ownership[cellX, cellY] = CellOwnership.Brown;
            }
            else
            {
                ownership[cellX, cellY] = CellOwnership.None;
            }
        }
    }

    public int CheckWinCondition()
    {
        // 가로 체크
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x <= gridSize - 5; x++)
            {
                CellOwnership first = ownership[x, y];
                if (first == CellOwnership.None) continue;

                bool isWin = true;
                for (int i = 1; i < 5; i++)
                {
                    if (ownership[x + i, y] != first)
                    {
                        isWin = false;
                        break;
                    }
                }

                if (isWin)
                    return first == CellOwnership.Sky ? 1 : 2;
            }
        }

        // 세로 체크
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y <= gridSize - 5; y++)
            {
                CellOwnership first = ownership[x, y];
                if (first == CellOwnership.None) continue;

                bool isWin = true;
                for (int i = 1; i < 5; i++)
                {
                    if (ownership[x, y + i] != first)
                    {
                        isWin = false;
                        break;
                    }
                }

                if (isWin)
                    return first == CellOwnership.Sky ? 1 : 2;
            }
        }

        // 대각선 (\) 체크
        for (int x = 0; x <= gridSize - 5; x++)
        {
            for (int y = 0; y <= gridSize - 5; y++)
            {
                CellOwnership first = ownership[x, y];
                if (first == CellOwnership.None) continue;

                bool isWin = true;
                for (int i = 1; i < 5; i++)
                {
                    if (ownership[x + i, y + i] != first)
                    {
                        isWin = false;
                        break;
                    }
                }

                if (isWin)
                    return first == CellOwnership.Sky ? 1 : 2;
            }
        }

        // 대각선 (/) 체크
        for (int x = 0; x <= gridSize - 5; x++)
        {
            for (int y = 4; y < gridSize; y++)
            {
                CellOwnership first = ownership[x, y];
                if (first == CellOwnership.None) continue;

                bool isWin = true;
                for (int i = 1; i < 5; i++)
                {
                    if (ownership[x + i, y - i] != first)
                    {
                        isWin = false;
                        break;
                    }
                }

                if (isWin)
                    return first == CellOwnership.Sky ? 1 : 2;
            }
        }

        return 0;
    }

    void UpdateOwnershipTexts()
    {
        // 최적화 4: 변경된 것만 업데이트
        for (int cellX = 0; cellX < gridSize; cellX++)
        {
            for (int cellY = 0; cellY < gridSize; cellY++)
            {
                // 상태가 바뀌었을 때만 업데이트
                if (ownership[cellX, cellY] != previousOwnership[cellX, cellY])
                {
                    TextMesh textMesh = ownershipTexts[cellX, cellY];

                    switch (ownership[cellX, cellY])
                    {
                        case CellOwnership.Sky:
                            textMesh.text = "O";
                            textMesh.gameObject.SetActive(true);
                            break;
                        case CellOwnership.Brown:
                            textMesh.text = "X";
                            textMesh.gameObject.SetActive(true);
                            break;
                        case CellOwnership.None:
                            textMesh.gameObject.SetActive(false);
                            break;
                    }

                    previousOwnership[cellX, cellY] = ownership[cellX, cellY];
                }
            }
        }
    }

    void DrawBackground()
    {
        // 최적화: 변경된 픽셀만 다시 그리기
        int minClickableY = GetMinClickableY();
        int maxClickableY = GetMaxClickableY();

        foreach (Vector2Int pixel in dirtyPixels)
        {
            int x = pixel.x;
            int y = pixel.y;

            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                Color color = GetPixelColor(x, y, minClickableY, maxClickableY);
                texture.SetPixel(x, y, color);
            }
        }
    }

    Color GetPixelColor(int x, int y, int minClickableY, int maxClickableY)
    {
        if (grid[x, y] != CellType.Empty)
        {
            return grid[x, y] switch
            {
                CellType.SkySand => GameManager.Instance.skyColor,
                CellType.BrownSand => GameManager.Instance.brownColor,
                CellType.Wall => GameManager.Instance.wallColor,
                _ => GameManager.Instance.boardBackgroundColor
            };
        }

        bool isInClickableArea = x > 0 && x < width - 1 &&
                                  y > minClickableY && y <= maxClickableY &&
                                  y > 0;
        return isInClickableArea ? GameManager.Instance.clickableAreaColor : GameManager.Instance.boardBackgroundColor;
    }

    public bool SpawnSand(int gridX, int gridY, CellType sandType, int amount)
    {
        int spawnedCount = 0;

        for (int dy = 0; dy < SPAWN_PATTERN_HEIGHT && spawnedCount < amount; dy++)
        {
            for (int dx = -1; dx <= 1 && spawnedCount < amount; dx++)
            {
                int posX = gridX + dx;
                int posY = gridY + dy;

                if (IsInBounds(posX, posY) && grid[posX, posY] == CellType.Empty)
                {
                    grid[posX, posY] = sandType;
                    MarkPixelDirty(posX, posY);
                    MarkCellDirtyByPixel(posX, posY);
                    spawnedCount++;
                }
            }
        }

        return spawnedCount > 0;
    }

    public void DropSandChunk(int centerX, int centerY, CellType sandType)
    {
        int halfSize = cellPixelSize / 2;

        int droppedCount = 0;
        int targetAmount = cellPixelSize * cellPixelSize;

        for (int dy = -halfSize; dy < halfSize && droppedCount < targetAmount; dy++)
        {
            for (int dx = -halfSize; dx < halfSize && droppedCount < targetAmount; dx++)
            {
                int posX = centerX + dx;
                int posY = centerY + dy;

                if (IsInBounds(posX, posY) && grid[posX, posY] == CellType.Empty)
                {
                    grid[posX, posY] = sandType;
                    MarkPixelDirty(posX, posY);
                    MarkCellDirtyByPixel(posX, posY);
                    droppedCount++;
                }
            }
        }

        Debug.Log($"Dropped {droppedCount} sand particles in a chunk at ({centerX}, {centerY})");
    }

    public void DropSandRectangle(int centerX, int centerY, float widthCells, float heightCells, CellType sandType)
    {
        int widthPixels = Mathf.RoundToInt(widthCells * cellPixelSize);
        int heightPixels = Mathf.RoundToInt(heightCells * cellPixelSize);

        int halfWidth = widthPixels / 2;
        int halfHeight = heightPixels / 2;

        int droppedCount = 0;
        int targetAmount = widthPixels * heightPixels;

        for (int dy = -halfHeight; dy < halfHeight && droppedCount < targetAmount; dy++)
        {
            for (int dx = -halfWidth; dx < halfWidth && droppedCount < targetAmount; dx++)
            {
                int posX = centerX + dx;
                int posY = centerY + dy;

                if (IsInBounds(posX, posY) && grid[posX, posY] == CellType.Empty)
                {
                    grid[posX, posY] = sandType;
                    MarkPixelDirty(posX, posY);
                    MarkCellDirtyByPixel(posX, posY);
                    droppedCount++;
                }
            }
        }

        Debug.Log($"Dropped {droppedCount} sand particles in {widthCells}x{heightCells} rectangle at ({centerX}, {centerY})");
    }

    public void ResetBoard()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] != CellType.Wall)
                {
                    grid[x, y] = CellType.Empty;
                }
            }
        }

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                ownership[x, y] = CellOwnership.None;
            }
        }

        for (int cellX = 0; cellX < gridSize; cellX++)
        {
            for (int cellY = 0; cellY < gridSize; cellY++)
            {
                ownershipTexts[cellX, cellY].gameObject.SetActive(false);
            }
        }

        MarkAllDirty();
        UpdateTexture();
    }

    int GetMinClickableY()
    {
        int actualEndRow = Mathf.Max(clickableStartRow, clickableEndRow);
        return height - 1 - (actualEndRow * cellPixelSize);
    }

    int GetMaxClickableY()
    {
        int actualStartRow = Mathf.Min(clickableStartRow, clickableEndRow);
        return height - 1 - ((actualStartRow - 1) * cellPixelSize);
    }

    bool IsSand(CellType type)
    {
        return type == CellType.SkySand || type == CellType.BrownSand;
    }

    public bool IsInClickableArea(int x, int y)
    {
        if (!IsInBounds(x, y)) return false;

        int minClickableY = GetMinClickableY();
        int maxClickableY = GetMaxClickableY();

        return y > minClickableY && y <= maxClickableY;
    }

    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    public CellType GetCell(int x, int y)
    {
        return IsInBounds(x, y) ? grid[x, y] : CellType.Wall;
    }

    public CellOwnership GetCellOwnership(int cellX, int cellY)
    {
        if (cellX >= 0 && cellX < gridSize && cellY >= 0 && cellY < gridSize)
        {
            return ownership[cellX, cellY];
        }
        return CellOwnership.None;
    }

    public int GetWidth() => width;
    public int GetHeight() => height;
    public int GetCellPixelSize() => cellPixelSize;
}