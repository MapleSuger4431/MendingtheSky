using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 网格管理器：grid 状态数组、洞生成（感染式蔓延 + 修订版合并规则）、动态补充。
    /// cell 坐标约定：Vector2Int.x = row，Vector2Int.y = col；row 0 为最上一行。
    /// grid 状态：-1 正常 / 0 缺口 / 1 已填 / 2 闭合中。
    /// 蔓延/合并规则按《设计评审与定稿》修订版：
    ///   禁止与「已填过的洞」相邻；mergeChance = 允许贴向未填洞的概率；
    ///   贴上未填洞则必然合并（最多一次）；超 maxMergedHoleSize 则作废重试；
    ///   合并提交时 generatedHoleCount--（配额回退）。
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        public const int CellNormal = -1;
        public const int CellHole = 0;
        public const int CellFilled = 1;
        public const int CellClosing = 2;

        [Header("蔓延参数（待讨论项，稳定后可挪入 LevelConfig）")]
        [SerializeField] private float weightUp = 1f;
        [SerializeField] private float weightDown = 1f;
        [SerializeField] private float weightLeft = 1f;
        [SerializeField] private float weightRight = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("达到 minHoleSize 后，每扩张一步的停止概率")]
        private float stopChancePerStep = 0.25f;
        [SerializeField, Tooltip("单次生成洞的最大重试次数")]
        private int maxSpawnAttempts = 30;

        private int[,] grid;
        private int rows;
        private int cols;
        private float cellSize = 1f;
        private Vector3 origin; // 格子 (0,0) 左上角的世界坐标
        private readonly List<Hole> holes = new List<Hole>();
        private readonly Dictionary<Vector2Int, Hole> cellToHole = new Dictionary<Vector2Int, Hole>();
        private LevelConfig levelConfig;
        private float spawnTimer;
        private int generatedHoleCount;
        private int nextHoleId = 1;
        private System.Random random;

        [Header("阶段 5 接线（可为空；为空时逻辑照常运行，便于 EditMode 测试）")]
        [SerializeField] private GridVisual gridVisual;
        [SerializeField] private InventoryManager inventoryManager;

        public void GenerateLevel(LevelConfig levelConfig)
        {
            this.levelConfig = levelConfig;
            random = levelConfig.GetSeed() != 0
                ? new System.Random(levelConfig.GetSeed())
                : new System.Random();

            rows = random.Next(levelConfig.GetMinRows(), levelConfig.GetMaxRows() + 1);
            cellSize = ComputeFittedCellSize(rows);
            cols = ComputeFittedCols(cellSize);

            grid = new int[rows, cols];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    grid[r, c] = CellNormal;
                }
            }

            holes.Clear();
            cellToHole.Clear();
            generatedHoleCount = 0;
            spawnTimer = 0f;
            nextHoleId = 1;

            ComputeOrigin();
            GenerateInitialHoles();
        }

        /// <summary>
        /// 动态补洞。阶段 1 临时由 Update 自驱；阶段 4 起由 GameManager 在 Playing 状态集中调用。
        /// </summary>
        public void Tick(float dt)
        {
            if (levelConfig == null)
            {
                return;
            }
            if (!levelConfig.IsEndless() && generatedHoleCount >= levelConfig.GetTotalHoles())
            {
                return;
            }
            spawnTimer += dt;
            if (spawnTimer < levelConfig.GetHoleSpawnInterval())
            {
                return;
            }
            spawnTimer = 0f;
            if (GetUnfilledHoleCount() >= levelConfig.GetHoleCount())
            {
                return;
            }
            Hole newHole = TrySpawnNewHole();
            if (newHole != null)
            {
                generatedHoleCount++;
                // 动态补洞后立刻发放灵石（阶段 5：由 GridManager 直接通知背篓）
                if (inventoryManager != null)
                {
                    inventoryManager.AddLingShiFromHole(newHole, levelConfig);
                }
            }
        }

        // 阶段 4 起：洞的动态补充由 GameManager 在 Playing 状态集中调用 Tick(dt) 驱动，
        // 此类不再自己写 Unity Update()，避免暂停时仍生成洞或重复驱动。

        /// <summary>
        /// 尝试生成一个新洞（含修订版合并规则）。失败返回 null（比如网格几乎被占满）。
        /// </summary>
        public Hole TrySpawnNewHole()
        {
            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                bool allowTouchUnfilled = random.NextDouble() < levelConfig.GetMergeChance();
                int seedR = random.Next(0, rows);
                int seedC = random.Next(0, cols);
                if (!CanSpread(seedR, seedC, allowTouchUnfilled))
                {
                    continue;
                }
                List<Vector2Int> cells = SpreadHole(seedR, seedC, allowTouchUnfilled);
                if (!IsHoleShapeValid(cells))
                {
                    continue;
                }
                Hole newHole = CreateHole(cells);
                Hole mergeTarget = FindAdjacentUnfilledHole(newHole);
                if (mergeTarget != null)
                {
                    // 贴上了未填洞 → 必然合并；超上限则本次生成作废重试
                    if (mergeTarget.GetRequiredCellCount() + newHole.GetRequiredCellCount()
                        > levelConfig.GetMaxMergedHoleSize())
                    {
                        RemoveHole(newHole);
                        continue;
                    }
                    CommitMerge(mergeTarget, newHole);
                    return mergeTarget;
                }
                return newHole;
            }
            return null;
        }

        /// <summary>
        /// 调试入口：等价于 Tick 内的一次成功生成（计数 + 生成）。
        /// 对应文档阶段 1 验证点「手动调 TrySpawnNewHole 能加新洞」。
        /// </summary>
        public Hole SpawnHoleForDebug()
        {
            if (levelConfig == null)
            {
                return null;
            }
            Hole newHole = TrySpawnNewHole();
            if (newHole != null)
            {
                generatedHoleCount++;
            }
            return newHole;
        }

        /// <summary>
        /// 调试入口（K 键）：把整个网格的每一格都变成缺口，形成一个大洞。
        /// 用于检查全网格渲染与边界生成（性能与视觉完整性）。
        /// </summary>
        public Hole DebugFillAllAsHoles()
        {
            holes.Clear();
            cellToHole.Clear();
            var cells = new List<Vector2Int>(rows * cols);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    cells.Add(new Vector2Int(r, c));
                }
            }
            Hole hole = CreateHole(cells);
            generatedHoleCount = 1;
            return hole;
        }

        /// <summary>
        /// 感染式蔓延：从种子格向四方向随机扩张。
        /// 约束：格子数 [minHoleSize, maxHoleSize]；包围盒行数 [minHoleRows, maxHoleRows]
        /// 与列数 [minHoleCols, maxHoleCols]（默认 3×3 ~ 5×6）。
        /// 达到 minHoleSize 且包围盒达标后，每步按 stopChancePerStep 概率停止。
        /// </summary>
        private List<Vector2Int> SpreadHole(int seedR, int seedC, bool allowTouchUnfilled)
        {
            int minSize = levelConfig.GetMinHoleSize();
            int maxSize = levelConfig.GetMaxHoleSize();
            int minRowsSpan = levelConfig.GetMinHoleRows();
            int minColsSpan = levelConfig.GetMinHoleCols();
            var cells = new List<Vector2Int> { new Vector2Int(seedR, seedC) };
            var inHole = new HashSet<Vector2Int> { cells[0] };
            var candidates = new List<Vector2Int>();
            var candidateDirs = new List<EdgeDirection>();
            int minR = seedR, maxR = seedR, minC = seedC, maxC = seedC;

            while (cells.Count < maxSize)
            {
                candidates.Clear();
                candidateDirs.Clear();
                for (int i = 0; i < cells.Count; i++)
                {
                    CollectCandidate(cells[i], -1, 0, EdgeDirection.Up, allowTouchUnfilled, inHole,
                        minR, maxR, minC, maxC, candidates, candidateDirs);
                    CollectCandidate(cells[i], 1, 0, EdgeDirection.Down, allowTouchUnfilled, inHole,
                        minR, maxR, minC, maxC, candidates, candidateDirs);
                    CollectCandidate(cells[i], 0, -1, EdgeDirection.Left, allowTouchUnfilled, inHole,
                        minR, maxR, minC, maxC, candidates, candidateDirs);
                    CollectCandidate(cells[i], 0, 1, EdgeDirection.Right, allowTouchUnfilled, inHole,
                        minR, maxR, minC, maxC, candidates, candidateDirs);
                }
                if (candidates.Count == 0)
                {
                    break;
                }
                int picked = WeightedPick(candidateDirs);
                Vector2Int next = candidates[picked];
                if (!inHole.Add(next))
                {
                    continue;
                }
                cells.Add(next);
                minR = Mathf.Min(minR, next.x);
                maxR = Mathf.Max(maxR, next.x);
                minC = Mathf.Min(minC, next.y);
                maxC = Mathf.Max(maxC, next.y);
                bool bboxReady = (maxR - minR + 1) >= minRowsSpan && (maxC - minC + 1) >= minColsSpan;
                if (cells.Count >= minSize && bboxReady && random.NextDouble() < stopChancePerStep)
                {
                    break;
                }
            }
            return cells;
        }

        private void CollectCandidate(Vector2Int from, int dr, int dc, EdgeDirection dir,
            bool allowTouchUnfilled, HashSet<Vector2Int> inHole,
            int minR, int maxR, int minC, int maxC,
            List<Vector2Int> candidates, List<EdgeDirection> candidateDirs)
        {
            int r = from.x + dr;
            int c = from.y + dc;
            // 包围盒上限：加入后行/列跨度不得超过 maxHoleRows / maxHoleCols
            if (Mathf.Max(maxR, r) - Mathf.Min(minR, r) + 1 > levelConfig.GetMaxHoleRows())
            {
                return;
            }
            if (Mathf.Max(maxC, c) - Mathf.Min(minC, c) + 1 > levelConfig.GetMaxHoleCols())
            {
                return;
            }
            var cell = new Vector2Int(r, c);
            if (inHole.Contains(cell) || !CanSpread(r, c, allowTouchUnfilled))
            {
                return;
            }
            candidates.Add(cell);
            candidateDirs.Add(dir);
        }

        /// <summary>生成后的形状校验：格子数与包围盒都必须在配置范围内。</summary>
        private bool IsHoleShapeValid(List<Vector2Int> cells)
        {
            if (cells == null
                || cells.Count < levelConfig.GetMinHoleSize()
                || cells.Count > levelConfig.GetMaxHoleSize())
            {
                return false;
            }
            int minR = int.MaxValue, maxR = int.MinValue, minC = int.MaxValue, maxC = int.MinValue;
            foreach (Vector2Int cell in cells)
            {
                minR = Mathf.Min(minR, cell.x);
                maxR = Mathf.Max(maxR, cell.x);
                minC = Mathf.Min(minC, cell.y);
                maxC = Mathf.Max(maxC, cell.y);
            }
            int rowSpan = maxR - minR + 1;
            int colSpan = maxC - minC + 1;
            return rowSpan >= levelConfig.GetMinHoleRows() && rowSpan <= levelConfig.GetMaxHoleRows()
                && colSpan >= levelConfig.GetMinHoleCols() && colSpan <= levelConfig.GetMaxHoleCols();
        }

        private int WeightedPick(List<EdgeDirection> candidateDirs)
        {
            float total = 0f;
            for (int i = 0; i < candidateDirs.Count; i++)
            {
                total += GetDirectionWeight(candidateDirs[i]);
            }
            float roll = (float)(random.NextDouble() * total);
            for (int i = 0; i < candidateDirs.Count; i++)
            {
                roll -= GetDirectionWeight(candidateDirs[i]);
                if (roll <= 0f)
                {
                    return i;
                }
            }
            return candidateDirs.Count - 1;
        }

        private float GetDirectionWeight(EdgeDirection dir)
        {
            switch (dir)
            {
                case EdgeDirection.Up: return weightUp;
                case EdgeDirection.Down: return weightDown;
                case EdgeDirection.Left: return weightLeft;
                case EdgeDirection.Right: return weightRight;
                default: return 1f;
            }
        }

        /// <summary>
        /// 修订版 CanSpread：只能进 grid==-1 的格子；
        /// 不得与「已填过的洞」（nowCellCount > 0，含闭合中）的任何格子相邻；
        /// 未 roll 中 mergeChance 时，不得贴任何已有洞。
        /// </summary>
        private bool CanSpread(int r, int c, bool allowTouchUnfilled)
        {
            if (!IsInside(r, c) || grid[r, c] != CellNormal)
            {
                return false;
            }
            for (int i = 0; i < 4; i++)
            {
                int nr = r + NeighborOffsets[i].x;
                int nc = c + NeighborOffsets[i].y;
                if (!IsInside(nr, nc))
                {
                    continue;
                }
                Hole neighbor = GetHoleAt(nr, nc);
                if (neighbor == null)
                {
                    continue;
                }
                if (neighbor.GetNowCellCount() > 0)
                {
                    return false;
                }
                if (!allowTouchUnfilled)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>找到与本洞相邻的、未填过的已有洞（最多返回一个，对应「最多合并一次」）。</summary>
        private Hole FindAdjacentUnfilledHole(Hole hole)
        {
            foreach (Vector2Int cell in hole.GetCells())
            {
                for (int i = 0; i < 4; i++)
                {
                    int nr = cell.x + NeighborOffsets[i].x;
                    int nc = cell.y + NeighborOffsets[i].y;
                    if (!IsInside(nr, nc))
                    {
                        continue;
                    }
                    Hole neighbor = GetHoleAt(nr, nc);
                    if (neighbor != null && neighbor != hole
                        && neighbor.GetNowCellCount() == 0 && !neighbor.IsClosing())
                    {
                        return neighbor;
                    }
                }
            }
            return null;
        }

        private Hole CreateHole(List<Vector2Int> cells)
        {
            var hole = new Hole();
            hole.Init(nextHoleId++, cells);
            holes.Add(hole);
            foreach (Vector2Int cell in cells)
            {
                grid[cell.x, cell.y] = CellHole;
                cellToHole[cell] = hole;
            }
            return hole;
        }

        private void RemoveHole(Hole hole)
        {
            foreach (Vector2Int cell in hole.GetCells())
            {
                grid[cell.x, cell.y] = CellNormal;
                cellToHole.Remove(cell);
            }
            holes.Remove(hole);
        }

        /// <summary>
        /// 合并提交：absorbed 并入 survivor，cellToHole 换绑（grid 保持 CellHole 不变），
        /// absorbed 从列表摘除，generatedHoleCount--（配额回退，保证胜利条件可达）。
        /// </summary>
        private void CommitMerge(Hole survivor, Hole absorbed)
        {
            List<Vector2Int> absorbedCells = absorbed.GetCells();
            survivor.MergeWith(absorbed);
            foreach (Vector2Int cell in absorbedCells)
            {
                cellToHole[cell] = survivor;
            }
            holes.Remove(absorbed);
            generatedHoleCount--;
        }

        private static readonly Vector2Int[] NeighborOffsets =
        {
            new Vector2Int(-1, 0), // Up
            new Vector2Int(1, 0),  // Down
            new Vector2Int(0, -1), // Left
            new Vector2Int(0, 1)   // Right
        };

        /// <summary>开局生成：单关 min(holeCount, totalHoles) 个，无尽模式 holeCount 个。</summary>
        private void GenerateInitialHoles()
        {
            int count = levelConfig.IsEndless()
                ? levelConfig.GetHoleCount()
                : Mathf.Min(levelConfig.GetHoleCount(), levelConfig.GetTotalHoles());
            for (int i = 0; i < count; i++)
            {
                Hole hole = TrySpawnNewHole();
                if (hole == null)
                {
                    break;
                }
                generatedHoleCount++;
            }
        }

        /// <summary>
        /// 按相机视口自动收缩格子尺寸：保证 rows 行完整落在上半屏（视口 0.5~0.95）。
        /// 配置的 cellSize 作为上限；无相机时（EditMode 测试）直接用配置值。
        /// </summary>
        private float ComputeFittedCellSize(int rows)
        {
            float configured = levelConfig.GetCellSize();
            Camera cam = Camera.main;
            if (cam == null)
            {
                return configured;
            }
            float distance = Mathf.Abs(cam.transform.position.z);
            float top = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.95f, distance)).y;
            float half = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance)).y;
            float availableHeight = Mathf.Abs(top - half);
            return Mathf.Min(configured, availableHeight / Mathf.Max(1, rows));
        }

        /// <summary>
        /// 列数自适应：用可用宽度（视口 0.03~0.97）除以格宽，使网格恰好铺满上半屏宽度。
        /// 结果钳制在 LevelConfig 的 minCols/maxCols 内；无相机时按配置范围随机。
        /// </summary>
        private int ComputeFittedCols(float cellSize)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return random.Next(levelConfig.GetMinCols(), levelConfig.GetMaxCols() + 1);
            }
            float distance = Mathf.Abs(cam.transform.position.z);
            float left = cam.ViewportToWorldPoint(new Vector3(0.03f, 0.5f, distance)).x;
            float right = cam.ViewportToWorldPoint(new Vector3(0.97f, 0.5f, distance)).x;
            int fitted = Mathf.RoundToInt(Mathf.Abs(right - left) / Mathf.Max(0.0001f, cellSize));
            return Mathf.Clamp(fitted, levelConfig.GetMinCols(), levelConfig.GetMaxCols());
        }

        /// <summary>
        /// 网格定位：屏幕上方居中（视口 y=0.95 处为网格顶边），绝不硬编码世界坐标。
        /// 无相机时（EditMode 测试）降级为以世界原点居中。
        /// </summary>
        private void ComputeOrigin()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                origin = new Vector3(-cols * cellSize * 0.5f, rows * cellSize * 0.5f, 0f);
                return;
            }
            float distance = Mathf.Abs(cam.transform.position.z);
            Vector3 topCenter = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.95f, distance));
            origin = new Vector3(topCenter.x - cols * cellSize * 0.5f, topCenter.y, 0f);
        }

        public bool IsInside(int r, int c)
        {
            return r >= 0 && r < rows && c >= 0 && c < cols;
        }

        public int GetCell(int r, int c)
        {
            return IsInside(r, c) ? grid[r, c] : CellNormal;
        }

        public bool IsHole(int r, int c) { return GetCell(r, c) == CellHole; }
        public bool IsFilled(int r, int c) { return GetCell(r, c) == CellFilled; }
        public bool IsNormal(int r, int c) { return GetCell(r, c) == CellNormal; }
        public bool IsClosing(int r, int c) { return GetCell(r, c) == CellClosing; }

        /// <summary>该格能否放灵石：只有缺口格（grid==0）可以放。阶段 3 由 MatchChecker 组合使用。</summary>
        public bool CanPlace(int r, int c)
        {
            return IsInside(r, c) && grid[r, c] == CellHole;
        }

        /// <summary>
        /// 放置灵石：按锚点把形状覆盖的所有格置为已填（grid=1），并通知所属洞填充进度。
        /// 阶段 5 将在此触发 CheckHoleClosed → MarkHoleClosing。
        /// </summary>
        public void FillCells(LingShi ls, int targetRow, int targetCol)
        {
            if (ls == null)
            {
                return;
            }
            var placed = new List<Vector2Int>();
            foreach (Vector2Int cell in ls.GetCurrentCells())
            {
                int r = targetRow + cell.x;
                int c = targetCol + cell.y;
                if (!IsInside(r, c))
                {
                    continue;
                }
                Hole hole = GetHoleAt(r, c);
                grid[r, c] = CellFilled;
                hole?.NotifyCellFilled();
                placed.Add(new Vector2Int(r, c));
            }
            ls.MarkPlaced(placed);
        }

        /// <summary>从网格取下灵石：已填格恢复为缺口（grid=0），并回退洞的填充进度。</summary>
        public void UnfillCells(LingShi ls)
        {
            if (ls == null)
            {
                return;
            }
            foreach (Vector2Int cell in ls.GetPlacedCells())
            {
                if (!IsInside(cell.x, cell.y))
                {
                    continue;
                }
                if (grid[cell.x, cell.y] != CellFilled)
                {
                    continue;
                }
                Hole hole = GetHoleAt(cell.x, cell.y);
                hole?.NotifyCellUnfilled();
                grid[cell.x, cell.y] = CellHole;
            }
        }

        /// <summary>场上未闭合洞数量（含已部分填的）。</summary>
        public int GetUnfilledHoleCount()
        {
            int count = 0;
            for (int i = 0; i < holes.Count; i++)
            {
                if (!holes[i].IsClosing())
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 灵气消耗基数：只统计 grid==0 的空缺格（已填、闭合中一律不扣）。
        /// 洞数量级小，按需遍历即可；若后续 profiling 发现问题再改缓存计数。
        /// </summary>
        public int GetUnfilledCellCount()
        {
            int count = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (grid[r, c] == CellHole)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        public Hole GetHoleAt(int r, int c)
        {
            if (!IsInside(r, c))
            {
                return null;
            }
            cellToHole.TryGetValue(new Vector2Int(r, c), out Hole hole);
            return hole;
        }

        public Hole GetHoleById(int id)
        {
            for (int i = 0; i < holes.Count; i++)
            {
                if (holes[i].GetHoleId() == id)
                {
                    return holes[i];
                }
            }
            return null;
        }

        /// <summary>返回洞列表副本，避免外部直接改内部数据。</summary>
        public List<Hole> GetAllHoles()
        {
            return new List<Hole>(holes);
        }

        public int GetGeneratedHoleCount() { return generatedHoleCount; }
        public int GetRows() { return rows; }
        public int GetCols() { return cols; }
        public float GetCellSize() { return cellSize; }
        public Vector3 GetOrigin() { return origin; }

        /// <summary>格子中心的世界坐标。</summary>
        public Vector3 GridToWorld(int r, int c)
        {
            return new Vector3(
                origin.x + (c + 0.5f) * cellSize,
                origin.y - (r + 0.5f) * cellSize,
                0f);
        }

        /// <summary>世界坐标 → 格子（x=row, y=col）；越界坐标也会返回，调用方用 IsInside 判断。</summary>
        public Vector2Int WorldToGrid(Vector3 pos)
        {
            int c = Mathf.FloorToInt((pos.x - origin.x) / cellSize);
            int r = Mathf.FloorToInt((origin.y - pos.y) / cellSize);
            return new Vector2Int(r, c);
        }
    }
}
