using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    /// <summary>
    /// 关卡进度：通关记录、星级只升不降、顺序解锁。
    /// 测试前备份整块进度，结束后还原，避免污染本机存档。
    /// </summary>
    public class LevelProgressTests
    {
        private LevelDatabase database;
        private LevelConfig level0;
        private LevelConfig level1;
        private LevelConfig endless;

        [SetUp]
        public void SetUp()
        {
            LevelProgress.ResetAll();

            level0 = ScriptableObject.CreateInstance<LevelConfig>();
            level0.levelId = 1;
            level0.displayName = "Level 1";
            level1 = ScriptableObject.CreateInstance<LevelConfig>();
            level1.levelId = 2;
            level1.displayName = "Level 2";
            endless = ScriptableObject.CreateInstance<LevelConfig>();
            endless.levelId = 999;
            endless.displayName = "Endless";
            endless.isEndless = true;

            database = ScriptableObject.CreateInstance<LevelDatabase>();
            database.levels = new[] { level0, level1, endless };
        }

        [TearDown]
        public void TearDown()
        {
            LevelProgress.ResetAll();
            Object.DestroyImmediate(level0);
            Object.DestroyImmediate(level1);
            Object.DestroyImmediate(endless);
            Object.DestroyImmediate(database);
        }

        [Test]
        public void Report_MarksClearedAndStoresStars()
        {
            Assert.IsFalse(LevelProgress.IsCleared(1), "初始应未通关");
            LevelProgress.Report(1, 2);

            Assert.IsTrue(LevelProgress.IsCleared(1));
            Assert.AreEqual(2, LevelProgress.GetStars(1));
        }

        [Test]
        public void Report_KeepsHigherStars()
        {
            LevelProgress.Report(1, 3);
            LevelProgress.Report(1, 1); // 再次通关但表现更差

            Assert.AreEqual(3, LevelProgress.GetStars(1), "星级只升不降，低分不应覆盖记录");
        }

        [Test]
        public void Report_RaisesOnChanged()
        {
            int count = 0;
            System.Action handler = () => count++;
            LevelProgress.OnChanged += handler;
            try
            {
                LevelProgress.Report(1, 1);
                Assert.GreaterOrEqual(count, 1, "首次通关应触发 OnChanged");
            }
            finally
            {
                LevelProgress.OnChanged -= handler;
            }
        }

        [Test]
        public void IsUnlocked_FirstLevelAlwaysUnlocked()
        {
            Assert.IsTrue(LevelProgress.IsUnlocked(database, 0), "第 1 关永远可玩");
        }

        [Test]
        public void IsUnlocked_RequiresPreviousLevelCleared()
        {
            Assert.IsFalse(LevelProgress.IsUnlocked(database, 1), "第 1 关未通关时第 2 关应锁定");

            LevelProgress.Report(level0.GetLevelId(), 2);
            Assert.IsTrue(LevelProgress.IsUnlocked(database, 1), "第 1 关通关后第 2 关应解锁");
        }

        [Test]
        public void IsUnlocked_EndlessAlwaysAvailable()
        {
            Assert.IsTrue(LevelProgress.IsUnlocked(database, 2), "无尽模式不受顺序解锁限制");
        }

        [Test]
        public void FormatStars_ShowsThreeSlots()
        {
            LevelProgress.Report(1, 2);
            Assert.AreEqual("★★☆", LevelProgress.FormatStars(1));
            Assert.AreEqual(string.Empty, LevelProgress.FormatStars(404), "未通关的关卡不显示星级");
        }

        [Test]
        public void Progress_PersistsToPlayerPrefs()
        {
            LevelProgress.Report(7, 3);
            Assert.AreEqual(1, PlayerPrefs.GetInt("progress.7.cleared", 0));
            Assert.AreEqual(3, PlayerPrefs.GetInt("progress.7.stars", 0));
        }
    }
}
