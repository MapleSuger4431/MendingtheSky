using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 关卡配置。maxQi 不单独配置，等于 initialQi；qiWarningRatio 写死 0.2f。
    /// 创建方式：Project 窗口右键 → Create → MendingtheSky → Level Config。
    /// </summary>
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "MendingtheSky/Level Config")]
    public class LevelConfig : ScriptableObject
    {
        [Header("基础")]
        public int levelId;
        public string displayName;
        public bool isEndless;

        [Header("确定性：0=每次随机，非0=固定种子（测试/复现用）")]
        public int seed;

        [Header("网格参数（上半屏工作台）")]
        [Tooltip("网格行数范围：10-15")]
        public int minRows = 10;
        public int maxRows = 15;
        [Tooltip("列数由屏幕宽度自动计算以铺满上半屏；此处仅为安全钳制范围")]
        public int minCols = 30;
        public int maxCols = 70;
        [Tooltip("格子最大世界尺寸；运行时按上半屏高度自动收缩，保证 rows 行完整容纳")]
        public float cellSize = 0.6f;

        [Header("洞口参数")]
        [Tooltip("场上同时存在的未闭合洞上限")]
        public int holeCount = 3;
        [Tooltip("本关总共要填的洞数；无尽模式忽略")]
        public int totalHoles = 10;
        [Tooltip("单个洞的格子数下限：9")]
        public int minHoleSize = 9;
        [Tooltip("单个洞的格子数上限：30（= 5×6 满格）")]
        public int maxHoleSize = 30;
        [Tooltip("合并后洞的格子数上限（合并洞不受单洞包围盒限制）")]
        public int maxMergedHoleSize = 45;
        [Tooltip("单洞包围盒行数范围：3-5")]
        public int minHoleRows = 3;
        public int maxHoleRows = 5;
        [Tooltip("单洞包围盒列数范围：3-6")]
        public int minHoleCols = 3;
        public int maxHoleCols = 6;
        [Range(0f, 1f)]
        [Tooltip("修订版语义：本次蔓延「允许贴向未填洞」的概率；贴上了就必然合并")]
        public float mergeChance = 0.3f;
        public float holeSpawnInterval = 5f;

        [Header("灵气参数")]
        public float initialQi = 100f;
        public float qiPerCellPerSecond = 0.1f;

        [Header("灵石参数")]
        public int redundantCount = 2;
        public int redundantThreshold = 4;
        public int potMaxCount = 3;
        [Range(0f, 1f)]
        [Tooltip("初始灵石带随机朝向的概率（仅对旋转后形态不同的形状生效；0=全部正向，1=全部随机）")]
        public float initialRotationChance = 0.4f;

        [Header("灵石种类池（权重随机，阶段2接入）")]
        public LingShiTypeEntry[] lingShiTypePool;

        public int GetLevelId() { return levelId; }
        public string GetDisplayName() { return displayName; }
        public bool IsEndless() { return isEndless; }
        public int GetSeed() { return seed; }
        public int GetMinRows() { return minRows; }
        public int GetMaxRows() { return maxRows; }
        public int GetMinCols() { return minCols; }
        public int GetMaxCols() { return maxCols; }
        public float GetCellSize() { return cellSize; }
        public int GetHoleCount() { return holeCount; }
        public int GetTotalHoles() { return totalHoles; }
        public int GetMinHoleSize() { return minHoleSize; }
        public int GetMaxHoleSize() { return maxHoleSize; }
        public int GetMaxMergedHoleSize() { return maxMergedHoleSize; }
        public int GetMinHoleRows() { return minHoleRows; }
        public int GetMaxHoleRows() { return maxHoleRows; }
        public int GetMinHoleCols() { return minHoleCols; }
        public int GetMaxHoleCols() { return maxHoleCols; }
        public float GetMergeChance() { return mergeChance; }
        public float GetHoleSpawnInterval() { return holeSpawnInterval; }
        public float GetInitialQi() { return initialQi; }
        public float GetQiPerCellPerSecond() { return qiPerCellPerSecond; }
        public int GetRedundantCount() { return redundantCount; }
        public int GetRedundantThreshold() { return redundantThreshold; }
        public int GetPotMaxCount() { return potMaxCount; }
        public float GetInitialRotationChance() { return initialRotationChance; }
        public LingShiTypeEntry[] GetLingShiTypePool() { return lingShiTypePool; }
    }
}
