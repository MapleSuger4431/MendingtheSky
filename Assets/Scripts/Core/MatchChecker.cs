using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 放置匹配检查（静态工具）：
    /// 形状覆盖的所有格必须在网格内、都是缺口格（grid==0）、且属于同一个洞。
    /// 2026-10-25 用户确认：放置阶段不做形状清单限制（自由放置优先），
    /// 形状清单只影响「预览颜色」和「洞口填满时是否愈合」（Hole.MatchesPlacedShapes）。
    /// 同形状灵石可互换，不校验来源洞。
    /// </summary>
    public static class MatchChecker
    {
        public static bool CanPlace(LingShi ls, int targetRow, int targetCol, GridManager gridManager)
        {
            return GetMatchedHole(ls, targetRow, targetCol, gridManager) != null;
        }

        /// <summary>
        /// 拖拽预览三态：
        /// 几何不可放 → Invalid（红）；
        /// 几何可放且本洞清单还有这个形状的额度 → Valid（绿）；
        /// 几何可放但清单里没有这个形状了 → ShapeNotInHole（黄，仍允许放下，只是提醒填满后不会愈合）。
        /// 未登记清单的洞（测试/调试）一律 Valid。
        /// </summary>
        public static PreviewHint GetPreviewHint(LingShi ls, int targetRow, int targetCol,
            GridManager gridManager)
        {
            Hole hole = GetMatchedHole(ls, targetRow, targetCol, gridManager);
            if (hole == null)
            {
                return PreviewHint.Invalid;
            }
            if (!hole.HasShapeEntries())
            {
                return PreviewHint.Valid;
            }
            return hole.GetRemainingShapeQuota(ls.GetShapeSignature()) > 0
                ? PreviewHint.Valid
                : PreviewHint.ShapeNotInHole;
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
