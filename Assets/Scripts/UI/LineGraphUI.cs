using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Minimal in-house line graph for the Control Center miner dashboard's per-automaton $/min
    // series - no existing chart component anywhere in the project to build on. Renders each
    // series as a polyline of thin pooled Image segments inside plotArea, normalized to the
    // largest value across all currently-set series so every automaton's line shares one scale.
    public class LineGraphUI : MonoBehaviour
    {
        [SerializeField] private RectTransform plotArea;
        [SerializeField] private Image segmentPrefab;
        [SerializeField] private Color[] seriesColors = { Color.cyan, Color.yellow, Color.magenta };

        private readonly List<List<Image>> segmentPoolBySeries = new();

        // One entry per automaton series, oldest-to-newest $/min buckets.
        public void SetSeries(IReadOnlyList<IReadOnlyList<float>> seriesList)
        {
            if (plotArea == null || segmentPrefab == null)
            {
                Debug.LogError($"{nameof(LineGraphUI)} on {name} is missing plotArea or segmentPrefab.");
                return;
            }

            float maxValue = 0.01f;
            foreach (var series in seriesList)
            {
                foreach (var v in series) maxValue = Mathf.Max(maxValue, v);
            }

            for (int s = 0; s < seriesList.Count; s++)
            {
                DrawSeries(s, seriesList[s], maxValue);
            }

            for (int s = seriesList.Count; s < segmentPoolBySeries.Count; s++)
            {
                foreach (var seg in segmentPoolBySeries[s]) seg.gameObject.SetActive(false);
            }
        }

        private void DrawSeries(int seriesIndex, IReadOnlyList<float> values, float maxValue)
        {
            var pool = GetOrCreatePool(seriesIndex);

            if (values.Count < 2)
            {
                foreach (var seg in pool) seg.gameObject.SetActive(false);
                return;
            }

            float width = plotArea.rect.width;
            float height = plotArea.rect.height;
            int segmentCount = values.Count - 1;

            for (int i = 0; i < segmentCount; i++)
            {
                var segment = GetOrCreateSegment(pool, i);
                Vector2 from = PointFor(i, values[i], values.Count, width, height, maxValue);
                Vector2 to = PointFor(i + 1, values[i + 1], values.Count, width, height, maxValue);
                Vector2 diff = to - from;

                segment.rectTransform.anchoredPosition = from;
                segment.rectTransform.sizeDelta = new Vector2(diff.magnitude, segment.rectTransform.sizeDelta.y);
                segment.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);
                segment.color = seriesColors.Length > 0 ? seriesColors[seriesIndex % seriesColors.Length] : Color.white;
                segment.gameObject.SetActive(true);
            }

            for (int i = segmentCount; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }

        private static Vector2 PointFor(int index, float value, int pointCount, float width, float height, float maxValue)
        {
            float x = pointCount <= 1 ? 0f : width * index / (pointCount - 1);
            float y = height * Mathf.Clamp01(value / maxValue);
            return new Vector2(x, y);
        }

        private List<Image> GetOrCreatePool(int seriesIndex)
        {
            while (segmentPoolBySeries.Count <= seriesIndex) segmentPoolBySeries.Add(new List<Image>());
            return segmentPoolBySeries[seriesIndex];
        }

        private Image GetOrCreateSegment(List<Image> pool, int index)
        {
            if (index < pool.Count) return pool[index];

            var segment = Instantiate(segmentPrefab, plotArea);
            segment.rectTransform.anchorMin = Vector2.zero;
            segment.rectTransform.anchorMax = Vector2.zero;
            segment.rectTransform.pivot = new Vector2(0f, 0.5f);
            pool.Add(segment);
            return segment;
        }
    }
}
