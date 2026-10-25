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
            ls.SetSourceHoleId(1); // 本洞切出（sourceHoleId >= 0 才参与形状校验）
            hole.AddLingShi(ls);
            Assert.IsTrue(hole.MatchesPlacedShapes(), "形状一致时应通过");

            var go2 = new GameObject("MismatchPiece");
            var ls2 = go2.AddComponent<LingShi>();
            var shape2 = new LingShiShape();
            shape2.Init(new List<Vector2Int> { Vector2Int.zero });
            ls2.Init(type, shape2, Vector3.zero, 1f, 1f);
            ls2.SetSourceHoleId(1);
            hole.AddLingShi(ls2);
            Assert.IsFalse(hole.MatchesPlacedShapes(), "出现清单外形状应不通过");
            Assert.AreEqual(1, hole.GetUnexpectedShapeEntries().Count, "单格形状应被识别为清单外形状");

            hole.RemoveLingShi(ls2);
            Assert.IsTrue(hole.MatchesPlacedShapes(), "移除清单外形状后应恢复通过");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(go2);
            Object.DestroyImmediate(type);
        }

        [Test]
        public void MatchesPlacedShapes_IgnoresRedundantPieces()
        {
            // 2026-10-25 定稿：冗余补给灵石（sourceHoleId == -1）没有来源洞，不参与形状校验
            var hole = new Hole();
            hole.Init(1, new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) });
            hole.AddRequiredShapeEntry(new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) });

            var type = ScriptableObject.CreateInstance<LingShiTypeConfig>();
            var go = new GameObject("RedundantSingle");
            var ls = go.AddComponent<LingShi>();
            var shape = new LingShiShape();
            shape.Init(new List<Vector2Int> { Vector2Int.zero }); // 清单里没有单格，但它是冗余块
            ls.Init(type, shape, Vector3.zero, 1f, 1f);
            ls.SetSourceHoleId(-1);
            hole.AddLingShi(ls);

            Assert.IsTrue(hole.MatchesPlacedShapes(), "冗余灵石不占清单额度，不应导致判定失败");
            Assert.AreEqual(0, hole.GetUnexpectedShapeEntries().Count, "冗余灵石不算「多出的形状」");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(type);
        }

        [Test]
        public void MatchesPlacedShapes_AllowsUnusedListEntries()
        {
            // 清单不要求被全部抵扣：冗余灵石占掉的格子，对应清单里没被用上的条目
            var hole = new Hole();
            hole.Init(1, new List<Vector2Int>
            {
                Vector2Int.zero, new Vector2Int(0, 1), new Vector2Int(0, 2)
            });
            hole.AddRequiredShapeEntry(new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) }); // 横二连
            hole.AddRequiredShapeEntry(new List<Vector2Int> { Vector2Int.zero }); // 单格

            var type = ScriptableObject.CreateInstance<LingShiTypeConfig>();

            var goPair = new GameObject("OwnPair");
            var ownPair = goPair.AddComponent<LingShi>();
            var pairShape = new LingShiShape();
            pairShape.Init(new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) });
            ownPair.Init(type, pairShape, Vector3.zero, 1f, 1f);
            ownPair.SetSourceHoleId(1);
            hole.AddLingShi(ownPair);

            var goMono = new GameObject("RedundantMono");
            var redundantMono = goMono.AddComponent<LingShi>();
            var monoShape = new LingShiShape();
            monoShape.Init(new List<Vector2Int> { Vector2Int.zero });
            redundantMono.Init(type, monoShape, Vector3.zero, 1f, 1f);
            redundantMono.SetSourceHoleId(-1);
            hole.AddLingShi(redundantMono);

            Assert.IsTrue(hole.MatchesPlacedShapes(),
                "清单的单格条目没被本洞灵石用上，但缺口由冗余灵石占着，应判定通过");

            Object.DestroyImmediate(goPair);
            Object.DestroyImmediate(goMono);
            Object.DestroyImmediate(type);
        }

        [Test]
        public void GetRemainingShapeQuota_CountsDownByOwnPiecesOnly()
        {
            var pair = new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) };
            string signature = ShapeCutter.GetCanonicalSignature(pair);

            var hole = new Hole();
            hole.Init(1, new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) });
            hole.AddRequiredShapeEntry(pair);
            Assert.AreEqual(1, hole.GetRemainingShapeQuota(signature), "还没放时应有 1 条额度");

            var type = ScriptableObject.CreateInstance<LingShiTypeConfig>();

            var goOwn = new GameObject("OwnPair");
            var own = goOwn.AddComponent<LingShi>();
            var ownShape = new LingShiShape();
            ownShape.Init(pair);
            own.Init(type, ownShape, Vector3.zero, 1f, 1f);
            own.SetSourceHoleId(1);
            hole.AddLingShi(own);
            Assert.AreEqual(0, hole.GetRemainingShapeQuota(signature), "本洞同形状块抵扣后额度应为 0");

            var goRedundant = new GameObject("RedundantMono");
            var redundant = goRedundant.AddComponent<LingShi>();
            var monoShape = new LingShiShape();
            monoShape.Init(new List<Vector2Int> { Vector2Int.zero });
            redundant.Init(type, monoShape, Vector3.zero, 1f, 1f);
            redundant.SetSourceHoleId(-1);
            hole.AddLingShi(redundant);
            Assert.AreEqual(0, hole.GetRemainingShapeQuota(signature), "冗余灵石不占清单额度");

            Object.DestroyImmediate(goOwn);
            Object.DestroyImmediate(goRedundant);
            Object.DestroyImmediate(type);
        }
    }
}
