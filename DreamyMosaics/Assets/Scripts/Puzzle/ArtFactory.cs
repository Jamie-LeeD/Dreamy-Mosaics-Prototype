using UnityEngine;

namespace DreamyMosaics.Puzzle
{
    public static class ArtFactory
    {
        public static Sprite CreateMemorySprite()
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / radius;
                    float dy = (y - center) / radius;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float halo = Mathf.Exp(-distance * distance * 4.2f);
                    float core = Mathf.Exp(-distance * distance * 22f);
                    float alpha = Mathf.Clamp01(halo * 0.72f + core);
                    if (alpha < 0.02f)
                        alpha = 0f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        public static Sprite CreateHeartSprite()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color fill = new Color(0.86f, 0.28f, 0.42f, 1f);
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;
            FillCircle(pixels, size, 20, 38, 14, fill);
            FillCircle(pixels, size, 44, 38, 14, fill);
            for (int y = 8; y <= 36; y++)
            {
                float t = (y - 8f) / 28f;
                int half = Mathf.RoundToInt(Mathf.Lerp(6f, 22f, t));
                for (int x = 32 - half; x <= 32 + half; x++)
                    Set(pixels, size, x, y, fill);
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static void FillCircle(Color[] pixels, int size, int cx, int cy, int radius, Color color)
        {
            int r2 = radius * radius;
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy <= r2)
                        Set(pixels, size, x, y, color);
                }
            }
        }

        static void Set(Color[] pixels, int size, int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= size || y >= size)
                return;
            pixels[y * size + x] = color;
        }
    }
}
