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
