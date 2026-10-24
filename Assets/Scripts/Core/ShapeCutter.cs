using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 把洞的格子集合切成一组灵石形状，保证一定能拼满洞口。
    /// 贪心放置 + 最多 30 次重试；重试用于满足「每洞 ≤ maxPieces 块」的约束。
    /// 无法用形状池覆盖的残余格退化为单格块（保证可拼满）。
    /// </summary>
    public static class ShapeCutter
    {
        public const int DefaultMaxPieces = 8;

        public static List<List<Vector2Int>> Cut(List<Vector2Int> holeCells, ShapePoolConfig shapePool,
            int maxPieces = DefaultMaxPieces, System.Random rng = null)
        {
            if (rng == null)
            {
                rng = new System.Random();
            }
            List<List<Vector2Int>> best = null;
            int bestCount = int.MaxValue;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                List<List<Vector2Int>> result = TryCutOnce(holeCells, shapePool, rng);
                if (result.Count <= maxPieces)
                {
                    return result;
                }
                if (result.Count < bestCount)
                {
                    best = result;
                    bestCount = result.Count;
                }
            }
            return best;
        }

        private static List<List<Vector2Int>> TryCutOnce(List<Vector2Int> holeCells,
            ShapePoolConfig shapePool, System.Random rng)
        {
            var remaining = new List<Vector2Int>(holeCells);
            var remainingSet = new HashSet<Vector2Int>(holeCells);
            var result = new List<List<Vector2Int>>();
            List<List<Vector2Int>> variants = BuildVariants(shapePool);
            if (variants.Count == 0)
            {
                // 形状池为空：全部退化为单格
                foreach (Vector2Int cell in holeCells)
                {
                    result.Add(new List<Vector2Int> { Vector2Int.zero });
                }
                return result;
            }

            while (remaining.Count > 0)
            {
                // 取剩余格中最左上角作为锚点：从角落开始装填，最大限度避免孤立碎片
                int anchorIndex = 0;
                for (int i = 1; i < remaining.Count; i++)
                {
                    if (remaining[i].x < remaining[anchorIndex].x
                        || (remaining[i].x == remaining[anchorIndex].x && remaining[i].y < remaining[anchorIndex].y))
                    {
                        anchorIndex = i;
                    }
                }
                Vector2Int anchor = remaining[anchorIndex];
                bool placed = false;

                for (int k = 0; k < variants.Count && !placed; k++)
                {
                    List<Vector2Int> variant = variants[k];
                    foreach (Vector2Int alignCell in variant)
                    {
                        if (!CanPlaceAligned(variant, alignCell, anchor, remainingSet))
                        {
                            continue;
                        }
                        var piece = new List<Vector2Int>(variant.Count);
                        foreach (Vector2Int cell in variant)
                        {
                            var target = new Vector2Int(
                                anchor.x + cell.x - alignCell.x,
                                anchor.y + cell.y - alignCell.y);
                            piece.Add(new Vector2Int(cell.x - alignCell.x, cell.y - alignCell.y));
                            remainingSet.Remove(target);
                            remaining.Remove(target);
                        }
                        result.Add(piece);
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    remainingSet.Remove(anchor);
                    remaining.Remove(anchor);
                    result.Add(new List<Vector2Int> { Vector2Int.zero });
                }
            }
            return result;
        }

        private static bool CanPlaceAligned(List<Vector2Int> variant, Vector2Int alignCell,
            Vector2Int anchor, HashSet<Vector2Int> remainingSet)
        {
            foreach (Vector2Int cell in variant)
            {
                var target = new Vector2Int(
                    anchor.x + cell.x - alignCell.x,
                    anchor.y + cell.y - alignCell.y);
                if (!remainingSet.Contains(target))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 可解性校验：给定「剩余格」与「尚未使用的灵石形状」，判断能否恰好完全覆盖。
        /// 用于放置前的死锁防护——玩家可以自由选择落点，但会让洞口再也拼不满的落点会被拒绝。
        ///
        /// 算法：取最左上角剩余格（任何可行铺法都必须有某块覆盖它），
        /// 枚举「哪块灵石、哪种旋转、用它的哪个格子对准该格」，递归搜索。
        /// 空洞规模 ≤45 格、块数 ≤8，分支有限；超过节点上限时放行（宁可放行也不误拒合法操作）。
        /// </summary>
        public static bool CanTile(List<Vector2Int> remainingCells, List<List<Vector2Int>> pieces)
        {
            if (remainingCells == null || pieces == null)
            {
                return false;
            }
            int remainingCount = remainingCells.Count;
            int pieceCellCount = 0;
            foreach (List<Vector2Int> piece in pieces)
            {
                pieceCellCount += piece.Count;
            }
            if (remainingCount != pieceCellCount)
            {
                return false; // 格子数与剩余块总格数不一致，必然拼不满
            }
            if (remainingCount == 0)
            {
                return true;
            }
            var remaining = new HashSet<Vector2Int>(remainingCells);
            var used = new bool[pieces.Count];
            int nodes = 0;
            return TryTile(remaining, pieces, used, ref nodes);
        }

        /// <summary>搜索节点上限：防止极端形状导致卡顿；超出后放行。</summary>
        private const int CanTileNodeLimit = 200000;

        private static bool TryTile(HashSet<Vector2Int> remaining, List<List<Vector2Int>> pieces,
            bool[] used, ref int nodes)
        {
            if (remaining.Count == 0)
            {
                return true;
            }
            if (++nodes > CanTileNodeLimit)
            {
                return true; // 预算用尽：倾向于放行，避免误拒玩家操作
            }

            // 取最左上角剩余格：必须被某一块覆盖
            var target = new Vector2Int(int.MaxValue, int.MaxValue);
            foreach (Vector2Int cell in remaining)
            {
                if (cell.x < target.x || (cell.x == target.x && cell.y < target.y))
                {
                    target = cell;
                }
            }

            for (int i = 0; i < pieces.Count; i++)
            {
                if (used[i])
                {
                    continue;
                }
                List<Vector2Int> rotated = pieces[i];
                for (int r = 0; r < 4; r++)
                {
                    if (r > 0)
                    {
                        rotated = Rotate90(rotated);
                    }
                    foreach (Vector2Int alignCell in rotated)
                    {
                        var covered = new List<Vector2Int>(rotated.Count);
                        bool fits = true;
                        foreach (Vector2Int cell in rotated)
                        {
                            var t = new Vector2Int(
                                target.x + cell.x - alignCell.x,
                                target.y + cell.y - alignCell.y);
                            if (!remaining.Contains(t))
                            {
                                fits = false;
                                break;
                            }
                            covered.Add(t);
                        }
                        if (!fits)
                        {
                            continue;
                        }
                        foreach (Vector2Int t in covered)
                        {
                            remaining.Remove(t);
                        }
                        used[i] = true;
                        if (TryTile(remaining, pieces, used, ref nodes))
                        {
                            return true;
                        }
                        used[i] = false;
                        foreach (Vector2Int t in covered)
                        {
                            remaining.Add(t);
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>展开形状池的 4 种旋转（去重），并按权重重复条目实现加权。</summary>
        private static List<List<Vector2Int>> BuildVariants(ShapePoolConfig shapePool)
        {
            var variants = new List<List<Vector2Int>>();
            var seen = new HashSet<string>();
            foreach (ShapeEntry entry in shapePool.GetShapes())
            {
                if (entry == null || entry.cells == null || entry.cells.Count == 0)
                {
                    continue;
                }
                int repeat = Mathf.Clamp(Mathf.RoundToInt(entry.weight), 1, 5);
                for (int r = 0; r < 4; r++)
                {
                    var rotated = new List<Vector2Int>(entry.cells);
                    for (int step = 0; step < r; step++)
                    {
                        rotated = Rotate90(rotated);
                    }
                    string key = GetSignature(rotated);
                    if (!seen.Add(key))
                    {
                        continue;
                    }
                    for (int i = 0; i < repeat; i++)
                    {
                        variants.Add(rotated);
                    }
                }
            }
            // 大形状优先：先尝试覆盖更多格子，显著减少单格碎块
            variants.Sort((a, b) => b.Count.CompareTo(a.Count));
            return variants;
        }

        private static List<Vector2Int> Rotate90(List<Vector2Int> cells)
        {
            var result = new List<Vector2Int>(cells.Count);
            foreach (Vector2Int cell in cells)
            {
                result.Add(new Vector2Int(cell.y, -cell.x));
            }
            return result;
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
    }
}
