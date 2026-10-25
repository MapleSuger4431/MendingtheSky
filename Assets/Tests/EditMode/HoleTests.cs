using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    public class HoleTests
    {
        [Test]
        public void BoundaryEdges_SingleCell_Returns4()
        {
            var hole = new Hole();
            hole.Init(1, new List<Vector2Int> { new Vector2Int(2, 3) });
            Assert.AreEqual(4, hole.GetBoundaryEdges().Count);
        }

        [Test]
        public void BoundaryEdges_TwoByTwoBlock_Returns8()
        {
            var hole = new Hole();
            hole.Init(1, new List<Vector2Int>
            {
                new Vector2Int(0, 0), new Vector2Int(0, 1),
                new Vector2Int(1, 0), new Vector2Int(1, 1)
            });
            Assert.AreEqual(8, hole.GetBoundaryEdges().Count);
        }

        [Test]
        public void IsFull_InitiallyFalse_TrueAfterFillingAllCells()
        {
            var hole = new Hole();
            hole.Init(1, new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1) });
            Assert.IsFalse(hole.IsFull());
            hole.NotifyCellFilled();
            Assert.IsFalse(hole.IsFull());
            hole.NotifyCellFilled();
            Assert.IsTrue(hole.IsFull());
        }

        [Test]
        public void MergeWith_TwoAdjacentCells_SixBoundaryEdges()
        {
            var a = new Hole();
            a.Init(1, new List<Vector2Int> { new Vector2Int(0, 0) });
            var b = new Hole();
            b.Init(2, new List<Vector2Int> { new Vector2Int(0, 1) });

            a.MergeWith(b);

            Assert.AreEqual(2, a.GetRequiredCellCount());
            Assert.AreEqual(6, a.GetBoundaryEdges().Count);
            Assert.IsTrue(a.IsCellInHole(0, 1));
        }

        [Test]
        public void GetCells_ReturnsCopy_MutationDoesNotAffectHole()
        {
            var hole = new Hole();
            hole.Init(1, new List<Vector2Int> { new Vector2Int(0, 0) });
            List<Vector2Int> copy = hole.GetCells();
            copy.Clear();
            Assert.AreEqual(1, hole.GetRequiredCellCount());
        }

        [Test]
        public void NotifyCellUnfilled_NeverBelowZero()
        {
            var hole = new Hole();
            hole.Init(1, new List<Vector2Int> { new Vector2Int(0, 0) });
            hole.NotifyCellUnfilled();
            Assert.AreEqual(0, hole.GetNowCellCount());
        }

        [Test]
        public void GetQiReward_SumsPlacedPieceValues_AndRemovalUpdates()
        {
            var typeA = ScriptableObject.CreateInstance<LingShiTypeConfig>();
            typeA.qiValue = 2f;
            var typeB = ScriptableObject.CreateInstance<LingShiTypeConfig>();
            typeB.qiValue = 3f;

            var goA = new GameObject("PieceA");
            var lsA = goA.AddComponent<LingShi>();
            var shapeA = new LingShiShape();
            shapeA.Init(new List<Vector2Int> { Vector2Int.zero });
            lsA.Init(typeA, shapeA, Vector3.zero, 1f, 1f);

            var goB = new GameObject("PieceB");
            var lsB = goB.AddComponent<LingShi>();
            var shapeB = new LingShiShape();
            shapeB.Init(new List<Vector2Int> { Vector2Int.zero });
            lsB.Init(typeB, shapeB, Vector3.zero, 1f, 1f);

            var hole = new Hole();
            hole.Init(1, new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) });
            hole.AddLingShi(lsA);
            hole.AddLingShi(lsB);
            Assert.AreEqual(5f, hole.GetQiReward(), 0.001f, "返还 = 两块灵石 qiValue 之和");

            hole.RemoveLingShi(lsA);
            Assert.AreEqual(3f, hole.GetQiReward(), 0.001f, "取下后返还值应减少");

            Object.DestroyImmediate(goA);
            Object.DestroyImmediate(goB);
            Object.DestroyImmediate(typeA);
            Object.DestroyImmediate(typeB);
        }

        [Test]
        public void MatchesPlacedShapes_ComparesShapeMultiset()
        {
            // 2026-10-25 用户设计：填满后只有「形状组合符合需求清单」才能愈合
            var hole = new Hole();
            hole.Init(1, new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) });
            hole.AddRequiredShapeEntry(new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) });

            var type = ScriptableObject.CreateInstance<LingShiTypeConfig>();
            var go = new GameObject("MatchPiece");
            var ls = go.AddComponent<LingShi>();
            var shape = new LingShiShape();
            shape.Init(new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) });
            ls.Init(type, shape, Vector3.zero, 1f, 1f);
            hole.AddLingShi(ls);
            Assert.IsTrue(hole.MatchesPlacedShapes(), "形状一致时应通过");

            var go2 = new GameObject("MismatchPiece");
            var ls2 = go2.AddComponent<LingShi>();
            var shape2 = new LingShiShape();
            shape2.Init(new List<Vector2Int> { Vector2Int.zero });
            ls2.Init(type, shape2, Vector3.zero, 1f, 1f);
            hole.AddLingShi(ls2);
            Assert.IsFalse(hole.MatchesPlacedShapes(), "出现清单外形状应不通过");

            hole.RemoveLingShi(ls2);
            Assert.IsTrue(hole.MatchesPlacedShapes(), "移除清单外形状后应恢复通过");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(go2);
            Object.DestroyImmediate(type);
        }
    }
}
