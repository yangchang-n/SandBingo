using UnityEngine;
using System.Collections.Generic;

// SandSimulator 가 실행 중에 생성하는 보드 렌더러
// 모래 텍스처, 격자선, 클릭 가능 영역 테두리, 칸 소유권 O X 표시, 보드 프레임을 그림
public class SandBoardRenderer : MonoBehaviour
{
    private const int WIDTH = SandSimulator.WIDTH;
    private const int HEIGHT = SandSimulator.HEIGHT;
    private const int GRID_SIZE = SandSimulator.GRID_SIZE;

    private SandSimulator simulator;

    // 배경과 모래를 그리는 텍스처. 매 프레임 바뀐 픽셀만 다시 칠함
    private Texture2D mainTexture;

    private TextMesh[,] ownershipTexts;
    private SandSimulator.CellOwnership[,] previousOwnership;

    private Color skyColor;
    private Color brownColor;

    private static readonly Color BOARD_BACKGROUND_COLOR = new Color(0xDE / 255f, 0x9E / 255f, 0x4A / 255f);
    private static readonly Color CLICKABLE_AREA_COLOR = new Color(0xFF / 255f, 0xD7 / 255f, 0x98 / 255f);
    private static readonly Color GRID_LINE_COLOR = Color.black;
    private static readonly Color WALL_COLOR = new Color(0f, 0f, 0f, 0f);

    private const int CLICKABLE_BORDER_THICKNESS = 2;
    private static readonly Color CLICKABLE_BORDER_COLOR = Color.white;

    private static readonly Color OWNERSHIP_TEXT_COLOR = new Color(1f, 1f, 1f, 0.8f);
    private const int OWNERSHIP_CHARACTER_SIZE = 100;
    private const int OWNERSHIP_FONT_SIZE = 14;

    // 화면에 보이는 프레임 테두리 두께. 단위는 보드와 같은 월드 유닛
    // 0 이하이면 두께 보정 없이 원본 이미지를 그대로 늘린다
    private const float BOARD_FRAME_BORDER_THICKNESS = 4f;

    // 정렬 순서. 숫자가 클수록 앞에 그려짐
    private const int SORTING_BOARD = 0;
    private const int SORTING_GRID_LINES = 5;
    private const int SORTING_CLICKABLE_BORDER = 6;
    public const int SORTING_OWNERSHIP_TEXT = 10;
    private const int SORTING_FRAME = 20;

    // 보드 프레임 스프라이트. SandSimulator 가 이 컴포넌트를 생성한 직후 넣어준다
    public SpriteRenderer boardFrameRenderer;

    // 클릭 가능 영역의 경계가 계산된 뒤에 호출해야 한다
    public void Initialize(SandSimulator owner)
    {
        simulator = owner;

        skyColor = GameManager.Instance.GetPlayerColor(0);
        brownColor = GameManager.Instance.GetPlayerColor(1);

        // 배열의 기본값이 CellOwnership.None 이라 따로 채우지 않음
        previousOwnership = new SandSimulator.CellOwnership[GRID_SIZE, GRID_SIZE];

        mainTexture = CreateLayer(gameObject, SORTING_BOARD);
        SetupGridLines();
        SetupClickableBorder();
        CreateOwnershipTexts();
        SlicedFrameFitter.Fit(boardFrameRenderer, WIDTH, HEIGHT, transform.position, BOARD_FRAME_BORDER_THICKNESS, SORTING_FRAME);
    }

    Texture2D CreateLayer(GameObject owner, int sortingOrder)
    {
        SpriteRenderer layerRenderer = owner.AddComponent<SpriteRenderer>();
        layerRenderer.sortingOrder = sortingOrder;

        Texture2D texture = new Texture2D(WIDTH, HEIGHT);
        texture.filterMode = FilterMode.Point;

        layerRenderer.sprite = Sprite.Create(
            texture,
            new Rect(0, 0, WIDTH, HEIGHT),
            new Vector2(0.5f, 0.5f),
            1f
        );

        return texture;
    }

    Texture2D CreateChildLayer(string name, int sortingOrder)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(transform);
        child.transform.localPosition = Vector3.zero;

        return CreateLayer(child, sortingOrder);
    }

    static void ClearTexture(Texture2D texture)
    {
        Color transparent = new Color(0, 0, 0, 0);

        for (int x = 0; x < WIDTH; x++)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                texture.SetPixel(x, y, transparent);
            }
        }
    }

    // 텍스처 밖으로 나가는 부분은 잘라낸다. 경계는 모두 포함
    static void FillRect(Texture2D texture, int xMin, int xMax, int yMin, int yMax, Color color)
    {
        for (int x = Mathf.Max(xMin, 0); x <= Mathf.Min(xMax, WIDTH - 1); x++)
        {
            for (int y = Mathf.Max(yMin, 0); y <= Mathf.Min(yMax, HEIGHT - 1); y++)
            {
                texture.SetPixel(x, y, color);
            }
        }
    }

    void SetupGridLines()
    {
        Texture2D texture = CreateChildLayer("GridLines", SORTING_GRID_LINES);
        ClearTexture(texture);

        // 세로선
        for (int i = 0; i <= GRID_SIZE; i++)
        {
            int x = SandSimulator.CellOriginPixel(i);
            for (int y = 1; y < HEIGHT; y++)
            {
                texture.SetPixel(x, y, GRID_LINE_COLOR);
            }
        }

        // 가로선
        for (int i = 0; i <= GRID_SIZE; i++)
        {
            int y = SandSimulator.CellOriginPixel(i);
            if (y >= HEIGHT) break;

            for (int x = 1; x < WIDTH - 1; x++)
            {
                texture.SetPixel(x, y, GRID_LINE_COLOR);
            }
        }

        texture.Apply();
    }

    // 클릭 가능 영역의 y 아래 경계는 포함하지 않기 때문에 위쪽 변은 영역 경계보다 한 줄 위에서 시작된다
    void SetupClickableBorder()
    {
        Texture2D texture = CreateChildLayer("ClickableBorder", SORTING_CLICKABLE_BORDER);
        ClearTexture(texture);

        int minX = simulator.ClickableMinX;
        int maxX = simulator.ClickableMaxX;
        int minY = simulator.ClickableMinY;
        int maxY = simulator.ClickableMaxY;
        int thickness = CLICKABLE_BORDER_THICKNESS;

        FillRect(texture, minX, maxX, maxY + 2 - thickness, maxY + 1, CLICKABLE_BORDER_COLOR);
        FillRect(texture, minX, maxX, minY, minY + thickness - 1, CLICKABLE_BORDER_COLOR);
        FillRect(texture, minX, minX + thickness - 1, minY, maxY, CLICKABLE_BORDER_COLOR);
        FillRect(texture, maxX - thickness + 1, maxX, minY, maxY, CLICKABLE_BORDER_COLOR);

        texture.Apply();
    }

    void CreateOwnershipTexts()
    {
        ownershipTexts = new TextMesh[GRID_SIZE, GRID_SIZE];

        GameObject textsParent = new GameObject("OwnershipTexts");
        textsParent.transform.SetParent(transform);
        textsParent.transform.localPosition = Vector3.zero;

        for (int cellX = 0; cellX < GRID_SIZE; cellX++)
        {
            for (int cellY = 0; cellY < GRID_SIZE; cellY++)
            {
                GameObject textObj = new GameObject($"Text_{cellX}_{cellY}");
                textObj.transform.SetParent(textsParent.transform);

                TextMesh textMesh = textObj.AddComponent<TextMesh>();

                textMesh.text = "";
                textMesh.characterSize = OWNERSHIP_CHARACTER_SIZE;
                textMesh.fontSize = OWNERSHIP_FONT_SIZE;
                textMesh.color = OWNERSHIP_TEXT_COLOR;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.fontStyle = FontStyle.Bold;

                MeshRenderer meshRenderer = textObj.GetComponent<MeshRenderer>();
                meshRenderer.sortingOrder = SORTING_OWNERSHIP_TEXT;

                textObj.transform.position = SandSimulator.CellCenterToWorld(new Vector2Int(cellX, cellY), -1f);
                textObj.transform.localScale = Vector3.one * 0.1f;

                ownershipTexts[cellX, cellY] = textMesh;
            }
        }
    }

    public void DrawBackground(HashSet<Vector2Int> dirtyPixels, SandSimulator.CellType[,] grid)
    {
        foreach (Vector2Int pixel in dirtyPixels)
        {
            int x = pixel.x;
            int y = pixel.y;

            if (x >= 0 && x < WIDTH && y >= 0 && y < HEIGHT)
            {
                Color color = GetPixelColor(x, y, grid);
                mainTexture.SetPixel(x, y, color);
            }
        }
    }

    Color GetPixelColor(int x, int y, SandSimulator.CellType[,] grid)
    {
        if (grid[x, y] != SandSimulator.CellType.Empty)
        {
            return grid[x, y] switch
            {
                SandSimulator.CellType.SkySand => skyColor,
                SandSimulator.CellType.BrownSand => brownColor,
                SandSimulator.CellType.Wall => WALL_COLOR,
                _ => BOARD_BACKGROUND_COLOR
            };
        }

        return simulator.IsInClickableArea(x, y) ? CLICKABLE_AREA_COLOR : BOARD_BACKGROUND_COLOR;
    }

    public void UpdateOwnershipTexts(SandSimulator.CellOwnership[,] ownership)
    {
        for (int cellX = 0; cellX < GRID_SIZE; cellX++)
        {
            for (int cellY = 0; cellY < GRID_SIZE; cellY++)
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
        for (int cellX = 0; cellX < GRID_SIZE; cellX++)
        {
            for (int cellY = 0; cellY < GRID_SIZE; cellY++)
            {
                ownershipTexts[cellX, cellY].gameObject.SetActive(false);
                previousOwnership[cellX, cellY] = SandSimulator.CellOwnership.None;
            }
        }
    }
}
