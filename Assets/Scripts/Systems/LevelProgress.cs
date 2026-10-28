using System;
using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 关卡进度存档（PlayerPrefs）：是否通关 + 星级，以及「顺序解锁」判定。
    ///
    /// 规则（2026-10-28 用户定稿）：
    /// - 星级按通关时剩余灵气比例：≥50% 三星、≥25% 两星、通关但更低一星；
    /// - 星级只升不降，重复通关不会把记录刷低；
    /// - 顺序解锁：第 0 关永远可玩，第 N 关需要第 N-1 关已通关（无尽关不受限制）。
    /// </summary>
    public static class LevelProgress
    {
        private const string ClearedPrefix = "progress.";
        private const string ClearedSuffix = ".cleared";
        private const string StarsSuffix = ".stars";

        /// <summary>进度变化事件：选关面板据此刷新按钮状态。</summary>
        public static event Action OnChanged;

        private static readonly Dictionary<int, int> clearedCache = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> starsCache = new Dictionary<int, int>();

        /// <summary>某关是否已通关。</summary>
        public static bool IsCleared(int levelId)
        {
            return GetInt(clearedCache, ClearedPrefix + levelId + ClearedSuffix) == 1;
        }

        /// <summary>某关的历史最高星级（0 表示还没通关）。</summary>
        public static int GetStars(int levelId)
        {
            return Mathf.Clamp(GetInt(starsCache, ClearedPrefix + levelId + StarsSuffix), 0, 3);
        }

        /// <summary>
        /// 记录一次通关。星级只升不降；同一关卡重复通关不会覆盖更高的记录。
        /// </summary>
        public static void Report(int levelId, int stars)
        {
            int clamped = Mathf.Clamp(stars, 1, 3);
            bool changed = false;

            if (!IsCleared(levelId))
            {
                clearedCache[levelId] = 1;
                PlayerPrefs.SetInt(ClearedPrefix + levelId + ClearedSuffix, 1);
                changed = true;
            }
            if (clamped > GetStars(levelId))
            {
                starsCache[levelId] = clamped;
                PlayerPrefs.SetInt(ClearedPrefix + levelId + StarsSuffix, clamped);
                changed = true;
            }
            if (!changed)
            {
                return;
            }
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        /// <summary>
        /// 第 index 关是否解锁：第 0 关永远可玩；无尽关永远可玩；
        /// 其余关卡要求前一关（普通关序列）已通关。
        /// </summary>
        public static bool IsUnlocked(LevelDatabase database, int index)
        {
            if (database == null)
            {
                return index == 0;
            }
            LevelConfig config = database.GetLevel(index);
            if (config != null && config.IsEndless())
            {
                return true;
            }
            if (index <= 0)
            {
                return true;
            }
            LevelConfig previous = database.GetLevel(index - 1);
            if (previous == null)
            {
                return true;
            }
            return IsCleared(previous.GetLevelId());
        }

        /// <summary>星级显示：★★☆ 这种三格形式（未通关返回空串）。</summary>
        public static string FormatStars(int levelId)
        {
            int stars = GetStars(levelId);
            if (stars <= 0)
            {
                return string.Empty;
            }
            return new string('★', stars) + new string('☆', 3 - stars);
        }

        /// <summary>清空所有进度（调试用）。</summary>
        public static void ResetAll()
        {
            foreach (int id in new List<int>(clearedCache.Keys))
            {
                PlayerPrefs.DeleteKey(ClearedPrefix + id + ClearedSuffix);
                PlayerPrefs.DeleteKey(ClearedPrefix + id + StarsSuffix);
            }
            // 缓存里没有记录的关卡也一并清掉（最多扫到 999 关，开销可忽略）
            for (int id = 0; id < 1000; id++)
            {
                PlayerPrefs.DeleteKey(ClearedPrefix + id + ClearedSuffix);
                PlayerPrefs.DeleteKey(ClearedPrefix + id + StarsSuffix);
            }
            clearedCache.Clear();
            starsCache.Clear();
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        private static int GetInt(Dictionary<int, int> cache, string key)
        {
            int levelId = ParseLevelId(key);
            if (cache.TryGetValue(levelId, out int cached))
            {
                return cached;
            }
            int value = PlayerPrefs.GetInt(key, 0);
            cache[levelId] = value;
            return value;
        }

        private static int ParseLevelId(string key)
        {
            // key 形如 progress.<id>.cleared / progress.<id>.stars
            int start = ClearedPrefix.Length;
            int end = key.LastIndexOf('.');
            if (end <= start)
            {
                return -1;
            }
            return int.TryParse(key.Substring(start, end - start), out int id) ? id : -1;
        }
    }
}
