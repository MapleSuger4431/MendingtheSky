using System;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 灵气系统（设计文档 5.16 + 评审 P0-5 定稿）。
    ///
    /// 规则：
    /// - maxQi = 关卡配置的 initialQi，返还时用 Mathf.Min 限制不溢出；
    /// - 每帧消耗 = 「grid == 0 的空缺格数量」× qiPerCellPerSecond × dt
    ///   （已填格 grid==1 与闭合中 grid==2 都不扣，符合"堵住就不漏气"的直觉）；
    /// - currentQi 归零即失败：夹到 0、置 isEmpty、只触发一次 OnQiEmpty；
    /// - 比例 ≤ 0.2 触发警告（变红闪烁），回到阈值以上解除。
    ///
    /// 驱动方式：由 GameManager 在 Playing 状态下每帧调用 Tick(dt)，
    /// 不在此类里使用 Unity 的 Update()，保证暂停时不会误扣。
    /// </summary>
    public class QiManager : MonoBehaviour
    {
        [Header("引用（可由 GameManager.Init 注入）")]
        [SerializeField] private GridManager gridManager;

        [Header("运行时数值（调试面板只读）")]
        [SerializeField] private float currentQi;
        [SerializeField] private float maxQi = 100f;
        [SerializeField] private float qiPerCellPerSecond = 0.1f;
        [Tooltip("无尽模式难度：灵气消耗倍率，1 = 关卡原始速率")]
        [SerializeField] private float difficultyMultiplier = 1f;
        [SerializeField] private bool isWarning;
        [SerializeField] private bool isEmpty;

        [Header("调试（运行时可关闭消耗，便于测试）")]
        [SerializeField] private bool consumptionEnabled = true;

        /// <summary>低灵气警告阈值：写死 0.2（设计决策：原型阶段不做配置）。</summary>
        public const float WarningRatio = 0.2f;

        /// <summary>灵气变化事件：参数为 (当前值, 上限)。UI 据此刷新灵气条。</summary>
        public event Action<float, float> OnQiChanged;

        /// <summary>灵气归零事件：GameManager 监听后切到 Lose。</summary>
        public event Action OnQiEmpty;

        /// <summary>警告状态变化事件：true=进入低灵气警告，false=解除。</summary>
        public event Action<bool> OnQiWarning;

        /// <summary>
        /// 关卡开始时初始化。maxQi 取 initialQi（不单独配置上限）。
        /// </summary>
        public void Init(LevelConfig levelConfig, GridManager grid)
        {
            gridManager = grid;
            maxQi = Mathf.Max(1f, levelConfig.GetInitialQi());
            currentQi = maxQi;
            qiPerCellPerSecond = levelConfig.GetQiPerCellPerSecond();
            difficultyMultiplier = 1f;
            isWarning = false;
            isEmpty = false;
            OnQiChanged?.Invoke(currentQi, maxQi);
            OnQiWarning?.Invoke(false);
            AudioManager.Instance?.StopWarningLoop();
        }

        /// <summary>
        /// 每帧消耗灵气（只由 GameManager 在 Playing 状态调用）。
        /// 消耗基数 = 所有未闭合洞中「仍是缺口」的格子数（不含已填与闭合中）。
        /// </summary>
        public void Tick(float dt)
        {
            // consumptionEnabled 为 false 时暂停扣费（调试用，便于从容测试）
            if (!consumptionEnabled || isEmpty || gridManager == null)
            {
                return;
            }
            int unfilledCells = gridManager.GetUnfilledCellCount();
            if (unfilledCells <= 0)
            {
                return;
            }
            // 无尽模式下 difficultyMultiplier 会随已补洞数增长（见 EndlessDifficulty）
            SpendQi(unfilledCells * qiPerCellPerSecond * difficultyMultiplier * dt);
        }

        /// <summary>
        /// 设置无尽模式的灵气消耗倍率（1 = 原始速率）。普通关不需要调用。
        /// </summary>
        public void SetDifficultyMultiplier(float multiplier)
        {
            difficultyMultiplier = Mathf.Max(0f, multiplier);
        }

        /// <summary>当前灵气消耗倍率（HUD 显示难度用）。</summary>
        public float GetDifficultyMultiplier() { return difficultyMultiplier; }

        /// <summary>
        /// 增加灵气（洞口闭合返还）。上限 clamp，不溢出；失败后不再变化。
        /// </summary>
        public void AddQi(float amount)
        {
            if (amount <= 0f || isEmpty)
            {
                return;
            }
            currentQi = Mathf.Min(currentQi + amount, maxQi);
            OnQiChanged?.Invoke(currentQi, maxQi);
            EvaluateWarning();
        }

        /// <summary>
        /// 扣减灵气。归零时夹到 0、只触发一次 OnQiEmpty，并保持 isEmpty。
        /// </summary>
        public void SpendQi(float amount)
        {
            if (amount <= 0f || isEmpty)
            {
                return;
            }
            currentQi = Mathf.Max(0f, currentQi - amount);
            OnQiChanged?.Invoke(currentQi, maxQi);
            if (currentQi <= 0f)
            {
                isEmpty = true;
                OnQiEmpty?.Invoke();
                return;
            }
            EvaluateWarning();
        }

        public void ResetQi()
        {
            currentQi = maxQi;
            isEmpty = false;
            isWarning = false;
            OnQiChanged?.Invoke(currentQi, maxQi);
            OnQiWarning?.Invoke(false);
            AudioManager.Instance?.StopWarningLoop();
        }

        public float GetCurrentQi() { return currentQi; }
        public float GetMaxQi() { return maxQi; }
        public float GetQiPerCellPerSecond() { return qiPerCellPerSecond; }
        public bool IsWarning() { return isWarning; }
        public bool IsEmpty() { return isEmpty; }

        /// <summary>灵气消耗是否开启（调试开关，由 DebugCheats 的 T 键切换）。</summary>
        public bool IsConsumptionEnabled() { return consumptionEnabled; }

        /// <summary>开启/关闭灵气消耗。关闭时 Tick 不再扣费，但返还/加灵气仍可用。</summary>
        public void SetConsumptionEnabled(bool enabled)
        {
            consumptionEnabled = enabled;
        }

        /// <summary>当前比例（0~1），供 UI 的 fillAmount 使用。</summary>
        public float GetRatio()
        {
            return maxQi > 0f ? Mathf.Clamp01(currentQi / maxQi) : 0f;
        }

        /// <summary>
        /// 根据当前比例更新警告状态，只在跨过阈值时触发事件，避免每帧刷 UI。
        /// </summary>
        private void EvaluateWarning()
        {
            bool shouldWarn = GetRatio() <= WarningRatio;
            if (shouldWarn != isWarning)
            {
                isWarning = shouldWarn;
                OnQiWarning?.Invoke(isWarning);
                // 告急音效：进入低灵气时循环播放，脱离后停止
                if (isWarning)
                {
                    AudioManager.Instance?.PlayWarningLoop();
                }
                else
                {
                    AudioManager.Instance?.StopWarningLoop();
                }
            }
        }
    }
}
