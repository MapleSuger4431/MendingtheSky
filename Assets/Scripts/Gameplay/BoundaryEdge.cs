using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 洞口的一条边界边：某个格子的某条边。
    /// cell 约定：x = row，y = col（与 GridManager.grid[r,c] 一致）。
    /// </summary>
    public struct BoundaryEdge
    {
        public Vector2Int cell;
        public EdgeDirection direction;

        public BoundaryEdge(Vector2Int cell, EdgeDirection direction)
        {
            this.cell = cell;
            this.direction = direction;
        }
    }
}
