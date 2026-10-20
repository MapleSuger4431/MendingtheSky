using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 纯 C# 类：一个洞口（一组四连通的格子 + 边界边 + 填充进度）。
    /// 阶段 1 范围：cells / boundaryEdges / IsFull / MergeWith / 闭合标记接口。
    /// placedLingShi（灵石持有）与 GetQiReward 在阶段 3/4 接入。
    /// cell 坐标约定：x = row，y = col。
    /// </summary>
    public class Hole
    {
        private int holeId;
        private List<Vector2Int> cells;
        private List<BoundaryEdge> boundaryEdges;
        private int nowCellCount;
        private bool isClosing;

        public void Init(int holeId, List<Vector2Int> cells)
        {
            this.holeId = holeId;
            this.cells = new List<Vector2Int>(cells);
            nowCellCount = 0;
            isClosing = false;
            CalculateBoundaryEdges();
        }

        /// <summary>
        /// 把 other 的格子并入本洞并回收边界。按修订版蔓延规则，
        /// 只有未填过的洞（nowCellCount == 0）会参与合并。
        /// cellToHole 字典的换绑与 other 的回收由 GridManager 负责。
        /// </summary>
        public void MergeWith(Hole other)
        {
            foreach (Vector2Int cell in other.cells)
            {
                if (!cells.Contains(cell))
                {
                    cells.Add(cell);
                }
            }
            nowCellCount += other.nowCellCount;
            CalculateBoundaryEdges();
        }

        public bool IsCellInHole(int r, int c)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].x == r && cells[i].y == c)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>当前已填格子数。阶段 3 由 FillCells/UnfillCells 增减。</summary>
        public int GetNowCellCount()
        {
            return nowCellCount;
        }

        /// <summary>填满本洞需要的格子总数。</summary>
        public int GetRequiredCellCount()
        {
            return cells.Count;
        }

        public bool IsFull()
        {
            return nowCellCount >= cells.Count;
        }

        /// <summary>阶段 3 由 GridManager.FillCells/UnfillCells 调用。</summary>
        public void NotifyCellFilled()
        {
            nowCellCount++;
        }

        public void NotifyCellUnfilled()
        {
            nowCellCount = Mathf.Max(0, nowCellCount - 1);
        }

        public void StartClosing()
        {
            isClosing = true;
        }

        public bool IsClosing()
        {
            return isClosing;
        }

        public void FinishClosing()
        {
            isClosing = false;
        }

        /// <summary>返回格子列表副本，避免外部直接改内部数据。</summary>
        public List<Vector2Int> GetCells()
        {
            return new List<Vector2Int>(cells);
        }

        public List<BoundaryEdge> GetBoundaryEdges()
        {
            return new List<BoundaryEdge>(boundaryEdges);
        }

        public int GetHoleId()
        {
            return holeId;
        }

        /// <summary>
        /// 边界边 = 格子朝某个方向、且该方向邻居不在洞内的边。
        /// </summary>
        private void CalculateBoundaryEdges()
        {
            var cellSet = new HashSet<Vector2Int>(cells);
            boundaryEdges = new List<BoundaryEdge>();
            foreach (Vector2Int cell in cells)
            {
                if (!cellSet.Contains(new Vector2Int(cell.x - 1, cell.y)))
                {
                    boundaryEdges.Add(new BoundaryEdge(cell, EdgeDirection.Up));
                }
                if (!cellSet.Contains(new Vector2Int(cell.x + 1, cell.y)))
                {
                    boundaryEdges.Add(new BoundaryEdge(cell, EdgeDirection.Down));
                }
                if (!cellSet.Contains(new Vector2Int(cell.x, cell.y - 1)))
                {
                    boundaryEdges.Add(new BoundaryEdge(cell, EdgeDirection.Left));
                }
                if (!cellSet.Contains(new Vector2Int(cell.x, cell.y + 1)))
                {
                    boundaryEdges.Add(new BoundaryEdge(cell, EdgeDirection.Right));
                }
            }
        }
    }
}
