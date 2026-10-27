using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    /// <summary>
    /// 方案 D 的补给算法单测：打包顶替的几何判定 + 缺口账算。
    /// 只测纯计算，不依赖场景与预制体。
    /// </summary>
    public class SupplyTests
    {
        private static HashSet<Vector2Int> Set(params Vector2Int[] cells)
        {
            return new HashSet<Vector2Int>(cells);
        }

        [Test]
        public void TryFit_AcceptsShapeWhenAllCellsLandInside()
        {
            var hole = Set(new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2));
            var pair = new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) };
            Assert.IsTrue(InventoryManager.TryFit(pair, hole), "二连应能塞进三连缺口");
        }

        [Test]
        public void TryFit_RejectsShapeWhenPartOfItIsOutside()
        {
            // 只有一个孤立格，二连塞不进去
            var hole = Set(new Vector2Int(0, 0));
            var pair = new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) };
            Assert.IsFalse(InventoryManager.TryFit(pair, hole), "缺口只有 1 格时二连应放不下");
        }

        [Test]
        public void TryFit_RejectsWhenFreeCellsAreNotContiguous()
        {
            // 两个格子不相邻，二连依然塞不进去
            var hole = Set(new Vector2Int(0, 0), new Vector2Int(0, 2));
            var pair = new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) };
            Assert.IsFalse(InventoryManager.TryFit(pair, hole));
        }

        [Test]
        public void TryFit_HandlesNonZeroOriginShapes()
        {
            // 形状自身坐标带负值（玩家旋转后可能出现），仍应正确平移匹配
            var hole = Set(new Vector2Int(3, 3), new Vector2Int(3, 4));
            var shape = new List<Vector2Int> { new Vector2Int(0, -1), Vector2Int.zero };
            Assert.IsTrue(InventoryManager.TryFit(shape, hole));
        }

        [Test]
        public void ComputeDeficit_OnlyPositiveWhenStockIsShort()
        {
            // 缺口 20 格，库存 15 格，队列里还有 2 格，余量 3 → 还差 6
            Assert.AreEqual(6, InventoryManager.ComputeDeficit(20, 15, 2, 3));

            // 库存充足（含余量）→ 不需要补
            Assert.AreEqual(0, Mathf.Max(0, InventoryManager.ComputeDeficit(20, 25, 0, 3)));
        }

        [Test]
        public void ComputeDeficit_NegativeMarginIsTreatedAsZero()
        {
            Assert.AreEqual(10, InventoryManager.ComputeDeficit(10, 0, 0, -5));
        }

        [Test]
        public void ComputeDeficit_CountsPendingPiecesAsStock()
        {
            // 队列里还没弹出的石头也要算进"已有"，否则会补过头
            Assert.AreEqual(0, Mathf.Max(0, InventoryManager.ComputeDeficit(10, 4, 6, 0)));
        }
    }
}
