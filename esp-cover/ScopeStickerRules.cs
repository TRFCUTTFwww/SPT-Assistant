using System;
using System.Numerics;
using System.Globalization;

namespace LocalSpt5Esp;

internal static class ScopeStickerRules
{
    internal readonly record struct Layout(float X, float Y, float Width, float Height);
    internal readonly record struct Rgb(byte R, byte G, byte B)
    {
        internal string Hex => $"#{R:X2}{G:X2}{B:X2}";
    }

    internal static bool TryHex(string? text, out Rgb color)
    {
        color = default;
        string value = (text ?? "").Trim();
        if (value.StartsWith('#')) value = value[1..];
        if (value.Length != 6 || !uint.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint rgb)) return false;
        color = new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb); return true;
    }

    internal static bool TryRgb(string r, string g, string b, out Rgb color)
    {
        color = default;
        if (!byte.TryParse(r, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte red) ||
            !byte.TryParse(g, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte green) ||
            !byte.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte blue)) return false;
        color = new(red, green, blue); return true;
    }

    internal static byte ContrastLevel(float brightness, bool linear) => brightness <= (linear ? .18f : .5f) ? (byte)255 : (byte)0;
    internal static bool FlashActive(float now, float start, float duration) =>
        float.IsFinite(now) && float.IsFinite(start) && float.IsFinite(duration) && duration > 0 && now >= start && now - start < duration;

    internal static bool TryLayout(int imageWidth, int imageHeight, int targetWidth, int targetHeight,
        float widthPercent, float xPercent, float yPercent, out Layout layout, float rotation = 0)
    {
        layout = default;
        if (imageWidth <= 0 || imageHeight <= 0 || targetWidth <= 0 || targetHeight <= 0 ||
            !float.IsFinite(widthPercent) || !float.IsFinite(xPercent) || !float.IsFinite(yPercent) || !float.IsFinite(rotation)) return false;
        float width = Math.Clamp(widthPercent, 1, 150) / 100;
        // Coordinates belong to the optic texture, whose aspect can differ
        // from both the artwork and the output monitor.
        float height = width * imageHeight / imageWidth * targetWidth / targetHeight;
        float x = Math.Clamp(xPercent, -150, 150) / 100, y = Math.Clamp(yPercent, -150, 150) / 100;
        layout = new(x, y, width, height);
        float left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue, top = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            var point = Vertex(layout, i, rotation, (float)targetWidth / targetHeight);
            left = Math.Min(left, point.X); right = Math.Max(right, point.X);
            bottom = Math.Min(bottom, point.Y); top = Math.Max(top, point.Y);
        }
        return left < 1 && bottom < 1 && right > 0 && top > 0;
    }

    internal static ScopeStickerProfile Scale(ScopeStickerProfile value, float factor, float anchorX, float anchorY)
    {
        if (!float.IsFinite(factor) || factor <= 0 || !float.IsFinite(anchorX) || !float.IsFinite(anchorY)) return value;
        float width = Math.Clamp(value.Width * factor, 1, 150), ratio = width / value.Width;
        return (value with { Width = width, X = anchorX + (value.X - anchorX) * ratio,
            Y = anchorY + (value.Y - anchorY) * ratio }).Normalized();
    }

    // Normalized vertices never use camera position, projection, FOV or world
    // transforms. A late overlay draw therefore cannot inherit temporal jitter.
    internal static Vector3 Vertex(Layout layout, int index, float degrees = 0, float aspect = 1)
    {
        if (degrees == 0) return new(layout.X + (index is 1 or 2 ? layout.Width : 0),
            layout.Y + (index >= 2 ? layout.Height : 0), 0);
        float x = (index is 1 or 2 ? .5f : -.5f) * layout.Width;
        float y = (index >= 2 ? .5f : -.5f) * layout.Height;
        // Rotate around the artwork center in target pixels, rather than UV
        // units, so non-square optic textures do not shear the artwork.
        float angle = degrees * (MathF.PI / 180), cos = MathF.Cos(angle), sin = MathF.Sin(angle);
        return new(layout.X + layout.Width * .5f + x * cos + y * sin / aspect,
            layout.Y + layout.Height * .5f - x * aspect * sin + y * cos, 0);
    }
}
