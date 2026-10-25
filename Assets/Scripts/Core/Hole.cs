using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 纯 C# 类：一个洞口（一组四连通的格子 + 边界边 + 填充进度 + 已放置灵石的登记）。
    /// 阶段 5 起维护 placedLingShi：洞口闭合时据此计算灵气返还并销毁灵石。
    /// cell 坐标约定：x = row，y = col。
    /// </summary>
    public class Hole
    {
        private int holeId;
        private List<Vector2Int> cells;
        private List<BoundaryEdge> boundaryEdges;
        private int nowCellCount;
        private bool isClosing;

        /// <summary>放置在本洞上的灵石（由 GridManager.FillCells/UnfillCells 维护）。</summary>
        private readonly List<LingShi> placedLingShi = new List<LingShi>();

        /// <summary>
        /// 本洞的「需求形状清单」：生成洞时切出的灵石形状（每个条目对应一块切出的灵石）。
        /// 放置阶段不限制形状（自由放置）；只在洞口填满时参与两件事：
        /// 1) 拖拽预览颜色（清单里还有该形状 → 绿色，没有 → 黄色，见 MatchChecker.GetPreviewHint）；
        /// 2) 愈合判定（Hole.MatchesPlacedShapes，冗余灵石不参与）。
        /// 未登记清单的洞（EditMode 测试/调试）一律通过。
        /// </summary>
        private readonly List<List<Vector2Int>> requiredShapeEntries = new List<List<Vector2Int>>();
        private bool hasShapeEntries;

        public void Init(int holeId, List<Vector2Int> cells)
        {
            this.holeId = holeId;
            this.cells = new List<Vector2Int>(cells);
            nowCellCount = 0;
            isClosing = false;
            placedLingShi.Clear();
            requiredShapeEntries.Clear();
            hasShapeEntries = false;
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
            // 正常流程只有未填洞参与合并，这里保险起见把灵石登记也合并过来
            foreach (LingShi ls in other.placedLingShi)
            {
                if (ls != null && !placedLingShi.Contains(ls))
                {
                    placedLingShi.Add(ls);
                }
            }
            // 形状清单也一并合并（正常流程合并的是未填洞，清单尚未被消耗）
            foreach (List<Vector2Int> entry in other.requiredShapeEntries)
            {
                requiredShapeEntries.Add(new List<Vector2Int>(entry));
            }
            hasShapeEntries |= other.hasShapeEntries;
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

        /// <summary>登记一块放到本洞上的灵石（同一块不会重复登记）。</summary>
        public void AddLingShi(LingShi ls)
        {
            if (ls != null && !placedLingShi.Contains(ls))
            {
                placedLingShi.Add(ls);
            }
        }

        /// <summary>取消登记（玩家从工作台取下该块时调用）。</summary>
        public void RemoveLingShi(LingShi ls)
        {
            placedLingShi.Remove(ls);
        }

        /// <summary>返回本洞已放置灵石的副本。</summary>
        public List<LingShi> GetPlacedLingShi()
        {
            return new List<LingShi>(placedLingShi);
        }

        // ---------------- 需求形状清单（2026-10-25 用户设计） ----------------

        /// <summary>登记一条需求形状（生成洞时为每块切出的灵石登记一次）。</summary>
        public void AddRequiredShapeEntry(List<Vector2Int> cells)
        {
            requiredShapeEntries.Add(new List<Vector2Int>(cells));
            hasShapeEntries = true;
        }

        /// <summary>本洞是否登记过形状清单（测试/调试洞可能没有）。</summary>
        public bool HasShapeEntries()
        {
            return hasShapeEntries;
        }

        /// <summary>
        /// 愈合判定（2026-10-25 定稿）：
        /// 洞内每一块「非冗余」灵石的形状，都要能在清单里抵扣掉一条同签名（旋转无关）的条目。
        /// - 冗余灵石（sourceHoleId == -1）没有来源洞，不参与校验；
        /// - 不要求清单被全部抵扣：冗余灵石占掉的格子，对应的就是清单里没被用上的条目。
        ///   洞格数 = 清单格数 = 已放格数，所以无冗余灵石时「每块都能抵扣」自动等价于「多重集合完全相等」。
        /// 未登记清单的洞（EditMode 测试/调试）默认通过。
        /// </summary>
        public bool MatchesPlacedShapes()
        {
            if (!hasShapeEntries)
            {
                return true;
            }
            var required = BuildRequiredCounts();
            foreach (LingShi ls in placedLingShi)
            {
                if (ls == null || ls.GetSourceHoleId() < 0)
                {
                    continue; // 冗余补给灵石：不占清单额度
                }
                string sig = ls.GetShapeSignature();
                if (!required.TryGetValue(sig, out int count) || count <= 0)
                {
                    return false; // 出现清单外的形状（很可能是别的洞切出来的）
                }
                required[sig] = count - 1;
            }
            return true;
        }

        /// <summary>
        /// 清单里还有几条这个形状没被洞内「非冗余」灵石抵扣（≤0 表示没有额度了）。
        /// 拖拽预览据此区分绿色（还有额度，放下去有助于愈合）与黄色（清单里没有这个形状）。
        /// </summary>
        public int GetRemainingShapeQuota(string signature)
        {
            if (!hasShapeEntries || string.IsNullOrEmpty(signature))
            {
                return 0;
            }
            var remaining = BuildRequiredCounts();
            foreach (LingShi ls in placedLingShi)
            {
                if (ls == null || ls.GetSourceHoleId() < 0)
                {
                    continue;
                }
                string sig = ls.GetShapeSignature();
                if (remaining.TryGetValue(sig, out int count) && count > 0)
                {
                    remaining[sig] = count - 1;
                }
            }
            return remaining.TryGetValue(signature, out int quota) ? Mathf.Max(0, quota) : 0;
        }

        /// <summary>
        /// 洞内「清单里抵扣不到」的非冗余灵石形状（诊断用：这些很可能来自其他洞口）。
        /// </summary>
        public List<List<Vector2Int>> GetUnexpectedShapeEntries()
        {
            var unexpected = new List<List<Vector2Int>>();
            if (!hasShapeEntries)
            {
                return unexpected;
            }
            var remaining = BuildRequiredCounts();
            foreach (LingShi ls in placedLingShi)
            {
                if (ls == null || ls.GetSourceHoleId() < 0)
                {
                    continue;
                }
                string sig = ls.GetShapeSignature();
                if (remaining.TryGetValue(sig, out int count) && count > 0)
                {
                    remaining[sig] = count - 1;
                    continue;
                }
                unexpected.Add(ls.GetBaseCells());
            }
            return unexpected;
        }

        /// <summary>需求清单 → 形状签名计数表。</summary>
        private Dictionary<string, int> BuildRequiredCounts()
        {
            var required = new Dictionary<string, int>();
            foreach (List<Vector2Int> entry in requiredShapeEntries)
            {
                string sig = ShapeCutter.GetCanonicalSignature(entry);
                required.TryGetValue(sig, out int count);
                required[sig] = count + 1;
            }
            return required;
        }

        /// <summary>返回需求形状清单副本（冗余补给按清单生成同形状替补）。</summary>
        public List<List<Vector2Int>> GetRequiredShapeEntries()
        {
            var copy = new List<List<Vector2Int>>(requiredShapeEntries.Count);
            foreach (List<Vector2Int> entry in requiredShapeEntries)
            {
                copy.Add(new List<Vector2Int>(entry));
            }
            return copy;
        }

        /// <summary>
        /// 计算「清单里还没被用上」的形状：需求清单减去已放置的非冗余灵石（按形状多重集合抵消）。
        /// 与 GetUnexpectedShapeEntries 一起组成填满但未愈合时的诊断日志。
        /// </summary>
        public List<List<Vector2Int>> GetMissingShapeEntries()
        {
            var remaining = new List<List<Vector2Int>>();
            if (!hasShapeEntries)
            {
                return remaining;
            }
            foreach (List<Vector2Int> entry in requiredShapeEntries)
            {
                remaining.Add(new List<Vector2Int>(entry));
            }
            foreach (LingShi ls in placedLingShi)
            {
                if (ls == null || ls.GetSourceHoleId() < 0)
                {
                    continue; // 冗余灵石不占清单额度，与 MatchesPlacedShapes 保持一致
                }
                string sig = ls.GetShapeSignature();
                for (int i = 0; i < remaining.Count; i++)
                {
                    if (ShapeCutter.GetCanonicalSignature(remaining[i]) == sig)
                    {
                        remaining.RemoveAt(i);
                        break;
                    }
                }
            }
            return remaining;
        }

        /// <summary>
        /// 灵气返还 = 本洞所有灵石的 qiValue 之和（按种类配置）。
        /// 在 FinishHoleClosing 时调用，此时灵石尚未销毁。
        /// </summary>
        public float GetQiReward()
        {
            float reward = 0f;
            foreach (LingShi ls in placedLingShi)
            {
                if (ls != null && ls.GetTypeConfig() != null)
                {
                    reward += ls.GetTypeConfig().GetQiValue();
                }
            }
            return reward;
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
