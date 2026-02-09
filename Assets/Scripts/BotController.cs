using UnityEngine;
using System.Collections.Generic;

public class BotController : MonoBehaviour
{
    private SandSimulator sandSimulator;
    private List<int> oasisSandXPositions = new List<int>();

    // 통계 데이터
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

        // 평균 계산
        float sum = 0;
        foreach (int x in oasisSandXPositions)
        {
            sum += x;
        }
        averageX = sum / oasisSandXPositions.Count;

        // 표준편차 계산
        float varianceSum = 0;
        foreach (int x in oasisSandXPositions)
        {
            float diff = x - averageX;
            varianceSum += diff * diff;
        }
        standardDeviation = Mathf.Sqrt(varianceSum / oasisSandXPositions.Count);

        Debug.Log($"Bot Analysis - Average X: {averageX:F2}, StdDev: {standardDeviation:F2}, Samples: {oasisSandXPositions.Count}");
    }

    public void ExecuteBotTurn()
    {
        CalculateStatistics();

        int cellPixelSize = sandSimulator.GetCellPixelSize();
        int gridSize = 15; // sandSimulator에서 가져올 수 있으면 더 좋음

        // 최소 오프셋: 0.5칸 (두 덩이가 겹치지 않도록)
        float minOffset = cellPixelSize * 0.5f;
        float offset = Mathf.Max(standardDeviation, minOffset);

        // 두 위치 계산
        float leftX = averageX - offset;
        float rightX = averageX + offset;

        // 화면 경계 계산
        float leftBoundary = 1 + cellPixelSize / 2f;
        float rightBoundary = 1 + (gridSize - 1) * cellPixelSize + cellPixelSize / 2f;

        // 경계 보정
        if (leftX < leftBoundary)
        {
            // 왼쪽이 경계 밖으로 나감
            float shift = leftBoundary - leftX;
            leftX = leftBoundary;
            rightX += shift;

            // 보정 후 오른쪽도 경계를 벗어나는지 확인
            if (rightX > rightBoundary)
            {
                rightX = rightBoundary;
                // 최소 간격 유지하면서 왼쪽도 조정
                leftX = rightX - (cellPixelSize * 1.0f); // 최소 1칸 간격
                if (leftX < leftBoundary)
                {
                    leftX = leftBoundary;
                }
            }
        }
        else if (rightX > rightBoundary)
        {
            // 오른쪽이 경계 밖으로 나감
            float shift = rightX - rightBoundary;
            rightX = rightBoundary;
            leftX -= shift;

            // 보정 후 왼쪽도 경계를 벗어나는지 확인
            if (leftX < leftBoundary)
            {
                leftX = leftBoundary;
                // 최소 간격 유지하면서 오른쪽도 조정
                rightX = leftX + (cellPixelSize * 1.0f); // 최소 1칸 간격
                if (rightX > rightBoundary)
                {
                    rightX = rightBoundary;
                }
            }
        }

        int targetY = GetThirdRowY();

        int leftXInt = Mathf.RoundToInt(leftX);
        int rightXInt = Mathf.RoundToInt(rightX);

        Debug.Log($"Bot dropping 2 chunks at X: {leftXInt} and {rightXInt}, Y: {targetY}");

        // 두 덩이 떨어뜨리기
        sandSimulator.DropSandChunk(leftXInt, targetY, SandSimulator.CellType.BrownSand);
        sandSimulator.DropSandChunk(rightXInt, targetY, SandSimulator.CellType.BrownSand);

        // 다음 턴을 위해 데이터 초기화
        ClearOasisData();
    }

    int GetThirdRowY()
    {
        // 상단 3번째 줄의 Y 좌표 계산
        int cellPixelSize = sandSimulator.GetCellPixelSize();
        int height = sandSimulator.GetHeight();

        // 위에서 3번째 줄 (인덱스는 2)
        int rowIndex = 2;
        int pixelY = height - 1 - (rowIndex * cellPixelSize) - cellPixelSize / 2;

        return pixelY;
    }

    public float GetAverageX() => averageX;
    public float GetStandardDeviation() => standardDeviation;
}