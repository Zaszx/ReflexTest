using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class GameManager
{
    private sealed class TransitionCellLayout
    {
        public GameSquare cell;
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 sourceCenter, sourceSize, destinationCenter, destinationSize;
        public bool added, removed;
        public int shell;
    }

    private readonly List<TransitionCellLayout> transitionCellLayouts = new List<TransitionCellLayout>();
    private readonly List<GameSquare> retiredGridCells = new List<GameSquare>();
    private readonly Vector3[] transitionBoundsCorners = new Vector3[4];
    private GridLayoutGroup transitionLayout;
    private int transitionSourceCount, transitionDestinationCount;

    private void PrepareGridResize(int sourceCount, int destinationCount, float sourceSide)
    {
        transitionSourceCount = Mathf.Max(2, sourceCount);
        transitionDestinationCount = Mathf.Max(2, destinationCount);
        transitionLayout = gridContentRoot.GetComponent<GridLayoutGroup>();
        // Capture the real settled layout before releasing its driven transforms.
        Canvas.ForceUpdateCanvases();
        var oldCells = instantiatedSquares.ToArray();
        var centers = new Vector2[oldCells.Length];
        var sizes = new Vector2[oldCells.Length];
        for (int i = 0; i < oldCells.Length; i++)
        {
            var rect = (RectTransform)oldCells[i].transform;
            centers[i] = (Vector2)rect.localPosition / Mathf.Max(1, sourceSide);
            sizes[i] = rect.rect.size / Mathf.Max(1, sourceSide);
        }
        transitionLayout.enabled = false;
        instantiatedSquares.Clear();
        transitionCellLayouts.Clear();
        retiredGridCells.Clear();

        float destinationCell = NormalizedTransitionCellSide(transitionDestinationCount);
        for (int y = 0; y < transitionDestinationCount; y++)
        {
            for (int x = 0; x < transitionDestinationCount; x++)
            {
                bool added = x >= transitionSourceCount || y >= transitionSourceCount;
                int oldIndex = y * transitionSourceCount + x;
                GameSquare cell = added ? CreateGridCell(x, y) : oldCells[oldIndex];
                cell.gridX = x; cell.gridY = y;
                cell.transform.SetSiblingIndex(instantiatedSquares.Count);
                instantiatedSquares.Add(cell);
                Vector2 destination = TransitionCellCenter(x, y, transitionDestinationCount);
                var item = TrackTransitionCell(cell);
                item.added = added;
                item.sourceCenter = added ? destination : centers[oldIndex];
                item.sourceSize = added ? Vector2.one * destinationCell : sizes[oldIndex];
                item.destinationCenter = destination;
                item.destinationSize = Vector2.one * destinationCell;
                item.shell = Mathf.Max(x, y) - transitionSourceCount;
            }
        }
        for (int y = 0; y < transitionSourceCount; y++)
        {
            for (int x = 0; x < transitionSourceCount; x++)
            {
                if (x < transitionDestinationCount && y < transitionDestinationCount) continue;
                int index = y * transitionSourceCount + x;
                var item = TrackTransitionCell(oldCells[index]);
                item.removed = true;
                item.sourceCenter = item.destinationCenter = centers[index];
                item.sourceSize = item.destinationSize = sizes[index];
                // Outer shells disappear first, working inward to the shared square.
                item.shell = transitionSourceCount - 1 - Mathf.Max(x, y);
                retiredGridCells.Add(oldCells[index]);
            }
        }
    }

    private TransitionCellLayout TrackTransitionCell(GameSquare cell)
    {
        var rect = (RectTransform)cell.transform;
        var group = cell.GetComponent<CanvasGroup>();
        if (group == null) group = cell.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = group.interactable = false;
        cell.SetAnimationsPaused(true);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        var item = new TransitionCellLayout { cell = cell, rect = rect, group = group };
        transitionCellLayouts.Add(item);
        return item;
    }

    private static float NormalizedTransitionCellSide(int count) => (1f - .025f * (count - 1)) / count;

    private static Vector2 TransitionCellCenter(int x, int y, int count)
    {
        float side = NormalizedTransitionCellSide(count);
        return new Vector2(-.5f + side * .5f + x * (side + .025f),
            .5f - side * .5f - y * (side + .025f));
    }

    private float RenderGridResize(float progress)
    {
        bool growing = transitionDestinationCount > transitionSourceCount;
        float layoutProgress = growing ? Mathf.InverseLerp(0, .6f, progress) : Mathf.InverseLerp(.4f, 1, progress);
        float pose = NeonMotion.Ease(layoutProgress, NeonMotion.T.levelTransitionEasing);
        int shells = Mathf.Abs(transitionDestinationCount - transitionSourceCount);
        float shellDuration = shells > 1 ? .24f : .4f;
        foreach (var item in transitionCellLayouts)
        {
            float visibility = 1;
            float shellOffset = item.shell / (float)Mathf.Max(1, shells - 1);
            if (item.added)
            {
                float start = .6f + .16f * shellOffset;
                visibility = NeonMotion.Ease(Mathf.InverseLerp(start, start + shellDuration, progress), NeonMotion.T.levelTransitionEasing);
            }
            else if (item.removed)
            {
                float start = .16f * shellOffset;
                visibility = 1 - NeonMotion.Ease(Mathf.InverseLerp(start, start + shellDuration, progress), NeonMotion.T.levelTransitionEasing);
            }
            item.rect.anchoredPosition = Vector2.Lerp(item.sourceCenter, item.destinationCenter, pose) * baseGridSide;
            item.rect.sizeDelta = Vector2.Lerp(item.sourceSize, item.destinationSize, pose) * baseGridSide;
            item.rect.localScale = Vector3.one * (NeonTheme.ReducedEffects ? 1 : Mathf.Lerp(.2f, 1, visibility));
            item.group.alpha = visibility;
        }
        return pose;
    }

    private void ContainResizingGrid()
    {
        // A rotating board can reach a wider AABB between two individually valid
        // endpoints. Constrain only the visual transform, never saved motion state.
        var allowed = boundsRoot.rect;
        float padding = Mathf.Min(allowed.width, allowed.height) * gameConfig.gameplayBoundsPaddingNormalized;
        allowed.xMin += padding; allowed.xMax -= padding;
        allowed.yMin += padding; allowed.yMax -= padding;
        GetTransitionBounds(out Vector2 min, out Vector2 max);
        Vector2 extent = max - min;
        float fit = Mathf.Min(1, allowed.width / Mathf.Max(1, extent.x), allowed.height / Mathf.Max(1, extent.y));
        gridContentRoot.localScale *= fit;
        GetTransitionBounds(out min, out max);
        Vector2 shift = Vector2.zero;
        if (min.x < allowed.xMin) shift.x = allowed.xMin - min.x;
        else if (max.x > allowed.xMax) shift.x = allowed.xMax - max.x;
        if (min.y < allowed.yMin) shift.y = allowed.yMin - min.y;
        else if (max.y > allowed.yMax) shift.y = allowed.yMax - max.y;
        gridContentRoot.position += boundsRoot.TransformVector(shift);
    }

    private void GetTransitionBounds(out Vector2 min, out Vector2 max)
    {
        gridContentRoot.GetWorldCorners(transitionBoundsCorners);
        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector3 corner in transitionBoundsCorners)
        {
            Vector2 point = boundsRoot.InverseTransformPoint(corner);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
    }

    private void FinishGridResize()
    {
        if (transitionLayout == null) return;
        foreach (var item in transitionCellLayouts)
        {
            if (item.removed)
            {
                item.cell.gameObject.SetActive(false);
                Destroy(item.cell.gameObject);
            }
            else
            {
                item.rect.localScale = Vector3.one;
                item.group.alpha = 1;
                item.group.blocksRaycasts = item.group.interactable = true;
            }
        }
        transitionLayout.enabled = true;
        LayoutRebuilder.ForceRebuildLayoutImmediate(gridContentRoot);
        transitionLayout = null;
        transitionCellLayouts.Clear();
        retiredGridCells.Clear();
    }
}
