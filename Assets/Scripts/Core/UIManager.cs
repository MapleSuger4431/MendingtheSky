using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MendingtheSky
{
    /// <summary>
    /// UI 管理器（阶段 4 范围）：HUD（灵气条 / 灵气数值 / 关卡名）与游戏内面板（暂停 / 失败 / 胜利）。
    ///
    /// 职责：
    /// - 监听 QiManager 的事件刷新灵气条与数值；
    /// - 低灵气时灵气条变红并做透明度闪烁（使用 unscaledDeltaTime，暂停时也能闪）；
    /// - ESC 切换暂停（移动端后续换成屏幕按钮，见设计文档 2.1）；
    /// - 绑定暂停面板的三个按钮（继续 / 重开 / 返回）。
    ///
    /// 说明：本阶段 UI 文本使用英文/数字（TMP 默认字体），
    /// 中文字体与多语言切换按设计评审第七节在本地化步骤接入。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("HUD（游戏中常驻）")]
        [SerializeField] private GameObject hudPanel;
        [Tooltip("灵气条填充图（Image.fillAmount 驱动）")]
        [SerializeField] private Image qiBarFill;
        [Tooltip("灵气数值文本，如 87 / 100")]
        [SerializeField] private TextMeshProUGUI qiText;
        [SerializeField] private TextMeshProUGUI levelNameText;
        [Tooltip("临时提示文本（如锅已满）")]
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("灵气条颜色")]
        [SerializeField] private Color normalQiColor = new Color(0.35f, 0.85f, 0.75f, 1f);
        [SerializeField] private Color warningQiColor = new Color(1f, 0.32f, 0.32f, 1f);
        [Tooltip("低灵气闪烁速度")]
        [SerializeField] private float warningBlinkSpeed = 2f;

        [Header("面板")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject losePanel;
        [SerializeField] private GameObject winPanel;
        [Tooltip("失败面板结算文本")]
        [SerializeField] private TextMeshProUGUI loseText;
        [Tooltip("胜利面板结算文本")]
        [SerializeField] private TextMeshProUGUI winText;

        [Header("暂停面板按钮")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button backToIdleButton;
        [Tooltip("失败面板重试按钮")]
        [SerializeField] private Button loseRetryButton;
        [Tooltip("胜利面板重试按钮（原型与重开相同）")]
        [SerializeField] private Button winRetryButton;

        [Header("设置面板")]
        [Tooltip("切换「放置预览高亮」的按钮（暂停面板内）")]
        [SerializeField] private Button previewToggleButton;
        [Tooltip("显示当前预览开关状态的文本")]
        [SerializeField] private TextMeshProUGUI previewToggleText;

        [Header("引用")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private QiManager qiManager;
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

        private bool isWarning;
        private float blinkTimer;

        private void Awake()
        {
            BindButtons();
        }

        private void Start()
        {
            if (qiManager != null)
            {
                qiManager.OnQiChanged += UpdateQi;
                qiManager.OnQiWarning += OnQiWarning;
            }
            GameSettings.OnChanged += RefreshSettingsLabels;
            RefreshSettingsLabels();
            HidePause();
            HideResult();
        }

        private void OnDestroy()
        {
            if (qiManager != null)
            {
                qiManager.OnQiChanged -= UpdateQi;
                qiManager.OnQiWarning -= OnQiWarning;
            }
            GameSettings.OnChanged -= RefreshSettingsLabels;
        }

        private void Update()
        {
            // ESC 切换暂停：只在 Playing / Paused 两个状态之间切换
            if (Input.GetKeyDown(pauseKey) && gameManager != null)
            {
                GameState state = gameManager.GetState();
                if (state == GameState.Playing)
                {
                    gameManager.PauseGame();
                }
                else if (state == GameState.Paused)
                {
                    gameManager.ResumeGame();
                }
            }

            // 低灵气闪烁：只改灵气条颜色透明度（暂停时仍闪，用 unscaled 时间）
            if (isWarning && qiBarFill != null)
            {
                blinkTimer += Time.unscaledDeltaTime;
                float wave = Mathf.PingPong(blinkTimer * warningBlinkSpeed, 1f);
                Color color = warningQiColor;
                color.a = Mathf.Lerp(0.45f, 1f, wave);
                qiBarFill.color = color;
            }
        }

        /// <summary>显示 HUD（关卡开始时调用），并把灵气条刷新到当前值。</summary>
        public void ShowHUD(LevelConfig levelConfig)
        {
            if (hudPanel != null)
            {
                hudPanel.SetActive(true);
            }
            HidePause();
            HideResult();
            if (levelNameText != null)
            {
                levelNameText.text = $"LV {levelConfig.GetLevelId()}";
            }
            if (statusText != null)
            {
                statusText.text = string.Empty;
            }
            if (qiManager != null)
            {
                UpdateQi(qiManager.GetCurrentQi(), qiManager.GetMaxQi());
            }
        }

        public void HideHUD()
        {
            if (hudPanel != null)
            {
                hudPanel.SetActive(false);
            }
        }

        public void ShowPause()
        {
            if (pausePanel != null)
            {
                pausePanel.SetActive(true);
            }
        }

        public void HidePause()
        {
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }
        }

        /// <summary>失败结算：显示已填洞数与总洞数。</summary>
        public void ShowLose(int filledHoles, int totalHoles)
        {
            HidePause();
            if (losePanel != null)
            {
                losePanel.SetActive(true);
            }
            if (loseText != null)
            {
                loseText.text = $"Holes {filledHoles} / {totalHoles}";
            }
        }

        /// <summary>胜利结算（阶段 5 洞闭合后触发）。</summary>
        public void ShowWin(int filledHoles, int totalHoles)
        {
            HidePause();
            if (winPanel != null)
            {
                winPanel.SetActive(true);
            }
            if (winText != null)
            {
                winText.text = $"Holes {filledHoles} / {totalHoles}";
            }
        }

        public void HideResult()
        {
            if (losePanel != null)
            {
                losePanel.SetActive(false);
            }
            if (winPanel != null)
            {
                winPanel.SetActive(false);
            }
        }

        public void HideAllPanels()
        {
            HidePause();
            HideResult();
        }

        /// <summary>
        /// 刷新灵气条与数值。fillAmount 需要灵气条贴图是「满格」状态（设计文档 5.20）。
        /// </summary>
        public void UpdateQi(float current, float max)
        {
            if (qiBarFill != null)
            {
                qiBarFill.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
                if (!isWarning)
                {
                    qiBarFill.color = normalQiColor;
                }
            }
            if (qiText != null)
            {
                qiText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
        }

        /// <summary>进入/解除低灵气警告：切换颜色并重置闪烁计时。</summary>
        public void OnQiWarning(bool warning)
        {
            isWarning = warning;
            blinkTimer = 0f;
            if (qiBarFill != null)
            {
                qiBarFill.color = warning ? warningQiColor : normalQiColor;
            }
        }

        public void ShowStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        /// <summary>暂停面板按钮与设置按钮绑定（继续 / 重开 / 返回 / 预览高亮开关）。</summary>
        private void BindButtons()
        {
            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(() => gameManager?.ResumeGame());
            }
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(() => gameManager?.RestartLevel());
            }
            if (backToIdleButton != null)
            {
                backToIdleButton.onClick.AddListener(() => gameManager?.BackToIdle());
            }
            if (loseRetryButton != null)
            {
                loseRetryButton.onClick.AddListener(() => gameManager?.RestartLevel());
            }
            if (winRetryButton != null)
            {
                winRetryButton.onClick.AddListener(() => gameManager?.RestartLevel());
            }
            if (previewToggleButton != null)
            {
                previewToggleButton.onClick.AddListener(() =>
                {
                    GameSettings.TogglePlacementPreview();
                    RefreshSettingsLabels();
                });
            }
        }

        /// <summary>
        /// 刷新设置面板文本（预览高亮 ON/OFF）。
        /// 说明：中文字体接入后改成 L10n key，见设计评审第七节。
        /// </summary>
        public void RefreshSettingsLabels()
        {
            if (previewToggleText != null)
            {
                previewToggleText.text = GameSettings.ShowPlacementPreview ? "Preview: ON" : "Preview: OFF";
            }
        }
    }
}
