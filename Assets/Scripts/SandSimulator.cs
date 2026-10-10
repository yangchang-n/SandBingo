using UnityEngine;
using System.Collections.Generic;

public class SandSimulator : MonoBehaviour
{
    // 보드 크기 : 15 x 15칸, 칸 하나는 20픽셀
    // 폭은 좌우 벽 1픽셀씩, 높이는 바닥 1픽셀을 더한 값
    public const int GRID_SIZE = 15;
    public const int CELL_PIXEL_SIZE = 20;
    public const int WIDTH = GRID_SIZE * CELL_PIXEL_SIZE + 2;
    public const int HEIGHT = GRID_SIZE * CELL_PIXEL_SIZE + 1;

    [Header("Clickable Area Settings")]
    [Range(1, 15)]
    public int clickableStartRow = 2;
    [Range(1, 15)]
    public int clickableEndRow = 5;

    // 보드 프레임 스프라이트. 실행 중에 생성되는 SandBoardRenderer 에 전달
    [Header("Frame Overlay")]
    public SpriteRenderer boardFrameRenderer;

    // 클릭 가능 영역의 픽셀 경계. y 는 아래 경계를 포함하지 않음
    public int ClickableMinX { get; private set; }
    public int ClickableMaxX { get; private set; }
    public int ClickableMinY { get; private set; }
    public int ClickableMaxY { get; private set; }

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

    // 바뀐 픽셀과 칸만 다시 그리기 위한 기록
    private HashSet<Vector2Int> dirtyPixels = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> dirtyCells = new HashSet<Vector2Int>();

    // 한 번에 생성되는 패턴의 크기. GameManager 가 생성 속도를 맞출 때 사용
    public const int SPAWN_PATTERN_HEIGHT = 3;
    public const int SPAWN_PATTERN_HALF_WIDTH = 1;
    public const int SPAWN_PATTERN_WIDTH = SPAWN_PATTERN_HALF_WIDTH * 2 + 1;
    private const float OWNERSHIP_THRESHOLD = 0.5f;

    void Start()
    {
        CalculateClickableArea();
        InitializeGrid();
        InitializeOwnership();
        SetupRenderer();

        MarkAllDirty();
        UpdateTexture();
    }

    void CalculateClickableArea()
    {
        int halfCell = CELL_PIXEL_SIZE / 2;
        ClickableMinX = 1 + halfCell;
        ClickableMaxX = WIDTH - 1 - halfCell;

        int actualStartRow = Mathf.Min(clickableStartRow, clickableEndRow);
        int actualEndRow = Mathf.Max(clickableStartRow, clickableEndRow);

        ClickableMinY = HEIGHT - 1 - (actualEndRow * CELL_PIXEL_SIZE);
        ClickableMaxY = HEIGHT - 1 - ((actualStartRow - 1) * CELL_PIXEL_SIZE);

        Debug.Log($"Clickable Area: X({ClickableMinX}-{ClickableMaxX}), Y({ClickableMinY}-{ClickableMaxY})");
    }

    void InitializeGrid()
    {
        grid = new CellType[WIDTH, HEIGHT];

        for (int x = 0; x < WIDTH; x++)
        {
            grid[x, 0] = CellType.Wall;
        }

        for (int y = 0; y < HEIGHT; y++)
        {
            grid[0, y] = CellType.Wall;
            grid[WIDTH - 1, y] = CellType.Wall;
        }
    }

    void InitializeOwnership()
    {
        ownership = new CellOwnership[GRID_SIZE, GRID_SIZE];
        ClearOwnership();
    }

    void ClearOwnership()
    {
        for (int x = 0; x < GRID_SIZE; x++)
        {
            for (int y = 0; y < GRID_SIZE; y++)
            {
                ownership[x, y] = CellOwnership.None;
            }
        }
    }

    void SetupRenderer()
    {
        boardRenderer = gameObject.AddComponent<SandBoardRenderer>();
        boardRenderer.boardFrameRenderer = boardFrameRenderer;

        boardRenderer.Initialize(this);
    }

    public bool SimulatePhysics()
    {
        bool sandMoved = false;

        for (int y = 1; y < HEIGHT - 1; y++)
        {
            bool scanLeft = Random.value > 0.5f;

            for (int x = 1; x < WIDTH - 1; x++)
            {
                int currentX = scanLeft ? x : WIDTH - 1 - x;

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

    // 바로 아래가 비었으면 아래로, 아니면 랜덤으로 고른 대각선 쪽, 그다음 반대쪽 대각선 순서로 떨어진다
    // 랜덤은 바로 아래가 막혔을 때만 사용
    bool UpdateSand(int x, int y)
    {
        if (grid[x, y - 1] == CellType.Empty)
        {
            MoveSand(x, y, x, y - 1);
            return true;
        }

        int direction = Random.value > 0.5f ? 1 : -1;

        if (grid[x + direction, y - 1] == CellType.Empty)
        {
            MoveSand(x, y, x + direction, y - 1);
            return true;
        }

        if (grid[x - direction, y - 1] == CellType.Empty)
        {
            MoveSand(x, y, x - direction, y - 1);
            return true;
        }

        return false;
    }

    void MoveSand(int fromX, int fromY, int toX, int toY)
    {
        grid[toX, toY] = grid[fromX, fromY];
        grid[fromX, fromY] = CellType.Empty;

        MarkPixelDirty(fromX, fromY);
        MarkPixelDirty(toX, toY);
        MarkCellDirtyByPixel(fromX, fromY);
        MarkCellDirtyByPixel(toX, toY);
    }

    void MarkPixelDirty(int x, int y)
    {
        dirtyPixels.Add(new Vector2Int(x, y));
    }

    void MarkCellDirtyByPixel(int x, int y)
    {
        int cellX = (x - 1) / CELL_PIXEL_SIZE;
        int cellY = (y - 1) / CELL_PIXEL_SIZE;

        if (cellX >= 0 && cellX < GRID_SIZE && cellY >= 0 && cellY < GRID_SIZE)
        {
            dirtyCells.Add(new Vector2Int(cellX, cellY));
        }
    }

    void MarkAllDirty()
    {
        dirtyPixels.Clear();
        dirtyCells.Clear();

        for (int x = 0; x < WIDTH; x++)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                dirtyPixels.Add(new Vector2Int(x, y));
            }
        }

        for (int cellX = 0; cellX < GRID_SIZE; cellX++)
        {
            for (int cellY = 0; cellY < GRID_SIZE; cellY++)
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

            int startX = CellOriginPixel(cellX);
            int startY = CellOriginPixel(cellY);
            int endX = startX + CELL_PIXEL_SIZE;
            int endY = startY + CELL_PIXEL_SIZE;

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

            float skyRatio = (float)skyCount / (CELL_PIXEL_SIZE * CELL_PIXEL_SIZE);
            float brownRatio = (float)brownCount / (CELL_PIXEL_SIZE * CELL_PIXEL_SIZE);

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

    // 점수 판정 방향. 가로, 세로, 대각선 두 방향
    private static readonly Vector2Int[] SCORE_DIRECTIONS =
    {
        new Vector2Int(1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(1, 1),
        new Vector2Int(1, -1)
    };

    private const int MIN_SCORE_LINE_LENGTH = 5;
    private const int BASE_LINE_SCORE = 100;
    private const int EXTRA_CELL_SCORE = 50;

    public ScoreResult CalculateScoreAndGetCells()
    {
        ScoreResult result = new ScoreResult
        {
            scoreLines = new List<ScoreLine>(),
            oasisScore = 0,
            mudScore = 0,
            cellsToRemove = new HashSet<Vector2Int>()
        };

        foreach (Vector2Int direction in SCORE_DIRECTIONS)
        {
            CollectScoreLines(direction.x, direction.y, ref result);
        }

        if (result.cellsToRemove.Count > 0)
        {
            Debug.Log($"Score Calculation: Oasis +{result.oasisScore}, Mud +{result.mudScore}, Removed {result.cellsToRemove.Count} cells");
        }

        return result;
    }

    // 한 방향에서 같은 소유자가 이어지는 최대 구간을 한 번씩만 찾는다
    // 방향이 다른 줄끼리 칸을 공유하면 각각 별개의 줄로 인정
    void CollectScoreLines(int dx, int dy, ref ScoreResult result)
    {
        for (int y = 0; y < GRID_SIZE; y++)
        {
            for (int x = 0; x < GRID_SIZE; x++)
            {
                CellOwnership owner = ownership[x, y];
                if (owner == CellOwnership.None) continue;

                // 진행 방향의 바로 뒤 칸이 같은 소유자면 이 칸은 줄의 시작점이 아님
                // 이 검사가 없으면 같은 줄을 시작점만 옮겨가며 여러 번 세게 된다
                if (GetCellOwnership(x - dx, y - dy) == owner) continue;

                int count = 1;
                while (GetCellOwnership(x + count * dx, y + count * dy) == owner)
                {
                    count++;
                }

                if (count < MIN_SCORE_LINE_LENGTH) continue;

                int score = BASE_LINE_SCORE + (count - MIN_SCORE_LINE_LENGTH) * EXTRA_CELL_SCORE;

                ScoreLine line = new ScoreLine
                {
                    cells = new List<Vector2Int>(),
                    score = owner == CellOwnership.Sky ? score : -score
                };

                for (int i = 0; i < count; i++)
                {
                    Vector2Int cell = new Vector2Int(x + i * dx, y + i * dy);
                    line.cells.Add(cell);
                    result.cellsToRemove.Add(cell);
                }

                result.scoreLines.Add(line);

                if (owner == CellOwnership.Sky)
                    result.oasisScore += score;
                else
                    result.mudScore += score;
            }
        }
    }

    public void RemoveCells(HashSet<Vector2Int> cells)
    {
        foreach (Vector2Int cell in cells)
        {
            int cellX = cell.x;
            int cellY = cell.y;

            int startX = CellOriginPixel(cellX);
            int startY = CellOriginPixel(cellY);
            int endX = startX + CELL_PIXEL_SIZE;
            int endY = startY + CELL_PIXEL_SIZE;

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
            for (int dx = -SPAWN_PATTERN_HALF_WIDTH; dx <= SPAWN_PATTERN_HALF_WIDTH && spawnedCount < amount; dx++)
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
        int widthPixels = Mathf.RoundToInt(widthCells * CELL_PIXEL_SIZE);
        int heightPixels = Mathf.RoundToInt(heightCells * CELL_PIXEL_SIZE);

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
    }

    public void ResetBoard()
    {
        for (int x = 0; x < WIDTH; x++)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                if (grid[x, y] != CellType.Wall)
                {
                    grid[x, y] = CellType.Empty;
                }
            }
        }

        ClearOwnership();

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
        return x >= ClickableMinX && x <= ClickableMaxX &&
               y > ClickableMinY && y <= ClickableMaxY;
    }

    bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < WIDTH && y >= 0 && y < HEIGHT;
    }

    public CellOwnership GetCellOwnership(int cellX, int cellY)
    {
        if (cellX >= 0 && cellX < GRID_SIZE && cellY >= 0 && cellY < GRID_SIZE)
        {
            return ownership[cellX, cellY];
        }
        return CellOwnership.None;
    }

    // ===== 보드 좌표 변환 =====
    // 보드 오브젝트는 월드 원점에 있고 보드 1픽셀이 월드 1단위

    // 칸의 왼쪽 아래 픽셀. 바깥 벽 1픽셀 안쪽에서 시작
    public static int CellOriginPixel(int cell) => 1 + cell * CELL_PIXEL_SIZE;

    public static int CellCenterPixel(int cell) => CellOriginPixel(cell) + CELL_PIXEL_SIZE / 2;

    public static float PixelToWorldX(float pixelX) => pixelX - WIDTH / 2f;
    public static float PixelToWorldY(float pixelY) => pixelY - HEIGHT / 2f;

    public static Vector3 CellCenterToWorld(Vector2Int cell, float z)
    {
        return new Vector3(PixelToWorldX(CellCenterPixel(cell.x)), PixelToWorldY(CellCenterPixel(cell.y)), z);
    }

    public static Vector2Int WorldToPixel(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.RoundToInt(worldPos.x + WIDTH / 2f), Mathf.RoundToInt(worldPos.y + HEIGHT / 2f));
    }
}
