using UnityEngine;
using System.Collections.Generic;

public class BotController : MonoBehaviour
{
    private SandSimulator sandSimulator;
    private List<int> oasisSandXPositions = new List<int>();

    private float averageX;
    private float standardDeviation;

    void Start()
    {
        sandSimulator = FindObjectOfType<SandSimulator>();
    }

    public void RecordOasisSandPosition(int x)
    {
        oasisSandXPositions.Add(x);
    }

    public void ClearOasisData()
    {
        oasisSandXPositions.Clear();
    }

    void CalculateStatistics()
    {
        if (oasisSandXPositions.Count == 0)
        {
            averageX = sandSimulator.GetWidth() / 2f;
            standardDeviation = 0f;
            return;
        }

        float sum = 0;
        foreach (int x in oasisSandXPositions)
            sum += x;
        averageX = sum / oasisSandXPositions.Count;

        float varianceSum = 0;
        foreach (int x in oasisSandXPositions)
        {
            float diff = x - averageX;
            varianceSum += diff * diff;
        }
        standardDeviation = Mathf.Sqrt(varianceSum / oasisSandXPositions.Count);

        Debug.Log($"Bot Analysis - Average X: {averageX:F2}, StdDev: {standardDeviation:F2}, Samples: {oasisSandXPositions.Count}");
    }

    public void ExecuteBotTurn(int difficulty)
    {
        CalculateStatistics();

        switch (difficulty)
        {
            case 1: ExecuteEasyBot();   break;
            case 2: ExecuteMediumBot(); break;
            case 3: ExecuteHardBot();   break;
        }

        ClearOasisData();
    }

    void ExecuteEasyBot()
    {
        int cellPixelSize = sandSimulator.GetCellPixelSize();
        float minOffset = cellPixelSize * 0.5f;
        float offset = Mathf.Max(standardDeviation, minOffset);

        float leftX = averageX - offset;
        float rightX = averageX + offset;

        AdjustTwoPositionsForBoundary(ref leftX, ref rightX, cellPixelSize);

        int targetY = GetThirdRowY();

        Debug.Log($"Easy Bot: Dropping 0.8-cell chunks at X: {leftX:F1}, {rightX:F1}");

        sandSimulator.DropSandRectangle(Mathf.RoundToInt(leftX), targetY,
            1.0f, 0.8f, SandSimulator.CellType.BrownSand);
        sandSimulator.DropSandRectangle(Mathf.RoundToInt(rightX), targetY,
            1.0f, 0.8f, SandSimulator.CellType.BrownSand);
    }

    void ExecuteMediumBot()
    {
        int cellPixelSize = sandSimulator.GetCellPixelSize();
        float minOffset = cellPixelSize * 0.5f;
        float offset = Mathf.Max(standardDeviation, minOffset);

        float leftX = averageX - offset;
        float rightX = averageX + offset;

        AdjustTwoPositionsForBoundary(ref leftX, ref rightX, cellPixelSize);

        int targetY = GetThirdRowY();

        Debug.Log($"Medium Bot: Dropping 1.2-cell chunks at X: {leftX:F1}, {rightX:F1}");

        sandSimulator.DropSandRectangle(Mathf.RoundToInt(leftX), targetY,
            1.0f, 1.2f, SandSimulator.CellType.BrownSand);
        sandSimulator.DropSandRectangle(Mathf.RoundToInt(rightX), targetY,
            1.0f, 1.2f, SandSimulator.CellType.BrownSand);
    }

    void ExecuteHardBot()
    {
        int cellPixelSize = sandSimulator.GetCellPixelSize();
        float minOffset = cellPixelSize * 0.5f;
        float offset = Mathf.Max(standardDeviation, minOffset);

        float leftX = averageX - offset;
        float centerX = averageX;
        float rightX = averageX + offset;

        AdjustThreePositionsForBoundary(ref leftX, ref centerX, ref rightX, cellPixelSize);

        int targetY = GetThirdRowY();

        Debug.Log($"Hard Bot: Dropping 3 chunks at X: {leftX:F1}, {centerX:F1}, {rightX:F1}");

        sandSimulator.DropSandChunk(Mathf.RoundToInt(leftX),   targetY, SandSimulator.CellType.BrownSand);
        sandSimulator.DropSandChunk(Mathf.RoundToInt(centerX), targetY, SandSimulator.CellType.BrownSand);
        sandSimulator.DropSandChunk(Mathf.RoundToInt(rightX),  targetY, SandSimulator.CellType.BrownSand);
    }

    // gridSize는 SandSimulator에서 직접 읽어 계산
    void GetBoundaries(int cellPixelSize, out float leftBoundary, out float rightBoundary)
    {
        int gridSize = sandSimulator.GetGridSize();
        leftBoundary  = 1 + cellPixelSize / 2f;
        rightBoundary = 1 + (gridSize - 1) * cellPixelSize + cellPixelSize / 2f;
    }

    void AdjustTwoPositionsForBoundary(ref float leftX, ref float rightX, int cellPixelSize)
    {
        GetBoundaries(cellPixelSize, out float leftBoundary, out float rightBoundary);

        if (leftX < leftBoundary)
        {
            float shift = leftBoundary - leftX;
            leftX = leftBoundary;
            rightX += shift;

            if (rightX > rightBoundary)
            {
                rightX = rightBoundary;
                leftX = rightX - cellPixelSize;
                if (leftX < leftBoundary) leftX = leftBoundary;
            }
        }
        else if (rightX > rightBoundary)
        {
            float shift = rightX - rightBoundary;
            rightX = rightBoundary;
            leftX -= shift;

            if (leftX < leftBoundary)
            {
                leftX = leftBoundary;
                rightX = leftX + cellPixelSize;
                if (rightX > rightBoundary) rightX = rightBoundary;
            }
        }
    }

    void AdjustThreePositionsForBoundary(ref float leftX, ref float centerX, ref float rightX, int cellPixelSize)
    {
        GetBoundaries(cellPixelSize, out float leftBoundary, out float rightBoundary);

        float minGap = cellPixelSize;

        if (leftX < leftBoundary)
        {
            leftX   = leftBoundary;
            centerX = leftX + minGap;
            rightX  = centerX + minGap;

            if (rightX > rightBoundary)
            {
                float totalWidth = rightBoundary - leftBoundary;
                leftX   = leftBoundary;
                centerX = leftBoundary + totalWidth / 2f;
                rightX  = rightBoundary;
            }
        }
        else if (rightX > rightBoundary)
        {
            rightX  = rightBoundary;
            centerX = rightX - minGap;
            leftX   = centerX - minGap;

            if (leftX < leftBoundary)
            {
                float totalWidth = rightBoundary - leftBoundary;
                leftX   = leftBoundary;
                centerX = leftBoundary + totalWidth / 2f;
                rightX  = rightBoundary;
            }
        }
        else
        {
            if (centerX - leftX < minGap)
            {
                centerX = leftX + minGap;
                if (rightX - centerX < minGap)
                    rightX = centerX + minGap;
            }

            if (rightX - centerX < minGap)
                rightX = centerX + minGap;

            if (rightX > rightBoundary)
            {
                float overflow = rightX - rightBoundary;
                leftX   -= overflow;
                centerX -= overflow;
                rightX   = rightBoundary;

                if (leftX < leftBoundary)
                {
                    float totalWidth = rightBoundary - leftBoundary;
                    leftX   = leftBoundary;
                    centerX = leftBoundary + totalWidth / 2f;
                    rightX  = rightBoundary;
                }
            }
        }
    }

    int GetThirdRowY()
    {
        int cellPixelSize = sandSimulator.GetCellPixelSize();
        int height = sandSimulator.GetHeight();

        int rowIndex = 2;
        int pixelY = height - 1 - (rowIndex * cellPixelSize) - cellPixelSize / 2;

        return pixelY;
    }
}
