using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 放置匹配检查（静态工具）：
    /// 形状覆盖的所有格必须在网格内、都是缺口格（grid==0）、且属于同一个洞。
    /// 2026-10-24 用户反馈：不同洞口的同形状灵石可以互换，取消「灵石绑定来源洞」的硬校验
    ///（sourceHoleId 仍记录用于调试/统计，但不参与可放判定）。
    /// </summary>
    public static class MatchChecker
    {
        public static bool CanPlace(LingShi ls, int targetRow, int targetCol, GridManager gridManager)
        {
            return GetMatchedHole(ls, targetRow, targetCol, gridManager) != null;
        }

        /// <summary>目标锚点覆盖的所有绝对格（无论是否可放都返回，用于预览高亮）。</summary>
        public static List<Vector2Int> GetMatchedCells(LingShi ls, int targetRow, int targetCol,
            GridManager gridManager)
        {
            var result = new List<Vector2Int>();
            if (ls == null)
            {
                return result;
            }
            foreach (Vector2Int cell in ls.GetCurrentCells())
            {
                result.Add(new Vector2Int(targetRow + cell.x, targetCol + cell.y));
            }
            return result;
        }

        /// <summary>可放置时返回目标洞，否则返回 null。</summary>
        public static Hole GetMatchedHole(LingShi ls, int targetRow, int targetCol, GridManager gridManager)
        {
            if (!CheckShapePlacement(ls, targetRow, targetCol, gridManager, out Hole hole))
            {
                return null;
            }
            // 不再校验来源洞：形状匹配 + 同洞即可，同形状灵石可互换使用
            return hole;
        }

        private static bool CheckShapePlacement(LingShi ls, int targetRow, int targetCol,
            GridManager gridManager, out Hole hole)
        {
            hole = null;
            if (ls == null || gridManager == null)
            {
                return false;
            }
            if (ls.GetState() != LingShiState.Cooked)
            {
                return false;
            }
            foreach (Vector2Int cell in ls.GetCurrentCells())
            {
                int r = targetRow + cell.x;
                int c = targetCol + cell.y;
                if (!gridManager.IsInside(r, c))
                {
                    return false;
                }
                if (gridManager.GetCell(r, c) != GridManager.CellHole)
                {
                    return false;
                }
                Hole cellHole = gridManager.GetHoleAt(r, c);
                if (cellHole == null)
                {
                    return false;
                }
                if (hole == null)
                {
                    hole = cellHole;
                }
                else if (hole != cellHole)
                {
                    return false; // 跨洞
                }
            }
            return hole != null;
        }
    }
}
