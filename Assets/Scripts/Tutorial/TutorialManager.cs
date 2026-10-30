using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 新手引导驱动：只在第一关的首次游玩时启动，按 5 步推进，完成后写入 PlayerPrefs 不再出现。
    ///
    /// 采用轮询而不是给各系统加事件——引导是一次性的教学流程，每 0.2 秒查一次状态足够，
    /// 而且不会为了教学去改动 InventoryManager / PotManager / GridManager 的核心逻辑。
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PotManager potManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private TutorialOverlay overlay;
        [Tooltip("锅区域的碰撞体：高亮框套在它外面")]
        [SerializeField] private Collider2D potHighlightCollider;

        [Header("节奏")]
        [Tooltip("完成条件检查间隔（秒）")]
        [SerializeField] private float checkInterval = 0.2f;
        [Tooltip("最后一句提示停留多久")]
        [SerializeField] private float celebrateSeconds = 2f;

        private const string KeyCompleted = "tutorial.completed";

        private TutorialStep step = TutorialStep.Inactive;
        private float checkTimer;
        private float celebrateTimer;
        private int closedHolesAtStart;

        /// <summary>屏幕空间的矩形（中心 + 尺寸），供高亮框使用。</summary>
        private struct ScreenRect
        {
            public Vector2 center;
            public Vector2 size;
            public bool IsValid => size.x > 1f && size.y > 1f;
        }

        public TutorialStep GetStep() { return step; }

        public bool IsRunning()
        {
            return step != TutorialStep.Inactive && step != TutorialStep.Done;
        }

        /// <summary>引导是否已经完成过（完成后不再自动出现）。</summary>
        public static bool HasCompleted()
        {
            return PlayerPrefs.GetInt(KeyCompleted, 0) == 1;
        }

        /// <summary>清掉完成标记，下次进第一关会重新引导（设置面板的「重看引导」用）。</summary>
        public static void ResetCompleted()
        {
            PlayerPrefs.DeleteKey(KeyCompleted);
            PlayerPrefs.Save();
        }

        private void Awake()
        {
            if (overlay != null)
            {
                overlay.OnSkip += Skip;
            }
        }

        private void OnDestroy()
        {
            if (overlay != null)
            {
                overlay.OnSkip -= Skip;
            }
        }

        /// <summary>第一关首次游玩时开始引导；其它关卡、已完成、或引用缺失时什么都不做。</summary>
        public void TryBegin()
        {
            if (HasCompleted() || gameManager == null || overlay == null)
            {
                return;
            }
            if (gameManager.GetCurrentLevelIndex() != 0)
            {
                return;
            }
            closedHolesAtStart = gameManager.GetFilledHoleCount();
            step = TutorialStep.DragToPot;
            checkTimer = 0f;
            ShowCurrentStep();
        }

        private void Update()
        {
            if (!IsRunning())
            {
                return;
            }
            // 暂停 / 结算时先收起，恢复后按当前步骤重新显示，避免压在半透明面板上
            if (gameManager != null && !gameManager.IsPlaying())
            {
                overlay?.Hide();
                return;
            }
            if (step == TutorialStep.Celebrate && overlay != null && !overlay.IsVisible)
            {
                // 从暂停恢复：庆祝提示也需要重新显示
                ShowCurrentStep();
            }

            checkTimer += Time.deltaTime;
            if (checkTimer < checkInterval)
            {
                return;
            }
            checkTimer = 0f;

            switch (step)
            {
                case TutorialStep.DragToPot:
                    if (potManager != null && potManager.GetAllPieces().Count > 0)
                    {
                        Advance();
                    }
                    break;
                case TutorialStep.WaitCooking:
                    if (HasCookedInPot())
                    {
                        Advance();
                    }
                    break;
                case TutorialStep.DragToGrid:
                    if (HasPlacedCell())
                    {
                        Advance();
                    }
                    break;
                case TutorialStep.FillHole:
                    if (gameManager != null && gameManager.GetFilledHoleCount() > closedHolesAtStart)
                    {
                        Advance();
                    }
                    break;
                case TutorialStep.Celebrate:
                    celebrateTimer -= checkInterval;
                    if (celebrateTimer <= 0f)
                    {
                        Finish();
                    }
                    break;
            }
        }

        /// <summary>玩家点了「跳过」：直接标记完成，不再打扰。</summary>
        public void Skip()
        {
            if (!IsRunning())
            {
                return;
            }
            Finish();
        }

        private void Advance()
        {
            int next = (int)step + 1;
            if (next > (int)TutorialStep.Celebrate)
            {
                Finish();
                return;
            }
            step = (TutorialStep)next;

            if (step == TutorialStep.FillHole)
            {
                // 从这一刻开始算「又补好了一个洞」
                closedHolesAtStart = gameManager != null ? gameManager.GetFilledHoleCount() : 0;
            }
            if (step == TutorialStep.Celebrate)
            {
                celebrateTimer = celebrateSeconds;
            }
            ShowCurrentStep();
        }

        private void Finish()
        {
            step = TutorialStep.Done;
            overlay?.Hide();
            PlayerPrefs.SetInt(KeyCompleted, 1);
            PlayerPrefs.Save();
        }

        private void ShowCurrentStep()
        {
            if (overlay == null)
            {
                return;
            }
            switch (step)
            {
                case TutorialStep.DragToPot:
                    ShowAtWorld("tutorial.drag_to_pot", GetPotScreenRect());
                    break;
                case TutorialStep.WaitCooking:
                    ShowAtWorld("tutorial.wait_cooking", GetPotScreenRect());
                    break;
                case TutorialStep.DragToGrid:
                    ShowAtWorld("tutorial.drag_to_grid", GetHoleScreenRect());
                    break;
                case TutorialStep.FillHole:
                    ShowAtWorld("tutorial.fill_hole", GetHoleScreenRect());
                    break;
                case TutorialStep.Celebrate:
                    overlay.ShowText("tutorial.celebrate");
                    break;
                default:
                    overlay.Hide();
                    break;
            }
        }

        private void ShowAtWorld(string key, ScreenRect rect)
        {
            if (overlay == null)
            {
                return;
            }
            if (rect.IsValid)
            {
                overlay.ShowAt(key, rect.center, rect.size);
            }
            else
            {
                overlay.ShowText(key);
            }
        }

        private bool HasCookedInPot()
        {
            if (potManager == null)
            {
                return false;
            }
            foreach (LingShi ls in potManager.GetAllPieces())
            {
                if (ls != null && ls.GetState() == LingShiState.Cooked)
                {
                    return true;
                }
            }
            return false;
        }

        private bool HasPlacedCell()
        {
            if (gridManager == null)
            {
                return false;
            }
            int rows = gridManager.GetRows();
            int cols = gridManager.GetCols();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (gridManager.IsFilled(r, c))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>锅区域的屏幕矩形（用碰撞体包围盒投影，比手填坐标稳）。</summary>
        private ScreenRect GetPotScreenRect()
        {
            Camera cam = Camera.main;
            if (cam == null || potHighlightCollider == null)
            {
                return default;
            }
            Bounds b = potHighlightCollider.bounds;
            Vector3 min = cam.WorldToScreenPoint(new Vector3(b.min.x, b.min.y, b.center.z));
            Vector3 max = cam.WorldToScreenPoint(new Vector3(b.max.x, b.max.y, b.center.z));
            return new ScreenRect
            {
                center = new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f),
                size = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y))
            };
        }

        /// <summary>第一个洞的屏幕矩形（洞的格子包围盒）。</summary>
        private ScreenRect GetHoleScreenRect()
        {
            Camera cam = Camera.main;
            if (cam == null || gridManager == null)
            {
                return default;
            }
            List<Hole> holes = gridManager.GetAllHoles();
            if (holes.Count == 0)
            {
                return default;
            }
            List<Vector2Int> cells = holes[0].GetCells();
            if (cells == null || cells.Count == 0)
            {
                return default;
            }

            float half = gridManager.GetCellSize() * 0.5f;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector2Int cell in cells)
            {
                Vector3 w = gridManager.GridToWorld(cell.x, cell.y);
                min = Vector2.Min(min, new Vector2(w.x - half, w.y - half));
                max = Vector2.Max(max, new Vector2(w.x + half, w.y + half));
            }

            Vector3 cornerMin = cam.WorldToScreenPoint(new Vector3(min.x, min.y, 0f));
            Vector3 cornerMax = cam.WorldToScreenPoint(new Vector3(max.x, max.y, 0f));
            return new ScreenRect
            {
                center = new Vector2((cornerMin.x + cornerMax.x) * 0.5f, (cornerMin.y + cornerMax.y) * 0.5f),
                size = new Vector2(Mathf.Abs(cornerMax.x - cornerMin.x), Mathf.Abs(cornerMax.y - cornerMin.y))
            };
        }
    }
}
