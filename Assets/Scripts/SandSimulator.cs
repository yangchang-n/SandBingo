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

    private CellType[,] grid;
    private CellOwnership[,] ownership;

    // Renderer
    private SandBoardRenderer boardRenderer;

    // 최적화: 더티 플래그
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
        // SandBoardRenderer 컴포넌트 추가
        boardRenderer = gameObject.AddComponent<SandBoardRenderer>();
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

    public int CheckWinCondition()
    {
        // 가로
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

        // 세로
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

        // 대각선 (\)
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

        // 대각선 (/)
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
}