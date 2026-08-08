using UnityEngine;

public static class UnityExtensions
{
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
