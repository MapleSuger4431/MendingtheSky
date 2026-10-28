using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 游戏总控（设计文档 5.17）：状态机、关卡启动流程、每帧集中驱动各系统。
    ///
    /// 设计要点（评审定稿）：
    /// - 各 Manager 不自己写 Unity Update()，统一由本类在 Playing 状态下 Tick(dt)，
    ///   暂停只写一处，不会漏系统；
    /// - 暂停采用 Time.timeScale = 0（物理/动画/协程自动冻结）+ 状态检查双保险；
    /// - 失败由 QiManager.OnQiEmpty 事件驱动；胜利由阶段 5 的洞口闭合回调驱动。
    ///
    /// 原型阶段只配置单关（levelConfig 直接拖入 Inspector），关卡数据库在后续接入。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("关卡（二选一：配置了数据库则优先用数据库）")]
        [Tooltip("多关卡/无尽模式的入口；为空时回退到下面的单关引用")]
        [SerializeField] private LevelDatabase levelDatabase;
        [Tooltip("单关直连（原型模式）；数据库不为空时忽略")]
        [SerializeField] private LevelConfig levelConfig;

        [Header("系统引用（全部挂在同一物体或已接线）")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private GridVisual gridVisual;
        [SerializeField] private InventoryManager inventoryManager;
        [SerializeField] private PotManager potManager;
        [SerializeField] private QiManager qiManager;
        [SerializeField] private DragController dragController;
        [SerializeField] private UIManager uiManager;

        [Header("开始界面要隐藏的场景物件（进入关卡后再显示）")]
        [Tooltip("背篓区域根物体")]
        [SerializeField] private GameObject basketArea;
        [Tooltip("锅区域根物体")]
        [SerializeField] private GameObject potArea;
        [Tooltip("灵石根物体（回主界面时旧石头还在，不藏会悬在空中）")]
        [SerializeField] private GameObject lingShiRoot;

        [Header("本地化")]
        [Tooltip("简中 / 繁中 / 英文三张表；为空时界面会直接显示 key")]
        [SerializeField] private LocalizationDatabase localizationDatabase;

        [Header("运行时状态（只读）")]
        [SerializeField] private GameState state = GameState.Idle;
        [SerializeField] private int filledHoleCount;
        [SerializeField] private int currentLevelIndex;

        private void Awake()
        {
            // 必须早于所有 OnEnable：场景里的 LocalizedText 会在 OnEnable 里取文案
            L10n.Init(localizationDatabase);
        }

        private void Start()
        {
            // 启动进入主界面，并从这里开始循环 BGM（主界面就开始播）
            AudioManager.Instance?.PlayMusic();
            BackToIdle();
        }

        /// <summary>开始游戏：优先从关卡数据库的第 0 关开始，否则回退到单关引用。</summary>
        public void StartGame()
        {
            if (levelDatabase != null && levelDatabase.GetTotalLevels() > 0)
            {
                StartLevelByIndex(0);
                return;
            }
            if (levelConfig == null)
            {
                Debug.LogError("[GameManager] levelDatabase / levelConfig 都未配置，无法开始游戏");
                return;
            }
            StartLevel(levelConfig);
        }

        /// <summary>从数据库里找第一个 isEndless 的关卡并开始（主界面「无尽模式」按钮用）。</summary>
        public void StartEndless()
        {
            if (levelDatabase == null)
            {
                Debug.LogWarning("[GameManager] 未配置 levelDatabase，无法进入无尽模式");
                return;
            }
            for (int i = 0; i < levelDatabase.GetTotalLevels(); i++)
            {
                LevelConfig config = levelDatabase.GetLevel(i);
                if (config != null && config.IsEndless())
                {
                    StartLevelByIndex(i);
                    return;
                }
            }
            Debug.LogWarning("[GameManager] 关卡数据库里没有 isEndless 的关卡");
        }

        /// <summary>当前关之后是否还有关卡（胜利面板据此显示「下一关」或「再玩一次」）。</summary>
        public bool HasNextLevel()
        {
            return levelDatabase != null && currentLevelIndex + 1 < levelDatabase.GetTotalLevels();
        }

        /// <summary>
        /// 按关卡数据库索引开始（越界会夹到有效范围）。
        /// 多关卡与无尽模式都走这里；单关模式仍可直接 StartLevel(config)。
        /// </summary>
        public void StartLevelByIndex(int index)
        {
            if (levelDatabase == null)
            {
                Debug.LogWarning("[GameManager] 未配置 levelDatabase，无法按索引开始关卡");
                return;
            }
            int total = levelDatabase.GetTotalLevels();
            if (total <= 0)
            {
                Debug.LogWarning("[GameManager] levelDatabase 里没有关卡");
                return;
            }
            currentLevelIndex = Mathf.Clamp(index, 0, total - 1);
            LevelConfig config = levelDatabase.GetLevel(currentLevelIndex);
            if (config == null)
            {
                Debug.LogWarning($"[GameManager] 关卡索引 {currentLevelIndex} 没有对应配置");
                return;
            }
            StartLevel(config);
        }

        /// <summary>
        /// 关卡启动流程（设计文档 5.17 的 10 步）：
        /// 1) 清理并生成网格 → 2) 初始化灵气 → 3) 配置锅容量并清空 →
        /// 4) 生成背篓初始灵石 → 5) 复位拖拽状态 → 6) 显示 HUD → 7) 进入 Playing。
        /// </summary>
        public void StartLevel(LevelConfig config)
        {
            if (config == null)
            {
                return;
            }
            levelConfig = config;
            Time.timeScale = 1f; // 从暂停/失败状态重开时恢复时间
            filledHoleCount = 0;
            // 进入关卡：把背篓/锅/灵石显示出来（主界面是隐藏的）
            SetPlaygroundVisible(true);
            // 重开局先掐掉上一局残留的循环音（告急心跳 / 熬煮咕嘟）
            AudioManager.Instance?.StopWarningLoop();
            AudioManager.Instance?.StopCookLoop();

            // 1) 网格与洞
            gridManager.GenerateLevel(config);
            gridVisual.Init(gridManager);
            gridVisual.RefreshAll();

            // 2) 灵气（先解除旧订阅再订阅，避免重开时重复响应）
            qiManager.OnQiEmpty -= HandleQiEmpty;
            qiManager.OnQiEmpty += HandleQiEmpty;
            qiManager.Init(config, gridManager);

            // 3) 锅
            potManager.SetMaxCount(config.GetPotMaxCount());
            potManager.Clear();

            // 4) 背篓初始灵石（按洞切割；方案 D 下不再有冗余补给）
            inventoryManager.InvalidatePendingSpawns();
            inventoryManager.GenerateInitialInventory(config, gridManager);

            // 5) 输入状态复位（手里/暂存/预览全部清空）
            dragController.ResetAll();

            // 6) UI
            uiManager.ShowHUD(config);

            // 7) 进入进行中
            SetState(GameState.Playing);
        }

        /// <summary>
        /// 集中驱动：只有 Playing 状态才推进灵气、洞生成、熬煮。
        /// 暂停时 Time.timeScale = 0，即使此处漏判也不会推进（双保险）。
        /// </summary>
        private void Update()
        {
            if (state != GameState.Playing)
            {
                return;
            }
            float dt = Time.deltaTime;
            qiManager.Tick(dt);
            gridManager.Tick(dt);
            potManager.Tick(dt);
        }

        /// <summary>暂停：冻结时间并显示暂停面板。</summary>
        public void PauseGame()
        {
            if (state != GameState.Playing)
            {
                return;
            }
            SetState(GameState.Paused);
            Time.timeScale = 0f;
            uiManager.ShowPause();
        }

        /// <summary>继续：恢复时间并隐藏暂停面板。</summary>
        public void ResumeGame()
        {
            if (state != GameState.Paused)
            {
                return;
            }
            Time.timeScale = 1f;
            SetState(GameState.Playing);
            uiManager.HidePause();
        }

        /// <summary>重开关卡（暂停面板 / 失败结算使用）。</summary>
        public void RestartLevel()
        {
            Time.timeScale = 1f;
            if (levelConfig == null)
            {
                // 还没开始过任何关卡（例如停在主界面）时，重开等于回主界面
                BackToIdle();
                return;
            }
            StartLevel(levelConfig);
        }

        /// <summary>下一关：数据库里有下一关就进入，否则重开当前关。</summary>
        public void NextLevel()
        {
            if (levelDatabase != null && levelDatabase.GetTotalLevels() > 0
                && currentLevelIndex + 1 < levelDatabase.GetTotalLevels())
            {
                StartLevelByIndex(currentLevelIndex + 1);
                return;
            }
            RestartLevel();
        }

        /// <summary>当前关卡在数据库中的索引（单关模式恒为 0）。</summary>
        public int GetCurrentLevelIndex() { return currentLevelIndex; }

        /// <summary>返回主界面：清理运行时状态、隐藏游戏 UI、显示主菜单。</summary>
        public void BackToIdle()
        {
            Time.timeScale = 1f;
            SetState(GameState.Idle);
            if (dragController != null)
            {
                dragController.ResetAll();
            }
            // 回主界面：作废所有待弹出的石头，避免在菜单界面里冒出石头
            if (inventoryManager != null)
            {
                inventoryManager.InvalidatePendingSpawns();
            }
            // 主界面不显示背篓/锅/灵石
            SetPlaygroundVisible(false);
            uiManager.HideAllPanels();
            uiManager.HideHUD();
            uiManager.ShowMenu();
        }

        /// <summary>
        /// 开始界面与游戏内的场景物件切换：主界面隐藏背篓、锅与灵石，进入关卡后再显示。
        /// 灵石要一起藏——回主界面时旧石头还没销毁，只藏背篓会看到石头悬在空中。
        /// </summary>
        private void SetPlaygroundVisible(bool visible)
        {
            if (basketArea != null) basketArea.SetActive(visible);
            if (potArea != null) potArea.SetActive(visible);
            if (lingShiRoot != null) lingShiRoot.SetActive(visible);
        }

        /// <summary>
        /// 洞口闭合回调（阶段 5 接入）：增加灵气、累计已填洞数、检查胜利。
        /// 现在先实现接口与计数逻辑，等阶段 5 的 MarkHoleClosing/FinishHoleClosing 调它。
        /// </summary>
        public void OnHoleClosed(Hole hole, float qiReward)
        {
            // 只有进行中才结算；失败/胜利后到达的闭合回调直接忽略
            if (state != GameState.Playing)
            {
                return;
            }
            qiManager.AddQi(qiReward);
            filledHoleCount++;
            // 方案 D：洞消失后需要重新算缺口，补上玩家可能失去的那部分石头
            inventoryManager.InvalidatePendingSpawns();
            inventoryManager.EnsureSupply(levelConfig);
            if (levelConfig != null && levelConfig.IsEndless())
            {
                // 无尽模式：每补好一个洞，按难度曲线上调消耗 / 洞尺寸 / 补洞间隔
                ApplyEndlessDifficulty();
                if (uiManager != null)
                {
                    // HUD 上实时显示「已补 N 洞 · 难度 ×X」
                    uiManager.UpdateEndlessProgress(filledHoleCount, qiManager.GetDifficultyMultiplier());
                }
            }
            CheckWinCondition();
        }

        /// <summary>
        /// 按已补洞数把难度曲线应用到各系统（只对无尽模式生效；曲线为空则什么都不做）。
        /// </summary>
        private void ApplyEndlessDifficulty()
        {
            EndlessDifficulty difficulty = levelConfig != null ? levelConfig.GetEndlessDifficulty() : null;
            if (difficulty == null)
            {
                return;
            }
            float costMultiplier = difficulty.GetCostMultiplier(filledHoleCount);
            qiManager.SetDifficultyMultiplier(costMultiplier);
            gridManager.SetHoleCellBonus(difficulty.GetCellBonus(filledHoleCount));
            gridManager.SetHoleIntervalMultiplier(difficulty.GetIntervalMultiplier(filledHoleCount));
        }

        /// <summary>灵气归零（失败）：只从 Playing 进入 Lose，避免重复结算。</summary>
        private void HandleQiEmpty()
        {
            if (state != GameState.Playing)
            {
                return;
            }
            SetState(GameState.Lose);
            AudioManager.Instance?.PlaySfx(SfxId.Failure);
            if (dragController != null)
            {
                dragController.ResetAll(); // 手上/暂存的灵石复位，避免悬空
            }
            if (levelConfig != null && levelConfig.IsEndless())
            {
                // 无尽模式：分数 = 本局补洞数，顺便写入最高纪录
                int best = EndlessRecord.Report(filledHoleCount);
                uiManager.ShowLoseEndless(filledHoleCount, best);
                return;
            }
            uiManager.ShowLose(filledHoleCount, levelConfig != null ? levelConfig.GetTotalHoles() : 0);
        }

        /// <summary>
        /// 胜利检查：单关模式「已闭合洞数 ≥ 总洞数」；无尽模式无胜利。
        /// 注意：按评审 P0-1，合并功能已用配额回退保证可闭合洞数等于 totalHoles，
        /// 所以这里用 filledHoleCount 判定是有效的。
        /// </summary>
        public void CheckWinCondition()
        {
            if (levelConfig == null || levelConfig.IsEndless())
            {
                return;
            }
            if (filledHoleCount >= levelConfig.GetTotalHoles())
            {
                SetState(GameState.Win);
                AudioManager.Instance?.PlaySfx(SfxId.Success);
                if (dragController != null)
                {
                    dragController.ResetAll();
                }
                uiManager.ShowWin(filledHoleCount, levelConfig.GetTotalHoles());
            }
        }

        public GameState GetState() { return state; }
        public bool IsPlaying() { return state == GameState.Playing; }
        public bool IsPaused() { return state == GameState.Paused; }
        public int GetFilledHoleCount() { return filledHoleCount; }

        /// <summary>当前关卡配置（调试快捷键 / 动态补洞发石需要）。</summary>
        public LevelConfig GetLevelConfig() { return levelConfig; }

        private void SetState(GameState newState)
        {
            GameState oldState = state;
            state = newState;
            if (oldState == newState)
            {
                return;
            }

            // 离开「进行中」（暂停 / 失败 / 胜利 / 回主界面）时停掉所有循环音效，
            // 否则结算后告急心跳与熬煮咕嘟会一直响（2026-10-27 用户反馈）
            if (newState != GameState.Playing)
            {
                AudioManager.Instance?.StopWarningLoop();
                AudioManager.Instance?.StopCookLoop();
                return;
            }

            // 暂停 → 继续：按当前状态把循环音重新挂上，避免暂停再继续后永远没声音
            if (oldState == GameState.Paused)
            {
                if (qiManager != null && qiManager.IsWarning())
                {
                    AudioManager.Instance?.PlayWarningLoop();
                }
                if (potManager != null && potManager.GetCookingLingShi().Count > 0)
                {
                    AudioManager.Instance?.PlayCookLoop();
                }
            }
        }

        private void OnDestroy()
        {
            if (qiManager != null)
            {
                qiManager.OnQiEmpty -= HandleQiEmpty;
            }
        }
    }
}
