using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 背篓仓库：无限容量，可同时存在 InBasket（未煮）与 Cooked（已煮回落）。
    /// 负责：按洞切割生成灵石、动态补洞发石、冗余补给、工作台暂存回落。
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        [Header("生成")]
        [SerializeField] private Transform root;
        [SerializeField] private GameObject lingShiPrefab;
        [SerializeField] private ShapePoolConfig shapePoolConfig;
        [SerializeField] private Transform basketSpawnPoint;
        [SerializeField] private float itemScale = 0.6f;
        [SerializeField] private int maxPiecesPerHole = ShapeCutter.DefaultMaxPieces;
        [SerializeField] private float spawnJitter = 0.15f;
        [Tooltip("初始灵石随机朝向：避免拿起来无需旋转即可拼，保留旋转玩法")]
        [SerializeField] private bool randomizeInitialRotation = true;

        [Header("补给节奏（2026-10-27 方案 D）")]
        [Tooltip("开洞时逐块弹出的间隔（秒）")]
        [SerializeField] private float holeSpawnStagger = 0.12f;
        [Tooltip("兜底补给逐块弹出的间隔（秒）")]
        [SerializeField] private float supplyStagger = 0.35f;
        [Tooltip("补给时额外预留的格数余量，避免玩家放一块就触发一次补给")]
        [SerializeField] private int supplyMargin = 3;
        [Tooltip("废石观测：每隔多少秒最多打一条废石统计日志")]
        [SerializeField] private float wasteLogInterval = 3f;

        private readonly List<LingShi> lingShiList = new List<LingShi>();
        private GridManager gridManager;
        private readonly System.Random random = new System.Random();
        private int nextSortingIndex = 0;

        /// <summary>
        /// 待生成队列的世代号：洞愈合 / 合并 / 重开关卡时递增，
        /// 队列里世代对不上的项直接丢弃（防止弹出过程中洞已消失）。
        /// </summary>
        private int spawnGeneration;
        private readonly Queue<PendingPiece> pendingPieces = new Queue<PendingPiece>();
        private Coroutine spawnRoutine;
        private float lastWasteLogTime;

        private struct PendingPiece
        {
            public List<Vector2Int> cells;
            public int generation;
        }

        public void GenerateInitialInventory(LevelConfig levelConfig, GridManager gridManager)
        {
            this.gridManager = gridManager;
            ClearAll();
            foreach (Hole hole in gridManager.GetAllHoles())
            {
                AddLingShiFromHole(hole, levelConfig);
            }
        }

        /// <summary>为新洞切出灵石并投放到背篓（开局生成 / 新建整洞时调用）。</summary>
        public void AddLingShiFromHole(Hole hole, LevelConfig levelConfig)
        {
            if (hole == null)
            {
                return;
            }
            AddLingShiForCells(hole, hole.GetCells(), levelConfig);
        }

        /// <summary>
        /// 为洞口的这一批格子备石（开洞 / 合并补洞时调用），2026-10-27 方案 D：
        /// 1) 先用背篓里现有的石头顶替能塞进这批格子的部分（把废石消化掉，少发新石）；
        /// 2) 剩下没被覆盖的格子才切开，逐块弹出（未熬煮，进背篓）。
        /// </summary>
        public void AddLingShiForCells(Hole hole, List<Vector2Int> cells, LevelConfig levelConfig)
        {
            if (hole == null || cells == null || cells.Count == 0 || shapePoolConfig == null)
            {
                return;
            }
            var remaining = new List<Vector2Int>(cells);
            int packed = PackExistingStones(remaining);

            var pieces = remaining.Count > 0
                ? ShapeCutter.Cut(remaining, shapePoolConfig, maxPiecesPerHole, random)
                : new List<List<Vector2Int>>();

            EnqueuePieces(pieces, holeSpawnStagger);
            if (packed > 0 || pieces.Count > 0)
            {
                AudioManager.Instance?.PlaySfx(SfxId.Spawn);
            }
        }

        /// <summary>
        /// 兜底补给（方案 D 核心）：库存格数不足以填满所有开放洞时，按「各洞当前剩余空格」补石。
        /// 只切剩余空格而不是重切整洞，保证切出来的块一定塞得进该洞；
        /// 逐洞处理、不跨洞混切。
        /// </summary>
        public void EnsureSupply(LevelConfig levelConfig)
        {
            if (gridManager == null || levelConfig == null || shapePoolConfig == null)
            {
                return;
            }

            var holes = new List<Hole>();
            int need = 0;
            foreach (Hole hole in gridManager.GetAllHoles())
            {
                if (hole == null || hole.IsClosing())
                {
                    continue;
                }
                holes.Add(hole);
                need += hole.GetRemainingCellCount();
            }
            if (need <= 0)
            {
                return;
            }

            int have = GetTotalCellCount();
            int deficit = ComputeDeficit(need, have, GetPendingCellCount(), supplyMargin);
            if (deficit <= 0)
            {
                LogWasteStats(holes);
                return;
            }

            // 先补剩余最多的洞，减少玩家"没石头可用"的等待
            holes.Sort((a, b) => b.GetRemainingCellCount().CompareTo(a.GetRemainingCellCount()));
            foreach (Hole hole in holes)
            {
                if (deficit <= 0)
                {
                    break;
                }
                List<Vector2Int> unfilled = GetUnfilledCells(hole);
                if (unfilled.Count == 0)
                {
                    continue;
                }
                var pieces = ShapeCutter.Cut(unfilled, shapePoolConfig, maxPiecesPerHole, random);
                foreach (List<Vector2Int> piece in pieces)
                {
                    if (deficit <= 0)
                    {
                        break;
                    }
                    EnqueuePieces(new List<List<Vector2Int>> { piece }, supplyStagger);
                    deficit -= piece.Count;
                }
            }
        }

        /// <summary>把现有石头塞进这批格子里（能塞的都算覆盖），返回顶替的块数。</summary>
        private int PackExistingStones(List<Vector2Int> remaining)
        {
            var free = new HashSet<Vector2Int>(remaining);
            int packed = 0;
            // 大块优先：大块更难找到位置，先用掉能减少后续碎片
            var candidates = new List<LingShi>(lingShiList);
            candidates.Sort((a, b) => ShapeCellCount(b).CompareTo(ShapeCellCount(a)));
            foreach (LingShi ls in candidates)
            {
                if (ls == null || ls.GetState() == LingShiState.Placed)
                {
                    continue;
                }
                List<Vector2Int> shape = ls.GetCurrentCells();
                if (!TryFit(shape, free))
                {
                    continue;
                }
                foreach (Vector2Int cell in shape)
                {
                    free.Remove(cell);
                }
                packed++;
            }
            remaining.Clear();
            remaining.AddRange(free);
            return packed;
        }

        /// <summary>
        /// 补给缺口计算：还需要补多少格。
        /// = 全部开放洞剩余空格 + 余量 − 库存格数 − 已在队列里待生成的格数。
        /// 抽成静态方法便于单测（2026-10-27 方案 D）。
        /// </summary>
        public static int ComputeDeficit(int needCells, int haveCells, int pendingCells, int margin)
        {
            return needCells + Mathf.Max(0, margin) - Mathf.Max(0, haveCells) - Mathf.Max(0, pendingCells);
        }

        /// <summary>形状的格子能否全部落在 free 里（允许平移）。</summary>
        public static bool TryFit(List<Vector2Int> shape, HashSet<Vector2Int> free)
        {
            if (shape == null || shape.Count == 0 || free.Count < shape.Count)
            {
                return false;
            }
            foreach (Vector2Int anchor in free)
            {
                bool ok = true;
                foreach (Vector2Int offset in shape)
                {
                    if (!free.Contains(new Vector2Int(anchor.x + offset.x, anchor.y + offset.y)))
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>洞里还没被填上的格子（补给按这个轮廓切石）。</summary>
        private List<Vector2Int> GetUnfilledCells(Hole hole)
        {
            var result = new List<Vector2Int>();
            foreach (Vector2Int cell in hole.GetCells())
            {
                if (gridManager.GetCell(cell.x, cell.y) == GridManager.CellHole)
                {
                    result.Add(cell);
                }
            }
            return result;
        }

        /// <summary>库存总格数（不含已放到洞里的块）：背篓 + 锅里 + 暂存都算。</summary>
        public int GetTotalCellCount()
        {
            int total = 0;
            foreach (LingShi ls in lingShiList)
            {
                if (ls == null || ls.GetState() == LingShiState.Placed)
                {
                    continue;
                }
                total += ShapeCellCount(ls);
            }
            return total;
        }

        private int GetPendingCellCount()
        {
            int total = 0;
            foreach (PendingPiece piece in pendingPieces)
            {
                if (piece.cells != null)
                {
                    total += piece.cells.Count;
                }
            }
            return total;
        }

        private static int ShapeCellCount(LingShi ls)
        {
            return ls != null ? ls.GetCurrentCells().Count : 0;
        }

        /// <summary>某块石头是否已经塞不进任何一个开放洞（废石判定，仅统计用）。</summary>
        public bool IsWasteStone(LingShi ls)
        {
            if (ls == null || gridManager == null)
            {
                return false;
            }
            var shape = ls.GetCurrentCells();
            foreach (Hole hole in gridManager.GetAllHoles())
            {
                if (hole == null || hole.IsClosing())
                {
                    continue;
                }
                if (TryFit(shape, new HashSet<Vector2Int>(GetUnfilledCells(hole))))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>废石观测：定期打一条统计日志（方案 D 先观测，不自动销毁）。</summary>
        private void LogWasteStats(List<Hole> holes)
        {
            if (Time.unscaledTime - lastWasteLogTime < wasteLogInterval)
            {
                return;
            }
            int waste = 0;
            int total = 0;
            foreach (LingShi ls in lingShiList)
            {
                if (ls == null || ls.GetState() == LingShiState.Placed)
                {
                    continue;
                }
                total++;
                if (IsWasteStone(ls))
                {
                    waste++;
                }
            }
            if (waste == 0)
            {
                return;
            }
            lastWasteLogTime = Time.unscaledTime;
            Debug.Log($"[补给] 开放洞 {holes.Count} 个，库存 {total} 块，其中放不进任何洞的废石 {waste} 块" +
                      $"（{(total > 0 ? waste * 100f / total : 0f):0.0}%）");
        }

        /// <summary>
        /// 丢弃一块石头（方案 D 预留出口）：从库存移除并销毁，不返还灵气。
        /// 目前只作为扩展点，未绑定到玩家输入，等废石观测数据出来再决定是否开放。
        /// </summary>
        public bool TryDiscard(LingShi ls)
        {
            if (ls == null)
            {
                return false;
            }
            lingShiList.Remove(ls);
            AudioManager.Instance?.PlaySfx(SfxId.Invalid);
            Destroy(ls.gameObject);
            return true;
        }

        /// <summary>入队待生成的石头；EditMode（非运行态）直接同步生成，方便测试。</summary>
        private void EnqueuePieces(List<List<Vector2Int>> pieces, float stagger)
        {
            if (pieces == null || pieces.Count == 0)
            {
                return;
            }
            var levelConfig = FindLevelConfig();
            if (levelConfig == null)
            {
                return;
            }
            if (!Application.isPlaying)
            {
                // EditMode 测试没有协程，直接生成
                foreach (List<Vector2Int> piece in pieces)
                {
                    CreateLingShi(levelConfig, piece, GetSpawnPosition());
                }
                return;
            }
            foreach (List<Vector2Int> piece in pieces)
            {
                pendingPieces.Enqueue(new PendingPiece { cells = piece, generation = spawnGeneration });
            }
            if (spawnRoutine == null)
            {
                spawnRoutine = StartCoroutine(SpawnQueueRoutine(stagger));
            }
        }

        private System.Collections.IEnumerator SpawnQueueRoutine(float stagger)
        {
            var levelConfig = FindLevelConfig();
            while (pendingPieces.Count > 0)
            {
                PendingPiece piece = pendingPieces.Dequeue();
                if (piece.cells != null && piece.generation == spawnGeneration && levelConfig != null)
                {
                    CreateLingShi(levelConfig, piece.cells, GetSpawnPosition());
                }
                if (pendingPieces.Count > 0)
                {
                    yield return new WaitForSeconds(stagger);
                }
            }
            spawnRoutine = null;
        }

        /// <summary>作废所有待生成项（洞愈合 / 合并 / 重开关卡时调用）。</summary>
        public void InvalidatePendingSpawns()
        {
            spawnGeneration++;
            pendingPieces.Clear();
        }

        private LevelConfig FindLevelConfig()
        {
            if (gridManager != null && gridManager.GetLevelConfig() != null)
            {
                return gridManager.GetLevelConfig();
            }
            return null;
        }

        /// <summary>工作台暂存灵石回落：保持 Cooked 状态，插值缩放回背篓。</summary>
        public void AddCookedLingShi(LingShi ls)
        {
            if (ls == null)
            {
                return;
            }
            ls.SetState(LingShiState.Cooked);
            ls.SetTargetScale(itemScale);
            ReturnToBasket(ls);
        }

        /// <summary>把灵石放回背篓（保持当前状态；InBasket 与 Cooked 都适用）。</summary>
        public void ReturnToBasket(LingShi ls)
        {
            if (ls == null)
            {
                return;
            }
            if (!lingShiList.Contains(ls))
            {
                lingShiList.Add(ls);
            }
            ls.SetTransparent(false);
            ls.SetTargetScale(itemScale);
            SpawnAtBasketTop(ls);
        }

        public void AddLingShi(LingShi ls)
        {
            if (ls != null && !lingShiList.Contains(ls))
            {
                lingShiList.Add(ls);
            }
        }

        public void RemoveLingShi(LingShi ls)
        {
            lingShiList.Remove(ls);
        }

        public List<LingShi> GetAll()
        {
            return new List<LingShi>(lingShiList);
        }

        /// <summary>可用 = 背篓未煮 + 已煮（冗余触发条件按此计数）。</summary>
        public int GetAvailableCount()
        {
            return GetInBasketCount() + GetCookedCount();
        }

        public int GetInBasketCount()
        {
            int count = 0;
            foreach (LingShi ls in lingShiList)
            {
                if (ls != null && ls.GetState() == LingShiState.InBasket)
                {
                    count++;
                }
            }
            return count;
        }

        public int GetCookedCount()
        {
            int count = 0;
            foreach (LingShi ls in lingShiList)
            {
                if (ls != null && ls.GetState() == LingShiState.Cooked)
                {
                    count++;
                }
            }
            return count;
        }

        public void ClearAll()
        {
            foreach (LingShi ls in lingShiList)
            {
                if (ls != null)
                {
                    Destroy(ls.gameObject);
                }
            }
            lingShiList.Clear();
        }

        /// <summary>纯视觉扰动（防止灵石完全静止重叠）。</summary>
        public void ShuffleVisual()
        {
            foreach (LingShi ls in lingShiList)
            {
                if (ls == null)
                {
                    continue;
                }
                Vector3 pos = ls.transform.position;
                pos.x += ((float)random.NextDouble() - 0.5f) * 0.05f;
                ls.transform.position = pos;
            }
        }

        private LingShiTypeConfig PickRandomType(LingShiTypeEntry[] pool)
        {
            if (pool == null || pool.Length == 0)
            {
                return null;
            }
            float total = 0f;
            foreach (LingShiTypeEntry entry in pool)
            {
                if (entry != null && entry.typeConfig != null)
                {
                    total += Mathf.Max(0.0001f, entry.weight);
                }
            }
            if (total <= 0f)
            {
                return null;
            }
            float roll = (float)(random.NextDouble() * total);
            foreach (LingShiTypeEntry entry in pool)
            {
                if (entry == null || entry.typeConfig == null)
                {
                    continue;
                }
                roll -= Mathf.Max(0.0001f, entry.weight);
                if (roll <= 0f)
                {
                    return entry.typeConfig;
                }
            }
            return pool[pool.Length - 1] != null ? pool[pool.Length - 1].typeConfig : null;
        }

        private LingShi CreateLingShi(LevelConfig levelConfig, List<Vector2Int> cells, Vector3 position,
            int sourceHoleId = -1)
        {
            if (lingShiPrefab == null)
            {
                Debug.LogError("[InventoryManager] lingShiPrefab 未配置");
                return null;
            }
            LingShiTypeConfig type = PickRandomType(levelConfig.GetLingShiTypePool());
            if (type == null)
            {
                Debug.LogError("[InventoryManager] LevelConfig.lingShiTypePool 为空");
                return null;
            }
            var shape = new LingShiShape();
            shape.Init(cells);
            // 初始朝向：按关卡概率随机，且只对「旋转后形态不同」的形状生效。
            // 单格等旋转不变形没有随机旋转的意义；概率由 LevelConfig.initialRotationChance 控制。
            if (randomizeInitialRotation
                && shape.HasDistinctRotations()
                && random.NextDouble() < levelConfig.GetInitialRotationChance())
            {
                shape.SetRotationSteps(random.Next(4));
            }
            GameObject go = Instantiate(lingShiPrefab, position, Quaternion.identity, root);
            var ls = go.GetComponent<LingShi>();
            ls.Init(type, shape, position, itemScale, gridManager != null ? gridManager.GetCellSize() : 1f);
            ls.SetSourceHoleId(sourceHoleId);
            // 每块灵石独立的排序基准：轮廓在 base、填充在 base+1，相邻灵石之间保留分界轮廓
            ls.SetSortingBase(5 + (nextSortingIndex % 30) * 2);
            nextSortingIndex++;
            ls.SetVelocity(new Vector2(((float)random.NextDouble() - 0.5f) * 0.4f, 0f));
            lingShiList.Add(ls);
            return ls;
        }

        private Vector3 GetSpawnPosition()
        {
            Vector3 pos = basketSpawnPoint != null ? basketSpawnPoint.position : transform.position;
            pos.x += ((float)random.NextDouble() - 0.5f) * spawnJitter * 2f;
            return pos;
        }

        private void SpawnAtBasketTop(LingShi ls)
        {
            ls.transform.position = GetSpawnPosition();
            ls.SetPhysicsBodyType(RigidbodyType2D.Dynamic);
            ls.SetSimulated(true);
            ls.SetVelocity(new Vector2(((float)random.NextDouble() - 0.5f) * 0.4f, 0f));
        }

        private List<Vector2Int> PickRandomShapeCells()
        {
            List<ShapeEntry> shapes = shapePoolConfig != null ? shapePoolConfig.GetShapes() : null;
            if (shapes == null || shapes.Count == 0)
            {
                return new List<Vector2Int> { Vector2Int.zero };
            }
            ShapeEntry entry = PickWeightedEntry(shapes);
            if (entry == null || entry.cells == null || entry.cells.Count == 0)
            {
                return new List<Vector2Int> { Vector2Int.zero };
            }
            var cells = new List<Vector2Int>(entry.cells);
            int rotation = random.Next(4);
            for (int i = 0; i < rotation; i++)
            {
                cells = Rotate90(cells);
            }
            return cells;
        }

        /// <summary>按权重随机挑选形状条目（权重越低出现越少；十字等挑战形状用低权重）。</summary>
        private ShapeEntry PickWeightedEntry(List<ShapeEntry> shapes)
        {
            float total = 0f;
            foreach (ShapeEntry entry in shapes)
            {
                if (entry != null && entry.cells != null && entry.cells.Count > 0)
                {
                    total += Mathf.Max(0.01f, entry.weight);
                }
            }
            if (total <= 0f)
            {
                return shapes[0];
            }
            float roll = (float)(random.NextDouble() * total);
            foreach (ShapeEntry entry in shapes)
            {
                if (entry == null || entry.cells == null || entry.cells.Count == 0)
                {
                    continue;
                }
                roll -= Mathf.Max(0.01f, entry.weight);
                if (roll <= 0f)
                {
                    return entry;
                }
            }
            return shapes[shapes.Count - 1];
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
    }
}
