using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MendingtheSky.Tests
{
    /// <summary>
    /// 阶段 5 洞口闭合流程测试（无 GridVisual 时立即结算，便于纯逻辑验证）：
    /// 填满 → 自动进入闭合 → 结算后洞被移除、格子恢复为 -1。
    /// 2026-10-25 定稿：愈合只看是否填满，灵石按来源洞决定销毁或退回背篓。
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
            cleanup.Add(go);
            var ls = go.AddComponent<LingShi>();
            var shape = new LingShiShape();
            shape.Init(new List<Vector2Int> { Vector2Int.zero });
            ls.Init(typeConfig, shape, Vector3.zero, 1f, 1f);
            ls.SetSourceHoleId(sourceHoleId);
            ls.SetState(LingShiState.Cooked);
            return ls;
        }

        /// <summary>创建背篓并接到 GridManager（inventoryManager 是 private [SerializeField]）。</summary>
        private InventoryManager CreateInventory()
        {
            var go = new GameObject("Inventory_ClosingTest");
            cleanup.Add(go);
            var inventory = go.AddComponent<InventoryManager>();
            var so = new SerializedObject(grid);
            so.FindProperty("inventoryManager").objectReferenceValue = inventory;
            so.ApplyModifiedPropertiesWithoutUndo();
            return inventory;
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
        public void FullWithShapeMismatch_StillHeals()
        {
            // 填满即愈合：即使已放形状与切出清单不符（借用别的形状拼满），也正常愈合
            Hole hole = grid.GetAllHoles()[0];
            var cells = hole.GetCells();
            int holeId = hole.GetHoleId();
            // 需求清单登记为「一块 domino」，实际上用单格填满
            hole.AddRequiredShapeEntry(new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) });

            foreach (Vector2Int cell in cells)
            {
                grid.FillCells(CreateMonomino(), cell.x, cell.y);
            }

            Assert.IsNull(grid.GetHoleById(holeId), "填满即愈合，不看形状组合");
        }

        [Test]
        public void FullWithMatchingShapes_Heals()
        {
            // 需求清单与放置形状一致 → 正常愈合
            Hole hole = grid.GetAllHoles()[0];
            var cells = hole.GetCells();
            int holeId = hole.GetHoleId();
            foreach (Vector2Int _ in cells)
            {
                hole.AddRequiredShapeEntry(new List<Vector2Int> { Vector2Int.zero });
            }

            foreach (Vector2Int cell in cells)
            {
                grid.FillCells(CreateMonomino(), cell.x, cell.y);
            }

            Assert.IsNull(grid.GetHoleById(holeId), "形状一致时应愈合移除");
        }

        [Test]
        public void OwnLingShi_IsConsumed_WhenHoleHeals()
        {
            // 方案 A：本洞自己切出的灵石在愈合时被销毁
            InventoryManager inventory = CreateInventory();
            Hole hole = grid.GetAllHoles()[0];
            int holeId = hole.GetHoleId();

            var pieces = new List<LingShi>();
            foreach (Vector2Int cell in hole.GetCells())
            {
                LingShi piece = CreateMonomino(holeId);
                pieces.Add(piece);
                grid.FillCells(piece, cell.x, cell.y);
            }

            Assert.IsNull(grid.GetHoleById(holeId), "洞应已愈合");
            Assert.AreEqual(0, inventory.GetAll().Count, "本洞灵石被销毁，不该退回背篓");
            foreach (LingShi piece in pieces)
            {
                Assert.IsTrue(piece == null, "本洞自己的灵石应被销毁");
            }
        }

        [Test]
        public void BorrowedLingShi_IsReturnedToBasket_WhenHoleHeals()
        {
            // 方案 A：其它洞来源的灵石不销毁，退回背篓并保持已煮
            InventoryManager inventory = CreateInventory();
            List<Hole> holes = grid.GetAllHoles();
            Hole closingHole = holes[0];
            int closingHoleId = closingHole.GetHoleId();
            int sourceHoleId = holes[1].GetHoleId();

            var pieces = new List<LingShi>();
            foreach (Vector2Int cell in closingHole.GetCells())
            {
                LingShi piece = CreateMonomino(sourceHoleId);
                pieces.Add(piece);
                grid.FillCells(piece, cell.x, cell.y);
            }

            Assert.IsNull(grid.GetHoleById(closingHoleId), "洞应已愈合");
            Assert.AreEqual(pieces.Count, inventory.GetAll().Count, "借来的灵石应全部退回背篓");
            foreach (LingShi piece in pieces)
            {
                Assert.IsTrue(piece != null, "借来的灵石不应被销毁");
                Assert.AreEqual(LingShiState.Cooked, piece.GetState(), "退回背篓后应保持已煮");
            }
        }
    }
}
