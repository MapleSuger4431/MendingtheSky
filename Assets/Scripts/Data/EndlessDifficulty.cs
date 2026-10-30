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

        [Header("锅容量（前期紧、后期放宽）")]
        [Tooltip("开局锅容量")]
        public int potStartCount = 2;
        [Tooltip("每补多少个洞，锅容量 +1")]
        public int potTierHoles = 6;
        [Tooltip("锅容量上限")]
        public int potMaxCount = 4;

        [Header("初始旋转概率")]
        [Range(0f, 1f)]
        [Tooltip("开局时多格灵石带随机朝向的概率")]
        public float rotationStartChance = 0.4f;
        [Tooltip("每补多少个洞提升一档")]
        public int rotationTierHoles = 5;
        [Range(0f, 0.2f)]
        [Tooltip("每档提升的概率")]
        public float rotationIncreasePerTier = 0.05f;
        [Range(0f, 1f)]
        [Tooltip("概率上限")]
        public float rotationMaxChance = 0.9f;

        [Header("预煮熟石头（前期给、后期收紧）")]
        [Range(0f, 1f)]
        [Tooltip("开局时每块新石直接是「已煮」的概率")]
        public float precookedStartChance = 0.4f;
        [Tooltip("每补多少个洞降低一档")]
        public int precookedTierHoles = 5;
        [Range(0f, 0.2f)]
        [Tooltip("每档降低的概率")]
        public float precookedReducePerTier = 0.1f;
        [Range(0f, 1f)]
        [Tooltip("概率下限（0 = 后期完全不预煮熟）")]
        public float precookedMinChance;

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

        /// <summary>
        /// 当前锅容量：开局 potStartCount，每 potTierHoles 洞 +1，封顶 potMaxCount。
        /// 与其它轴相反，这条是「放宽」——后期石头更难转、耗气更快，给玩家一个能同时多煮的补偿。
        /// </summary>
        public int GetPotCapacity(int clearedHoles)
        {
            int cap = Mathf.Max(1, potMaxCount);
            if (potTierHoles <= 0)
            {
                return Mathf.Clamp(potStartCount, 1, cap);
            }
            int tiers = Mathf.Max(0, clearedHoles) / potTierHoles;
            return Mathf.Clamp(potStartCount + tiers, 1, cap);
        }

        /// <summary>当前初始旋转概率（每 rotationTierHoles 洞提升一档，封顶 rotationMaxChance）。</summary>
        public float GetRotationChance(int clearedHoles)
        {
            float raw = rotationStartChance;
            if (clearedHoles > 0 && rotationTierHoles > 0 && rotationIncreasePerTier > 0f)
            {
                int tiers = clearedHoles / rotationTierHoles;
                raw += rotationIncreasePerTier * tiers;
            }
            return Mathf.Clamp(raw, 0f, Mathf.Max(rotationStartChance, rotationMaxChance));
        }

        /// <summary>
        /// 当前「新石直接煮熟」的概率：开局 precookedStartChance，每 precookedTierHoles 洞降一档，
        /// 降到 precookedMinChance 为止。作用在每次生成的石头上（开局与补给同一路径），
        /// 所以「前期给糖、后期收紧」在单局内是真的会变化。
        /// </summary>
        public float GetPrecookedChance(int clearedHoles)
        {
            float raw = precookedStartChance;
            if (clearedHoles > 0 && precookedTierHoles > 0 && precookedReducePerTier > 0f)
            {
                int tiers = clearedHoles / precookedTierHoles;
                raw -= precookedReducePerTier * tiers;
            }
            return Mathf.Clamp(raw, Mathf.Max(0f, precookedMinChance), 1f);
        }
    }
}
