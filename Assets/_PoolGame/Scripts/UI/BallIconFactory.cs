using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Balls;

namespace VTG.Pool.UI
{
    /// <summary>Procedural HUD sprites (ball icons, circles). Generated once and cached.</summary>
    public static class BallIconFactory
    {
        private const int Size = 64;
        private static readonly Dictionary<int, Sprite> BallSprites = new Dictionary<int, Sprite>();
        private static Sprite circle;

        /// <summary>Plain white anti-aliased circle (tint with Image.color).</summary>
        public static Sprite Circle
        {
            get
            {
                if (circle == null)
                {
                    circle = CreateSprite("Circle", (x, y, r) => Color.white);
                }

                return circle;
            }
        }

        public static Sprite ForBall(int number)
        {
            if (BallSprites.TryGetValue(number, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            Color color = BallDefinition.StandardColor(number);
            bool stripe = number >= 9;
            sprite = CreateSprite($"BallIcon_{number}", (x, y, r) =>
            {
                if (number == 0)
                {
                    return color;
                }

                if (r < 0.32f)
                {
                    return Color.white;
                }

                if (stripe)
                {
                    return Mathf.Abs(y) < 0.45f ? color : new Color(0.96f, 0.95f, 0.9f);
                }

                return color;
            });
            BallSprites[number] = sprite;
            return sprite;
        }

        private delegate Color Shader(float x, float y, float radius);

        private static Sprite CreateSprite(string name, Shader shader)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            var pixels = new Color[Size * Size];
            float half = Size * 0.5f;
            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    float x = (px + 0.5f - half) / half;
                    float y = (py + 0.5f - half) / half;
                    float r = Mathf.Sqrt(x * x + y * y);
                    float alpha = Mathf.Clamp01((1f - r) * half);
                    Color c = shader(x, y, r);
                    c.a *= alpha;
                    pixels[py * Size + px] = c;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
