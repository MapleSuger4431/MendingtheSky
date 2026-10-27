using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    /// <summary>
    /// 阶段 5 洞口闭合流程测试（无 GridVisual 时立即结算，便于纯逻辑验证）：
    /// 填满 → 自动进入闭合 → 结算后洞被移除、格子恢复为 -1。
    /// </summary>
    public class ClosingFlowTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private GridManager grid;
        private LevelConfig config;
        private LingShiTypeConfig typeConfig;

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

            var go = new GameObject("Grid_ClosingTest");
            cleanup.Add(go);
            grid = go.AddComponent<GridManager>();
            grid.GenerateLevel(config);

            typeConfig = ScriptableObject.CreateInstance<LingShiTypeConfig>();
            cleanup.Add(typeConfig);
            typeConfig.typeId = "test";
            typeConfig.cookTime = 1f;
            typeConfig.qiValue = 2f;
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

        private LingShi CreateMonomino(int sourceHoleId = -1)
        {
            var go = new GameObject("Monomino_ClosingTest");
            var ls = go.AddComponent<LingShi>();
            var shape = new LingShiShape();
            shape.Init(new List<Vector2Int> { Vector2Int.zero });
            ls.Init(typeConfig, shape, Vector3.zero, 1f, 1f);
            ls.SetSourceHoleId(sourceHoleId);
            ls.SetState(LingShiState.Cooked);
            return ls;
        }

        [Test]
        public void FillAllCells_AutoClosesAndRemovesHole()
        {
            Hole hole = grid.GetAllHoles()[0];
            var cells = hole.GetCells();
            int holeId = hole.GetHoleId();

            foreach (Vector2Int cell in cells)
            {
                LingShi piece = CreateMonomino();
                grid.FillCells(piece, cell.x, cell.y);
            }

            // 无 GridVisual → 立即结算：洞被移除、所有格子恢复为正常
            Assert.IsNull(grid.GetHoleById(holeId), "填满后洞应被移除");
            foreach (Vector2Int cell in cells)
            {
                Assert.AreEqual(GridManager.CellNormal, grid.GetCell(cell.x, cell.y),
                    $"格子 {cell} 应恢复为正常");
            }
        }

        [Test]
        public void PartialFill_DoesNotCloseHole()
        {
            Hole hole = grid.GetAllHoles()[0];
            var cells = hole.GetCells();
            LingShi piece = CreateMonomino();
            grid.FillCells(piece, cells[0].x, cells[0].y);

            Assert.IsNotNull(grid.GetHoleById(hole.GetHoleId()), "未填满时洞不应移除");
            Assert.IsFalse(hole.IsClosing(), "未填满时不应进入闭合");
        }

        [Test]
        public void Full_HealsRegardlessOfShapes()
        {
            // 2026-10-27 方案 D：不再校验形状清单，洞填满就愈合（形状随便拼）
            Hole hole = grid.GetAllHoles()[0];
            var cells = hole.GetCells();
            int holeId = hole.GetHoleId();

            foreach (Vector2Int cell in cells)
            {
                grid.FillCells(CreateMonomino(999), cell.x, cell.y);
            }

            Assert.IsNull(grid.GetHoleById(holeId), "填满即愈合，不应再校验形状组合");
        }

        [Test]
        public void FullWithRedundantPieces_IgnoresShapeList()
        {
            // 冗余灵石（sourceHoleId == -1）不参与形状校验：全部用冗余单格填满也应愈合
            Hole hole = grid.GetAllHoles()[0];
            var cells = hole.GetCells();
            int holeId = hole.GetHoleId();

            foreach (Vector2Int cell in cells)
            {
                grid.FillCells(CreateMonomino(-1), cell.x, cell.y);
            }

            Assert.IsNull(grid.GetHoleById(holeId), "冗余灵石不占清单额度，填满即愈合");
        }

        [Test]
        public void FullWithMatchingShapes_Heals()
        {
            // 用本洞切出的灵石填满 → 愈合移除（方案 D 下这也只是"填满即愈合"的一个特例）
            Hole hole = grid.GetAllHoles()[0];
            var cells = hole.GetCells();
            int holeId = hole.GetHoleId();

            foreach (Vector2Int cell in cells)
            {
                grid.FillCells(CreateMonomino(holeId), cell.x, cell.y);
            }

            Assert.IsNull(grid.GetHoleById(hole.GetHoleId()), "填满时应愈合移除");
        }
    }
}
