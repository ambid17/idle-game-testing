using UnityEngine;
using UnityEngine.UI;

public static class UnityExtensions
{
    private const string IconForegroundName = "IconForeground";

    // UI counterpart of ChunkTilemapView's dirt-behind-ore layering: with a background, the Image
    // shows the background and a child Image (created on first use, stretched over it) shows the
    // foreground on top. Without one, the Image shows the sprite directly and the child is hidden.
    public static void SetIcon(this Image image, Sprite sprite, Sprite background)
    {
        var foregroundTransform = image.transform.Find(IconForegroundName);
        if (background == null)
        {
            image.sprite = sprite;
            if (foregroundTransform != null) foregroundTransform.gameObject.SetActive(false);
            return;
        }

        Image foreground;
        if (foregroundTransform == null)
        {
            var go = new GameObject(IconForegroundName, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(image.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            foreground = go.GetComponent<Image>();
            foreground.raycastTarget = false;
            foreground.preserveAspect = image.preserveAspect;
        }
        else
        {
            foreground = foregroundTransform.GetComponent<Image>();
            foregroundTransform.gameObject.SetActive(true);
        }

        image.sprite = background;
        foreground.sprite = sprite;
    }

    public static bool Contains(this LayerMask mask, int layer)
    {
        return mask == (mask | (1 << layer));
    }

    public static string ToFormattedString(this Vector3 vector, int decimalPlaces = 2)
    {
        string format = "F" + decimalPlaces;
        return $"({vector.x.ToString(format)}, {vector.y.ToString(format)}, {vector.z.ToString(format)})";
    }

    public static string ToFormattedString(this Vector2 vector, int decimalPlaces = 2)
    {
        string format = "F" + decimalPlaces;
        return $"({vector.x.ToString(format)}, {vector.y.ToString(format)})";
    }

    public static string ToFormattedString(this Vector2Int vector)
    {
        return $"({vector.x}, {vector.y})";
    }

    public static string ToFormattedString(this Vector2Int? vector)
    {
        if(vector == null) return "(null)";
        return $"({vector?.x}, {vector?.y})";
    }
}
