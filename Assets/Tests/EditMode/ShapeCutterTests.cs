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

        /// <summary>
        /// 校验切割结果。注意：ShapeCutter.Cut 返回的是「相对锚点」坐标（每块都以 (0,0) 为原点），
        /// 不能直接和洞的绝对坐标比对；这里改用「块格数之和 = 洞格数」+ CanTile 精确铺满校验。
        /// </summary>
        private static void AssertCoverage(List<Vector2Int> holeCells, List<List<Vector2Int>> pieces)
        {
            Assert.IsNotNull(pieces, "切割结果不应为 null");
            int pieceCellCount = 0;
            foreach (List<Vector2Int> piece in pieces)
            {
                Assert.IsNotEmpty(piece, "不允许出现空块");
                Assert.IsTrue(piece.Contains(Vector2Int.zero), "每块必须以 (0,0) 为锚点");
                Assert.AreEqual(piece.Count, new HashSet<Vector2Int>(piece).Count, "同一块内不应有重复格");
                pieceCellCount += piece.Count;
            }
            Assert.AreEqual(holeCells.Count, pieceCellCount, "块格数之和应等于洞格数");
            Assert.IsTrue(ShapeCutter.CanTile(holeCells, pieces), "切出的形状应能恰好铺满该洞");
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

        [Test]
        public void CanTile_CutPieces_CanExactlyCoverHole()
        {
            // 切出来的整组灵石必须能恰好铺满原洞（切割的基本承诺）
            var hole = new List<Vector2Int>();
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    hole.Add(new Vector2Int(r, c));
                }
            }
            List<List<Vector2Int>> pieces = ShapeCutter.Cut(hole, pool, 8, new System.Random(3));
            Assert.IsTrue(ShapeCutter.CanTile(hole, pieces), "切出的块应能铺满原洞");
        }

        [Test]
        public void CanTile_DeadlockArrangement_ReturnsFalse()
        {
            // T 形 4 格，用两块横向 2 格（共 4 格）永远铺不满 → 必须判定不可解
            var remaining = new List<Vector2Int>
            {
                new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 1)
            };
            var pieces = new List<List<Vector2Int>>
            {
                new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1) },
                new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1) }
            };
            Assert.IsFalse(ShapeCutter.CanTile(remaining, pieces), "T 形无法被两块横条铺满");
        }

        [Test]
        public void CanTile_CountMismatch_ReturnsFalse()
        {
            var remaining = new List<Vector2Int>
            {
                new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0)
            };
            var pieces = new List<List<Vector2Int>>
            {
                new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1) }
            };
            Assert.IsFalse(ShapeCutter.CanTile(remaining, pieces), "格子数与块格数不一致必须判 false");
        }

        [Test]
        public void CanTile_RotationAllowsFit_ReturnsTrue()
        {
            // 剩余是竖 2 格，块的基础形状是横 2 格；允许旋转时应当可解
            var remaining = new List<Vector2Int>
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0)
            };
            var pieces = new List<List<Vector2Int>>
            {
                new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1) }
            };
            Assert.IsTrue(ShapeCutter.CanTile(remaining, pieces), "横条旋转 90° 后应能铺满竖洞");
        }
    }
}
