using UnityEngine;
using System.Collections.Generic;

public class BotController : MonoBehaviour
{
    private SandSimulator sandSimulator;
    private List<int> oasisSandXPositions = new List<int>();

    private float averageX;
    private float standardDeviation;

    // 진흙 모양 정의 - 높이와 개수만 다르고 너비는 항상 1칸 고정
    // 난이도 번호와의 매핑은 이 클래스가 아니라 호출하는 쪽(GameManager)이 담당한다
    public struct MudPattern
    {
        public float heightCells;
        public int count;
    }

    // 개수별 초기 생성 위치 배율 (평균 대비 표준편차의 배수)
    // 그 외의 개수는 지원하지 않는다
    private static readonly Dictionary<int, float[]> OFFSET_MULTIPLIERS = new Dictionary<int, float[]>
    {
        { 1, new float[] { 0f } },
        { 2, new float[] { -1f, 1f } },
        { 3, new float[] { -1f, 0f, 1f } },
        { 4, new float[] { -0.9f, -0.3f, 0.3f, 0.9f } },
        { 5, new float[] { -1f, -0.5f, 0f, 0.5f, 1f } },
    };

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

    static float[] GetOffsetMultipliers(int count)
    {
        if (OFFSET_MULTIPLIERS.TryGetValue(count, out float[] multipliers))
            return multipliers;

        Debug.LogWarning($"GetOffsetMultipliers: {count}개에 대한 배율이 정의되어 있지 않아 평균 위치에서 균등 배치합니다");
        return new float[count];
    }

    // 진흙 모양(높이, 개수)을 직접 받아서 낙하시킨다
    // 난이도 번호가 무엇이든, 심지어 커스텀 스테이지처럼 번호 자체가 없어도 그대로 동작한다
    public void ExecuteBotTurn(MudPattern pattern)
    {
        CalculateStatistics();

        int cellPixelSize = sandSimulator.GetCellPixelSize();
        int targetY = GetThirdRowY();

        float[] positions = CalculateDropPositions(pattern.count, cellPixelSize);

        Debug.Log($"Bot Turn: Dropping {pattern.count} chunk(s), height {pattern.heightCells}, at X: {string.Join(", ", positions)}");

        foreach (float x in positions)
        {
            sandSimulator.DropSandRectangle(Mathf.RoundToInt(x), targetY,
                1f, pattern.heightCells, SandSimulator.CellType.BrownSand);
        }

        ClearOasisData();
    }

    // 오아시스 모래 통계를 바탕으로 진흙 개수만큼의 최종 생성 위치를 계산한다
    // 개수에 상관없이 이 파이프라인 하나로 전부 처리한다
    float[] CalculateDropPositions(int count, int cellPixelSize)
    {
        float minGap = cellPixelSize;
        float[] multipliers = GetOffsetMultipliers(count);

        float[] targets = new float[count];
        for (int i = 0; i < count; i++)
            targets[i] = averageX + multipliers[i] * standardDeviation;

        float[] gapped = EnforceMinimumGap(targets, minGap);

        GetBoundaries(cellPixelSize, out float leftBoundary, out float rightBoundary);
        float availableWidth = rightBoundary - leftBoundary;

        float[] scaled = ScaleToFitWidth(gapped, availableWidth, averageX);
        float[] shifted = ShiftIntoBounds(scaled, leftBoundary, rightBoundary);

        return shifted;
    }

    // 등위회귀(PAVA) - 순서를 유지하면서 최소 간격을 만족시키되 원래 목표값에서 최소한만 움직인다
    // 겹치는 구간을 무게중심 기준으로 묶어서 처리하므로 표준편차가 0이어도 항상 대칭 결과가 나온다
    static float[] EnforceMinimumGap(float[] targetPositions, float minGap)
    {
        int n = targetPositions.Length;
        if (n == 0) return targetPositions;

        float[] q = new float[n];
        for (int i = 0; i < n; i++)
            q[i] = targetPositions[i] - i * minGap;

        List<float> blockSums = new List<float>();
        List<int> blockCounts = new List<int>();

        for (int i = 0; i < n; i++)
        {
            blockSums.Add(q[i]);
            blockCounts.Add(1);

            while (blockSums.Count > 1)
            {
                int last = blockSums.Count - 1;
                float lastAvg = blockSums[last] / blockCounts[last];
                float prevAvg = blockSums[last - 1] / blockCounts[last - 1];

                if (lastAvg >= prevAvg) break;

                blockSums[last - 1] += blockSums[last];
                blockCounts[last - 1] += blockCounts[last];
                blockSums.RemoveAt(last);
                blockCounts.RemoveAt(last);
            }
        }

        float[] result = new float[n];
        int index = 0;
        for (int b = 0; b < blockSums.Count; b++)
        {
            float avg = blockSums[b] / blockCounts[b];
            for (int k = 0; k < blockCounts[b]; k++)
            {
                result[index] = avg + index * minGap;
                index++;
            }
        }

        return result;
    }

    // 전체 폭이 가용 폭보다 넓으면 무게중심(centerX) 기준으로 비율만큼 축소한다
    // 표준편차가 극단적으로 커서 양쪽 경계를 동시에 벗어나려는 경우를 처리한다
    static float[] ScaleToFitWidth(float[] positions, float availableWidth, float centerX)
    {
        int n = positions.Length;
        if (n <= 1) return positions;

        float totalWidth = positions[n - 1] - positions[0];
        if (totalWidth <= availableWidth) return positions;

        float scale = availableWidth / totalWidth;
        float[] result = new float[n];
        for (int i = 0; i < n; i++)
            result[i] = centerX + (positions[i] - centerX) * scale;

        return result;
    }

    // 폭이 가용 폭 안에 들어온 뒤에는 통째로 밀기만 하면 양쪽 경계를 모두 만족시킬 수 있다
    static float[] ShiftIntoBounds(float[] positions, float leftBoundary, float rightBoundary)
    {
        int n = positions.Length;
        if (n == 0) return positions;

        float shift = 0f;
        if (positions[0] < leftBoundary)
            shift = leftBoundary - positions[0];
        else if (positions[n - 1] > rightBoundary)
            shift = rightBoundary - positions[n - 1];

        if (shift == 0f) return positions;

        float[] result = new float[n];
        for (int i = 0; i < n; i++)
            result[i] = positions[i] + shift;

        return result;
    }

    // gridSize는 SandSimulator에서 직접 읽어 계산
    void GetBoundaries(int cellPixelSize, out float leftBoundary, out float rightBoundary)
    {
        int gridSize = sandSimulator.GetGridSize();
        leftBoundary  = 1 + cellPixelSize / 2f;
        rightBoundary = 1 + (gridSize - 1) * cellPixelSize + cellPixelSize / 2f;
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
