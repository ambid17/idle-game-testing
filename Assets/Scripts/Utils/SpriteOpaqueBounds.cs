using System.Collections.Generic;
using UnityEngine;

// Top of a sprite's tight mesh (its opaque pixels), unlike Sprite.bounds which is the whole rect.
// Animation frames bob and shake inside a fixed rect, so things worn on top (automaton hats, player
// accessories) re-read the current frame's top every frame to ride along. Cached per sprite - the
// frames never change at runtime.
public static class SpriteOpaqueBounds
{
    private static readonly Dictionary<Sprite, float> tops = new();

    public static float Top(Sprite sprite)
    {
        if (sprite == null) return 0f;
        if (tops.TryGetValue(sprite, out float top)) return top;

        top = float.MinValue;
        foreach (var vertex in sprite.vertices) top = Mathf.Max(top, vertex.y);
        tops[sprite] = top;
        return top;
    }
}
