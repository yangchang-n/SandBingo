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
        {
            sum += x;
        }
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
            case 1:
                ExecuteEasyBot();
                break;
            case 2:
                ExecuteMediumBot();
                break;
            case 3:
                ExecuteHardBot();
                break;
        }

        ClearOasisData();
    }

    void ExecuteEasyBot()
    {
        // 0.8Ä­ µ¢ÀÌ 2°³ (¼¼·Î 0.8Ä­ x °¡·Î 1Ä­)
        int cellPixelSize = sandSimulator.GetCellPixelSize();
        float minOffset = cellPixelSize * 0.5f;
        float offset = Mathf.Max(standardDeviation, minOffset);

        float leftX = averageX - offset;
        float rightX = averageX + offset;

        AdjustPositionsForBoundary(ref leftX, ref rightX, cellPixelSize);

        int targetY = GetThirdRowY();

        Debug.Log($"Easy Bot: Dropping 0.8-cell chunks at X: {leftX:F1}, {rightX:F1}");

        sandSimulator.DropSandRectangle(Mathf.RoundToInt(leftX), targetY,
            1.0f, 0.8f, SandSimulator.CellType.BrownSand);
        sandSimulator.DropSandRectangle(Mathf.RoundToInt(rightX), targetY,
            1.0f, 0.8f, SandSimulator.CellType.BrownSand);
    }

    void ExecuteMediumBot()
    {
        // 1.2Ä­ µ¢ÀÌ 2°³ (¼¼·Î 1.2Ä­ x °¡·Î 1Ä­)
        int cellPixelSize = sandSimulator.GetCellPixelSize();
        float minOffset = cellPixelSize * 0.5f;
        float offset = Mathf.Max(standardDeviation, minOffset);

        float leftX = averageX - offset;
        float rightX = averageX + offset;

        AdjustPositionsForBoundary(ref leftX, ref rightX, cellPixelSize);

        int targetY = GetThirdRowY();

        Debug.Log($"Medium Bot: Dropping 1.2-cell chunks at X: {leftX:F1}, {rightX:F1}");

        sandSimulator.DropSandRectangle(Mathf.RoundToInt(leftX), targetY,
            1.0f, 1.2f, SandSimulator.CellType.BrownSand);
        sandSimulator.DropSandRectangle(Mathf.RoundToInt(rightX), targetY,
            1.0f, 1.2f, SandSimulator.CellType.BrownSand);
    }

    void ExecuteHardBot()
    {
        // 1Ä­ µ¢ÀÌ 3°³ (Á¤»ç°¢Çü)
        int cellPixelSize = sandSimulator.GetCellPixelSize();
        float minOffset = cellPixelSize * 0.5f;
        float offset = Mathf.Max(standardDeviation, minOffset);

        float leftX = averageX - offset;
        float centerX = averageX;
        float rightX = averageX + offset;

        // 3°³ À§Ä¡ Á¶Á¤
        AdjustThreePositionsForBoundary(ref leftX, ref centerX, ref rightX, cellPixelSize);

        int targetY = GetThirdRowY();

        Debug.Log($"Hard Bot: Dropping 3 chunks at X: {leftX:F1}, {centerX:F1}, {rightX:F1}");

        sandSimulator.DropSandChunk(Mathf.RoundToInt(leftX), targetY,
            SandSimulator.CellType.BrownSand);
        sandSimulator.DropSandChunk(Mathf.RoundToInt(centerX), targetY,
            SandSimulator.CellType.BrownSand);
        sandSimulator.DropSandChunk(Mathf.RoundToInt(rightX), targetY,
            SandSimulator.CellType.BrownSand);
    }

    void AdjustPositionsForBoundary(ref float leftX, ref float rightX, int cellPixelSize)
    {
        float leftBoundary = 1 + cellPixelSize / 2f;
        float rightBoundary = 1 + (15 - 1) * cellPixelSize + cellPixelSize / 2f;

        if (leftX < leftBoundary)
        {
            float shift = leftBoundary - leftX;
            leftX = leftBoundary;
            rightX += shift;

            if (rightX > rightBoundary)
            {
                rightX = rightBoundary;
                leftX = rightX - cellPixelSize;
                if (leftX < leftBoundary)
                {
                    leftX = leftBoundary;
                }
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
                if (rightX > rightBoundary)
                {
                    rightX = rightBoundary;
                }
            }
        }
    }

    void AdjustThreePositionsForBoundary(ref float leftX, ref float centerX, ref float rightX, int cellPixelSize)
    {
        float leftBoundary = 1 + cellPixelSize / 2f;
        float rightBoundary = 1 + (15 - 1) * cellPixelSize + cellPixelSize / 2f;

        // ÃÖ¼Ò °£°Ý: 1Ä­ (°ãÄ¡Áö ¾Ê°Ô)
        float minGap = cellPixelSize;

        // ¿ÞÂÊ °æ°è Ã¼Å©
        if (leftX < leftBoundary)
        {
            leftX = leftBoundary;
            centerX = leftX + minGap;
            rightX = centerX + minGap;

            if (rightX > rightBoundary)
            {
                // ¿À¸¥ÂÊµµ ¹þ¾î³²: ±Õµî ºÐ¹è
                float totalWidth = rightBoundary - leftBoundary;
                leftX = leftBoundary;
                centerX = leftBoundary + totalWidth / 2f;
                rightX = rightBoundary;
            }
        }
        // ¿À¸¥ÂÊ °æ°è Ã¼Å©
        else if (rightX > rightBoundary)
        {
            rightX = rightBoundary;
            centerX = rightX - minGap;
            leftX = centerX - minGap;

            if (leftX < leftBoundary)
            {
                // ¿ÞÂÊµµ ¹þ¾î³²: ±Õµî ºÐ¹è
                float totalWidth = rightBoundary - leftBoundary;
                leftX = leftBoundary;
                centerX = leftBoundary + totalWidth / 2f;
                rightX = rightBoundary;
            }
        }
        // °ãÄ§ Ã¼Å©
        else
        {
            // ¿ÞÂÊ-Áß¾Ó °£°Ý Ã¼Å©
            if (centerX - leftX < minGap)
            {
                centerX = leftX + minGap;
                if (rightX - centerX < minGap)
                {
                    rightX = centerX + minGap;
                }
            }

            // Áß¾Ó-¿À¸¥ÂÊ °£°Ý Ã¼Å©
            if (rightX - centerX < minGap)
            {
                rightX = centerX + minGap;
            }

            // ÀçÁ¶Á¤ ÈÄ °æ°è ¹þ¾î³² Ã¼Å©
            if (rightX > rightBoundary)
            {
                float overflow = rightX - rightBoundary;
                leftX -= overflow;
                centerX -= overflow;
                rightX = rightBoundary;

                if (leftX < leftBoundary)
                {
                    // ±Õµî ºÐ¹è
                    float totalWidth = rightBoundary - leftBoundary;
                    leftX = leftBoundary;
                    centerX = leftBoundary + totalWidth / 2f;
                    rightX = rightBoundary;
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