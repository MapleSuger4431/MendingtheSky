using System.IO;
using UnityEditor;
using UnityEngine;

namespace MendingtheSky.EditorTools
{
    /// <summary>
    /// 纯程序生成 16 张洞口瓦片（2026-10-28 第三版）。
    ///
    /// 目标：天被撕开的口子。
    /// - 洞体：近黑平涂 + 极淡噪点 + 少量暗红余烬点；
    /// - 边缘：多倍频平滑噪声做撕裂起伏，崩裂点处猛地加宽，四条边各自独立避免镜像对称；
    /// - 光带：1.2px 白芯 → 5px 紫层 → 6px 青白辉光（三次衰减，收紧不雾）；
    /// - 角部：两条暴露边相交处切出缺口，并沿斜线补辉光，读作撕裂而不是贴图漏洞。
    ///
    /// 掩码：1=上邻 2=下邻 4=左邻 8=右邻（同属一个洞）。
    /// </summary>
    public static class HoleTileGenerator
    {
        private const string OutDir = "Assets/Art/Hole";

        // ---- 洞体 ----
        private const int TileSize = 128;
        private static readonly Color BodyColor = new Color(0.031f, 0.020f, 0.043f, 1f);
        private const float BodyNoise = 0.012f;
        private const float EmberChance = 0.0006f;
        private const int Seed = 20261028;

        // ---- 光带 ----
        private const float CoreWidth = 1.2f;
        private const float PurpleWidth = 5.0f;
        private const float GlowWidth = 6.0f;
        private static readonly Color CoreColor = Color.white;
        private static readonly Color PurpleColor = new Color(0.78f, 0.52f, 0.98f, 1f);
        private static readonly Color GlowColor = new Color(0.70f, 0.90f, 1f, 1f);

        // ---- 撕裂感 ----
        private const float JagAmplitude = 5f;
        private const int BurstCount = 3;
        private const float CornerCutSize = 26f;

        [MenuItem("MendingtheSky/生成洞口瓦片")]
        public static void Generate()
        {
            for (int mask = 0; mask < 16; mask++)
            {
                Color[] pixels = BakeTile(mask);
                SaveTile(pixels, $"{OutDir}/hole_tile_{mask}.png");
            }
            AssetDatabase.Refresh();
            Debug.Log("[HoleTile] 已生成 16 张洞口瓦片（v3：分边崩裂 + 紫层 + 锐利衰减）");
        }

        private static float ValueNoise(float t, int seed)
        {
            int i = Mathf.FloorToInt(t);
            float f = t - i;
            float a = Hash(i, seed);
            float b = Hash(i + 1, seed);
            float u = f * f * (3f - 2f * f);
            return Mathf.Lerp(a, b, u) * 2f - 1f;
        }

        private static float Hash(int x, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + seed * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        private static float EdgeJitter(float along01, int seed, float[] burstPositions)
        {
            float t = along01 * TileSize;
            float n = ValueNoise(t * 0.06f, seed) * JagAmplitude
                      + ValueNoise(t * 0.17f, seed + 7) * JagAmplitude * 0.45f
                      + ValueNoise(t * 0.42f, seed + 13) * JagAmplitude * 0.22f
                      + ValueNoise(t * 1.10f, seed + 29) * JagAmplitude * 0.10f;

            for (int i = 0; i < burstPositions.Length; i++)
            {
                float d = Mathf.Abs(along01 - burstPositions[i]);
                if (d < 0.08f)
                {
                    n += Mathf.Pow(1f - d / 0.08f, 2f) * JagAmplitude * 2.2f;
                }
            }
            return n;
        }

        private static Color[] BakeTile(int mask)
        {
            bool openUp = (mask & 1) == 0;
            bool openDown = (mask & 2) == 0;
            bool openLeft = (mask & 4) == 0;
            bool openRight = (mask & 8) == 0;

            var rng = new System.Random(Seed + mask * 977);
            var pixels = new Color[TileSize * TileSize];
            int seed = Seed + mask * 131;

            float[] MakeBursts()
            {
                var arr = new float[BurstCount];
                for (int i = 0; i < BurstCount; i++)
                {
                    arr[i] = 0.10f + 0.80f * (float)rng.NextDouble();
                }
                return arr;
            }

            float[] burstsUp = MakeBursts();
            float[] burstsDown = MakeBursts();
            float[] burstsLeft = MakeBursts();
            float[] burstsRight = MakeBursts();

            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 0; x < TileSize; x++)
                {
                    float noise = (float)rng.NextDouble();
                    Color c = BodyColor;
                    c.r += (noise - 0.5f) * BodyNoise;
                    c.g += (noise - 0.5f) * BodyNoise;
                    c.b += (noise - 0.5f) * BodyNoise;
                    c.a = 1f;
                    if ((float)rng.NextDouble() < EmberChance)
                    {
                        c = Color.Lerp(c, new Color(0.28f, 0.08f, 0.08f, 1f), 0.8f);
                    }

                    float distance = float.MaxValue;
                    if (openUp)
                    {
                        float d = TileSize - 1 - y + EdgeJitter((float)x / TileSize, seed, burstsUp);
                        if (d < distance) distance = d;
                    }
                    if (openDown)
                    {
                        float d = y + EdgeJitter((float)x / TileSize, seed + 3, burstsDown);
                        if (d < distance) distance = d;
                    }
                    if (openLeft)
                    {
                        float d = x + EdgeJitter((float)y / TileSize, seed + 5, burstsLeft);
                        if (d < distance) distance = d;
                    }
                    if (openRight)
                    {
                        float d = TileSize - 1 - x + EdgeJitter((float)y / TileSize, seed + 11, burstsRight);
                        if (d < distance) distance = d;
                    }

                    if (distance == float.MaxValue)
                    {
                        pixels[y * TileSize + x] = c;
                        continue;
                    }

                    if (distance < CoreWidth)
                    {
                        c = CoreColor;
                    }
                    else if (distance < PurpleWidth)
                    {
                        float t = (distance - CoreWidth) / Mathf.Max(0.01f, PurpleWidth - CoreWidth);
                        c = Color.Lerp(CoreColor, PurpleColor, t);
                    }
                    else if (distance < GlowWidth)
                    {
                        float t = (distance - PurpleWidth) / Mathf.Max(0.01f, GlowWidth - PurpleWidth);
                        float glow = (1f - t) * (1f - t) * (1f - t);
                        c = Color.Lerp(c, GlowColor, glow * 0.92f);
                    }

                    if ((openUp && openLeft && x < CornerCutSize && TileSize - 1 - y < CornerCutSize) ||
                        (openUp && openRight && TileSize - 1 - x < CornerCutSize && TileSize - 1 - y < CornerCutSize) ||
                        (openDown && openLeft && x < CornerCutSize && y < CornerCutSize) ||
                        (openDown && openRight && TileSize - 1 - x < CornerCutSize && y < CornerCutSize))
                    {
                        float localX = openLeft ? x : TileSize - 1 - x;
                        float localY = openDown ? y : TileSize - 1 - y;
                        float cut = CornerCutSize + ValueNoise(localX * 0.2f, seed + 17) * 6f;
                        float cheb = localX + localY;

                        if (cheb < cut)
                        {
                            c.a = 0f;
                        }
                        else if (cheb < cut + GlowWidth)
                        {
                            float t = (cheb - cut) / GlowWidth;
                            float glow = (1f - t) * (1f - t);
                            c = Color.Lerp(c, GlowColor, glow * 0.9f);
                            if (cheb < cut + CoreWidth * 1.5f)
                            {
                                c = Color.Lerp(c, CoreColor, 0.85f);
                            }
                        }
                    }

                    pixels[y * TileSize + x] = c;
                }
            }
            ApplyInteriorCracks(pixels, rng);
            return pixels;
        }

        /// <summary>
        /// 洞内裂痕：从 AI 生成的裂纹光带里取裂纹线，以很低透明度叠在洞体内部，
        /// 每格 1~2 条、随机旋转与偏移，让洞里「有东西在裂」而不是一片纯黑。
        /// </summary>
        private static void ApplyInteriorCracks(Color[] pixels, System.Random rng)
        {
            const string crackPath = "Assets/TJGenerators/History/Sprite_20261028_131336.png";
            if (!File.Exists(crackPath))
            {
                return;
            }
            var crackTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!crackTex.LoadImage(File.ReadAllBytes(crackPath)))
            {
                Object.DestroyImmediate(crackTex);
                return;
            }

            int count = 1 + rng.Next(2);
            for (int i = 0; i < count; i++)
            {
                float scale = 0.55f + 0.35f * (float)rng.NextDouble();
                // 角度限制在一个较窄的族里（约 ±25°），避免三格方向完全无关
                float angle = 0.55f + 0.85f * (float)rng.NextDouble();
                float cx = TileSize * (0.3f + 0.4f * (float)rng.NextDouble());
                float cy = TileSize * (0.3f + 0.4f * (float)rng.NextDouble());
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                for (int y = 0; y < TileSize; y++)
                {
                    for (int x = 0; x < TileSize; x++)
                    {
                        int idx = y * TileSize + x;
                        if (pixels[idx].a < 0.5f)
                        {
                            continue;
                        }

                        float dx = x - cx;
                        float dy = y - cy;
                        float lx = (dx * cos + dy * sin) / scale;
                        float ly = (-dx * sin + dy * cos) / scale;
                        float u = 0.5f + lx / TileSize;
                        float v = 0.5f + ly / TileSize;
                        if (u < 0f || u > 1f || v < 0f || v > 1f)
                        {
                            continue;
                        }

                        Color crack = crackTex.GetPixel(
                            Mathf.Clamp((int)(u * crackTex.width), 0, crackTex.width - 1),
                            Mathf.Clamp((int)(v * crackTex.height), 0, crackTex.height - 1));
                        // 掐掉裂纹贴图里的高亮星芒（否则会读成星光/脏点），并压成偏紫的暗线
                        float a = Mathf.Min(crack.a, 0.5f) * 0.25f;
                        if (a <= 0.01f)
                        {
                            continue;
                        }
                        Color tint = Color.Lerp(new Color(0.58f, 0.50f, 0.74f, 1f), crack, 0.35f);
                        pixels[idx] = Color.Lerp(pixels[idx], tint, a);
                    }
                }
            }
            Object.DestroyImmediate(crackTex);
        }

        private static void SaveTile(Color[] pixels, string path)
        {
            var tex = new Texture2D(TileSize, TileSize, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti != null)
            {
                var st = new TextureImporterSettings();
                ti.ReadTextureSettings(st);
                st.spriteMeshType = SpriteMeshType.FullRect;
                st.spriteBorder = Vector4.zero;
                st.spritePixelsPerUnit = 100f;
                ti.SetTextureSettings(st);
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
        }
    }
}
