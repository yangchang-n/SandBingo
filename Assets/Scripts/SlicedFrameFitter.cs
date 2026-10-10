using UnityEngine;

// Sliced 방식 프레임 스프라이트를 대상 크기에 맞추는 공용 함수. 보드 프레임과 게이지 프레임에서 사용
public static class SlicedFrameFitter
{
    // Sliced 방식은 전체를 키워도 테두리가 원본 픽셀 두께로 그려져서 크게 늘리면 테두리가 상대적으로 얇아진다
    // 따라서 원하는 테두리 두께가 나오는 스케일을 먼저 구하고 전체 크기는 그 스케일로 나눠서 대상 크기에 맞춘다
    // borderThickness 가 0 이하이면 두께 보정 없이 원본 이미지를 그대로 늘린다
    public static void Fit(SpriteRenderer frame, float contentWidth, float contentHeight, Vector3 position, float borderThickness, int sortingOrder)
    {
        if (frame == null) return;

        frame.drawMode = SpriteDrawMode.Sliced;

        Sprite frameSprite = frame.sprite;
        float scale = 1f;

        if (frameSprite != null && borderThickness > 0f)
        {
            Vector4 border = frameSprite.border;
            float averageBorderPixels = (border.x + border.y + border.z + border.w) / 4f;

            if (averageBorderPixels > 0f)
                scale = borderThickness * frameSprite.pixelsPerUnit / averageBorderPixels;
        }

        // 프레임 테두리의 중심이 대상 가장자리에 오도록 전체 크기를 테두리 두께만큼 크게 설정
        // 2를 빼는 것은 직접 확인해서 맞춘 보정값
        float targetWidth = contentWidth + borderThickness - 2;
        float targetHeight = contentHeight + borderThickness - 2;

        frame.transform.localScale = new Vector3(scale, scale, 1f);
        frame.size = new Vector2(targetWidth / scale, targetHeight / scale);
        frame.transform.position = position;

        frame.sortingOrder = sortingOrder;
    }
}
