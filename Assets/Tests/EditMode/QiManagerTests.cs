using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    /// <summary>
    /// 灵气系统单元测试：初始化、每帧消耗（只算 grid==0）、返还 clamp、
    /// 归零只触发一次、低灵气警告阈值。
    /// </summary>
    public class QiManagerTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private GridManager grid;
        private QiManager qi;
        private LevelConfig config;

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
            config.initialQi = 100f;
            config.qiPerCellPerSecond = 0.1f;

            var gridGo = new GameObject("Grid_QiTest");
            cleanup.Add(gridGo);
            grid = gridGo.AddComponent<GridManager>();
            grid.GenerateLevel(config);

            var qiGo = new GameObject("Qi_Test");
            cleanup.Add(qiGo);
            qi = qiGo.AddComponent<QiManager>();
            qi.Init(config, grid);
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

        [Test]
        public void Init_SetsMaxAndCurrentToInitialQi()
        {
            Assert.AreEqual(100f, qi.GetMaxQi(), 0.001f);
            Assert.AreEqual(100f, qi.GetCurrentQi(), 0.001f);
            Assert.IsFalse(qi.IsEmpty());
        }

        [Test]
        public void Tick_ConsumesByUnfilledCellCount()
        {
            int cells = grid.GetUnfilledCellCount();
            Assert.Greater(cells, 0);
            float expected = cells * 0.1f; // 1 秒消耗 = 空缺格数 × 每秒每格消耗
            qi.Tick(1f);
            Assert.AreEqual(100f - expected, qi.GetCurrentQi(), 0.001f);
        }

        [Test]
        public void AddQi_ClampsToMax()
        {
            qi.SpendQi(30f);
            Assert.AreEqual(70f, qi.GetCurrentQi(), 0.001f);
            qi.AddQi(999f);
            Assert.AreEqual(100f, qi.GetCurrentQi(), 0.001f, "返还不得超过上限");
        }

        [Test]
        public void SpendQi_ZeroTriggersEmptyOnce()
        {
            int emptyEvents = 0;
            qi.OnQiEmpty += () => emptyEvents++;
            qi.SpendQi(9999f);
            Assert.AreEqual(0f, qi.GetCurrentQi(), 0.001f);
            Assert.IsTrue(qi.IsEmpty());
            Assert.AreEqual(1, emptyEvents);
            qi.SpendQi(10f); // 归零后不再变化、不重复触发
            Assert.AreEqual(1, emptyEvents);
            Assert.AreEqual(0f, qi.GetCurrentQi(), 0.001f);
        }

        [Test]
        public void Warning_EntersAndLeavesAtRatioThreshold()
        {
            bool warning = false;
            int changes = 0;
            qi.OnQiWarning += w => { warning = w; changes++; };

            // 降到 10%（< 20%）→ 进入警告
            qi.SpendQi(90f);
            Assert.IsTrue(warning);
            // 回到 30% → 解除警告
            qi.AddQi(20f);
            Assert.IsFalse(warning);
            Assert.GreaterOrEqual(changes, 2);
        }
    }
}
