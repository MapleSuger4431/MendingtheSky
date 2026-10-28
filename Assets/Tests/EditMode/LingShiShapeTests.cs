using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    public class LingShiShapeTests
    {
        private static HashSet<Vector2Int> ToSet(List<Vector2Int> cells)
        {
            return new HashSet<Vector2Int>(cells);
        }

        [Test]
        public void Rotate_FourTimes_ReturnsToBase()
        {
            var shape = new LingShiShape();
            shape.Init(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0) });
            HashSet<Vector2Int> baseSet = ToSet(shape.GetCurrentCells());
            for (int i = 0; i < 4; i++)
            {
                shape.Rotate();
            }
            Assert.AreEqual(baseSet, ToSet(shape.GetCurrentCells()));
        }

        [Test]
        public void Rotate90_MapsCellsClockwise_AnchorPreserved()
        {
            var shape = new LingShiShape();
            shape.Init(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1) });
            shape.Rotate();
            HashSet<Vector2Int> cells = ToSet(shape.GetCurrentCells());
            Assert.IsTrue(cells.Contains(new Vector2Int(0, 0)), "锚点 (0,0) 必须保留");
            Assert.IsTrue(cells.Contains(new Vector2Int(1, 0)), "(0,1) 顺时针旋转后应为 (1,0)");
        }

        [Test]
        public void RotateCounterClockwise_FourTimes_ReturnsToBase()
        {
            var shape = new LingShiShape();
            shape.Init(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0) });
            HashSet<Vector2Int> baseSet = ToSet(shape.GetCurrentCells());
            for (int i = 0; i < 4; i++)
            {
                shape.RotateCounterClockwise();
            }
            Assert.AreEqual(baseSet, ToSet(shape.GetCurrentCells()));
            Assert.AreEqual(0, shape.GetRotationSteps());
        }

        [Test]
        public void RotateAndCounterRotate_CancelOut()
        {
            var shape = new LingShiShape();
            shape.Init(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) });
            HashSet<Vector2Int> baseSet = ToSet(shape.GetCurrentCells());
            shape.Rotate();
            shape.RotateCounterClockwise();
            Assert.AreEqual(baseSet, ToSet(shape.GetCurrentCells()));
            Assert.AreEqual(0, shape.GetRotationSteps());
        }

        [Test]
        public void ThreeCounterClockwise_EqualsOneClockwise()
        {
            var ccw = new LingShiShape();
            ccw.Init(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0) });
            for (int i = 0; i < 3; i++)
            {
                ccw.RotateCounterClockwise();
            }

            var cw = new LingShiShape();
            cw.Init(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0) });
            cw.Rotate();

            Assert.AreEqual(ToSet(cw.GetCurrentCells()), ToSet(ccw.GetCurrentCells()));
            Assert.AreEqual(cw.GetRotationSteps(), ccw.GetRotationSteps());
        }

        [Test]
        public void RotateCounterClockwise_MapsCellsOppositeDirection()
        {
            var shape = new LingShiShape();
            shape.Init(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1) });
            shape.RotateCounterClockwise();
            HashSet<Vector2Int> cells = ToSet(shape.GetCurrentCells());
            Assert.IsTrue(cells.Contains(new Vector2Int(0, 0)), "锚点 (0,0) 必须保留");
            Assert.IsTrue(cells.Contains(new Vector2Int(-1, 0)), "(0,1) 逆时针旋转后应为 (-1,0)");
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var shape = new LingShiShape();
            shape.Init(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1) });
            LingShiShape clone = shape.Clone();
            clone.Rotate();
            Assert.AreNotEqual(ToSet(shape.GetCurrentCells()), ToSet(clone.GetCurrentCells()));
        }

        [Test]
        public void SetRotationSteps_ModuloFour()
        {
            var shape = new LingShiShape();
            shape.Init(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1) });
            shape.SetRotationSteps(5);
            Assert.AreEqual(1, shape.GetRotationSteps());
            shape.SetRotationSteps(-1);
            Assert.AreEqual(3, shape.GetRotationSteps());
        }

        [Test]
        public void GetBaseCells_ReturnsCopy()
        {
            var shape = new LingShiShape();
            shape.Init(new List<Vector2Int> { new Vector2Int(0, 0) });
            List<Vector2Int> copy = shape.GetBaseCells();
            copy.Clear();
            Assert.AreEqual(1, shape.GetCellCount());
        }
    }
}
