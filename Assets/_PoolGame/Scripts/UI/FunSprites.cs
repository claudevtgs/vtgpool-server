using UnityEngine;

namespace VTG.Pool.UI
{
    /// <summary>Procedural sprites for the comic / celebration effects (no imported art needed).</summary>
    public static class FunSprites
    {
        private static Sprite turtle;
        private static Sprite rays;
        private static Sprite vignette;

        /// <summary>Full-screen edge glow (transparent centre) to tint the screen border.</summary>
        public static Sprite Vignette => vignette != null ? vignette : (vignette = DrawVignette());

        private static Sprite DrawVignette()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - c) / c;
                    float dy = Mathf.Abs(y - c) / c;
                    float edge = Mathf.Clamp01((Mathf.Max(dx, dy) - 0.55f) / 0.45f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(edge * edge * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>A cartoon turtle seen from the side, walking right.</summary>
        public static Sprite Turtle => turtle != null ? turtle : (turtle = DrawTurtle());

        /// <summary>A white sun-burst (12 rays) to rotate behind an emblem.</summary>
        public static Sprite Rays => rays != null ? rays : (rays = DrawRays());

        private static Sprite DrawTurtle()
        {
            const int w = 256;
            const int h = 160;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[w * h];
            var skin = new Color32(150, 200, 90, 255);
            var skinDark = new Color32(95, 145, 55, 255);
            var shell = new Color32(70, 140, 60, 255);
            var shellLight = new Color32(120, 185, 80, 255);
            var rim = new Color32(200, 170, 80, 255);
            var outline = new Color32(30, 50, 25, 255);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color32 c = new Color32(0, 0, 0, 0);
                    // Legs (behind the shell).
                    if (Ellipse(x, y, 78, 38, 18, 26) || Ellipse(x, y, 168, 38, 18, 26)) c = skinDark;
                    // Tail and head.
                    if (Ellipse(x, y, 40, 62, 18, 8)) c = skinDark;
                    if (Ellipse(x, y, 214, 78, 30, 24)) c = skin;
                    // Shell dome with rim.
                    if (Ellipse(x, y, 124, 62, 84, 64) && y >= 50) c = rim;
                    if (Ellipse(x, y, 124, 62, 78, 60) && y >= 56)
                    {
                        c = shell;
                        // Hexagon-ish plates.
                        if (Ellipse(x, y, 124, 92, 22, 18) || Ellipse(x, y, 82, 76, 18, 15) || Ellipse(x, y, 166, 76, 18, 15)) c = shellLight;
                    }

                    // Eye and smile.
                    if (Ellipse(x, y, 224, 86, 7, 8)) c = new Color32(255, 255, 255, 255);
                    if (Ellipse(x, y, 226, 86, 3, 4)) c = outline;
                    if (Ellipse(x, y, 228, 68, 10, 3) && y < 69) c = outline;
                    pixels[y * w + x] = c;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
        }

        private static Sprite DrawRays()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c;
                    float dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / c;
                    float angle = Mathf.Atan2(dy, dx);
                    float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 6f)), 6f);
                    float alpha = Mathf.Clamp01(ray * (1f - r) * 1.6f + Mathf.Clamp01(1f - r * 2.2f) * 0.6f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite ghost;
        private static Sprite tumbleweed;
        private static Sprite rainCloud;
        private static Sprite sweat;

        /// <summary>The cue ball's ghost: white ball-ish sheet with a wavy hem, eyes, mouth and a halo.</summary>
        public static Sprite Ghost => ghost != null ? ghost : (ghost = DrawGhost());

        /// <summary>A brown tangle of twigs (rolls across the screen in silence).</summary>
        public static Sprite Tumbleweed => tumbleweed != null ? tumbleweed : (tumbleweed = DrawTumbleweed());

        /// <summary>A grey cloud with rain streaks under it.</summary>
        public static Sprite RainCloud => rainCloud != null ? rainCloud : (rainCloud = DrawRainCloud());

        /// <summary>A light-blue sweat drop.</summary>
        public static Sprite Sweat => sweat != null ? sweat : (sweat = DrawSweat());

        private static Sprite DrawGhost()
        {
            const int w = 160;
            const int h = 220;
            var pixels = new Color32[w * h];
            var body = new Color32(245, 248, 255, 235);
            var outline = new Color32(40, 50, 70, 255);
            var halo = new Color32(255, 215, 80, 255);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color32 c = new Color32(0, 0, 0, 0);
                    // Halo above the head.
                    bool ring = Ellipse(x, y, 80, 200, 46, 12) && !Ellipse(x, y, 80, 200, 36, 6);
                    if (ring) c = halo;
                    // Head (circle) + body (rectangle) with a wavy hem.
                    float hem = 28f + 9f * Mathf.Sin(x * 0.16f);
                    bool inBody = (Ellipse(x, y, 80, 120, 66, 66)) || (x >= 14 && x <= 146 && y >= hem && y <= 120);
                    bool inEdge = (Ellipse(x, y, 80, 120, 70, 70)) || (x >= 10 && x <= 150 && y >= hem - 4 && y <= 120);
                    if (inEdge && !inBody) c = outline;
                    if (inBody) c = body;
                    // Eyes and an "o" mouth.
                    if (Ellipse(x, y, 58, 132, 9, 13) || Ellipse(x, y, 102, 132, 9, 13)) c = outline;
                    if (Ellipse(x, y, 80, 98, 8, 10) && !Ellipse(x, y, 80, 98, 4, 6)) c = outline;
                    pixels[y * w + x] = c;
                }
            }

            return Make(pixels, w, h);
        }

        private static Sprite DrawTumbleweed()
        {
            const int size = 160;
            var pixels = new Color32[size * size];
            var random = new System.Random(7);
            float c = size * 0.5f;
            // Overlapping loops (wobbly circles) of twig strokes.
            for (int loop = 0; loop < 14; loop++)
            {
                float cx = c + (float)(random.NextDouble() - 0.5) * 40f;
                float cy = c + (float)(random.NextDouble() - 0.5) * 40f;
                float r = 30f + (float)random.NextDouble() * 34f;
                float wobble = (float)random.NextDouble() * 6f;
                byte shade = (byte)(120 + random.Next(60));
                var twig = new Color32(shade, (byte)(shade * 0.72f), (byte)(shade * 0.4f), 255);
                for (int step = 0; step < 720; step++)
                {
                    float a = step / 720f * Mathf.PI * 2f;
                    float rr = r + Mathf.Sin(a * 5f + loop) * wobble;
                    int px = Mathf.RoundToInt(cx + Mathf.Cos(a) * rr);
                    int py = Mathf.RoundToInt(cy + Mathf.Sin(a) * rr);
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int x = px + dx;
                            int y = py + dy;
                            if (x >= 0 && x < size && y >= 0 && y < size && (x - c) * (x - c) + (y - c) * (y - c) < (c - 2) * (c - 2))
                            {
                                pixels[y * size + x] = twig;
                            }
                        }
                    }
                }
            }

            return Make(pixels, size, size);
        }

        private static Sprite DrawRainCloud()
        {
            const int w = 300;
            const int h = 220;
            var pixels = new Color32[w * h];
            var cloud = new Color32(110, 118, 132, 245);
            var dark = new Color32(80, 86, 98, 255);
            var rain = new Color32(120, 170, 255, 210);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color32 c = new Color32(0, 0, 0, 0);
                    // Rain streaks (slanted) below the cloud.
                    if (y < 110 && y > 6 && ((x + y / 3) % 26) < 3 && ((y / 18) + (x / 26)) % 2 == 0 && x > 50 && x < 250) c = rain;
                    bool puff = Ellipse(x, y, 90, 140, 62, 46) || Ellipse(x, y, 160, 165, 72, 52) || Ellipse(x, y, 220, 140, 58, 42) || Ellipse(x, y, 150, 125, 110, 30);
                    if (puff) c = y < 128 ? dark : cloud;
                    pixels[y * w + x] = c;
                }
            }

            return Make(pixels, w, h);
        }

        private static Sprite DrawSweat()
        {
            const int w = 64;
            const int h = 96;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Round bottom, pointed top.
                    float top = Mathf.Clamp01((y - 30f) / 60f);
                    float half = y < 30 ? 0f : Mathf.Lerp(24f, 0f, top);
                    bool drop = Ellipse(x, y, 32, 30, 26, 26) || (y >= 30 && Mathf.Abs(x - 32) < half);
                    bool shine = Ellipse(x, y, 24, 34, 6, 9);
                    pixels[y * w + x] = drop ? (shine ? new Color32(240, 250, 255, 255) : new Color32(120, 200, 255, 240)) : new Color32(0, 0, 0, 0);
                }
            }

            return Make(pixels, w, h);
        }

        private static Sprite Make(Color32[] pixels, int w, int h)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static bool Ellipse(int x, int y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx;
            float dy = (y - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }
    }
}
