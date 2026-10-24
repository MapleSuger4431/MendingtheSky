using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    /// <summary>
    /// 锅系统单元测试：只接受未煮、容量限制、熬煮计时到点变 Cooked、
    /// 移除后释放容量。
    /// </summary>
    public class PotManagerTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private PotManager pot;
        private LingShiTypeConfig typeConfig;

        [SetUp]
        public void SetUp()
        {
            typeConfig = ScriptableObject.CreateInstance<LingShiTypeConfig>();
            cleanup.Add(typeConfig);
            typeConfig.typeId = "test";
            typeConfig.cookTime = 1f;
            typeConfig.qiValue = 1f;

            var potGo = new GameObject("Pot_Test");
            cleanup.Add(potGo);
            pot = potGo.AddComponent<PotManager>();
            pot.SetMaxCount(2);
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

        private LingShi CreatePiece(MendingtheSky.LingShiState state)
        {
            var go = new GameObject("LingShi_PotTest");
            cleanup.Add(go);
            var ls = go.AddComponent<LingShi>();
            var shape = new LingShiShape();
            shape.Init(new List<Vector2Int> { Vector2Int.zero });
            ls.Init(typeConfig, shape, Vector3.zero, 1f, 1f);
            if (state != MendingtheSky.LingShiState.InBasket)
            {
                ls.SetState(state);
            }
            return ls;
        }

        [Test]
        public void TryAdd_AcceptsInBasket_RejectsOtherStates()
        {
            LingShi inBasket = CreatePiece(MendingtheSky.LingShiState.InBasket);
            Assert.IsTrue(pot.TryAddLingShi(inBasket));
            Assert.AreEqual(MendingtheSky.LingShiState.Cooking, inBasket.GetState());

            LingShi cooked = CreatePiece(MendingtheSky.LingShiState.Cooked);
            Assert.IsFalse(pot.TryAddLingShi(cooked), "只有未煮灵石能进锅");
        }

        [Test]
        public void Capacity_BlocksWhenFull_FreesAfterRemove()
        {
            LingShi a = CreatePiece(MendingtheSky.LingShiState.InBasket);
            LingShi b = CreatePiece(MendingtheSky.LingShiState.InBasket);
            LingShi c = CreatePiece(MendingtheSky.LingShiState.InBasket);
            Assert.IsTrue(pot.TryAddLingShi(a));
            Assert.IsTrue(pot.TryAddLingShi(b));
            Assert.IsFalse(pot.TryAddLingShi(c), "容量为 2 时第三块应被拒绝");
            Assert.AreEqual(2, pot.GetCount());

            pot.RemoveLingShi(a);
            Assert.IsTrue(pot.TryAddLingShi(c), "移除后应重新有空位");
        }

        [Test]
        public void Tick_CooksAfterCookTime()
        {
            LingShi ls = CreatePiece(MendingtheSky.LingShiState.InBasket);
            Assert.IsTrue(pot.TryAddLingShi(ls));
            pot.Tick(0.6f);
            Assert.AreEqual(MendingtheSky.LingShiState.Cooking, ls.GetState(), "时间未到仍应熬煮中");
            pot.Tick(0.6f);
            Assert.AreEqual(MendingtheSky.LingShiState.Cooked, ls.GetState(), "超过 cookTime 应变为已煮");
        }

        [Test]
        public void Clear_RemovesAllPieces()
        {
            LingShi a = CreatePiece(MendingtheSky.LingShiState.InBasket);
            pot.TryAddLingShi(a);
            pot.Clear();
            Assert.AreEqual(0, pot.GetCount());
        }
    }
}
