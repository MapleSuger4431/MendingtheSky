using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 把形状格子翻译成玩家可读的名称（用于洞口「还缺什么形状」的 HUD 提示）。
    /// 通过规范化签名匹配内置形状表；未知形状回退为「N格形状」。
    /// </summary>
    public static class ShapeDescriber
    {
        private static readonly Dictionary<string, string> names = BuildNames();

        public static string Describe(List<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return "空形状";
            }
            string signature = ShapeCutter.GetCanonicalSignature(cells);
            if (names.TryGetValue(signature, out string name))
            {
                return name;
            }
            return $"{cells.Count}格形状";
        }

        private static Dictionary<string, string> BuildNames()
        {
            var result = new Dictionary<string, string>();
            void Add(string name, params int[] flat)
            {
                var cells = new List<Vector2Int>();
                for (int i = 0; i + 1 < flat.Length; i += 2)
                {
                    cells.Add(new Vector2Int(flat[i], flat[i + 1]));
                }
                result[ShapeCutter.GetCanonicalSignature(cells)] = name;
            }

            Add("单格", 0, 0);
            Add("二连", 0, 0, 0, 1);
            Add("三连", 0, 0, 0, 1, 0, 2);
            Add("L形（3格）", 0, 0, 1, 0, 1, 1);
            Add("O形（2×2）", 0, 0, 0, 1, 1, 0, 1, 1);
            Add("四连", 0, 0, 0, 1, 0, 2, 0, 3);
            Add("T形", 0, 0, 0, 1, 0, 2, 1, 1);
            Add("S形", 0, 1, 0, 2, 1, 0, 1, 1);
            Add("Z形", 0, 0, 0, 1, 1, 1, 1, 2);
            Add("J形", 0, 0, 1, 0, 2, 0, 2, 1);
            Add("L形", 0, 1, 1, 1, 2, 0, 2, 1);
            Add("P形", 0, 0, 0, 1, 1, 0, 1, 1, 2, 0);
            Add("P形（镜像）", 0, 0, 0, 1, 1, 0, 1, 1, 2, 1);
            Add("U形", 0, 0, 0, 2, 1, 0, 1, 1, 1, 2);
            Add("十字形", 0, 1, 1, 0, 1, 1, 1, 2, 2, 1);
            return result;
        }
    }
}
