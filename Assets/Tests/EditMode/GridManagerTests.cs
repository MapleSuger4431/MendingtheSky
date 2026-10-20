using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    public class GridManagerTests
    {
        private readonly List<Object> cleanup = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in cleanup)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }
            cleanup.Clear();
        }

        private GridManager CreateManager()
        {
            var go = new GameObject("GridManager_Test");
            cleanup.Add(go);
            return go.AddComponent<GridManager>();
        }

        private LevelConfig CreateConfig(int seed, int rows = 12, int cols = 24,
            int holeCount = 3, int totalHoles = 10, float mergeChance = 0.3f,
            int minHoleSize = 9, int maxHoleSize = 30)
        {
            var config = ScriptableObject.CreateInstance<LevelConfig>();
            cleanup.Add(config);
            config.seed = seed;
            config.isEndless = false;
            config.minRows = rows;
            config.maxRows = rows;
            config.minCols = cols;
            config.maxCols = cols;
            config.cellSize = 1f;
            config.holeCount = holeCount;
            config.totalHoles = totalHoles;
            config.minHoleSize = minHoleSize;
            config.maxHoleSize = maxHoleSize;
            config.maxMergedHoleSize = maxHoleSize * 2;
            config.minHoleRows = 3;
            config.maxHoleRows = 5;
            config.minHoleCols = 3;
            config.maxHoleCols = 6;
            config.mergeChance = mergeChance;
            return config;
        }

        private static List<Vector2Int> SortedCells(Hole hole)
        {
            List<Vector2Int> cells = hole.GetCells();
            cells.Sort((a, b) => a.x != b.x ? a.x - b.x : a.y - b.y);
            return cells;
        }

        [Test]
        public void GenerateLevel_SameSeed_ProducesIdenticalHoles()
        {
            GridManager a = CreateManager();
            GridManager b = CreateManager();
            a.GenerateLevel(CreateConfig(42));
            b.GenerateLevel(CreateConfig(42));

            Assert.AreEqual(a.GetRows(), b.GetRows());
            Assert.AreEqual(a.GetCols(), b.GetCols());
            List<Hole> holesA = a.GetAllHoles();
            List<Hole> holesB = b.GetAllHoles();
            Assert.AreEqual(holesA.Count, holesB.Count);
            for (int i = 0; i < holesA.Count; i++)
            {
                CollectionAssert.AreEqual(SortedCells(holesA[i]), SortedCells(holesB[i]));
            }
        }

        [Test]
        public void GenerateLevel_HolesInsideBounds_NoOverlap_SizeInRange()
        {
            GridManager gm = CreateManager();
            LevelConfig config = CreateConfig(7);
            gm.GenerateLevel(config);

            var seen = new HashSet<Vector2Int>();
            foreach (Hole hole in gm.GetAllHoles())
            {
                int size = hole.GetRequiredCellCount();
                Assert.GreaterOrEqual(size, config.GetMinHoleSize());
                Assert.LessOrEqual(size, config.GetMaxMergedHoleSize());
                foreach (Vector2Int cell in hole.GetCells())
                {
                    Assert.IsTrue(gm.IsInside(cell.x, cell.y), $"格子越界: {cell}");
                    Assert.IsTrue(seen.Add(cell), $"格子重叠: {cell}");
                    Assert.AreEqual(GridManager.CellHole, gm.GetCell(cell.x, cell.y));
                }
            }
        }

        [Test]
        public void QuotaBookkeeping_GeneratedCountEqualsAliveHoles_WithAndWithoutMerge()
        {
            // mergeChance=1 尽量触发合并，mergeChance=0 不触发；两种情况下记账都必须自洽
            foreach (float mergeChance in new[] { 0f, 1f })
            {
                for (int seed = 1; seed <= 10; seed++)
                {
                    GridManager gm = CreateManager();
                    gm.GenerateLevel(CreateConfig(seed, mergeChance: mergeChance));
                    Assert.AreEqual(gm.GetAllHoles().Count, gm.GetGeneratedHoleCount(),
                        $"配额记账不一致 (seed={seed}, mergeChance={mergeChance})");
                }
            }
        }

        [Test]
        public void Merge_SomeSeed_ProducesMergedHoleLargerThanMaxHoleSize()
        {
            bool mergedObserved = false;
            for (int seed = 1; seed <= 30 && !mergedObserved; seed++)
            {
                GridManager gm = CreateManager();
                LevelConfig config = CreateConfig(seed, rows: 12, cols: 12,
                    holeCount: 3, mergeChance: 1f, minHoleSize: 9, maxHoleSize: 15);
                gm.GenerateLevel(config);
                foreach (Hole hole in gm.GetAllHoles())
                {
                    if (hole.GetRequiredCellCount() > config.GetMaxHoleSize())
                    {
                        mergedObserved = true;
                        break;
                    }
                }
            }
            Assert.IsTrue(mergedObserved, "30 个种子内未观察到任何合并，合并路径可能未生效");
        }

        [Test]
        public void Spread_NeverAdjacentToFilledHole()
        {
            GridManager gm = CreateManager();
            gm.GenerateLevel(CreateConfig(11, holeCount: 1, totalHoles: 10, mergeChance: 1f));
            List<Hole> holes = gm.GetAllHoles();
            Assert.IsNotEmpty(holes);

            Hole filled = holes[0];
            filled.NotifyCellFilled(); // 模拟被填过（nowCellCount > 0）

            var filledCells = new HashSet<Vector2Int>(filled.GetCells());
            for (int i = 0; i < 10; i++)
            {
                Hole newHole = gm.SpawnHoleForDebug();
                if (newHole == null)
                {
                    break;
                }
                foreach (Vector2Int cell in newHole.GetCells())
                {
                    for (int d = 0; d < 4; d++)
                    {
                        int nr = cell.x + (d == 0 ? -1 : d == 1 ? 1 : 0);
                        int nc = cell.y + (d == 2 ? -1 : d == 3 ? 1 : 0);
                        Assert.IsFalse(filledCells.Contains(new Vector2Int(nr, nc)),
                            $"新洞格子 {cell} 与已填洞相邻");
                    }
                }
            }
        }

        [Test]
        public void GetUnfilledCellCount_EqualsTotalHoleCells_AtStart()
        {
            GridManager gm = CreateManager();
            gm.GenerateLevel(CreateConfig(5));
            int total = 0;
            foreach (Hole hole in gm.GetAllHoles())
            {
                total += hole.GetRequiredCellCount();
            }
            Assert.AreEqual(total, gm.GetUnfilledCellCount());
        }
    }
}
