using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    /// <summary>
    /// 无尽模式难度曲线：温和爬升 + 各项封顶。
    /// 只测纯计算，不依赖场景。
    /// </summary>
    public class EndlessDifficultyTests
    {
        private static EndlessDifficulty MakeWarm()
        {
            return new EndlessDifficulty
            {
                costIncreasePerHole = 0.02f,
                maxCostMultiplier = 3f,
                cellsTierHoles = 5,
                cellsPerTier = 1,
                maxCellBonus = 15,
                intervalTierHoles = 5,
                intervalReducePerTier = 0f,
                minIntervalMultiplier = 0.6f
            };
        }

        [Test]
        public void CostMultiplier_StartsAtOneAndGrows()
        {
            var d = MakeWarm();
            Assert.AreEqual(1f, d.GetCostMultiplier(0), 0.0001f, "开局不应有额外消耗");
            Assert.AreEqual(1.2f, d.GetCostMultiplier(10), 0.0001f, "补 10 洞 = +20%");
        }

        [Test]
        public void CostMultiplier_IsCapped()
        {
            var d = MakeWarm();
            Assert.AreEqual(3f, d.GetCostMultiplier(500), 0.0001f, "消耗倍率应封顶在 3 倍");
        }

        [Test]
        public void CellBonus_GrowsEveryFiveHolesAndCaps()
        {
            var d = MakeWarm();
            Assert.AreEqual(0, d.GetCellBonus(4), "不满 5 洞不加格");
            Assert.AreEqual(1, d.GetCellBonus(5));
            Assert.AreEqual(2, d.GetCellBonus(10));
            Assert.AreEqual(15, d.GetCellBonus(500), "格数增量应封顶 +15（30 → 45 格）");
        }

        [Test]
        public void IntervalMultiplier_StaysOneForChildFriendlyCurve()
        {
            var d = MakeWarm();
            Assert.AreEqual(1f, d.GetIntervalMultiplier(0), 0.0001f);
            Assert.AreEqual(1f, d.GetIntervalMultiplier(50), 0.0001f, "intervalReducePerTier=0 时补洞间隔不变");
        }

        [Test]
        public void IntervalMultiplier_ShortensAndHasFloor()
        {
            var d = MakeWarm();
            d.intervalReducePerTier = 0.08f;
            Assert.AreEqual(0.92f, d.GetIntervalMultiplier(5), 0.0001f, "每档 -8%");
            Assert.AreEqual(0.6f, d.GetIntervalMultiplier(500), 0.0001f, "间隔倍率应封底 0.6");
        }

        [Test]
        public void NegativeOrZeroInputs_AreSafe()
        {
            var d = MakeWarm();
            Assert.AreEqual(1f, d.GetCostMultiplier(-3), 0.0001f);
            Assert.AreEqual(0, d.GetCellBonus(-3));
            Assert.AreEqual(1f, d.GetIntervalMultiplier(-3), 0.0001f);
        }
    }
}
