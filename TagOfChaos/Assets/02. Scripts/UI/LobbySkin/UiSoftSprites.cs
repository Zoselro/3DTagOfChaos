using UnityEngine;

// 로비 효과(Request1009Plan.md §79)에 쓰는 부드러운 그림을 코드로 한 번 만들어 둔다 — 그림 파일이 필요 없다.
// 빛 판(둥근 사각형 바깥 빛, 9분할), 동그란 빛 점, 네 갈래 별, 이어지는 안개 무늬.
public static class UiSoftSprites
{
    private static Sprite glow, dot, star;
    private static Texture2D fog;

    // 둥근 사각형 둘레가 부드럽게 퍼지는 빛(가운데는 투명에 가깝다). 9분할 — 어떤 크기에도 늘어난다.
    public static Sprite Glow
    {
        get
        {
            if (glow != null) return glow;
            const int size = 128, edge = 48;
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // 안쪽 사각형(edge 안)까지의 거리 → 바깥으로 갈수록 옅어지고, 안쪽 깊이 들어가도 옅어진다(테두리 둘레만 빛)
                    float dx = Mathf.Max(edge - x, 0f, x - (size - 1 - edge));
                    float dy = Mathf.Max(edge - y, 0f, y - (size - 1 - edge));
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / edge;          // 0 = 안쪽 사각형, 1 = 그림 끝
                    float inner = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y)) / (float)edge; // 안쪽 깊이
                    float a = d > 0f ? Mathf.Pow(1f - Mathf.Clamp01(d), 2.2f) : Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(inner - 1f));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(a)));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            glow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(edge, edge, edge, edge));
            glow.name = "UiGlow";
            return glow;
        }
    }

    // 가운데가 밝고 둘레로 사라지는 점(반딧불·등불·보석 빛).
    public static Sprite Dot
    {
        get
        {
            if (dot != null) return dot;
            const int size = 64;
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = new Vector2(x - (size - 1) * 0.5f, y - (size - 1) * 0.5f).magnitude / (size * 0.5f);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            dot = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            dot.name = "UiDot";
            return dot;
        }
    }

    // 네 갈래로 뻗는 반짝임(보석).
    public static Sprite Star
    {
        get
        {
            if (star != null) return star;
            const int size = 64;
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - c) / c, dy = Mathf.Abs(y - c) / c;
                    float ray = Mathf.Max(Mathf.Clamp01(1f - dx) * Mathf.Clamp01(1f - dy * 9f), Mathf.Clamp01(1f - dy) * Mathf.Clamp01(1f - dx * 9f));
                    float core = Mathf.Clamp01(1f - new Vector2(dx, dy).magnitude * 2.5f);
                    float a = Mathf.Clamp01(Mathf.Pow(ray, 1.5f) + core);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            star = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            star.name = "UiStar";
            return star;
        }
    }

    // 좌우·위아래로 이어지는 안개 무늬(값 잡음 몇 겹). 흰색 + 알파.
    public static Texture2D Fog
    {
        get
        {
            if (fog != null) return fog;
            const int w = 256, h = 128;
            fog = NewTexture(w, h);
            fog.wrapModeU = TextureWrapMode.Repeat;
            fog.wrapModeV = TextureWrapMode.Clamp; // 위쪽은 서서히 사라진다(아래 fade)
            var rng = new System.Random(79);
            var px = new Color32[w * h];
            float[] amp = { 0.55f, 0.3f, 0.15f };
            int[] cells = { 4, 8, 16 };
            var grids = new float[3][,];
            for (int o = 0; o < 3; o++)
            {
                int cx = cells[o], cy = Mathf.Max(2, cells[o] / 2);
                grids[o] = new float[cx, cy];
                for (int i = 0; i < cx; i++) for (int j = 0; j < cy; j++) grids[o][i, j] = (float)rng.NextDouble();
            }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = 0f;
                    for (int o = 0; o < 3; o++)
                    {
                        int cx = grids[o].GetLength(0), cy = grids[o].GetLength(1);
                        float fx = x / (float)w * cx, fy = y / (float)h * cy;
                        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                        float tx = Smooth(fx - x0), ty = Smooth(fy - y0);
                        float a = grids[o][x0 % cx, y0 % cy], b = grids[o][(x0 + 1) % cx, y0 % cy];
                        float c = grids[o][x0 % cx, (y0 + 1) % cy], d = grids[o][(x0 + 1) % cx, (y0 + 1) % cy];
                        v += amp[o] * Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
                    }
                    float fade = Smooth(Mathf.Clamp01((1f - y / (float)(h - 1)) / 0.7f)) * Smooth(Mathf.Clamp01(y / (float)(h - 1) / 0.12f + 0.4f)); // 위 70%에서 0으로, 맨 아래 살짝
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(255 * fade * Mathf.Clamp01((v - 0.35f) * 1.8f)));
                }
            fog.SetPixels32(px);
            fog.Apply(false, true);
            fog.name = "UiFog";
            return fog;
        }
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static Texture2D NewTexture(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
}
