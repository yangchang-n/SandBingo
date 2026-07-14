using UnityEngine;
using System.Collections.Generic;

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

    // 보드 프레임 오브젝트 참조
    // 인스펙터에서 원하는 프레임 오브젝트를 넣었다 뺐다 하면서 확인하면 된다
    // SetupRenderer에서 생성되는 SandBoardRenderer에게 그대로 전달된다
    [Header("Frame Overlay")]
    public SpriteRenderer boardFrameRenderer;

    // 프레임 테두리가 실제로 보이길 원하는 두께이다. 값이 클수록 테두리가 두껍게 보인다
    // 직접 조정해서 확인한 값으로 고정했다. 더 이상 조정할 필요가 없어서 인스펙터에는 숨긴다
    [HideInInspector]
    public float boardFrameBorderThickness = 4f;

    private int width;
    private int height;

    // Clickable area boundaries
    private int clickableMinX;
    private int clickableMaxX;
    private int clickableMinY;
    private int clickableMaxY;

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

    public struct ScoreLine
    {
        public List<Vector2Int> cells;
        public int score;
        public CellOwnership ownership;
    }

    public struct ScoreResult
    {
        public List<ScoreLine> scoreLines;
        public int oasisScore;
        public int mudScore;
        public HashSet<Vector2Int> cellsToRemove;
    }

    private CellType[,] grid;
    private CellOwnership[,] ownership;

    // Renderer
    private SandBoardRenderer boardRenderer;

    // 최적화용 더티 플래그
    private HashSet<Vector2Int> dirtyPixels = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> dirtyCells = new HashSet<Vector2Int>();

    private const int SPAWN_PATTERN_HEIGHT = 3;
    private const float OWNERSHIP_THRESHOLD = 0.5f;

    void Start()
    {
        InitializeSettings();
        CalculateClickableArea();
        InitializeGrid();
        InitializeOwnership();
        SetupRenderer();

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

    void CalculateClickableArea()
    {
        int halfCell = cellPixelSize / 2;
        clickableMinX = 1 + halfCell;
        clickableMaxX = width - 1 - halfCell;

        int actualStartRow = Mathf.Min(clickableStartRow, clickableEndRow);
        int actualEndRow = Mathf.Max(clickableStartRow, clickableEndRow);

        clickableMinY = height - 1 - (actualEndRow * cellPixelSize);
        clickableMaxY = height - 1 - ((actualStartRow - 1) * cellPixelSize);

        Debug.Log($"Clickable Area: X({clickableMinX}-{clickableMaxX}), Y({clickableMinY}-{clickableMaxY})");
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
        boardRenderer = gameObject.AddComponent<SandBoardRenderer>();

        // 인스펙터에서 지정한 프레임 오브젝트와 테두리 두께 값을 새로 생성된 렌더러에게 그대로 전달한다
        boardRenderer.boardFrameRenderer = boardFrameRenderer;
        boardRenderer.boardFrameBorderThickness = boardFrameBorderThickness;

        boardRenderer.Initialize(
            width, height, gridSize, cellPixelSize,
            clickableMinX, clickableMaxX, clickableMinY, clickableMaxY
        );
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

        if (grid[x, y - 1] == CellType.Empty)
        {
            grid[x, y] = CellType.Empty;
            grid[x, y - 1] = sandType;

            MarkPixelDirty(x, y);
            MarkPixelDirty(x, y - 1);
            MarkCellDirtyByPixel(x, y);
            MarkCellDirtyByPixel(x, y - 1);

            return true;
        }

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
        UpdateOwnership();
        boardRenderer.DrawBackground(dirtyPixels, grid);
        boardRenderer.UpdateOwnershipTexts(ownership);
        boardRenderer.ApplyTexture();

        dirtyPixels.Clear();
        dirtyCells.Clear();
    }

    void UpdateOwnership()
    {
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

    public ScoreResult CalculateScoreAndGetCells()
    {
        ScoreResult result = new ScoreResult
        {
            scoreLines = new List<ScoreLine>(),
            oasisScore = 0,
            mudScore = 0,
            cellsToRemove = new HashSet<Vector2Int>()
        };

        // 가로 체크
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                CellOwnership owner = ownership[x, y];
                if (owner == CellOwnership.None) continue;

                int count = 1;
                while (x + count < gridSize && ownership[x + count, y] == owner)
                {
                    count++;
                }

                if (count >= 5)
                {
                    int score = 100 + (count - 5) * 50;

                    ScoreLine line = new ScoreLine
                    {
                        cells = new List<Vector2Int>(),
                        score = owner == CellOwnership.Sky ? score : -score,
                        ownership = owner
                    };

                    for (int i = 0; i < count; i++)
                    {
                        Vector2Int cell = new Vector2Int(x + i, y);
                        line.cells.Add(cell);
                        result.cellsToRemove.Add(cell);
                    }

                    result.scoreLines.Add(line);

                    if (owner == CellOwnership.Sky)
                        result.oasisScore += score;
                    else
                        result.mudScore += score;

                    x += count - 1;
                }
            }
        }

        // 세로 체크
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                CellOwnership owner = ownership[x, y];
                if (owner == CellOwnership.None) continue;

                int count = 1;
                while (y + count < gridSize && ownership[x, y + count] == owner)
                {
                    count++;
                }

                if (count >= 5)
                {
                    int score = 100 + (count - 5) * 50;

                    ScoreLine line = new ScoreLine
                    {
                        cells = new List<Vector2Int>(),
                        score = owner == CellOwnership.Sky ? score : -score,
                        ownership = owner
                    };

                    for (int i = 0; i < count; i++)
                    {
                        Vector2Int cell = new Vector2Int(x, y + i);
                        line.cells.Add(cell);
                        result.cellsToRemove.Add(cell);
                    }

                    result.scoreLines.Add(line);

                    if (owner == CellOwnership.Sky)
                        result.oasisScore += score;
                    else
                        result.mudScore += score;

                    y += count - 1;
                }
            }
        }

        // 대각선 체크 (역슬래시 방향)
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                CellOwnership owner = ownership[x, y];
                if (owner == CellOwnership.None) continue;

                int count = 1;
                while (x + count < gridSize && y + count < gridSize &&
                       ownership[x + count, y + count] == owner)
                {
                    count++;
                }

                if (count >= 5)
                {
                    int score = 100 + (count - 5) * 50;

                    ScoreLine line = new ScoreLine
                    {
                        cells = new List<Vector2Int>(),
                        score = owner == CellOwnership.Sky ? score : -score,
                        ownership = owner
                    };

                    for (int i = 0; i < count; i++)
                    {
                        Vector2Int cell = new Vector2Int(x + i, y + i);
                        line.cells.Add(cell);
                        result.cellsToRemove.Add(cell);
                    }

                    result.scoreLines.Add(line);

                    if (owner == CellOwnership.Sky)
                        result.oasisScore += score;
                    else
                        result.mudScore += score;

                    x += count - 1;
                }
            }
        }

        // 대각선 체크 (슬래시 방향)
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                CellOwnership owner = ownership[x, y];
                if (owner == CellOwnership.None) continue;

                int count = 1;
                while (x + count < gridSize && y - count >= 0 &&
                       ownership[x + count, y - count] == owner)
                {
                    count++;
                }

                if (count >= 5)
                {
                    int score = 100 + (count - 5) * 50;

                    ScoreLine line = new ScoreLine
                    {
                        cells = new List<Vector2Int>(),
                        score = owner == CellOwnership.Sky ? score : -score,
                        ownership = owner
                    };

                    for (int i = 0; i < count; i++)
                    {
                        Vector2Int cell = new Vector2Int(x + i, y - i);
                        line.cells.Add(cell);
                        result.cellsToRemove.Add(cell);
                    }

                    result.scoreLines.Add(line);

                    if (owner == CellOwnership.Sky)
                        result.oasisScore += score;
                    else
                        result.mudScore += score;

                    x += count - 1;
                }
            }
        }

        if (result.cellsToRemove.Count > 0)
        {
            Debug.Log($"Score Calculation: Oasis +{result.oasisScore}, Mud +{result.mudScore}, Removed {result.cellsToRemove.Count} cells");
        }

        return result;
    }

    public void RemoveCells(HashSet<Vector2Int> cells)
    {
        foreach (Vector2Int cell in cells)
        {
            int cellX = cell.x;
            int cellY = cell.y;

            int startX = 1 + cellX * cellPixelSize;
            int startY = 1 + cellY * cellPixelSize;
            int endX = startX + cellPixelSize;
            int endY = startY + cellPixelSize;

            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    if (grid[x, y] != CellType.Wall)
                    {
                        grid[x, y] = CellType.Empty;
                        MarkPixelDirty(x, y);
                    }
                }
            }

            ownership[cellX, cellY] = CellOwnership.None;
            MarkCellDirtyByPixel(startX, startY);
        }

        UpdateTexture();
    }

    public int SpawnSand(int gridX, int gridY, CellType sandType, int amount)
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

        return spawnedCount;
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

        boardRenderer.ResetOwnershipTexts();

        MarkAllDirty();
        UpdateTexture();
    }

    // Helper Methods
    bool IsSand(CellType type)
    {
        return type == CellType.SkySand || type == CellType.BrownSand;
    }

    public bool IsInClickableArea(int x, int y)
    {
        if (!IsInBounds(x, y)) return false;
        return x >= clickableMinX && x <= clickableMaxX &&
               y > clickableMinY && y <= clickableMaxY;
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
    public int GetGridSize() => gridSize;
}
