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
        [Tooltip("灵气条遮罩区域（RectMask2D）：宽度按灵气比例伸缩，露出的部分就是填充")]
        [SerializeField] private RectTransform qiBarFillArea;
        [Tooltip("灵气条填充图：固定满宽，始终不缩放，只被 qiBarFillArea 裁切")]
        [SerializeField] private Image qiBarFill;
        [Tooltip("满灵气时的遮罩宽度（像素）；<=0 表示首次刷新时按遮罩当前宽度自动记录")]
        [SerializeField] private float qiBarFillFullWidth;
        [Tooltip("灵气数值文本，如 87 / 100")]
        [SerializeField] private TextMeshProUGUI qiText;
        [SerializeField] private TextMeshProUGUI levelNameText;
        [Tooltip("临时提示文本（如锅已满）")]
        [SerializeField] private TextMeshProUGUI statusText;
        [Tooltip("HUD 右上角的暂停按钮（移动端没有 ESC 键）")]
        [SerializeField] private Button hudPauseButton;

        [Header("灵气条颜色")]
        [Tooltip("正常状态：白色 = 不改动美术原色")]
        [SerializeField] private Color normalQiColor = Color.white;
        [Tooltip("低灵气警告：整体染红（与美术底色相乘）")]
        [SerializeField] private Color warningQiColor = new Color(1f, 0.42f, 0.42f, 1f);
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
        [Tooltip("预览开关的图形（ui_toggle）：水平翻转表示开/关（美术把滑块画在左端）")]
        [SerializeField] private RectTransform previewToggleVisual;

        [Header("暂停面板")]
        [Tooltip("打开设置面板（语言切换已并入设置面板）")]
        [SerializeField] private Button pauseSettingsButton;

        [Header("主界面")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private Button menuStartButton;
        [SerializeField] private Button menuEndlessButton;
        [SerializeField] private Button menuLevelSelectButton;
        [Tooltip("主界面「设置」按钮：打开设置面板")]
        [SerializeField] private Button menuSettingsButton;
        [Tooltip("退出游戏（移动端平台不允许程序自杀式退出，启动时自动隐藏）")]
        [SerializeField] private Button menuQuitButton;
        [Tooltip("退出游戏二次确认弹窗")]
        [SerializeField] private GameObject quitConfirmPanel;
        [SerializeField] private Button quitCancelButton;
        [SerializeField] private Button quitConfirmButton;

        [Header("设置面板")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [Tooltip("音乐行的快捷静音按钮：点一下音乐静音，再点恢复（不改变滑块音量）")]
        [SerializeField] private Button musicMuteButton;
        [Tooltip("音乐静音按钮上的文字（静音 / 恢复）")]
        [SerializeField] private TextMeshProUGUI musicMuteText;
        [Tooltip("音效行的快捷静音按钮")]
        [SerializeField] private Button sfxMuteButton;
        [Tooltip("音效静音按钮上的文字")]
        [SerializeField] private TextMeshProUGUI sfxMuteText;
        [SerializeField] private TextMeshProUGUI musicValueText;
        [SerializeField] private TextMeshProUGUI sfxValueText;
        [SerializeField] private Button settingsLanguageButton;
        [SerializeField] private TextMeshProUGUI settingsLanguageText;
        [Tooltip("设置面板：旋转方式（按钮 / 手势 / 两者）循环切换")]
        [SerializeField] private Button settingsRotateButton;
        [Tooltip("旋转方式按钮上显示当前选择：按钮 / 手势 / 按钮 + 手势")]
        [SerializeField] private TextMeshProUGUI settingsRotateText;
        [SerializeField] private Button settingsKeybindButton;
        [Tooltip("键位等未开放功能的临时提示文本")]
        [SerializeField] private TextMeshProUGUI settingsHintText;
        [SerializeField] private Button settingsBackButton;

        [Header("键位设置面板")]
        [SerializeField] private GameObject keybindPanel;
        [SerializeField] private Button keybindRotateCwButton;
        [SerializeField] private TextMeshProUGUI keybindRotateCwText;
        [SerializeField] private Button keybindRotateCcwButton;
        [SerializeField] private TextMeshProUGUI keybindRotateCcwText;
        [SerializeField] private Button keybindResetButton;
        [SerializeField] private Button keybindBackButton;
        [SerializeField] private TextMeshProUGUI keybindHintText;

        /// <summary>正在等待玩家按下新键。</summary>
        private bool isListeningKey;
        /// <summary>true = 改顺时针键，false = 改逆时针键。</summary>
        private bool listeningClockwise;

        [Header("关卡选择")]
        [SerializeField] private GameObject levelSelectPanel;
        [Tooltip("日历格的父物体（不要挂 LayoutGroup，位置由代码按 4×2 计算）")]
        [SerializeField] private Transform levelSelectContent;
        [Tooltip("日历日格预设体（CalendarDayCell）：会被隐藏，按关卡数量复制")]
        [SerializeField] private Button levelSelectTemplate;
        [Tooltip("可玩未通关的日格底图")]
        [SerializeField] private Sprite dayCellSprite;
        [Tooltip("已通关的日格底图")]
        [SerializeField] private Sprite clearedCellSprite;
        [Tooltip("日历页眉文本（显示「立春 — 惊蛰」这样的范围）")]
        [SerializeField] private TextMeshProUGUI levelSelectTitleText;
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
        private bool lastIsEndless;
        [Tooltip("无尽模式当前难度倍率（HUD 显示用）")]
        private float lastDifficultyMultiplier = 1f;
        private int lastFilledHoles;
        private int lastTotalHoles = 1;
        [Tooltip("最近一次胜利获得的星级（0 = 不显示）")]
        private int lastStars;
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
            HideQuitConfirm();
            HideKeybind();
            // 移动端（iOS/Android）不允许程序主动退出，直接藏掉入口，避免点了没反应
            if (menuQuitButton != null && Application.isMobilePlatform)
            {
                menuQuitButton.gameObject.SetActive(false);
            }
            // 触屏没有键盘，键位设置那一行直接隐藏
            if (settingsKeybindButton != null && Application.isMobilePlatform)
            {
                settingsKeybindButton.gameObject.SetActive(false);
            }
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
            // 改键监听优先：此时按键是「要绑定的键」，不能顺带触发暂停
            if (isListeningKey)
            {
                HandleKeyListen();
                return;
            }

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
            lastIsEndless = levelConfig.IsEndless();
            lastFilledHoles = 0;
            lastTotalHoles = levelConfig.GetTotalHoles();
            lastDifficultyMultiplier = 1f;
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
            HideKeybind();
            HidePause();
            HideResult();
            HideQuitConfirm();
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

        /// <summary>打开退出确认弹窗（主界面「退出游戏」按钮）。</summary>
        public void ShowQuitConfirm()
        {
            if (quitConfirmPanel != null)
            {
                quitConfirmPanel.SetActive(true);
            }
        }

        public void HideQuitConfirm()
        {
            if (quitConfirmPanel != null)
            {
                quitConfirmPanel.SetActive(false);
            }
        }

        // ---------------- 键位设置 ----------------

        /// <summary>打开键位设置面板（从设置面板进入），返回时回到设置。</summary>
        public void ShowKeybind()
        {
            HideSettings();
            isListeningKey = false;
            if (keybindPanel != null)
            {
                keybindPanel.SetActive(true);
            }
            RefreshKeybindPanel();
            ShowKeybindHint(string.Empty);
        }

        public void HideKeybind()
        {
            isListeningKey = false;
            if (keybindPanel != null)
            {
                keybindPanel.SetActive(false);
            }
        }

        /// <summary>把当前键位写回面板（改键后、取消后、切语言后都调这里）。</summary>
        public void RefreshKeybindPanel()
        {
            if (keybindRotateCwText != null)
            {
                keybindRotateCwText.text = GameSettings.RotateClockwiseKey.ToString();
            }
            if (keybindRotateCcwText != null)
            {
                keybindRotateCcwText.text = GameSettings.RotateCounterClockwiseKey.ToString();
            }
        }

        private void StartKeyListen(bool clockwise)
        {
            isListeningKey = true;
            listeningClockwise = clockwise;
            TextMeshProUGUI label = clockwise ? keybindRotateCwText : keybindRotateCcwText;
            if (label != null)
            {
                label.text = L10n.Get("keybind.press");
            }
            ShowKeybindHint(string.Empty);
        }

        private void CancelKeyListen()
        {
            isListeningKey = false;
            RefreshKeybindPanel();
            ShowKeybindHint(string.Empty);
        }

        /// <summary>监听下一个按键：Esc 取消，鼠标键忽略，与另一动作冲突则提示并继续等。</summary>
        private void HandleKeyListen()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelKeyListen();
                return;
            }
            if (!Input.anyKeyDown)
            {
                return;
            }
            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (!Input.GetKeyDown(key))
                {
                    continue;
                }
                if (!GameSettings.IsBindableKey(key))
                {
                    continue;
                }
                TryBindKey(key);
                return;
            }
        }

        private void TryBindKey(KeyCode key)
        {
            KeyCode other = listeningClockwise
                ? GameSettings.RotateCounterClockwiseKey
                : GameSettings.RotateClockwiseKey;
            if (key == other)
            {
                // 保持监听状态，让玩家直接再按一个别的键
                ShowKeybindHint(L10n.Get("keybind.conflict"));
                return;
            }
            if (listeningClockwise)
            {
                GameSettings.RotateClockwiseKey = key;
            }
            else
            {
                GameSettings.RotateCounterClockwiseKey = key;
            }
            isListeningKey = false;
            RefreshKeybindPanel();
            ShowKeybindHint(string.Empty);
        }

        private void ShowKeybindHint(string message)
        {
            if (keybindHintText != null)
            {
                keybindHintText.text = message;
            }
        }

        /// <summary>
        /// 确认退出：编辑器里停止 Play Mode（否则编辑器不会有反应）；
        /// 打包后走 Application.Quit。移动端入口已隐藏，这里不会被调用。
        /// </summary>
        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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

            int slot = 0;
            string firstBanner = null;
            string lastBanner = null;

            for (int i = 0; i < levelDatabase.GetTotalLevels(); i++)
            {
                LevelConfig config = levelDatabase.GetLevel(i);
                if (config == null || config.IsEndless())
                {
                    continue; // 无尽模式不占日历格（主界面已有入口）
                }

                int index = i;
                Button cell = Instantiate(levelSelectTemplate, levelSelectContent);
                cell.gameObject.SetActive(true);
                cell.name = $"DayCell_{slot}";
                cell.GetComponent<RectTransform>().anchoredPosition = CellPosition(slot);

                bool unlocked = LevelProgress.IsUnlocked(levelDatabase, index);
                int stars = LevelProgress.GetStars(config.GetLevelId());

                // 三态：已通关用通关图；未解锁压暗去饱和；其余用默认日格
                Image background = cell.GetComponent<Image>();
                if (background != null)
                {
                    background.sprite = (unlocked && stars > 0) ? clearedCellSprite : dayCellSprite;
                    background.color = unlocked ? Color.white : new Color(0.86f, 0.86f, 0.90f, 1f);
                }
                cell.interactable = unlocked;

                string entry = FormatLevelEntry(config);
                TextMeshProUGUI numberText = cell.transform.Find("DayNumber")?.GetComponent<TextMeshProUGUI>();
                if (numberText != null)
                {
                    numberText.text = (slot + 1).ToString();
                }
                TextMeshProUGUI nameText = cell.transform.Find("DayName")?.GetComponent<TextMeshProUGUI>();
                if (nameText != null)
                {
                    nameText.text = entry;
                }

                // 星级 = 右上角竖排朱印，1~3 枚
                Transform seals = cell.transform.Find("Seals");
                if (seals != null)
                {
                    for (int s = 0; s < seals.childCount; s++)
                    {
                        seals.GetChild(s).gameObject.SetActive(s < stars);
                    }
                }

                if (firstBanner == null)
                {
                    firstBanner = BannerOf(entry);
                }
                lastBanner = BannerOf(entry);

                cell.onClick.RemoveAllListeners();
                AddClickSound(cell);
                cell.onClick.AddListener(() =>
                {
                    HideLevelSelect();
                    if (gameManager != null)
                    {
                        gameManager.StartLevelByIndex(index);
                    }
                });
                slot++;
            }

            if (levelSelectTitleText != null && firstBanner != null)
            {
                levelSelectTitleText.text = firstBanner + " — " + lastBanner;
            }
        }

        /// <summary>4 列 × 2 行的格位坐标：格 150、横向间距 40、纵向行距 210。</summary>
        private static Vector2 CellPosition(int slot)
        {
            int column = slot % 4;
            int row = slot / 4;
            return new Vector2(-285f + column * 190f, 150f - row * 210f);
        }

        /// <summary>取「立春 · 初候」里的节气部分，给页眉做「立春 — 惊蛰」范围。</summary>
        private static string BannerOf(string entry)
        {
            if (string.IsNullOrEmpty(entry))
            {
                return string.Empty;
            }
            int idx = entry.IndexOf('·');
            return idx > 0 ? entry.Substring(0, idx).Trim() : entry;
        }

        /// <summary>
        /// 关卡按钮文案：普通关优先用叙事名（level.N.name，如「立春 · 初候」），
        /// 没有配名字时退回「第 N 关」；无尽关显示「无尽模式」。
        /// </summary>
        private static string FormatLevelEntry(LevelConfig config)
        {
            if (config.IsEndless())
            {
                return L10n.Get("hud.endless");
            }
            string key = $"level.{config.GetLevelId()}.name";
            string name = L10n.Get(key);
            return name == key ? L10n.Get("hud.level", config.GetLevelId()) : name;
        }

        // ---------------- 设置面板 ----------------

        /// <summary>设置面板的返回目标：true = 回暂停面板，false = 回主界面。</summary>
        private bool settingsReturnToPause;

        /// <summary>打开设置面板（从主界面进入），返回时回主界面。</summary>
        public void ShowSettings()
        {
            ShowSettings(false);
        }

        /// <summary>
        /// 从暂停面板打开设置：先收起暂停面板，返回时回到暂停面板而不是主界面
        /// （否则玩家按一次返回就会丢掉当前局）。
        /// </summary>
        public void ShowSettingsFromPause()
        {
            ShowSettings(true);
        }

        private void ShowSettings(bool returnToPause)
        {
            settingsReturnToPause = returnToPause;
            if (returnToPause)
            {
                HidePause();
            }
            else
            {
                HideMenu();
            }
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
                musicSlider.SetValueWithoutNotify(Mathf.RoundToInt(GameSettings.MusicVolume * 100f));
            }
            if (sfxSlider != null)
            {
                sfxSlider.SetValueWithoutNotify(Mathf.RoundToInt(GameSettings.SfxVolume * 100f));
            }
            RefreshVolumeLabels();
            if (settingsLanguageText != null)
            {
                settingsLanguageText.text = L10n.GetLanguageDisplayName(L10n.GetCurrentLanguage());
            }
            if (settingsRotateText != null)
            {
                settingsRotateText.text = L10n.Get(GetRotateModeKey(GameSettings.RotateMode));
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
            // 静音按钮的文字表示「点下去会发生什么」：未静音显示「静音」，已静音显示「恢复」
            if (musicMuteText != null)
            {
                musicMuteText.text = L10n.Get(GameSettings.MusicMuted ? "settings.unmute" : "settings.mute");
            }
            if (sfxMuteText != null)
            {
                sfxMuteText.text = L10n.Get(GameSettings.SfxMuted ? "settings.unmute" : "settings.mute");
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

        /// <summary>胜利结算（阶段 5 洞闭合后触发）。stars = 本关星级（0 表示不显示）。</summary>
        public void ShowWin(int filledHoles, int totalHoles, int stars = 0)
        {
            HidePause();
            if (winPanel != null)
            {
                winPanel.SetActive(true);
            }
            lastFilledHoles = filledHoles;
            lastTotalHoles = totalHoles;
            lastStars = stars;
            RefreshWinText();
            if (winRetryButtonText != null)
            {
                // 有下一关显示「下一关」，最后一关显示「再玩一次」
                winRetryButtonText.text = L10n.Get(
                    gameManager != null && gameManager.HasNextLevel() ? "ui.next_level" : "ui.play_again");
            }
        }

        /// <summary>胜利文案 = 缺口进度 + 星级（★★☆），语言切换时也要重刷。</summary>
        private void RefreshWinText()
        {
            if (winText == null)
            {
                return;
            }
            string progress = L10n.Get("ui.holes_progress", lastFilledHoles, lastTotalHoles);
            winText.text = lastStars > 0
                ? progress + "  " + new string('★', lastStars) + new string('☆', 3 - lastStars)
                : progress;
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

        /// <summary>
        /// 每补好一个洞刷新 HUD 进度文本：普通关「已补 3 / 10」，无尽模式「无尽模式 · 已补 N 洞 · 难度 ×X」。
        /// 普通关也走这里，才能让右侧进度随补洞实时递增。
        /// </summary>
        public void UpdateHoleProgress(int filledHoles, int totalHoles, float difficultyMultiplier = 1f)
        {
            lastFilledHoles = filledHoles;
            lastTotalHoles = totalHoles;
            lastDifficultyMultiplier = difficultyMultiplier;
            if (levelNameText != null)
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
        /// 刷新灵气条与数值。
        /// 填充图是带柔边的整条美术，任何缩放都会变形；九宫格在这个尺寸下又会因为
        /// 边框大于矩形而把中间拉伸带压成 0（表现为整条看不见）。
        /// 因此改成「填充图固定满宽不缩放 + RectMask2D 按比例裁切」：
        /// 填多少就露出多少，左端造型始终不变，右端由遮罩直接切齐。
        /// </summary>
        public void UpdateQi(float current, float max)
        {
            if (qiBarFill != null)
            {
                float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
                if (qiBarFillArea != null)
                {
                    // 遮罩锚定在左侧（anchorMin.x == anchorMax.x == 0），改 sizeDelta.x 就是改「露出宽度」
                    if (qiBarFillFullWidth <= 0f)
                    {
                        // 首次刷新时按场景里的满宽记录一次，之后固定使用，避免越缩越小
                        qiBarFillFullWidth = Mathf.Max(1f, qiBarFillArea.sizeDelta.x);
                    }
                    qiBarFillArea.sizeDelta = new Vector2(qiBarFillFullWidth * ratio, qiBarFillArea.sizeDelta.y);
                }
                else
                {
                    // 没有遮罩容器时退回旧的 fillAmount 方案
                    qiBarFill.fillAmount = ratio;
                }
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
            // 统一先挂点击音，保证音效先于按钮动作播放
            AddClickSound(menuStartButton, menuEndlessButton, menuLevelSelectButton, menuSettingsButton, menuQuitButton);
            AddClickSound(quitCancelButton, quitConfirmButton);
            AddClickSound(levelSelectBackButton);
            AddClickSound(hudPauseButton);
            AddClickSound(resumeButton, restartButton, backToIdleButton);
            AddClickSound(loseRetryButton, loseMenuButton);
            AddClickSound(winRetryButton, winMenuButton);
            AddClickSound(previewToggleButton, pauseSettingsButton);
            AddClickSound(musicMuteButton, sfxMuteButton);
            AddClickSound(settingsBackButton, settingsLanguageButton, settingsKeybindButton, settingsRotateButton);
            AddClickSound(keybindRotateCwButton, keybindRotateCcwButton, keybindResetButton, keybindBackButton);

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
            if (menuQuitButton != null)
            {
                menuQuitButton.onClick.AddListener(ShowQuitConfirm);
            }
            if (quitCancelButton != null)
            {
                quitCancelButton.onClick.AddListener(HideQuitConfirm);
            }
            if (quitConfirmButton != null)
            {
                quitConfirmButton.onClick.AddListener(QuitGame);
            }

            // 设置面板
            if (settingsBackButton != null)
            {
                settingsBackButton.onClick.AddListener(() =>
                {
                    HideSettings();
                    // 从哪来回哪去：暂停进来就回暂停，否则回主界面
                    if (settingsReturnToPause)
                    {
                        ShowPause();
                    }
                    else
                    {
                        ShowMenu();
                    }
                });
            }
            if (settingsLanguageButton != null)
            {
                settingsLanguageButton.onClick.AddListener(() => L10n.CycleLanguage());
            }
            if (settingsRotateButton != null)
            {
                // 循环切换会写入 PlayerPrefs 并广播 OnChanged，RefreshSettingsLabels 会跟着刷新文案
                settingsRotateButton.onClick.AddListener(() => GameSettings.CycleRotateMode());
            }
            if (settingsKeybindButton != null)
            {
                // 键位设置：进入二级面板逐项改（移动端启动时会把这一行隐藏）
                settingsKeybindButton.onClick.AddListener(ShowKeybind);
            }
            if (keybindRotateCwButton != null)
            {
                keybindRotateCwButton.onClick.AddListener(() => StartKeyListen(true));
            }
            if (keybindRotateCcwButton != null)
            {
                keybindRotateCcwButton.onClick.AddListener(() => StartKeyListen(false));
            }
            if (keybindResetButton != null)
            {
                keybindResetButton.onClick.AddListener(() =>
                {
                    GameSettings.ResetRotateKeys();
                    RefreshKeybindPanel();
                    ShowKeybindHint(string.Empty);
                });
            }
            if (keybindBackButton != null)
            {
                keybindBackButton.onClick.AddListener(() =>
                {
                    HideKeybind();
                    ShowSettings();
                });
            }
            if (musicSlider != null)
            {
                // 0-100 整数档 = 1% 颗粒度；wholeNumbers 保证拖动时落在整数上
                musicSlider.minValue = 0f;
                musicSlider.maxValue = 100f;
                musicSlider.wholeNumbers = true;
                musicSlider.onValueChanged.AddListener(value =>
                {
                    // 拖动滑块视为「我要听声音」：顺手解除静音，省得玩家再点一次
                    GameSettings.MusicMuted = false;
                    GameSettings.MusicVolume = value / 100f;
                    RefreshVolumeLabels();
                });
            }
            if (sfxSlider != null)
            {
                sfxSlider.minValue = 0f;
                sfxSlider.maxValue = 100f;
                sfxSlider.wholeNumbers = true;
                sfxSlider.onValueChanged.AddListener(value =>
                {
                    GameSettings.SfxMuted = false;
                    GameSettings.SfxVolume = value / 100f;
                    RefreshVolumeLabels();
                });
            }
            if (musicMuteButton != null)
            {
                musicMuteButton.onClick.AddListener(() =>
                {
                    GameSettings.ToggleMusicMuted();
                    RefreshVolumeLabels();
                });
            }
            if (sfxMuteButton != null)
            {
                sfxMuteButton.onClick.AddListener(() =>
                {
                    GameSettings.ToggleSfxMuted();
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

            // HUD 暂停按钮（移动端没有 ESC 键，常驻右上角）
            if (hudPauseButton != null)
            {
                hudPauseButton.onClick.AddListener(() => gameManager?.PauseGame());
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
            if (pauseSettingsButton != null)
            {
                // 语言切换已并入设置面板；从暂停进入时返回要回暂停，不能踢回主界面
                pauseSettingsButton.onClick.AddListener(ShowSettingsFromPause);
            }
        }

        /// <summary>给按钮挂点击音（在按钮自身动作之前播放）。</summary>
        private static void AddClickSound(params Button[] buttons)
        {
            foreach (Button button in buttons)
            {
                if (button != null)
                {
                    button.onClick.AddListener(() => AudioManager.Instance?.PlaySfx(SfxId.Click));
                }
            }
        }

        /// <summary>旋转方式枚举 → L10n key。</summary>
        private static string GetRotateModeKey(RotateInputMode mode)
        {
            switch (mode)
            {
                case RotateInputMode.Gesture:
                    return "settings.rotate_gesture";
                case RotateInputMode.Both:
                    return "settings.rotate_both";
                default:
                    return "settings.rotate_buttons";
            }
        }

        /// <summary>
        /// 刷新设置面板文本（预览高亮 ON/OFF、旋转方式）。
        /// </summary>
        public void RefreshSettingsLabels()
        {
            if (previewToggleText != null)
            {
                previewToggleText.text = L10n.Get(GameSettings.ShowPlacementPreview
                    ? "ui.preview_on"
                    : "ui.preview_off");
            }
            if (settingsRotateText != null)
            {
                settingsRotateText.text = L10n.Get(GetRotateModeKey(GameSettings.RotateMode));
            }
            if (previewToggleVisual != null)
            {
                // 美术把滑块画在胶囊左端（= 关）；开启时水平翻转让滑块跑到右端，读起来就是「开」
                Vector3 scale = previewToggleVisual.localScale;
                scale.x = GameSettings.ShowPlacementPreview ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                previewToggleVisual.localScale = scale;
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
                // 星级也要跟着语言刷新，别把 ★ 覆盖掉
                RefreshWinText();
            }
            if (winRetryButtonText != null)
            {
                winRetryButtonText.text = L10n.Get(
                    gameManager != null && gameManager.HasNextLevel() ? "ui.next_level" : "ui.play_again");
            }
        }

        /// <summary>
        /// HUD 进度文本：普通关卡「已补 3 / 10」，无尽模式「无尽模式 · 已补 N 洞 · 难度 ×X」。
        /// 普通关不再显示关卡名——节气候名只出现在选关日历与结算面板。
        /// </summary>
        private string FormatLevelName()
        {
            if (!lastIsEndless)
            {
                return L10n.Get("hud.holes_progress", lastFilledHoles, lastTotalHoles);
            }
            string progress = L10n.Get("hud.endless_progress", lastFilledHoles);
            // 难度倍率只在真的变难后才显示，第一洞之前不占地方
            if (lastDifficultyMultiplier > 1.001f)
            {
                return progress + " · " + L10n.Get("hud.endless_diff", lastDifficultyMultiplier.ToString("0.0"));
            }
            return progress;
        }
    }
}
