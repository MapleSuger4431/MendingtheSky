using System.IO;
using UnityEditor;
using UnityEngine;

namespace MendingtheSky.EditorTools
{
    /// <summary>
    /// 按位掩码合成 16 张洞口瓦片（2026-10-28 方案）：
    /// 底 = hole_body 无缝材质，暴露的四边贴一条淡青白柔光带，相邻两条暴露边的交角做圆角遮罩。
    ///
    /// 掩码：1=上邻 2=下邻 4=左邻 8=右邻（同属一个洞）。
    /// 有邻居的一侧不画光带（洞内无缝），没邻居（暴露）的一侧才画。
    ///
    /// 用法：菜单 MendingtheSky/生成洞口瓦片，或调用 Generate()。
    /// </summary>
    public static class HoleTileGenerator
    {
        private const string BodyPath = "Assets/Art/Hole/hole_body_texture.png";
        private const string OutDir = "Assets/Art/Hole";
        private const int TileSize = 128;
        private const int BandWidth = 16;          // 光带在瓦片上的厚度（像素）
        private const float CornerRadius = 22f;    // 暴露边交角圆角半径

        [MenuItem("MendingtheSky/生成洞口瓦片")]
        public static void Generate()
        {
            Texture2D body = LoadReadableTexture(BodyPath);
            if (body == null)
            {
                Debug.LogError($"[HoleTile] 找不到洞体材质：{BodyPath}");
                return;
            }

            for (int mask = 0; mask < 16; mask++)
            {
                Color[] pixels = BakeTile(body, mask);
                SaveTile(pixels, $"{OutDir}/hole_tile_{mask}.png");
            }

            AssetDatabase.Refresh();
            Debug.Log($"[HoleTile] 已生成 16 张 {TileSize}×{TileSize} 瓦片到 {OutDir}");
        }

        /// <summary>把材质平铺到瓦片尺寸，并按掩码补光带与圆角。</summary>
        private static Color[] BakeTile(Texture2D body, int mask)
        {
            var result = new Color[TileSize * TileSize];
            int bw = body.width, bh = body.height;
            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 0; x < TileSize; x++)
                {
                    // 采样洞体材质（UV 平铺）
                    float u = (x + 0.5f) / TileSize * bw;
                    float v = (y + 0.5f) / TileSize * bh;
                    result[y * TileSize + x] = body.GetPixel((int)u % bw, (int)v % bh);
                }
            }

            bool openUp = (mask & 1) == 0;
            bool openDown = (mask & 2) == 0;
            bool openLeft = (mask & 4) == 0;
            bool openRight = (mask & 8) == 0;

            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 0; x < TileSize; x++)
                {
                    // 到各暴露边的最小距离（只在暴露边生效）
                    float distance = float.MaxValue;
                    if (openUp) distance = Mathf.Min(distance, TileSize - 1 - y);
                    if (openDown) distance = Mathf.Min(distance, y);
                    if (openLeft) distance = Mathf.Min(distance, x);
                    if (openRight) distance = Mathf.Min(distance, TileSize - 1 - x);

                    Color c = result[y * TileSize + x];

                    if (distance < BandWidth)
                    {
                        // 淡青白柔光：越靠边越亮，向内平方衰减
                        float t = 1f - Mathf.Clamp01(distance / BandWidth);
                        float glow = t * t;
                        var edge = new Color(0.78f, 0.95f, 1f, 1f);
                        c = Color.Lerp(c, edge, glow * 0.85f);
                    }

                    // 两条相邻暴露边的交角：裁成圆角（透明）
                    if ((openUp && openLeft && x < CornerRadius && TileSize - 1 - y < CornerRadius) ||
                        (openUp && openRight && TileSize - 1 - x < CornerRadius && TileSize - 1 - y < CornerRadius) ||
                        (openDown && openLeft && x < CornerRadius && y < CornerRadius) ||
                        (openDown && openRight && TileSize - 1 - x < CornerRadius && y < CornerRadius))
                    {
                        float cx = (openLeft ? CornerRadius : TileSize - 1 - CornerRadius);
                        float cy = (openDown ? CornerRadius : TileSize - 1 - CornerRadius);
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                        if (d > CornerRadius)
                        {
                            c.a = 0f; // 圆角外裁掉
                        }
                        else if (d > CornerRadius - 2f)
                        {
                            c.a *= Mathf.InverseLerp(CornerRadius, CornerRadius - 2f, d); // 一像素抗锯齿
                        }
                    }

                    result[y * TileSize + x] = c;
                }
            }
            return result;
        }

        private static void SaveTile(Color[] pixels, string path)
        {
            var tex = new Texture2D(TileSize, TileSize, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            // 导入为 Sprite（尺寸小、无边框）
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

        private static Texture2D LoadReadableTexture(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return tex.LoadImage(File.ReadAllBytes(path)) ? tex : null;
        }
    }
}
