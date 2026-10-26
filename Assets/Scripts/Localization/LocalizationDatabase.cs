using System;
using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 一条本地化文案：key 用英文蛇形命名（如 ui.pause / hud.level），value 是对应语言的文本。
    /// </summary>
    [Serializable]
    public class StringEntry
    {
        public string key;
        public string value;
    }

    /// <summary>一种语言的文案表（设计评审第七节：简中 / 繁中 / 英文各自独立，不做简繁自动转换）。</summary>
    [Serializable]
    public class LanguageTable
    {
        public SystemLanguage language = SystemLanguage.English;
        public List<StringEntry> entries = new List<StringEntry>();
    }

    /// <summary>
    /// 本地化数据库（ScriptableObject）：Inspector 里直接维护三种语言的 key-value。
    /// 运行时由 L10n.Init 载入并建立字典缓存；改文案不需要改代码。
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizationDatabase", menuName = "MendingtheSky/Localization Database")]
    public class LocalizationDatabase : ScriptableObject
    {
        [Tooltip("找不到系统语言对应表、或玩家没手动选过时使用的语言")]
        public SystemLanguage defaultLanguage = SystemLanguage.ChineseSimplified;

        public List<LanguageTable> tables = new List<LanguageTable>();

        /// <summary>取某种语言的表；没有则返回 null。</summary>
        public LanguageTable GetTable(SystemLanguage language)
        {
            foreach (LanguageTable table in tables)
            {
                if (table != null && table.language == language)
                {
                    return table;
                }
            }
            return null;
        }
    }
}
