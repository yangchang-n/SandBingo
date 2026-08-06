using UnityEngine;

// 게임씬의 튜토리얼 패널
// 대사 재생 자체는 DialogueSequenceBase 가 처리하고
// 여기서는 어느 챕터를 재생할지, 끝난 뒤 패널을 닫는 것, 강조 구멍을 옮기는 것만 담당한다
// 패널 오브젝트에 직접 붙여야 한다. 패널이 꺼져 있으면 Update 가 돌지 않아 입력도 자연히 차단된다
public class TutorialPanelUI : DialogueSequenceBase
{
    // 몇 번째 줄부터 어디를 비출지 정의한다
    // fromLine 이 작은 것부터 순서대로 적을 필요는 없고, 해당 줄 이하 중 가장 큰 것이 선택된다
    [System.Serializable]
    public class TutorialHighlight
    {
        [Tooltip("이 줄부터 아래 대상을 비춘다")]
        public int fromLine;

        [Tooltip("비울 대상. 비워두면 구멍 없이 화면 전체를 덮는다")]
        public RectTransform target;
    }

    [Header("Highlight Mask")]
    [Tooltip("사각형 네 장의 부모. 화면 전체를 덮는 크기여야 한다")]
    public RectTransform maskRoot;
    public RectTransform maskTop;
    public RectTransform maskBottom;
    public RectTransform maskLeft;
    public RectTransform maskRight;

    [Header("Highlight Timeline")]
    public TutorialHighlight[] highlights;

    // GetWorldCorners 가 매번 배열을 요구하므로 하나를 재사용한다
    private readonly Vector3[] worldCorners = new Vector3[4];

    // ===== 재생 제어 =====

    // 패널을 켜고 튜토리얼을 처음부터 재생한다
    // 이미 열려 있는 상태에서 다시 호출하면 첫 줄부터 다시 시작한다
    public void Play()
    {
        GlobalManager gm = GlobalManager.Instance;

        if (gm == null || gm.stage1TutorialChapter == null)
        {
            Debug.LogWarning("Tutorial chapter not available. Tutorial panel will stay closed.");
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        PlayChapter(gm.stage1TutorialChapter);
    }

    // 마지막 줄까지 진행했거나 스킵 버튼을 눌렀을 때 호출된다
    // GameSceneUI 의 스테이지 진입 연출이 이 패널이 꺼지기를 기다리고 있다
    protected override void OnSequenceFinished()
    {
        gameObject.SetActive(false);
    }

    // ===== 강조 구멍 =====

    protected override void OnLineChanged(int index, StoryLine line)
    {
        TutorialHighlight highlight = FindHighlight(index);

        if (highlight == null || highlight.target == null)
        {
            // 비출 대상이 없으면 넓이가 0인 구멍을 주어 화면 전체가 덮이게 한다
            ApplyHole(new Rect(0f, 0f, 0f, 0f));
            return;
        }

        ApplyHole(GetNormalizedHole(highlight.target));
    }

    // 해당 줄 이하의 fromLine 중 가장 큰 항목을 고른다
    TutorialHighlight FindHighlight(int lineIndex)
    {
        if (highlights == null) return null;

        TutorialHighlight found = null;
        int bestLine = -1;

        foreach (TutorialHighlight h in highlights)
        {
            if (h == null) continue;
            if (h.fromLine > lineIndex) continue;
            if (h.fromLine <= bestLine) continue;

            bestLine = h.fromLine;
            found = h;
        }

        return found;
    }

    // 대상의 화면상 사각형을 마스크 기준 0~1 비율로 바꾼다
    // 비율로 다루므로 해상도가 달라져도 구멍이 대상을 따라간다
    Rect GetNormalizedHole(RectTransform target)
    {
        if (maskRoot == null) return new Rect(0f, 0f, 0f, 0f);

        target.GetWorldCorners(worldCorners);

        Vector2 min = maskRoot.InverseTransformPoint(worldCorners[0]);
        Vector2 max = maskRoot.InverseTransformPoint(worldCorners[2]);

        Rect area = maskRoot.rect;

        float x0 = Mathf.Clamp01(Mathf.InverseLerp(area.xMin, area.xMax, min.x));
        float x1 = Mathf.Clamp01(Mathf.InverseLerp(area.xMin, area.xMax, max.x));
        float y0 = Mathf.Clamp01(Mathf.InverseLerp(area.yMin, area.yMax, min.y));
        float y1 = Mathf.Clamp01(Mathf.InverseLerp(area.yMin, area.yMax, max.y));

        return new Rect(x0, y0, x1 - x0, y1 - y0);
    }

    // 구멍의 위, 아래, 왼쪽, 오른쪽을 각각 한 장씩 채운다
    // hole 은 0~1 비율이며, 넓이가 0이면 위쪽 한 장이 화면 전체를 덮게 된다
    void ApplyHole(Rect hole)
    {
        SetAnchors(maskTop, new Vector2(0f, hole.yMax), new Vector2(1f, 1f));
        SetAnchors(maskBottom, new Vector2(0f, 0f), new Vector2(1f, hole.yMin));
        SetAnchors(maskLeft, new Vector2(0f, hole.yMin), new Vector2(hole.xMin, hole.yMax));
        SetAnchors(maskRight, new Vector2(hole.xMax, hole.yMin), new Vector2(1f, hole.yMax));
    }

    void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        if (rect == null) return;

        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
