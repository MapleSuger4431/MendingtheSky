using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    /// <summary>
    /// 关卡数据库的边界测试：空表、正常取关、越界返回 null。
    /// </summary>
    public class LevelDatabaseTests
    {
        private LevelConfig a;
        private LevelConfig b;
        private LevelDatabase db;

        [SetUp]
        public void SetUp()
        {
            a = ScriptableObject.CreateInstance<LevelConfig>();
            a.levelId = 1;
            a.displayName = "Level A";

            b = ScriptableObject.CreateInstance<LevelConfig>();
            b.levelId = 2;
            b.displayName = "Level B";

            db = ScriptableObject.CreateInstance<LevelDatabase>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Object.DestroyImmediate(db);
        }

        [Test]
        public void EmptyDatabase_ReturnsZeroAndNull()
        {
            db.levels = new LevelConfig[0];
            Assert.AreEqual(0, db.GetTotalLevels());
            Assert.IsNull(db.GetLevel(0));
        }

        [Test]
        public void NullLevels_ReturnsZeroAndNull()
        {
            db.levels = null;
            Assert.AreEqual(0, db.GetTotalLevels());
            Assert.IsNull(db.GetLevel(0));
        }

        [Test]
        public void GetLevel_ReturnsConfiguredLevels()
        {
            db.levels = new[] { a, b };
            Assert.AreEqual(2, db.GetTotalLevels());
            Assert.AreSame(a, db.GetLevel(0));
            Assert.AreSame(b, db.GetLevel(1));
        }

        [Test]
        public void GetLevel_OutOfRange_ReturnsNull()
        {
            db.levels = new[] { a, b };
            Assert.IsNull(db.GetLevel(-1), "负索引应返回 null");
            Assert.IsNull(db.GetLevel(2), "超出长度应返回 null");
        }
    }
}
