using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    /// <summary>
    /// 无尽模式最高纪录：首次记录、低分不覆盖、高分更新。
    /// 测试会临时清空 PlayerPrefs 里的纪录，结束后恢复原值。
    /// </summary>
    public class EndlessRecordTests
    {
        private bool hadKey;
        private int oldValue;

        [SetUp]
        public void SetUp()
        {
            hadKey = PlayerPrefs.HasKey(EndlessRecord.PrefKey);
            oldValue = PlayerPrefs.GetInt(EndlessRecord.PrefKey, 0);
            EndlessRecord.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (hadKey)
            {
                PlayerPrefs.SetInt(EndlessRecord.PrefKey, oldValue);
            }
            else
            {
                PlayerPrefs.DeleteKey(EndlessRecord.PrefKey);
            }
            PlayerPrefs.Save();
        }

        [Test]
        public void GetBest_NoRecord_ReturnsZero()
        {
            Assert.AreEqual(0, EndlessRecord.GetBest());
        }

        [Test]
        public void Report_FirstTime_SavesScore()
        {
            Assert.AreEqual(3, EndlessRecord.Report(3));
            Assert.AreEqual(3, EndlessRecord.GetBest());
        }

        [Test]
        public void Report_LowerScore_DoesNotOverwrite()
        {
            EndlessRecord.Report(5);
            Assert.AreEqual(5, EndlessRecord.Report(2), "低分不应覆盖最高纪录");
            Assert.AreEqual(5, EndlessRecord.GetBest());
        }

        [Test]
        public void Report_HigherScore_Updates()
        {
            EndlessRecord.Report(3);
            Assert.AreEqual(7, EndlessRecord.Report(7));
            Assert.AreEqual(7, EndlessRecord.GetBest());
        }
    }
}
