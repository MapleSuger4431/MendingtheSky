using System;
using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 轻量本地化（设计评审第七节，不用 Unity 官方 Localization 包）：
    /// - Init(db) 把每种语言的 key→value 建成字典；
    /// - Get(key) / Get(key, args) 取当前语言文案，缺失时回退 简中 → 英文 → key 本身（并打警告）；
    /// - SetLanguage 切换语言并写 PlayerPrefs，同时触发 OnLanguageChanged，让所有 UI 文本刷新；
    /// - 首次启动按 Application.systemLanguage 检测，玩家手动切换过则优先 PlayerPrefs。
    /// </summary>
    public static class L10n
    {
        private const string LanguagePrefKey = "mts.language";

        private static readonly Dictionary<SystemLanguage, Dictionary<string, string>> tableCache =
            new Dictionary<SystemLanguage, Dictionary<string, string>>();

        private static SystemLanguage currentLanguage = SystemLanguage.ChineseSimplified;
        private static SystemLanguage fallbackLanguage = SystemLanguage.ChineseSimplified;

        /// <summary>语言变化时触发，所有挂在场景里的 LocalizedText 与 UIManager 据此刷新。</summary>
        public static event Action OnLanguageChanged;

        /// <summary>由 GameManager.Awake 调用（早于所有 OnEnable，保证首次显示就是正确语言）。</summary>
        public static void Init(LocalizationDatabase database)
        {
            tableCache.Clear();
            if (database == null)
            {
                Debug.LogWarning("[L10n] 未配置 LocalizationDatabase，界面会直接显示 key");
                fallbackLanguage = SystemLanguage.ChineseSimplified;
                currentLanguage = DetectSystemLanguage();
                OnLanguageChanged?.Invoke();
                return;
            }

            foreach (LanguageTable table in database.tables)
            {
                if (table == null)
                {
                    continue;
                }
                var map = new Dictionary<string, string>();
                foreach (StringEntry entry in table.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.key))
                    {
                        continue;
                    }
                    map[entry.key] = entry.value;
                }
                tableCache[table.language] = map;
            }

            fallbackLanguage = database.defaultLanguage;
            currentLanguage = ResolveInitialLanguage(database);
            OnLanguageChanged?.Invoke();
        }

        /// <summary>取当前语言文案；缺失时按 当前 → 默认 → 英文 → key 的顺序回退。</summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }
            if (TryGet(currentLanguage, key, out string value))
            {
                return value;
            }
            if (TryGet(fallbackLanguage, key, out value))
            {
                return value;
            }
            if (TryGet(SystemLanguage.English, key, out value))
            {
                return value;
            }
            Debug.LogWarning($"[L10n] 缺少 key：{key}");
            return key;
        }

        /// <summary>带参数的文案，例如 ui.holes_progress = "已补缺口 {0} / {1}"。</summary>
        public static string Get(string key, params object[] args)
        {
            string template = Get(key);
            if (args == null || args.Length == 0)
            {
                return template;
            }
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                Debug.LogWarning($"[L10n] 文案占位符与参数不匹配：{key} = {template}");
                return template;
            }
        }

        public static void SetLanguage(SystemLanguage language)
        {
            currentLanguage = language;
            PlayerPrefs.SetInt(LanguagePrefKey, (int)language);
            PlayerPrefs.Save();
            OnLanguageChanged?.Invoke();
        }

        public static SystemLanguage GetCurrentLanguage()
        {
            return currentLanguage;
        }

        /// <summary>暂停面板按钮用：简体 → 繁體 → English → 简体。</summary>
        public static SystemLanguage CycleLanguage()
        {
            SystemLanguage next;
            switch (currentLanguage)
            {
                case SystemLanguage.ChineseSimplified:
                    next = SystemLanguage.ChineseTraditional;
                    break;
                case SystemLanguage.ChineseTraditional:
                    next = SystemLanguage.English;
                    break;
                default:
                    next = SystemLanguage.ChineseSimplified;
                    break;
            }
            SetLanguage(next);
            return next;
        }

        /// <summary>语言自称（三种语言下都显示自己的名字），用于「语言：简体中文」。</summary>
        public static string GetLanguageDisplayName(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.ChineseTraditional:
                    return Get("lang.zh_tw");
                case SystemLanguage.English:
                    return Get("lang.en");
                default:
                    return Get("lang.zh_cn");
            }
        }

        private static bool TryGet(SystemLanguage language, string key, out string value)
        {
            if (tableCache.TryGetValue(language, out Dictionary<string, string> map)
                && map.TryGetValue(key, out value))
            {
                return true;
            }
            value = null;
            return false;
        }

        /// <summary>优先用玩家上次手动选择的语言；否则按系统语言检测。</summary>
        private static SystemLanguage ResolveInitialLanguage(LocalizationDatabase database)
        {
            if (PlayerPrefs.HasKey(LanguagePrefKey))
            {
                var saved = (SystemLanguage)PlayerPrefs.GetInt(LanguagePrefKey);
                if (tableCache.ContainsKey(saved))
                {
                    return saved;
                }
            }
            SystemLanguage detected = DetectSystemLanguage();
            return tableCache.ContainsKey(detected) ? detected : database.defaultLanguage;
        }

        /// <summary>系统语言 → 本作支持的三语言（zh-TW / zh-HK 归为繁中，其余非中文归英文）。</summary>
        private static SystemLanguage DetectSystemLanguage()
        {
            SystemLanguage system = Application.systemLanguage;
            if (system == SystemLanguage.ChineseTraditional)
            {
                return SystemLanguage.ChineseTraditional;
            }
            if (system == SystemLanguage.ChineseSimplified || system == SystemLanguage.Chinese)
            {
                return SystemLanguage.ChineseSimplified;
            }
            return SystemLanguage.English;
        }
    }
}
