using UnityEngine;

public class SandSimulator : MonoBehaviour
{
    // Grid Settings
    private int gridSize;
    private int cellPixelSize;

    [Header("Clickable Area Settings")]
    [Range(1, 15)]
    public int clickableStartRow = 2;
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
    private TextMesh[,] ownershipTexts;
    private Texture2D texture;
    private SpriteRenderer spriteRenderer;

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
        CreateOwnershipTexts();
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

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                ownership[x, y] = CellOwnership.None;
            }
        }
    }

    void SetupRenderer()
    {
        texture = new Texture2D(width, height);
        texture.filterMode = FilterMode.Point;

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            1f
        );
        spriteRenderer.sprite = sprite;
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

    public void SimulatePhysics()
    {
        for (int y = 1; y < height - 1; y++)
        {
            bool scanLeft = Random.value > 0.5f;

            for (int x = 1; x < width - 1; x++)
            {
                int currentX = scanLeft ? x : width - 1 - x;

                if (IsSand(grid[currentX, y]))
                {
                    UpdateSand(currentX, y);
                }
            }
        }
    }

    void UpdateSand(int x, int y)
    {
        CellType sandType = grid[x, y];

        if (grid[x, y - 1] == CellType.Empty)
        {
            grid[x, y] = CellType.Empty;
            grid[x, y - 1] = sandType;
            return;
        }

        int direction = Random.value > 0.5f ? 1 : -1;

        if (grid[x + direction, y - 1] == CellType.Empty)
        {
            grid[x, y] = CellType.Empty;
            grid[x + direction, y - 1] = sandType;
        }
        else if (grid[x - direction, y - 1] == CellType.Empty)
        {
            grid[x, y] = CellType.Empty;
            grid[x - direction, y - 1] = sandType;
        }
    }

    public void UpdateTexture()
    {
        UpdateOwnership();
        DrawBackground();
        DrawGridLines();
        UpdateOwnershipTexts();
        texture.Apply();
    }

    void UpdateOwnership()
    {
        for (int cellX = 0; cellX < gridSize; cellX++)
        {
            for (int cellY = 0; cellY < gridSize; cellY++)
            {
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
    }

    public int CheckWinCondition()
    {
        // 1 = Sky 승리, 2 = Brown 승리, 0 = 승자 없음

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

        return 0; // 승자 없음
    }

    void UpdateOwnershipTexts()
    {
        for (int cellX = 0; cellX < gridSize; cellX++)
        {
            for (int cellY = 0; cellY < gridSize; cellY++)
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
            }
        }
    }

    void DrawBackground()
    {
        int minClickableY = GetMinClickableY();
        int maxClickableY = GetMaxClickableY();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
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

    void DrawGridLines()
    {
        for (int i = 0; i <= gridSize; i++)
        {
            int x = 1 + i * cellPixelSize;
            for (int y = 1; y < height; y++)
            {
                texture.SetPixel(x, y, GameManager.Instance.gridLineColor);
            }
        }

        for (int i = 0; i <= gridSize; i++)
        {
            int y = 1 + i * cellPixelSize;
            if (y >= height) break;

            for (int x = 1; x < width - 1; x++)
            {
                texture.SetPixel(x, y, GameManager.Instance.gridLineColor);
            }
        }
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
                    spawnedCount++;
                }
            }
        }

        return spawnedCount > 0;
    }

    public void ResetBoard()
    {
        // 그리드 초기화
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

        // 점유 상태 초기화
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                ownership[x, y] = CellOwnership.None;
            }
        }

        // 텍스트 초기화
        for (int cellX = 0; cellX < gridSize; cellX++)
        {
            for (int cellY = 0; cellY < gridSize; cellY++)
            {
                ownershipTexts[cellX, cellY].gameObject.SetActive(false);
            }
        }

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

    public void DropSandChunk(int centerX, int centerY, CellType sandType)
    {
        // 한 칸 분량 = cellPixelSize x cellPixelSize
        int halfSize = cellPixelSize / 2;

        int droppedCount = 0;
        int targetAmount = cellPixelSize * cellPixelSize; // 400개 (20x20)

        // 정사각형 모양으로 모래 떨어뜨림
        for (int dy = -halfSize; dy < halfSize && droppedCount < targetAmount; dy++)
        {
            for (int dx = -halfSize; dx < halfSize && droppedCount < targetAmount; dx++)
            {
                int posX = centerX + dx;
                int posY = centerY + dy;

                if (IsInBounds(posX, posY) && grid[posX, posY] == CellType.Empty)
                {
                    grid[posX, posY] = sandType;
                    droppedCount++;
                }
            }
        }

        Debug.Log($"Dropped {droppedCount} sand particles in a chunk at ({centerX}, {centerY})");
    }

    public void DropSandRectangle(int centerX, int centerY, float widthCells, float heightCells, CellType sandType)
    {
        // widthCells: 가로 칸 수 (예: 1.0)
        // heightCells: 세로 칸 수 (예: 0.8, 1.2)

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
                    droppedCount++;
                }
            }
        }

        Debug.Log($"Dropped {droppedCount} sand particles in {widthCells}x{heightCells} rectangle at ({centerX}, {centerY})");
    }
}