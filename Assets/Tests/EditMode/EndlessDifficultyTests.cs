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
                minIntervalMultiplier = 0.6f,
                potStartCount = 2,
                potTierHoles = 6,
                potMaxCount = 4,
                rotationStartChance = 0.4f,
                rotationTierHoles = 5,
                rotationIncreasePerTier = 0.05f,
                rotationMaxChance = 0.9f,
                precookedStartChance = 0.4f,
                precookedTierHoles = 5,
                precookedReducePerTier = 0.1f,
                precookedMinChance = 0f
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
            Assert.AreEqual(2, d.GetPotCapacity(-3), "锅容量不应因负数输入跌到 1 以下");
            Assert.AreEqual(0.4f, d.GetRotationChance(-3), 0.0001f);
            Assert.AreEqual(0.4f, d.GetPrecookedChance(-3), 0.0001f);
        }

        [Test]
        public void PotCapacity_StartsSmallAndWidens()
        {
            var d = MakeWarm();
            Assert.AreEqual(2, d.GetPotCapacity(0), "开局锅容量 2");
            Assert.AreEqual(2, d.GetPotCapacity(5), "不满一档不变");
            Assert.AreEqual(3, d.GetPotCapacity(6), "每 6 洞 +1");
            Assert.AreEqual(4, d.GetPotCapacity(12));
            Assert.AreEqual(4, d.GetPotCapacity(500), "锅容量应封顶 4");
        }

        [Test]
        public void RotationChance_RisesAndCaps()
        {
            var d = MakeWarm();
            Assert.AreEqual(0.4f, d.GetRotationChance(0), 0.0001f, "开局 0.4");
            Assert.AreEqual(0.45f, d.GetRotationChance(5), 0.0001f, "每 5 洞 +0.05");
            Assert.AreEqual(0.9f, d.GetRotationChance(500), 0.0001f, "概率应封顶 0.9");
        }

        [Test]
        public void PrecookedChance_ShrinksToZero()
        {
            var d = MakeWarm();
            Assert.AreEqual(0.4f, d.GetPrecookedChance(0), 0.0001f, "开局 40% 的新石直接煮熟");
            Assert.AreEqual(0.3f, d.GetPrecookedChance(5), 0.0001f, "每 5 洞 -0.1");
            Assert.AreEqual(0.2f, d.GetPrecookedChance(10), 0.0001f);
            Assert.AreEqual(0f, d.GetPrecookedChance(20), 0.0001f, "20 洞后不再预煮熟");
            Assert.AreEqual(0f, d.GetPrecookedChance(500), 0.0001f);
        }

        [Test]
        public void PrecookedChance_HonoursMinimum()
        {
            var d = MakeWarm();
            d.precookedMinChance = 0.1f;
            Assert.AreEqual(0.1f, d.GetPrecookedChance(50), 0.0001f, "配置了下限时不会降到 0");
        }
    }
}
