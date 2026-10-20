using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    public class ShapeCutterTests
    {
        private ShapePoolConfig pool;

        [SetUp]
        public void SetUp()
        {
            pool = ScriptableObject.CreateInstance<ShapePoolConfig>();
            pool.shapes = new List<ShapeEntry>();
            AddShape(new Vector2Int(0, 0));
            AddShape(new Vector2Int(0, 0), new Vector2Int(0, 1));
            AddShape(new Vector2Int(0, 0), new Vector2Int(1, 0));
            AddShape(new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2));
            AddShape(new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1));
            AddShape(new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1));
            AddShape(new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 1));
            AddShape(new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 2));
            AddShape(new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(2, 1));
            AddShape(new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 0));
            AddShape(new Vector2Int(0, 0), new Vector2Int(0, 2), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2));
            AddShape(new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(2, 1));
        }

        [TearDown]
        public void TearDown()
        {
            if (pool != null)
            {
                Object.DestroyImmediate(pool);
            }
        }

        private void AddShape(params Vector2Int[] cells)
        {
            pool.shapes.Add(new ShapeEntry { cells = cells.ToList(), weight = 1f });
        }

        private static void AssertCoverage(List<Vector2Int> holeCells, List<List<Vector2Int>> pieces)
        {
            var covered = new HashSet<Vector2Int>();
            foreach (List<Vector2Int> piece in pieces)
            {
                Assert.IsNotEmpty(piece);
                Assert.IsTrue(piece.Contains(Vector2Int.zero), "每块必须包含锚点 (0,0)");
                foreach (Vector2Int cell in piece)
                {
                    Assert.IsTrue(covered.Add(cell), $"格子 {cell} 被重复覆盖");
                }
            }
            var holeSet = new HashSet<Vector2Int>(holeCells);
            Assert.AreEqual(holeSet.Count, covered.Count, "覆盖格子数与洞不一致");
            Assert.IsTrue(holeSet.SetEquals(covered), "覆盖格子和洞不一致");
        }

        [Test]
        public void Cut_FullFiveBySix_CoversAllCells()
        {
            var hole = new List<Vector2Int>();
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 6; c++)
                {
                    hole.Add(new Vector2Int(r, c));
                }
            }
            var pieces = ShapeCutter.Cut(hole, pool, 8, new System.Random(42));
            AssertCoverage(hole, pieces);
            Assert.LessOrEqual(pieces.Count, 8, "5x6 洞应不超过 8 块");
        }

        [Test]
        public void Cut_LShapeNineCells_CoversAllCells()
        {
            var hole = new List<Vector2Int>();
            for (int r = 0; r < 5; r++) hole.Add(new Vector2Int(r, 0));
            for (int c = 1; c < 5; c++) hole.Add(new Vector2Int(4, c));
            // 5 + 4 = 9 格 L 形
            Assert.AreEqual(9, hole.Count);
            var pieces = ShapeCutter.Cut(hole, pool, 8, new System.Random(7));
            AssertCoverage(hole, pieces);
        }

        [Test]
        public void Cut_GeneratedHoles_AllCovered_AndMaxPieces()
        {
            var config = ScriptableObject.CreateInstance<LevelConfig>();
            config.seed = 99;
            config.minRows = 12;
            config.maxRows = 12;
            config.minCols = 24;
            config.maxCols = 24;
            config.holeCount = 4;
            config.totalHoles = 4;
            config.minHoleSize = 9;
            config.maxHoleSize = 30;
            config.maxMergedHoleSize = 60;
            config.minHoleRows = 3;
            config.maxHoleRows = 5;
            config.minHoleCols = 3;
            config.maxHoleCols = 6;
            config.mergeChance = 0f;

            var go = new GameObject("GridManager_CutTest");
            var grid = go.AddComponent<GridManager>();
            grid.GenerateLevel(config);
            Assert.Greater(grid.GetAllHoles().Count, 0);
            foreach (Hole hole in grid.GetAllHoles())
            {
                List<List<Vector2Int>> pieces = ShapeCutter.Cut(hole.GetCells(), pool, 8, new System.Random(hole.GetHoleId()));
                AssertCoverage(hole.GetCells(), pieces);
                Assert.LessOrEqual(pieces.Count, 8,
                    $"洞#{hole.GetHoleId()}（{hole.GetRequiredCellCount()} 格）块数超限：{pieces.Count}");
            }
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
        }
    }
}
