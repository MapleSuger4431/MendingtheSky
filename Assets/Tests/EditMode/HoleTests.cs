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
    }
}
