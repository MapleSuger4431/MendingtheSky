using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 灵石形状：以 baseCells 的 (0,0) 格为锚点，预计算 4 份旋转（顺时针 90° / 步）。
    /// cell 坐标约定：x = row，y = col；旋转映射 (r,c) → (c,-r)（屏幕 y 向下）。
    /// 旋转只改索引，数据只读；锚点 (0,0) 永远属于形状，旋转后不归一化。
    /// </summary>
    public class LingShiShape
    {
        private List<List<Vector2Int>> rotations;
        private int rotationSteps;
        private List<Vector2Int> baseCells;

        public void Init(List<Vector2Int> baseCells)
        {
            this.baseCells = new List<Vector2Int>(baseCells);
            PrecomputeRotations(this.baseCells);
            rotationSteps = 0;
        }

        public void Rotate()
        {
            rotationSteps = (rotationSteps + 1) % 4;
        }

        public void ResetRotation()
        {
            rotationSteps = 0;
        }

        public void SetRotationSteps(int steps)
        {
            rotationSteps = ((steps % 4) + 4) % 4;
        }

        public List<Vector2Int> GetCurrentCells()
        {
            return new List<Vector2Int>(rotations[rotationSteps]);
        }

        public int GetRotationSteps()
        {
            return rotationSteps;
        }

        public List<Vector2Int> GetBaseCells()
        {
            return new List<Vector2Int>(baseCells);
        }

        public int GetCellCount()
        {
            return baseCells.Count;
        }

        /// <summary>
        /// 旋转是否会改变形状的格子集合（以锚点 (0,0) 为基准）。
        /// 单格等旋转不变形返回 false —— 这种形状不需要初始随机朝向。
        /// </summary>
        public bool HasDistinctRotations()
        {
            if (rotations == null || rotations.Count < 2)
            {
                return false;
            }
            string baseSignature = GetSignature(rotations[0]);
            for (int i = 1; i < rotations.Count; i++)
            {
                if (GetSignature(rotations[i]) != baseSignature)
                {
                    return true;
                }
            }
            return false;
        }

        private static string GetSignature(List<Vector2Int> cells)
        {
            var copy = new List<Vector2Int>(cells);
            copy.Sort((a, b) => a.x != b.x ? a.x - b.x : a.y - b.y);
            var sb = new System.Text.StringBuilder();
            foreach (Vector2Int cell in copy)
            {
                sb.Append(cell.x).Append(',').Append(cell.y).Append(';');
            }
            return sb.ToString();
        }

        public LingShiShape Clone()
        {
            var clone = new LingShiShape();
            clone.Init(baseCells);
            clone.SetRotationSteps(rotationSteps);
            return clone;
        }

        private void PrecomputeRotations(List<Vector2Int> baseCells)
        {
            rotations = new List<List<Vector2Int>>(4);
            var current = new List<Vector2Int>(baseCells);
            rotations.Add(new List<Vector2Int>(current));
            for (int i = 1; i < 4; i++)
            {
                current = Rotate90(current);
                rotations.Add(current);
            }
        }

        /// <summary>顺时针 90°：(r,c) → (c,-r)。(0,0) 锚点保持不变。</summary>
        private static List<Vector2Int> Rotate90(List<Vector2Int> cells)
        {
            var result = new List<Vector2Int>(cells.Count);
            foreach (Vector2Int cell in cells)
            {
                result.Add(new Vector2Int(cell.y, -cell.x));
            }
            return result;
        }
    }
}
