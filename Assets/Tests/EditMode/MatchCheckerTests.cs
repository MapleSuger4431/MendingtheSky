using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    public class MatchCheckerTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private GridManager gridManager;
        private LevelConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<LevelConfig>();
            cleanup.Add(config);
            config.seed = 42;
            config.minRows = 12;
            config.maxRows = 12;
            config.minCols = 24;
            config.maxCols = 24;
            config.cellSize = 1f;
            config.holeCount = 3;
            config.totalHoles = 3;
            config.minHoleSize = 9;
            config.maxHoleSize = 30;
            config.maxMergedHoleSize = 60;
            config.minHoleRows = 3;
            config.maxHoleRows = 5;
            config.minHoleCols = 3;
            config.maxHoleCols = 6;
            config.mergeChance = 0f;

            var go = new GameObject("GridManager_MatchTest");
            cleanup.Add(go);
            gridManager = go.AddComponent<GridManager>();
            gridManager.GenerateLevel(config);
        }

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

        private LingShi CreatePiece(List<Vector2Int> cells, int sourceHoleId = -1, bool cooked = true)
        {
            var go = new GameObject("LingShi_Test");
            cleanup.Add(go);
            var ls = go.AddComponent<LingShi>();
            var typeConfig = ScriptableObject.CreateInstance<LingShiTypeConfig>();
            cleanup.Add(typeConfig);
            var shape = new LingShiShape();
            shape.Init(cells);
            ls.Init(typeConfig, shape, Vector3.zero, 1f, 1f);
            ls.SetSourceHoleId(sourceHoleId);
            if (cooked)
            {
                ls.SetState(LingShiState.Cooked);
            }
            return ls;
        }

        private static List<Vector2Int> FindAdjacentPair(Hole hole)
        {
            var set = new HashSet<Vector2Int>(hole.GetCells());
            foreach (Vector2Int cell in hole.GetCells())
            {
                var right = new Vector2Int(cell.x, cell.y + 1);
                if (set.Contains(right))
                {
                    return new List<Vector2Int> { cell, right };
                }
            }
            return null;
        }

        [Test]
        public void CanPlace_ValidOnHoleCells_True()
        {
            Hole hole = gridManager.GetAllHoles()[0];
            List<Vector2Int> pair = FindAdjacentPair(hole);
            Assert.IsNotNull(pair, "洞内应存在相邻格");
            LingShi piece = CreatePiece(new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) },
                hole.GetHoleId());
            Assert.IsTrue(MatchChecker.CanPlace(piece, pair[0].x, pair[0].y, gridManager));
        }

        [Test]
        public void CanPlace_OnNormalCell_False()
        {
            Hole hole = gridManager.GetAllHoles()[0];
            Vector2Int normal = new Vector2Int(0, 0);
            Assert.IsFalse(hole.IsCellInHole(normal.x, normal.y), "假设 (0,0) 不在洞内");
            LingShi piece = CreatePiece(new List<Vector2Int> { Vector2Int.zero }, hole.GetHoleId());
            Assert.IsFalse(MatchChecker.CanPlace(piece, normal.x, normal.y, gridManager));
        }

        [Test]
        public void CanPlace_NotCooked_False()
        {
            Hole hole = gridManager.GetAllHoles()[0];
            List<Vector2Int> pair = FindAdjacentPair(hole);
            LingShi piece = CreatePiece(new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) },
                hole.GetHoleId(), cooked: false);
            Assert.IsFalse(MatchChecker.CanPlace(piece, pair[0].x, pair[0].y, gridManager));
        }

        [Test]
        public void CanPlace_OtherHoleSameShape_True_SourceBindingRemoved()
        {
            // 2026-10-24 用户反馈：不同洞口的同形状灵石可以互换，取消来源洞硬绑定
            List<Hole> holes = gridManager.GetAllHoles();
            Assert.GreaterOrEqual(holes.Count, 2);
            Hole target = holes[0];
            Hole other = holes[1];
            List<Vector2Int> pair = FindAdjacentPair(target);
            LingShi piece = CreatePiece(new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) },
                other.GetHoleId());
            Assert.IsTrue(MatchChecker.CanPlace(piece, pair[0].x, pair[0].y, gridManager),
                "同形状灵石应可在任意洞口的合法位置放置");
        }

        [Test]
        public void FillCells_UpdatesGridAndHole_UnfillReverts()
        {
            Hole hole = gridManager.GetAllHoles()[0];
            List<Vector2Int> pair = FindAdjacentPair(hole);
            LingShi piece = CreatePiece(new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) },
                hole.GetHoleId());
            int before = hole.GetNowCellCount();
            gridManager.FillCells(piece, pair[0].x, pair[0].y);
            Assert.AreEqual(before + 2, hole.GetNowCellCount());
            Assert.AreEqual(GridManager.CellFilled, gridManager.GetCell(pair[0].x, pair[0].y));
            Assert.AreEqual(GridManager.CellFilled, gridManager.GetCell(pair[1].x, pair[1].y));
            Assert.AreEqual(2, piece.GetPlacedCells().Count);
            Assert.AreEqual(LingShiState.Placed, piece.GetState());

            gridManager.UnfillCells(piece);
            Assert.AreEqual(before, hole.GetNowCellCount());
            Assert.AreEqual(GridManager.CellHole, gridManager.GetCell(pair[0].x, pair[0].y));
            Assert.AreEqual(GridManager.CellHole, gridManager.GetCell(pair[1].x, pair[1].y));
        }

        [Test]
        public void FillAllCells_HoleIsFull()
        {
            Hole hole = gridManager.GetAllHoles()[0];
            foreach (Vector2Int cell in hole.GetCells())
            {
                LingShi single = CreatePiece(new List<Vector2Int> { Vector2Int.zero }, hole.GetHoleId());
                gridManager.FillCells(single, cell.x, cell.y);
            }
            Assert.IsTrue(hole.IsFull());
            Assert.AreEqual(0, gridManager.GetUnfilledCellCount());
        }

        [Test]
        public void CanPlace_RedundantPiece_WithoutSourceHole_True()
        {
            // 冗余灵石 sourceHoleId = -1（无来源洞绑定）：形状匹配且同洞即可放
            List<Hole> holes = gridManager.GetAllHoles();
            Assert.GreaterOrEqual(holes.Count, 1);
            Hole target = holes[0];
            List<Vector2Int> pair = FindAdjacentPair(target);
            Assert.IsNotNull(pair);
            LingShi redundant = CreatePiece(new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) }, -1);
            Assert.IsTrue(MatchChecker.CanPlace(redundant, pair[0].x, pair[0].y, gridManager),
                "冗余灵石没有来源洞绑定，形状匹配即应判可放");
        }

        [Test]
        public void CanPlace_RequiresMatchingShapeEntry()
        {
            // 2026-10-25 用户设计：洞口维护形状清单，清单里必须有对应形状才能放
            Hole hole = gridManager.GetAllHoles()[0];
            List<Vector2Int> pair = FindAdjacentPair(hole);
            hole.AddShapeEntry(new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) });

            LingShi domino = CreatePiece(new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) });
            Assert.IsTrue(MatchChecker.CanPlace(domino, pair[0].x, pair[0].y, gridManager),
                "清单内有该形状 → 可放");

            LingShi single = CreatePiece(new List<Vector2Int> { Vector2Int.zero });
            Assert.IsFalse(MatchChecker.CanPlace(single, pair[0].x, pair[0].y, gridManager),
                "清单里没有该形状 → 不可放");
        }

        [Test]
        public void FillConsumesShapeEntry_UnfillRestores()
        {
            Hole hole = gridManager.GetAllHoles()[0];
            List<Vector2Int> pair = FindAdjacentPair(hole);
            var shape = new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) };
            hole.AddShapeEntry(shape);
            LingShi piece = CreatePiece(shape);

            gridManager.FillCells(piece, pair[0].x, pair[0].y);
            Assert.AreEqual(0, hole.GetRemainingShapeEntries().Count, "放置后清单条目应被消耗");

            gridManager.UnfillCells(piece);
            Assert.AreEqual(1, hole.GetRemainingShapeEntries().Count, "取下后清单条目应归还");
        }
    }
}
