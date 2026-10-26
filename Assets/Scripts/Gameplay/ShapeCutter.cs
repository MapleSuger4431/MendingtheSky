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
            var variants = BuildVariants(shapePool, rng);
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
                    var variantEntry = variants[k];
                    // 低权重形状按概率跳过（权重 0.25 ≈ 75% 直接跳过），真正降低出现率
                    if (variantEntry.weight < 0.999f && rng.NextDouble() > variantEntry.weight)
                    {
                        continue;
                    }
                    List<Vector2Int> variant = variantEntry.cells;
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

        /// <summary>
        /// 形状的规范化签名：忽略平移与旋转（同一形状的 4 个朝向视为相同）。
        /// 用于「洞口形状清单」匹配——同形状的灵石可以互换，但必须清单里还有对应条目。
        /// </summary>
        public static string GetCanonicalSignature(List<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return string.Empty;
            }
            string best = null;
            var rotated = new List<Vector2Int>(cells);
            for (int r = 0; r < 4; r++)
            {
                if (r > 0)
                {
                    rotated = Rotate90(rotated);
                }
                string sig = GetNormalizedSignature(rotated);
                if (best == null || string.CompareOrdinal(sig, best) < 0)
                {
                    best = sig;
                }
            }
            return best;
        }

        /// <summary>平移归一化（最小行列归零）+ 排序，得到稳定的形状字符串。</summary>
        private static string GetNormalizedSignature(List<Vector2Int> cells)
        {
            int minRow = int.MaxValue;
            int minCol = int.MaxValue;
            foreach (Vector2Int cell in cells)
            {
                minRow = Mathf.Min(minRow, cell.x);
                minCol = Mathf.Min(minCol, cell.y);
            }
            var normalized = new List<Vector2Int>(cells.Count);
            foreach (Vector2Int cell in cells)
            {
                normalized.Add(new Vector2Int(cell.x - minRow, cell.y - minCol));
            }
            normalized.Sort((a, b) => a.x != b.x ? a.x - b.x : a.y - b.y);
            var sb = new System.Text.StringBuilder();
            foreach (Vector2Int cell in normalized)
            {
                sb.Append(cell.x).Append(',').Append(cell.y).Append(';');
            }
            return sb.ToString();
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

        /// <summary>
        /// 展开形状池的 4 种旋转（保留旋转差异），并按权重排序：
        /// 大形状优先；同尺寸内做加权随机洗牌（权重越低越靠后 → 被选中的概率越低）。
        /// 这样「十字」等低权重形状只会在其他同尺寸形状放不下时才被尝试。
        /// </summary>
        private static List<(List<Vector2Int> cells, float weight)> BuildVariants(ShapePoolConfig shapePool, System.Random rng)
        {
            var items = new List<(List<Vector2Int> cells, float weight)>();
            var seen = new HashSet<string>();
            foreach (ShapeEntry entry in shapePool.GetShapes())
            {
                if (entry == null || entry.cells == null || entry.cells.Count == 0)
                {
                    continue;
                }
                float weight = Mathf.Max(0.01f, entry.weight);
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
                    items.Add((rotated, weight));
                }
            }

            // 尺寸降序；同尺寸内加权随机洗牌
            items.Sort((a, b) => b.cells.Count.CompareTo(a.cells.Count));
            int index = 0;
            while (index < items.Count)
            {
                int end = index;
                while (end < items.Count && items[end].cells.Count == items[index].cells.Count)
                {
                    end++;
                }
                for (int k = end - 1; k > index; k--)
                {
                    float total = 0f;
                    for (int t = index; t <= k; t++)
                    {
                        total += items[t].weight;
                    }
                    float roll = (float)(rng.NextDouble() * total);
                    int pick = k;
                    for (int t = index; t <= k; t++)
                    {
                        roll -= items[t].weight;
                        if (roll <= 0f)
                        {
                            pick = t;
                            break;
                        }
                    }
                    var tmp = items[pick];
                    items[pick] = items[k];
                    items[k] = tmp;
                }
                index = end;
            }

            var variants = new List<(List<Vector2Int> cells, float weight)>(items.Count);
            foreach (var item in items)
            {
                variants.Add(item);
            }
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
