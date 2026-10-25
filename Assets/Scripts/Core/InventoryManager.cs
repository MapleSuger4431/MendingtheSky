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

        private readonly List<LingShi> lingShiList = new List<LingShi>();
        private GridManager gridManager;
        private readonly System.Random random = new System.Random();
        private int nextSortingIndex = 0;

        public void GenerateInitialInventory(LevelConfig levelConfig, GridManager gridManager)
        {
            this.gridManager = gridManager;
            ClearAll();
            foreach (Hole hole in gridManager.GetAllHoles())
            {
                AddLingShiFromHole(hole, levelConfig);
            }
            TryAddRedundant(levelConfig);
        }

        /// <summary>为新洞切出灵石并投放到背篓（阶段 5 动态补洞时调用）。</summary>
        public void AddLingShiFromHole(Hole hole, LevelConfig levelConfig)
        {
            if (hole == null || shapePoolConfig == null)
            {
                return;
            }
            List<List<Vector2Int>> pieces = ShapeCutter.Cut(hole.GetCells(), shapePoolConfig,
                maxPiecesPerHole, random);
            foreach (List<Vector2Int> cells in pieces)
            {
                // 为每块必需灵石在洞口登记形状条目：放置时按清单消耗
                hole.AddShapeEntry(cells);
                CreateLingShi(levelConfig, cells, GetSpawnPosition(), hole.GetHoleId());
            }
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

        /// <summary>
        /// 冗余补给：可用数量低于阈值时补 redundantCount 块。
        /// 形状从「开放洞口的剩余形状清单」中抽取（同形状替补）：
        /// 这样冗余灵石一定能用上，且不会产生无法匹配任何清单的垃圾形状。
        /// </summary>
        private void TryAddRedundant(LevelConfig levelConfig)
        {
            if (GetAvailableCount() >= levelConfig.GetRedundantThreshold())
            {
                return;
            }
            for (int i = 0; i < levelConfig.GetRedundantCount(); i++)
            {
                List<Vector2Int> cells = PickShapeFromOpenHoles();
                if (cells != null)
                {
                    CreateLingShi(levelConfig, cells, GetSpawnPosition());
                }
            }
        }

        /// <summary>从所有开放洞的剩余形状清单中随机取一个形状；没有清单时退回形状池随机。</summary>
        private List<Vector2Int> PickShapeFromOpenHoles()
        {
            if (gridManager != null)
            {
                var candidates = new List<List<Vector2Int>>();
                foreach (Hole hole in gridManager.GetAllHoles())
                {
                    if (hole.IsClosing())
                    {
                        continue;
                    }
                    candidates.AddRange(hole.GetRemainingShapeEntries());
                }
                if (candidates.Count > 0)
                {
                    return new List<Vector2Int>(candidates[random.Next(candidates.Count)]);
                }
            }
            return PickRandomShapeCells();
        }

        private List<Vector2Int> PickRandomShapeCells()
        {
            List<ShapeEntry> shapes = shapePoolConfig != null ? shapePoolConfig.GetShapes() : null;
            if (shapes == null || shapes.Count == 0)
            {
                return new List<Vector2Int> { Vector2Int.zero };
            }
            ShapeEntry entry = shapes[random.Next(shapes.Count)];
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
