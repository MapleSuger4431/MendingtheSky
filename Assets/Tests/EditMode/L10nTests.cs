using NUnit.Framework;
using UnityEngine;

namespace MendingtheSky.Tests
{
    /// <summary>
    /// 轻量本地化（设计评审第七节）的单元测试：
    /// 取当前语言文案、带参数格式化、缺 key 回退、切换事件、PlayerPrefs 持久化。
    /// </summary>
    public class L10nTests
    {
        private const string PrefKey = "mts.language";

        private LocalizationDatabase db;
        private int languageChangedCount;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(PrefKey);

            db = ScriptableObject.CreateInstance<LocalizationDatabase>();
            db.defaultLanguage = SystemLanguage.ChineseSimplified;
            db.tables.Clear();
            AddTable(SystemLanguage.ChineseSimplified, "暂停", "第 {0} 关");
            AddTable(SystemLanguage.ChineseTraditional, "暫停", "第 {0} 關");
            AddTable(SystemLanguage.English, "PAUSED", "LV {0}");

            languageChangedCount = 0;
            L10n.OnLanguageChanged += CountLanguageChanged;
            L10n.Init(db);
        }

        [TearDown]
        public void TearDown()
        {
            L10n.OnLanguageChanged -= CountLanguageChanged;
            PlayerPrefs.DeleteKey(PrefKey);
            if (db != null)
            {
                Object.DestroyImmediate(db);
            }
        }

        private void CountLanguageChanged()
        {
            languageChangedCount++;
        }

        private void AddTable(SystemLanguage language, string paused, string levelTemplate)
        {
            var table = new LanguageTable { language = language };
            table.entries.Add(new StringEntry { key = "ui.paused", value = paused });
            table.entries.Add(new StringEntry { key = "hud.level", value = levelTemplate });
            db.tables.Add(table);
        }

        [Test]
        public void Init_RaisesLanguageChanged()
        {
            Assert.GreaterOrEqual(languageChangedCount, 1, "Init 后应广播一次语言事件，让 UI 首次刷新");
        }

        [Test]
        public void Get_ReturnsTextOfCurrentLanguage()
        {
            L10n.SetLanguage(SystemLanguage.ChineseTraditional);
            Assert.AreEqual("暫停", L10n.Get("ui.paused"));

            L10n.SetLanguage(SystemLanguage.English);
            Assert.AreEqual("PAUSED", L10n.Get("ui.paused"));
        }

        [Test]
        public void Get_WithArgs_FormatsTemplate()
        {
            L10n.SetLanguage(SystemLanguage.ChineseSimplified);
            Assert.AreEqual("第 3 关", L10n.Get("hud.level", 3));

            L10n.SetLanguage(SystemLanguage.English);
            Assert.AreEqual("LV 5", L10n.Get("hud.level", 5));
        }

        [Test]
        public void MissingKey_FallsBackToDefaultLanguage()
        {
            // 只有简中表里有这个 key，切到繁中后应回退到默认语言（简中）的值。
            // 注意：L10n.Init 会把表拷进字典缓存，所以改完数据要重新 Init 一次。
            db.GetTable(SystemLanguage.ChineseSimplified).entries.Add(
                new StringEntry { key = "ui.cn_only", value = "只有简中" });
            L10n.Init(db);

            L10n.SetLanguage(SystemLanguage.ChineseTraditional);
            Assert.AreEqual("只有简中", L10n.Get("ui.cn_only"));
        }

        [Test]
        public void MissingKeyEverywhere_ReturnsKey()
        {
            L10n.SetLanguage(SystemLanguage.English);
            Assert.AreEqual("ui.not_exists", L10n.Get("ui.not_exists"),
                "任何语言都没有时返回 key 本身，方便在界面上发现漏翻");
        }

        [Test]
        public void SetLanguage_RaisesEventAndPersists()
        {
            int before = languageChangedCount;
            L10n.SetLanguage(SystemLanguage.English);

            Assert.Greater(languageChangedCount, before, "切换语言应触发 OnLanguageChanged");
            Assert.AreEqual((int)SystemLanguage.English, PlayerPrefs.GetInt(PrefKey, -1),
                "语言选择应写入 PlayerPrefs");
        }

        [Test]
        public void CycleLanguage_GoesSimpleThenTraditionalThenEnglish()
        {
            L10n.SetLanguage(SystemLanguage.ChineseSimplified);
            Assert.AreEqual(SystemLanguage.ChineseTraditional, L10n.CycleLanguage());
            Assert.AreEqual(SystemLanguage.English, L10n.CycleLanguage());
            Assert.AreEqual(SystemLanguage.ChineseSimplified, L10n.CycleLanguage());
        }

        [Test]
        public void SavedLanguage_TakesPrecedenceOverSystemDetection()
        {
            L10n.SetLanguage(SystemLanguage.ChineseTraditional);
            L10n.Init(db); // 模拟重启：应读取 PlayerPrefs 而不是系统语言
            Assert.AreEqual(SystemLanguage.ChineseTraditional, L10n.GetCurrentLanguage());
            Assert.AreEqual("暫停", L10n.Get("ui.paused"));
        }
    }
}
