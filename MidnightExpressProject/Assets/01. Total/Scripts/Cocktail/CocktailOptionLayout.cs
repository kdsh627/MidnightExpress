using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class CocktailOptionLayout : MonoBehaviour
{
    [SerializeField] private Vector2 _cellSize = new Vector2(184f, 110f);
    [SerializeField, Min(0f)] private float _horizontalSpacing = 16f;
    [SerializeField, Min(0f)] private float _verticalSpacing = 12f;

    private readonly List<RectTransform> _activeChildren = new List<RectTransform>(8);

    public void RefreshLayout()
    {
        CollectActiveChildren();
        int count = _activeChildren.Count;
        if (count == 0)
        {
            return;
        }

        int firstRowCount;
        int secondRowCount;
        if (count <= 4)
        {
            firstRowCount = count;
            secondRowCount = 0;
        }
        else if (count <= 6)
        {
            firstRowCount = 3;
            secondRowCount = count - firstRowCount;
        }
        else
        {
            firstRowCount = 4;
            secondRowCount = count - firstRowCount;
        }

        float rowOffset = secondRowCount > 0
            ? (_cellSize.y + _verticalSpacing) * 0.5f
            : 0f;
        PositionRow(0, firstRowCount, rowOffset);
        if (secondRowCount > 0)
        {
            PositionRow(firstRowCount, secondRowCount, -rowOffset);
        }
    }

    private void CollectActiveChildren()
    {
        _activeChildren.Clear();
        for (int index = 0; index < transform.childCount; index++)
        {
            Transform child = transform.GetChild(index);
            if (!child.gameObject.activeSelf || !(child is RectTransform rect))
            {
                continue;
            }

            _activeChildren.Add(rect);
        }
    }

    private void PositionRow(int startIndex, int count, float y)
    {
        float step = _cellSize.x + _horizontalSpacing;
        float firstX = -(count - 1) * step * 0.5f;

        for (int rowIndex = 0; rowIndex < count; rowIndex++)
        {
            RectTransform child = _activeChildren[startIndex + rowIndex];
            child.anchorMin = child.anchorMax = new Vector2(0.5f, 0.5f);
            child.pivot = new Vector2(0.5f, 0.5f);
            child.sizeDelta = _cellSize;
            child.anchoredPosition = new Vector2(
                Mathf.Round(firstX + rowIndex * step),
                Mathf.Round(y));
            child.localScale = Vector3.one;
        }
    }

    private void OnEnable()
    {
        RefreshLayout();
    }

    private void OnValidate()
    {
        _cellSize.x = Mathf.Max(1f, Mathf.Round(_cellSize.x));
        _cellSize.y = Mathf.Max(1f, Mathf.Round(_cellSize.y));
        _horizontalSpacing = Mathf.Max(0f, Mathf.Round(_horizontalSpacing));
        _verticalSpacing = Mathf.Max(0f, Mathf.Round(_verticalSpacing));
        RefreshLayout();
    }

    private void OnTransformChildrenChanged()
    {
        RefreshLayout();
    }

    private void OnRectTransformDimensionsChange()
    {
        RefreshLayout();
    }
}
