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
    /// - 绑定暂停面板的按钮（继续 / 重开 / 返回 / 预览开关 / 语言切换）。
    ///
    /// 文本：静态文案由场景里的 LocalizedText 组件负责；带参数的（关卡号、结算、语言名、预览开关）
    /// 由本类用 L10n.Get(key, args) 生成，并在 OnLanguageChanged 时统一刷新。
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

        [Header("语言切换（暂停面板）")]
        [Tooltip("循环切换 简体 → 繁體 → English 的按钮")]
        [SerializeField] private Button languageButton;
        [Tooltip("显示当前语言的文本，如「语言：简体中文」")]
        [SerializeField] private TextMeshProUGUI languageButtonText;

        [Header("主界面")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private Button menuStartButton;
        [SerializeField] private Button menuEndlessButton;
        [SerializeField] private Button menuLevelSelectButton;
        [Tooltip("主界面「设置」按钮：打开设置面板")]
        [SerializeField] private Button menuSettingsButton;

        [Header("设置面板")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TextMeshProUGUI musicValueText;
        [SerializeField] private TextMeshProUGUI sfxValueText;
        [SerializeField] private Button settingsLanguageButton;
        [SerializeField] private TextMeshProUGUI settingsLanguageText;
        [SerializeField] private Button settingsKeybindButton;
        [Tooltip("键位等未开放功能的临时提示文本")]
        [SerializeField] private TextMeshProUGUI settingsHintText;
        [SerializeField] private Button settingsBackButton;

        [Header("关卡选择")]
        [SerializeField] private GameObject levelSelectPanel;
        [Tooltip("关卡按钮的父物体（建议挂 VerticalLayoutGroup）")]
        [SerializeField] private Transform levelSelectContent;
        [Tooltip("关卡按钮模板：会被隐藏，按数据库条目复制")]
        [SerializeField] private Button levelSelectTemplate;
        [SerializeField] private Button levelSelectBackButton;
        [Tooltip("生成关卡列表用的数据库（与 GameManager 指向同一份）")]
        [SerializeField] private LevelDatabase levelDatabase;

        [Header("结算面板补充按钮")]
        [Tooltip("胜利面板主按钮的文本（下一关 / 再玩一次）")]
        [SerializeField] private TextMeshProUGUI winRetryButtonText;
        [Tooltip("胜利面板：返回主界面")]
        [SerializeField] private Button winMenuButton;
        [Tooltip("失败面板：返回主界面")]
        [SerializeField] private Button loseMenuButton;

        [Header("引用")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private QiManager qiManager;
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

        private bool isWarning;
        private float blinkTimer;
        private Coroutine hintRoutine;

        // 语言切换后需要重算的带参数文本的缓存值
        private int lastLevelId = 1;
        private bool lastIsEndless;
        private int lastFilledHoles;
        private int lastTotalHoles = 1;
        private int lastEndlessBest;

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
            L10n.OnLanguageChanged += RefreshLocalizedTexts;
            RefreshLocalizedTexts();
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
            L10n.OnLanguageChanged -= RefreshLocalizedTexts;
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
            // 进入关卡时关掉主界面/关卡选择/结算面板
            HideMenu();
            HideLevelSelect();
            HidePause();
            HideResult();
            lastLevelId = levelConfig.GetLevelId();
            lastIsEndless = levelConfig.IsEndless();
            lastFilledHoles = 0;
            if (levelNameText != null)
            {
                levelNameText.text = FormatLevelName();
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

        // ---------------- 主界面 / 关卡选择 ----------------

        /// <summary>显示主界面（启动时、以及从暂停/结算/设置返回时调用）。</summary>
        public void ShowMenu()
        {
            HideLevelSelect();
            HideSettings();
            HidePause();
            HideResult();
            if (menuPanel != null)
            {
                menuPanel.SetActive(true);
            }
        }

        public void HideMenu()
        {
            if (menuPanel != null)
            {
                menuPanel.SetActive(false);
            }
        }

        /// <summary>显示关卡选择：每次打开都按数据库重新生成按钮。</summary>
        public void ShowLevelSelect()
        {
            HideMenu();
            PopulateLevelSelect();
            if (levelSelectPanel != null)
            {
                levelSelectPanel.SetActive(true);
            }
        }

        public void HideLevelSelect()
        {
            if (levelSelectPanel != null)
            {
                levelSelectPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 按 LevelDatabase 生成关卡按钮（模板复制）。普通关显示「第 N 关」，无尽关显示「无尽模式」。
        /// 每次调用会先清掉上一次生成的按钮，只保留模板本身。
        /// </summary>
        public void PopulateLevelSelect()
        {
            if (levelSelectContent == null || levelSelectTemplate == null || levelDatabase == null)
            {
                return;
            }
            for (int i = levelSelectContent.childCount - 1; i >= 0; i--)
            {
                Transform child = levelSelectContent.GetChild(i);
                if (child == levelSelectTemplate.transform)
                {
                    continue;
                }
                // 先隐藏再 Destroy：Destroy 要到帧末才生效，不隐藏会和新按钮叠一帧
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            levelSelectTemplate.gameObject.SetActive(false);

            for (int i = 0; i < levelDatabase.GetTotalLevels(); i++)
            {
                LevelConfig config = levelDatabase.GetLevel(i);
                if (config == null)
                {
                    continue;
                }
                int index = i;
                Button button = Instantiate(levelSelectTemplate, levelSelectContent);
                button.gameObject.SetActive(true);
                button.name = $"LevelButton_{i}";
                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    // 关卡按钮文案由本方法生成（含关卡 id / 无尽标记），不挂 LocalizedText
                    LocalizedText existing = label.GetComponent<LocalizedText>();
                    if (existing != null)
                    {
                        Destroy(existing);
                    }
                    label.text = FormatLevelEntry(config);
                }
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    HideLevelSelect();
                    if (gameManager != null)
                    {
                        gameManager.StartLevelByIndex(index);
                    }
                });
            }
        }

        private static string FormatLevelEntry(LevelConfig config)
        {
            return config.IsEndless()
                ? L10n.Get("hud.endless")
                : L10n.Get("hud.level", config.GetLevelId());
        }

        // ---------------- 设置面板 ----------------

        /// <summary>打开设置面板（从主界面进入）。</summary>
        public void ShowSettings()
        {
            HideMenu();
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
            }
            RefreshSettingsPanel();
        }

        public void HideSettings()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }

        /// <summary>把当前设置值同步到面板控件（滑块、百分比、语言名）。</summary>
        public void RefreshSettingsPanel()
        {
            if (musicSlider != null)
            {
                musicSlider.SetValueWithoutNotify(Mathf.RoundToInt(GameSettings.MusicVolume * 10f));
            }
            if (sfxSlider != null)
            {
                sfxSlider.SetValueWithoutNotify(Mathf.RoundToInt(GameSettings.SfxVolume * 10f));
            }
            RefreshVolumeLabels();
            if (settingsLanguageText != null)
            {
                settingsLanguageText.text = L10n.GetLanguageDisplayName(L10n.GetCurrentLanguage());
            }
            if (settingsHintText != null)
            {
                settingsHintText.text = string.Empty;
            }
        }

        private void RefreshVolumeLabels()
        {
            if (musicValueText != null)
            {
                musicValueText.text = $"{Mathf.RoundToInt(GameSettings.MusicVolume * 100f)}%";
            }
            if (sfxValueText != null)
            {
                sfxValueText.text = $"{Mathf.RoundToInt(GameSettings.SfxVolume * 100f)}%";
            }
        }

        /// <summary>在设置面板底部显示一条临时提示（例如「键位修改稍后开放」）。</summary>
        private void ShowSettingsHint(string message)
        {
            if (settingsHintText == null)
            {
                return;
            }
            settingsHintText.text = message;
            if (hintRoutine != null)
            {
                StopCoroutine(hintRoutine);
            }
            hintRoutine = StartCoroutine(ClearHintAfter(2f));
        }

        private System.Collections.IEnumerator ClearHintAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (settingsHintText != null)
            {
                settingsHintText.text = string.Empty;
            }
            hintRoutine = null;
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
                lastFilledHoles = filledHoles;
                lastTotalHoles = totalHoles;
                loseText.text = L10n.Get("ui.holes_progress", filledHoles, totalHoles);
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
                lastFilledHoles = filledHoles;
                lastTotalHoles = totalHoles;
                winText.text = L10n.Get("ui.holes_progress", filledHoles, totalHoles);
            }
            if (winRetryButtonText != null)
            {
                // 有下一关显示「下一关」，最后一关显示「再玩一次」
                winRetryButtonText.text = L10n.Get(
                    gameManager != null && gameManager.HasNextLevel() ? "ui.next_level" : "ui.play_again");
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

        /// <summary>无尽模式：每补好一个洞刷新 HUD 上的「已补 N 洞」。</summary>
        public void UpdateEndlessProgress(int filledHoles)
        {
            lastFilledHoles = filledHoles;
            if (lastIsEndless && levelNameText != null)
            {
                levelNameText.text = FormatLevelName();
            }
        }

        /// <summary>无尽模式失败结算：本局补洞数与历史最高纪录。</summary>
        public void ShowLoseEndless(int filledHoles, int best)
        {
            HidePause();
            if (losePanel != null)
            {
                losePanel.SetActive(true);
            }
            lastFilledHoles = filledHoles;
            lastEndlessBest = best;
            if (loseText != null)
            {
                loseText.text = L10n.Get("ui.endless_result", filledHoles, best);
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

        /// <summary>所有按钮绑定：主界面、关卡选择、暂停、结算。</summary>
        private void BindButtons()
        {
            // 主界面
            if (menuStartButton != null)
            {
                menuStartButton.onClick.AddListener(() => gameManager?.StartGame());
            }
            if (menuEndlessButton != null)
            {
                menuEndlessButton.onClick.AddListener(() => gameManager?.StartEndless());
            }
            if (menuLevelSelectButton != null)
            {
                menuLevelSelectButton.onClick.AddListener(ShowLevelSelect);
            }
            if (menuSettingsButton != null)
            {
                menuSettingsButton.onClick.AddListener(ShowSettings);
            }

            // 设置面板
            if (settingsBackButton != null)
            {
                settingsBackButton.onClick.AddListener(() =>
                {
                    HideSettings();
                    ShowMenu();
                });
            }
            if (settingsLanguageButton != null)
            {
                settingsLanguageButton.onClick.AddListener(() => L10n.CycleLanguage());
            }
            if (settingsKeybindButton != null)
            {
                settingsKeybindButton.onClick.AddListener(() =>
                    ShowSettingsHint(L10n.Get("settings.keybind_todo")));
            }
            if (musicSlider != null)
            {
                musicSlider.minValue = 0f;
                musicSlider.maxValue = 10f;
                musicSlider.wholeNumbers = true;
                musicSlider.onValueChanged.AddListener(value =>
                {
                    GameSettings.MusicVolume = value / 10f;
                    RefreshVolumeLabels();
                });
            }
            if (sfxSlider != null)
            {
                sfxSlider.minValue = 0f;
                sfxSlider.maxValue = 10f;
                sfxSlider.wholeNumbers = true;
                sfxSlider.onValueChanged.AddListener(value =>
                {
                    GameSettings.SfxVolume = value / 10f;
                    RefreshVolumeLabels();
                });
            }
            if (levelSelectBackButton != null)
            {
                levelSelectBackButton.onClick.AddListener(() =>
                {
                    HideLevelSelect();
                    ShowMenu();
                });
            }

            // 暂停面板
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

            // 失败面板
            if (loseRetryButton != null)
            {
                loseRetryButton.onClick.AddListener(() => gameManager?.RestartLevel());
            }
            if (loseMenuButton != null)
            {
                loseMenuButton.onClick.AddListener(() => gameManager?.BackToIdle());
            }

            // 胜利面板：有下一关就进下一关，否则重开当前关
            if (winRetryButton != null)
            {
                winRetryButton.onClick.AddListener(() =>
                {
                    gameManager?.NextLevel();
                    HideResult();
                });
            }
            if (winMenuButton != null)
            {
                winMenuButton.onClick.AddListener(() => gameManager?.BackToIdle());
            }

            // 设置
            if (previewToggleButton != null)
            {
                previewToggleButton.onClick.AddListener(() =>
                {
                    GameSettings.TogglePlacementPreview();
                    RefreshSettingsLabels();
                });
            }
            if (languageButton != null)
            {
                // 循环 简体 → 繁體 → English，切换事件会触发 RefreshLocalizedTexts
                languageButton.onClick.AddListener(() => L10n.CycleLanguage());
            }
        }

        /// <summary>
        /// 刷新设置面板文本（预览高亮 ON/OFF）。
        /// </summary>
        public void RefreshSettingsLabels()
        {
            if (previewToggleText != null)
            {
                previewToggleText.text = L10n.Get(GameSettings.ShowPlacementPreview
                    ? "ui.preview_on"
                    : "ui.preview_off");
            }
        }

        /// <summary>
        /// 语言切换后统一刷新「由代码生成」的文本；场景里的静态文案由 LocalizedText 自行刷新。
        /// </summary>
        public void RefreshLocalizedTexts()
        {
            RefreshSettingsLabels();
            if (settingsLanguageText != null)
            {
                settingsLanguageText.text = L10n.GetLanguageDisplayName(L10n.GetCurrentLanguage());
            }
            if (levelNameText != null)
            {
                levelNameText.text = FormatLevelName();
            }
            string normalProgress = L10n.Get("ui.holes_progress", lastFilledHoles, lastTotalHoles);
            if (loseText != null)
            {
                loseText.text = lastIsEndless
                    ? L10n.Get("ui.endless_result", lastFilledHoles, lastEndlessBest)
                    : normalProgress;
            }
            if (winText != null)
            {
                winText.text = normalProgress;
            }
            if (winRetryButtonText != null)
            {
                winRetryButtonText.text = L10n.Get(
                    gameManager != null && gameManager.HasNextLevel() ? "ui.next_level" : "ui.play_again");
            }
            if (languageButtonText != null)
            {
                languageButtonText.text = L10n.Get("ui.language",
                    L10n.GetLanguageDisplayName(L10n.GetCurrentLanguage()));
            }
        }

        /// <summary>HUD 关卡名：普通关卡「第 N 关」，无尽模式「无尽模式 · 已补 N 洞」。</summary>
        private string FormatLevelName()
        {
            return lastIsEndless
                ? L10n.Get("hud.endless_progress", lastFilledHoles)
                : L10n.Get("hud.level", lastLevelId);
        }
    }
}
