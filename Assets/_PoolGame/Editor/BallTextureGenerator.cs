using UnityEngine;

namespace VTG.Pool.EditorTools
{
    /// <summary>
    /// Generates equirectangular ball textures (matching Unity's sphere UVs): solid or striped body,
    /// white number circles with a bitmap-font number on two opposite sides, and red marker dots on
    /// the cue ball so spin is visible. Placeholder art until Milestone 5.
    /// </summary>
    public static class BallTextureGenerator
    {
        private const int Width = 1024;
        private const int Height = 512;

        // 5x7 digit glyphs, rows top to bottom, 5 bits per row (MSB = left).
        private static readonly byte[][] Digits =
        {
            new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E }, // 0
            new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E }, // 1
            new byte[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F }, // 2
            new byte[] { 0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E }, // 3
            new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 }, // 4
            new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E }, // 5
            new byte[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E }, // 6
            new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 }, // 7
            new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E }, // 8
            new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C }  // 9
        };

        private static readonly Color Ivory = new Color(0.96f, 0.95f, 0.90f);
        private static readonly Color Ink = new Color(0.05f, 0.05f, 0.05f);
        private static readonly Color MarkerRed = new Color(0.8f, 0.08f, 0.08f);

        // Six dots on the axes make rotation about any axis visible on the cue ball.
        private static readonly Vector3[] MarkerAxes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        public static Texture2D Generate(int number, Color color)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, true);
            var pixels = new Color[Width * Height];
            bool stripe = number >= 9;
            bool cue = number == 0;

            // Number circle centres: on the equator facing +Z and -Z (u = 0.25 / 0.75 on Unity spheres).
            Vector3 numberA = DirectionFromUv(0.25f, 0.5f);
            Vector3 numberB = DirectionFromUv(0.75f, 0.5f);
            const float circleAngle = 0.36f; // radians
            const float stripeHalfHeight = 0.42f; // |latitude| below which the stripe band is coloured

            for (int y = 0; y < Height; y++)
            {
                float v = (y + 0.5f) / Height;
                for (int x = 0; x < Width; x++)
                {
                    float u = (x + 0.5f) / Width;
                    Vector3 direction = DirectionFromUv(u, v);
                    float latitude = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f));
                    Color pixel;

                    if (cue)
                    {
                        pixel = Ivory;
                        if (IsCueMarker(direction))
                        {
                            pixel = MarkerRed;
                        }
                    }
                    else
                    {
                        pixel = stripe ? (Mathf.Abs(latitude) < stripeHalfHeight ? color : Ivory) : color;
                        if (TryNumberPixel(direction, numberA, circleAngle, number, out Color numberPixel) ||
                            TryNumberPixel(direction, numberB, circleAngle, number, out numberPixel))
                        {
                            pixel = numberPixel;
                        }
                    }

                    pixels[y * Width + x] = pixel;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        /// <summary>Unity sphere mapping: u wraps around Y, v from south (0) to north (1) pole.</summary>
        private static Vector3 DirectionFromUv(float u, float v)
        {
            float longitude = (u - 0.75f) * Mathf.PI * 2f;
            float latitude = (v - 0.5f) * Mathf.PI;
            float cosLat = Mathf.Cos(latitude);
            return new Vector3(Mathf.Sin(longitude) * cosLat * -1f, Mathf.Sin(latitude), Mathf.Cos(longitude) * cosLat * -1f).normalized;
        }

        private static bool IsCueMarker(Vector3 direction)
        {
            for (int i = 0; i < MarkerAxes.Length; i++)
            {
                if (Vector3.Angle(direction, MarkerAxes[i]) < 9f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryNumberPixel(Vector3 direction, Vector3 center, float circleAngle, int number, out Color pixel)
        {
            float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(direction, center), -1f, 1f));
            if (angle > circleAngle)
            {
                pixel = default;
                return false;
            }

            // Local tangent frame on the circle (east = right, north = up as seen from outside).
            Vector3 north = Vector3.up;
            Vector3 east = Vector3.Cross(north, center).normalized;
            Vector3 projected = direction - center * Vector3.Dot(direction, center);
            float px = Vector3.Dot(projected, east) / Mathf.Sin(circleAngle);
            float py = Vector3.Dot(projected, north) / Mathf.Sin(circleAngle);
            pixel = IsGlyphInk(number, px, py) ? Ink : Color.white;
            return true;
        }

        /// <summary>px/py in [-1, 1] inside the circle.</summary>
        private static bool IsGlyphInk(int number, float px, float py)
        {
            string text = number.ToString();
            const float glyphHeight = 1.05f;
            float glyphWidth = glyphHeight * 5f / 7f;
            float spacing = glyphWidth * 0.25f;
            float totalWidth = text.Length * glyphWidth + (text.Length - 1) * spacing;
            float left = -totalWidth * 0.5f;
            float top = glyphHeight * 0.5f;

            for (int i = 0; i < text.Length; i++)
            {
                float glyphLeft = left + i * (glyphWidth + spacing);
                float gx = (px - glyphLeft) / glyphWidth;
                float gy = (top - py) / glyphHeight;
                if (gx < 0f || gx >= 1f || gy < 0f || gy >= 1f)
                {
                    continue;
                }

                int column = Mathf.FloorToInt(gx * 5f);
                int row = Mathf.FloorToInt(gy * 7f);
                byte bits = Digits[text[i] - '0'][row];
                if ((bits & (1 << (4 - column))) != 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
