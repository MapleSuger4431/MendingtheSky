using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 无尽模式难度曲线（2026-10-27 定稿：温和爬升 + 各项封顶）。
    ///
    /// 三个维度都随「已补洞数」线性爬升，并各自封顶，保证后期不会无限变态：
    /// - 灵气消耗：每补 1 洞 ×(1 + costIncreasePerHole)，最高 maxCostMultiplier 倍；
    /// - 洞的格数：每 cellsTierHoles 洞 +cellsPerTier 格，最多 +maxCellBonus 格；
    /// - 补洞间隔：每 intervalTierHoles 洞 ×(1 - intervalReducePerTier)，最低 minIntervalMultiplier 倍。
    ///
    /// 只对 isEndless 的关卡生效，普通关保持固定参数。
    /// </summary>
    [System.Serializable]
    public class EndlessDifficulty
    {
        [Header("灵气消耗")]
        [Tooltip("每补 1 个洞，灵气消耗速率乘以 (1 + 该值)。0.02 = 每洞 +2%")]
        public float costIncreasePerHole = 0.02f;
        [Tooltip("灵气消耗倍率上限（封顶后不再变难）")]
        public float maxCostMultiplier = 3f;

        [Header("洞口大小")]
        [Tooltip("每多少个洞，洞的格数上限 +cellsPerTier")]
        public int cellsTierHoles = 5;
        [Tooltip("每个难度档增加的格数")]
        public int cellsPerTier = 1;
        [Tooltip("洞格数的最大额外增量（封顶）")]
        public int maxCellBonus = 15;

        [Header("补洞间隔")]
        [Tooltip("每多少个洞，间隔缩短一档")]
        public int intervalTierHoles = 5;
        [Range(0f, 0.5f)]
        [Tooltip("每档缩短比例。0.08 = 每档 -8%；0 = 间隔不变（儿童向默认）")]
        public float intervalReducePerTier;
        [Range(0.1f, 1f)]
        [Tooltip("间隔倍率下限（封顶）")]
        public float minIntervalMultiplier = 0.6f;

        /// <summary>灵气消耗倍率（1 起步，封顶 maxCostMultiplier）。</summary>
        public float GetCostMultiplier(int clearedHoles)
        {
            if (clearedHoles <= 0)
            {
                return 1f;
            }
            float raw = 1f + costIncreasePerHole * clearedHoles;
            return Mathf.Min(raw, Mathf.Max(1f, maxCostMultiplier));
        }

        /// <summary>洞格数的额外增量（0 起步，封顶 maxCellBonus）。</summary>
        public int GetCellBonus(int clearedHoles)
        {
            if (clearedHoles <= 0 || cellsTierHoles <= 0 || cellsPerTier <= 0)
            {
                return 0;
            }
            int tiers = clearedHoles / cellsTierHoles;
            return Mathf.Min(tiers * cellsPerTier, Mathf.Max(0, maxCellBonus));
        }

        /// <summary>补洞间隔倍率（1 起步向下，封底 minIntervalMultiplier）。</summary>
        public float GetIntervalMultiplier(int clearedHoles)
        {
            if (clearedHoles <= 0 || intervalTierHoles <= 0 || intervalReducePerTier <= 0f)
            {
                return 1f;
            }
            int tiers = clearedHoles / intervalTierHoles;
            float raw = 1f - intervalReducePerTier * tiers;
            return Mathf.Max(raw, Mathf.Clamp(minIntervalMultiplier, 0.1f, 1f));
        }
    }
}
