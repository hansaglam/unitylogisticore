using UnityEngine;

namespace LogistiCore.UI
{
    public static class SafeAreaLayout
    {
        public static void CalculateAnchors(Rect safeArea, float screenWidth, float screenHeight, out Vector2 anchorMin, out Vector2 anchorMax)
        {
            if (screenWidth <= 0f || screenHeight <= 0f || safeArea.width <= 0f || safeArea.height <= 0f)
            {
                anchorMin = Vector2.zero;
                anchorMax = Vector2.one;
                return;
            }

            var minX = Mathf.Clamp01(safeArea.xMin / screenWidth);
            var minY = Mathf.Clamp01(safeArea.yMin / screenHeight);
            var maxX = Mathf.Clamp01(safeArea.xMax / screenWidth);
            var maxY = Mathf.Clamp01(safeArea.yMax / screenHeight);
            if (maxX < minX)
                maxX = minX;
            if (maxY < minY)
                maxY = minY;

            anchorMin = new Vector2(minX, minY);
            anchorMax = new Vector2(maxX, maxY);
        }
    }
}
