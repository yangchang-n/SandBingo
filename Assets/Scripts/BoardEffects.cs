using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 보드 위의 연출. 칸 제거 파티클, 점수 텍스트, Versus 승리 줄의 반복 파티클을 담당
public class BoardEffects : MonoBehaviour
{
    // 칸 제거 파티클의 수명과 점수 텍스트가 올라가는 시간
    private const float PARTICLE_LIFETIME = 1.5f;
    private const float SCORE_TEXT_DURATION = 1f;

    // Versus 승리 줄에서 파티클을 반복하는 시간 간격
    private const float VICTORY_PARTICLE_INTERVAL = 1f;

    private GameObject particlePrefab;

    // 위로 이동하는 점수 텍스트. 중간에 멈추면 직접 지워야 화면에 남지 않는다
    private readonly List<GameObject> activeScoreTexts = new List<GameObject>();

    public void Initialize(GameObject cellRemovalParticlePrefab)
    {
        particlePrefab = cellRemovalParticlePrefab;
    }

    // 줄이 완성됐을 때 점수 텍스트를 띄우고 지워질 칸에 파티클을 생성한다
    public void PlayLineClear(SandSimulator.ScoreResult scoreResult)
    {
        AudioManager.Play(Sfx.LineClear);
        SpawnScoreTexts(scoreResult.scoreLines);
        SpawnRemovalParticles(scoreResult.cellsToRemove);
    }

    // 승리 패널을 닫고 보드를 볼 때 완성된 줄이 보이도록 파티클을 계속 띄운다
    public void StartVictoryLoop(HashSet<Vector2Int> cells)
    {
        StartCoroutine(RepeatVictoryParticles(cells));
    }

    // 진행 중인 모든 연출을 멈춘다. 올라가던 점수 텍스트도 지운다
    public void StopAll()
    {
        StopAllCoroutines();

        foreach (GameObject textObj in activeScoreTexts)
        {
            if (textObj != null)
                Destroy(textObj);
        }
        activeScoreTexts.Clear();
    }

    IEnumerator RepeatVictoryParticles(HashSet<Vector2Int> cells)
    {
        WaitForSeconds interval = new WaitForSeconds(VICTORY_PARTICLE_INTERVAL);

        while (true)
        {
            SpawnRemovalParticles(cells);
            yield return interval;
        }
    }

    void SpawnRemovalParticles(HashSet<Vector2Int> cells)
    {
        if (particlePrefab == null)
        {
            Debug.LogWarning("Cell removal particle prefab not assigned!");
            return;
        }

        foreach (Vector2Int cell in cells)
        {
            Vector3 worldPos = SandSimulator.CellCenterToWorld(cell, -1f);
            GameObject particle = Instantiate(particlePrefab, worldPos, Quaternion.identity);
            Destroy(particle, PARTICLE_LIFETIME);
        }
    }

    // 줄의 양 끝 칸의 가운데에서 한 칸 위에 띄운다
    void SpawnScoreTexts(List<SandSimulator.ScoreLine> scoreLines)
    {
        foreach (var line in scoreLines)
        {
            Vector2Int firstCell = line.cells[0];
            Vector2Int lastCell = line.cells[line.cells.Count - 1];

            float avgPixelX = (SandSimulator.CellCenterPixel(firstCell.x) + SandSimulator.CellCenterPixel(lastCell.x)) / 2f;
            float avgPixelY = (SandSimulator.CellCenterPixel(firstCell.y) + SandSimulator.CellCenterPixel(lastCell.y)) / 2f;

            Vector3 startPos = new Vector3(
                SandSimulator.PixelToWorldX(avgPixelX),
                SandSimulator.PixelToWorldY(avgPixelY) + SandSimulator.CELL_PIXEL_SIZE,
                -1f);

            GameObject textObj = new GameObject("ScoreText");
            textObj.transform.position = startPos;

            TextMesh textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = line.score >= 0 ? $"+{line.score}" : $"{line.score}";
            textMesh.fontSize = 12;
            textMesh.characterSize = 100;
            textMesh.color = Color.white;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontStyle = FontStyle.Bold;

            MeshRenderer meshRenderer = textObj.GetComponent<MeshRenderer>();
            meshRenderer.sortingOrder = SandBoardRenderer.SORTING_OWNERSHIP_TEXT;

            textObj.transform.localScale = Vector3.one * 0.1f;

            activeScoreTexts.Add(textObj);
            StartCoroutine(AnimateScoreText(textObj, startPos));
        }
    }

    // 두 칸 높이만큼 올라가며 사라진다
    IEnumerator AnimateScoreText(GameObject textObj, Vector3 startPos)
    {
        float elapsed = 0f;
        Vector3 endPos = startPos + new Vector3(0, SandSimulator.CELL_PIXEL_SIZE * 2, 0);
        TextMesh textMesh = textObj.GetComponent<TextMesh>();
        Color startColor = textMesh.color;

        while (elapsed < SCORE_TEXT_DURATION)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / SCORE_TEXT_DURATION;

            textObj.transform.position = Vector3.Lerp(startPos, endPos, t);

            Color newColor = startColor;
            newColor.a = 1f - t;
            textMesh.color = newColor;

            yield return null;
        }

        activeScoreTexts.Remove(textObj);
        Destroy(textObj);
    }
}
