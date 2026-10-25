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
        [Header("关卡（原型：单关直接引用）")]
        [SerializeField] private LevelConfig levelConfig;

        [Header("系统引用（全部挂在同一物体或已接线）")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private GridVisual gridVisual;
        [SerializeField] private InventoryManager inventoryManager;
        [SerializeField] private PotManager potManager;
        [SerializeField] private QiManager qiManager;
        [SerializeField] private DragController dragController;
        [SerializeField] private UIManager uiManager;

        [Header("运行时状态（只读）")]
        [SerializeField] private GameState state = GameState.Idle;
        [SerializeField] private int filledHoleCount;

        private void Start()
        {
            StartGame();
        }

        /// <summary>开始游戏：原型直接进入配置的关卡。</summary>
        public void StartGame()
        {
            if (levelConfig == null)
            {
                Debug.LogError("[GameManager] levelConfig 未配置，无法开始游戏");
                return;
            }
            StartLevel(levelConfig);
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

            // 4) 背篓初始灵石（按洞切割 + 冗余补给）
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
            StartLevel(levelConfig);
        }

        /// <summary>下一关（原型单关：等价于重开；关卡数据库接入后改成 index+1）。</summary>
        public void NextLevel()
        {
            StartLevel(levelConfig);
        }

        /// <summary>返回主界面（原型无主界面：回到 Idle 并隐藏界面，等待外部重新开始）。</summary>
        public void BackToIdle()
        {
            Time.timeScale = 1f;
            SetState(GameState.Idle);
            uiManager.HideAllPanels();
            uiManager.HideHUD();
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
            CheckWinCondition();
            // 闭合会销毁洞内灵石；其它洞若因此缺少必要形状，立即补发（仍为 Playing 时）
            if (state == GameState.Playing)
            {
                EnsureHolesSolvable();
            }
        }

        /// <summary>灵气归零（失败）：只从 Playing 进入 Lose，避免重复结算。</summary>
        private void HandleQiEmpty()
        {
            if (state != GameState.Playing)
            {
                return;
            }
            SetState(GameState.Lose);
            if (dragController != null)
            {
                dragController.ResetAll(); // 手上/暂存的灵石复位，避免悬空
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
                if (dragController != null)
                {
                    dragController.ResetAll();
                }
                uiManager.ShowWin(filledHoleCount, levelConfig.GetTotalHoles());
            }
        }

        /// <summary>
        /// 全局可解性保障（2026-10-25 用户反馈）：
        /// 灵石可跨洞互换后，闭合的洞会销毁其中的灵石，可能带走其它洞需要的形状。
        /// 放置与闭合后检查每个开放洞：其剩余格能否被当前所有未放置灵石（背篓+锅+暂存）拼满；
        /// 不能则按剩余格重新切一批灵石投放到背篓，保证任何洞都不会陷入死局。
        /// </summary>
        public void EnsureHolesSolvable()
        {
            if (gridManager == null || inventoryManager == null || levelConfig == null)
            {
                return;
            }
            var unplaced = new List<List<Vector2Int>>();
            CollectUnplacedShapes(unplaced);

            foreach (Hole hole in gridManager.GetAllHoles())
            {
                if (hole.IsClosing())
                {
                    continue;
                }
                var remaining = new List<Vector2Int>();
                foreach (Vector2Int cell in hole.GetCells())
                {
                    if (gridManager.GetCell(cell.x, cell.y) == GridManager.CellHole)
                    {
                        remaining.Add(cell);
                    }
                }
                if (remaining.Count == 0)
                {
                    continue;
                }
                if (ShapeCutter.CanTile(remaining, unplaced, allowSubset: true))
                {
                    continue;
                }
                inventoryManager.SupplyPiecesForCells(remaining, hole.GetHoleId(), levelConfig);
                Debug.Log($"[GameManager] 洞口#{hole.GetHoleId()} 缺少可用形状，已补充灵石（剩余 {remaining.Count} 格）");
                // 新补的灵石进入背篓列表，重新收集供后续洞使用
                unplaced.Clear();
                CollectUnplacedShapes(unplaced);
            }
        }

        private void CollectUnplacedShapes(List<List<Vector2Int>> shapes)
        {
            CollectShapes(inventoryManager.GetAll(), shapes);
            if (potManager != null)
            {
                CollectShapes(potManager.GetAllPieces(), shapes);
            }
            if (dragController != null)
            {
                CollectShapes(dragController.GetStashedPieces(), shapes);
            }
        }

        private static void CollectShapes(List<LingShi> list, List<List<Vector2Int>> shapes)
        {
            if (list == null)
            {
                return;
            }
            foreach (LingShi ls in list)
            {
                if (ls == null)
                {
                    continue;
                }
                if (ls.GetState() != LingShiState.InBasket && ls.GetState() != LingShiState.Cooked)
                {
                    continue;
                }
                shapes.Add(ls.GetBaseCells());
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
            state = newState;
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
