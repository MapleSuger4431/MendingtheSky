using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 无尽模式最高纪录（PlayerPrefs 持久化）。
    /// 分数定义：一局内补好的洞口数；灵气耗尽时由 GameManager 调用 Report 结算。
    /// </summary>
    public static class EndlessRecord
    {
        public const string PrefKey = "mts.endless_best";

        /// <summary>历史最高补洞数（没有纪录时为 0）。</summary>
        public static int GetBest()
        {
            return PlayerPrefs.GetInt(PrefKey, 0);
        }

        /// <summary>提交本局成绩，返回更新后的最高纪录（不会用更低分覆盖）。</summary>
        public static int Report(int filledHoles)
        {
            int best = GetBest();
            if (filledHoles > best)
            {
                best = filledHoles;
                PlayerPrefs.SetInt(PrefKey, best);
                PlayerPrefs.Save();
            }
            return best;
        }

        /// <summary>清空纪录（调试 / 测试用）。</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
        }
    }
}
