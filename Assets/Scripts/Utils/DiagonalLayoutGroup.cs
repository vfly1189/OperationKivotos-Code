using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Layout/Diagonal Layout Group")]
public class DiagonalLayoutGroup : LayoutGroup
{
    [Header("Diagonal Settings")]
    public Vector2 spacing = new Vector2(50f, -50f); // X축, Y축 간격 (우측 하단으로 향하려면 Y를 음수로 설정)
    public Vector2 cellSize = new Vector2(100f, 100f);

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        // ContentSizeFitter를 위한 전체 너비 계산
        float totalWidth = padding.horizontal + (cellSize.x + Mathf.Abs(spacing.x)) * (rectChildren.Count - 1) + cellSize.x;
        SetLayoutInputForAxis(totalWidth, totalWidth, -1, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        // ContentSizeFitter를 위한 전체 높이 계산
        float totalHeight = padding.vertical + Mathf.Abs(spacing.y) * (rectChildren.Count - 1) + cellSize.y;
        SetLayoutInputForAxis(totalHeight, totalHeight, -1, 1);
    }

    public override void SetLayoutHorizontal()
    {
        for (int i = 0; i < rectChildren.Count; i++)
        {
            RectTransform child = rectChildren[i];
            // X축 위치 결정: 왼쪽 패딩 + (X 간격 * 인덱스)
            float posX = padding.left + (spacing.x * i);
            SetChildAlongAxis(child, 0, posX, cellSize.x);
        }
    }

    public override void SetLayoutVertical()
    {
        for (int i = 0; i < rectChildren.Count; i++)
        {
            RectTransform child = rectChildren[i];
            // Y축 위치 결정: 위쪽 패딩 + (Y 간격 * 인덱스)
            float posY = padding.top + (spacing.y * i);
            SetChildAlongAxis(child, 1, posY, cellSize.y);
        }
    }
}