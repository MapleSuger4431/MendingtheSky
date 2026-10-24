using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 拖拽控制器（阶段 3）：拾取 → 拖动（滚轮/R 旋转）→ 放置/回背篓/暂存。
    /// 拾取方案 A：只检测灵石碰撞体（Physics2D.OverlapPointAll），取排序最上层。
    /// OnMouseUp 判定优先级（评审 P0-4）：
    ///   ① 锅区（阶段 4 接入 PotManager）→ 现阶段跳过
    ///   ② 网格匹配 → 放置
    ///   ③ 背篓区 / InBasket 状态 → 回背篓
    ///   ④ 其他 → Cooked 进暂存（半透明悬空）；InBasket 强制回背篓
    /// 暂存只接受 Cooked（评审 P0-3）；拾取暂存块用 except 防自我回收（评审 P0-2）。
    /// </summary>
    public class DragController : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private GridVisual gridVisual;
        [SerializeField] private InventoryManager inventoryManager;
        [Tooltip("锅管理器（阶段 4 接入；未配置时锅分支自动跳过）")]
        [SerializeField] private PotManager potManager;
        [Tooltip("游戏总控（阶段 4 接入；用于暂停/结算时禁用拖拽输入）")]
        [SerializeField] private GameManager gameManager;
        [Tooltip("背篓区域触发器（用于判定指针是否在背篓上）")]
        [SerializeField] private Collider2D basketAreaCollider;

        [Header("输入")]
        [SerializeField] private KeyCode rotateKey = KeyCode.R;
        [SerializeField] private bool useMouseScroll = true;

        [Header("缩放")]
        [SerializeField] private float basketScale = 0.6f;
        [SerializeField] private float dragScale = 1f;
        [SerializeField] private float snapDuration = 0.15f;

        private LingShi draggingLingShi;
        private readonly List<LingShi> stashedLingShi = new List<LingShi>();
        private bool isDragging;
        private Vector3 dragOffset;
        private Camera cam;

        // 可解性缓存：只在「目标洞 + 锚点 + 旋转」变化时重新求解，避免每帧跑搜索
        private string lastSolveKey = string.Empty;
        private bool lastSolveResult = true;

        public bool IsDragging() { return isDragging; }

        /// <summary>当前正在拖拽的灵石（无则为 null）。调试与 UI 提示用。</summary>
        public LingShi GetDraggingLingShi() { return draggingLingShi; }

        private void Awake()
        {
            cam = Camera.main;
        }

        private void Update()
        {
            // 阶段 4：只在 GameManager 处于 Playing 时接受拖拽输入
            //（暂停 / 胜利 / 失败 / 未开始时不响应，避免暂停期间还能拖灵石）
            if (gameManager != null && !gameManager.IsPlaying())
            {
                return;
            }
            if (Input.GetMouseButtonDown(0))
            {
                OnMouseDown(Input.mousePosition);
            }
            if (isDragging)
            {
                if (Input.GetMouseButton(0))
                {
                    OnMouseDrag(Input.mousePosition);
                }
                if (Input.GetMouseButtonUp(0))
                {
                    OnMouseUp(Input.mousePosition);
                }
                HandleRotateInput();
            }
        }

        public void OnMouseDown(Vector2 screenPos)
        {
            if (isDragging)
            {
                return;
            }
            LingShi picked = TryPickUp(screenPos);
            if (picked == null)
            {
                return;
            }
            // 拾取新灵石时，其余暂存块返回背篓（被拾取的那块排除，防止自我回收）
            ReturnStashedToBasket(picked);

            // 若这块灵石来自锅里（Cooked 未拖走），先从锅的登记表移除，避免容量泄漏
            if (potManager != null)
            {
                potManager.RemoveLingShi(picked);
            }

            if (picked.GetState() == LingShiState.Placed)
            {
                gridManager.UnfillCells(picked);
                picked.SetState(LingShiState.Cooked);
            }
            StartDrag(picked, ScreenToWorld(screenPos));
        }

        private void StartDrag(LingShi ls, Vector3 mouseWorld)
        {
            draggingLingShi = ls;
            isDragging = true;
            dragOffset = ls.transform.position - mouseWorld;
            lastSolveKey = string.Empty; // 每次开始拖拽清空可解性缓存
            // 用 Kinematic 而不是 simulated=false：后者会让 Collider 退出物理查询，
            // 导致已放置/暂存的灵石之后无法再被拾取
            ls.SetPhysicsBodyType(RigidbodyType2D.Kinematic);
            ls.SetVelocity(Vector2.zero);
            ls.SetTransparent(false);
            ls.SetTargetScale(dragScale);
            UpdatePreview();
        }

        public void OnMouseDrag(Vector2 screenPos)
        {
            if (draggingLingShi == null)
            {
                return;
            }
            Vector3 world = ScreenToWorld(screenPos);
            draggingLingShi.transform.position = world + dragOffset;
            UpdatePreview();
        }

        public void OnMouseUp(Vector2 screenPos)
        {
            if (!isDragging || draggingLingShi == null)
            {
                return;
            }
            LingShi ls = draggingLingShi;
            Vector3 mouseWorld = ScreenToWorld(screenPos);

            // ① 指针在锅区且是未煮灵石 → 尝试放入锅（阶段 4）
            //    成功：状态变 Cooking、登记到锅、结束拖拽；
            //    失败（锅满）：继续走下面的分支，最终退回背篓。
            if (ls.CanDragToPot() && potManager != null && potManager.IsInPotArea(mouseWorld))
            {
                if (potManager.TryAddLingShi(ls))
                {
                    EndDrag();
                    return;
                }
                Debug.Log("[DragController] 锅已满，灵石退回背篓");
            }

            // ② 网格放置（仅 Cooked）
            if (ls.GetState() == LingShiState.Cooked)
            {
                Vector2Int anchor = gridManager.WorldToGrid(ls.transform.position);
                Hole targetHole = MatchChecker.GetMatchedHole(ls, anchor.x, anchor.y, gridManager);
                if (targetHole != null)
                {
                    List<Vector2Int> targetCells = MatchChecker.GetMatchedCells(ls, anchor.x, anchor.y, gridManager);
                    if (WouldKeepHoleSolvable(ls, anchor, targetCells))
                    {
                        lastSolveKey = string.Empty; // 局面变化，缓存失效
                        gridManager.FillCells(ls, anchor.x, anchor.y);
                        ls.SetTransparent(false);
                        ls.SetTargetScale(1f);
                        StartCoroutine(SnapToCell(ls, anchor));
                        EndDrag();
                        return;
                    }
                    // 形状能放但会导致本洞无法拼满：拒绝放置，落到下面的暂存/回篓分支
                    Debug.Log("[DragController] 该落点会导致洞口无法拼满，放置被拒绝");
                }
            }

            // ③ 背篓区 / InBasket 一律回背篓
            bool overBasket = IsPointerOverBasket(mouseWorld);
            if (overBasket || ls.GetState() == LingShiState.InBasket)
            {
                ReturnToBasket(ls);
            }
            // ④ Cooked 落空 → 暂存（半透明悬空）
            else
            {
                ls.SetTransparent(true);
                ls.SetPhysicsBodyType(RigidbodyType2D.Kinematic);
                ls.SetVelocity(Vector2.zero);
                ls.SetTargetScale(1f);
                stashedLingShi.Add(ls);
            }
            EndDrag();
        }

        private void EndDrag()
        {
            isDragging = false;
            draggingLingShi = null;
            ClearPreview();
        }

        private IEnumerator SnapToCell(LingShi ls, Vector2Int anchor)
        {
            Vector3 from = ls.transform.position;
            Vector3 to = gridManager.GridToWorld(anchor.x, anchor.y);
            float t = 0f;
            while (t < snapDuration && ls != null)
            {
                t += Time.deltaTime;
                ls.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t / snapDuration));
                yield return null;
            }
            if (ls != null)
            {
                ls.transform.position = to;
            }
        }

        private void HandleRotateInput()
        {
            if (draggingLingShi == null || !draggingLingShi.CanRotate())
            {
                return;
            }
            bool rotate = Input.GetKeyDown(rotateKey);
            if (useMouseScroll && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
            {
                rotate = true;
            }
            if (rotate)
            {
                draggingLingShi.Rotate();
                UpdatePreview();
            }
        }

        private void UpdatePreview()
        {
            // 设置项：关闭「放置预览高亮」后不给任何视觉提示（高手挑战模式）
            if (!GameSettings.ShowPlacementPreview)
            {
                ClearPreview();
                return;
            }
            if (draggingLingShi == null || draggingLingShi.GetState() != LingShiState.Cooked)
            {
                ClearPreview();
                return;
            }
            Vector2Int anchor = gridManager.WorldToGrid(draggingLingShi.transform.position);
            List<Vector2Int> cells = MatchChecker.GetMatchedCells(draggingLingShi, anchor.x, anchor.y, gridManager);
            bool valid = MatchChecker.CanPlace(draggingLingShi, anchor.x, anchor.y, gridManager);
            bool wrongHole = !valid && MatchChecker.IsShapeFitsOtherHole(draggingLingShi, anchor.x, anchor.y, gridManager);
            if (valid)
            {
                // 死锁防护：即使形状能放，也不允许放完后本洞再也拼不满的位置
                valid = WouldKeepHoleSolvable(draggingLingShi, anchor, cells);
            }
            gridVisual.ShowPreview(cells, valid, wrongHole);
        }

        /// <summary>
        /// 放置前的可解性校验（死锁防护）：
        /// 除被拖拽的这块外，本洞「其余未放置的灵石」必须恰好能铺满「剩余缺口格」。
        /// 这样无论玩家把棋子放在哪个被允许的位置，洞口都保证还能拼满，
        /// 不会出现"形状明明还能放，但剩余块怎么摆都填不满"的死局。
        /// 结果按「洞 + 锚点 + 旋转步数」缓存，拖动时只在位置变化时重新求解。
        /// </summary>
        private bool WouldKeepHoleSolvable(LingShi dragging, Vector2Int anchor, List<Vector2Int> targetCells)
        {
            Hole hole = gridManager.GetHoleAt(anchor.x, anchor.y);
            if (hole == null)
            {
                return false;
            }
            string key = $"{hole.GetHoleId()}_{anchor.x}_{anchor.y}_{dragging.GetRotationSteps()}";
            if (key == lastSolveKey)
            {
                return lastSolveResult;
            }
            lastSolveKey = key;

            // 1) 剩余缺口格 = 洞内所有未填格 - 本次放置覆盖的格
            var remaining = new List<Vector2Int>();
            foreach (Vector2Int cell in hole.GetCells())
            {
                if (gridManager.GetCell(cell.x, cell.y) == GridManager.CellHole)
                {
                    remaining.Add(cell);
                }
            }
            foreach (Vector2Int cell in targetCells)
            {
                remaining.Remove(cell);
            }

            // 2) 本洞其余未放置的灵石：背篓 + 锅 + 暂存，排除正在拖拽的这块
            var pieces = new List<List<Vector2Int>>();
            CollectSolvablePieces(inventoryManager != null ? inventoryManager.GetAll() : null, dragging, hole, pieces);
            CollectSolvablePieces(potManager != null ? potManager.GetAllPieces() : null, dragging, hole, pieces);
            CollectSolvablePieces(stashedLingShi, dragging, hole, pieces);

            lastSolveResult = ShapeCutter.CanTile(remaining, pieces);
            return lastSolveResult;
        }

        /// <summary>从来源列表收集属于该洞、尚未放置的灵石形状（基础格，求解器会尝试旋转）。</summary>
        private static void CollectSolvablePieces(List<LingShi> source, LingShi dragging, Hole hole,
            List<List<Vector2Int>> pieces)
        {
            if (source == null)
            {
                return;
            }
            foreach (LingShi ls in source)
            {
                if (ls == null || ls == dragging)
                {
                    continue;
                }
                if (ls.GetSourceHoleId() != hole.GetHoleId())
                {
                    continue;
                }
                if (ls.GetState() != LingShiState.InBasket && ls.GetState() != LingShiState.Cooked)
                {
                    continue;
                }
                pieces.Add(ls.GetBaseCells());
            }
        }

        private void ClearPreview()
        {
            if (gridVisual != null)
            {
                gridVisual.ClearPreview();
            }
        }

        private void ReturnToBasket(LingShi ls)
        {
            ls.SetTransparent(false);
            ls.SetTargetScale(basketScale);
            inventoryManager.ReturnToBasket(ls);
        }

        /// <summary>把暂存灵石送回背篓；except 为正在被拾取的那块（从暂存列表移除但不回收）。</summary>
        public void ReturnStashedToBasket(LingShi except = null)
        {
            for (int i = stashedLingShi.Count - 1; i >= 0; i--)
            {
                LingShi ls = stashedLingShi[i];
                if (ls == null)
                {
                    stashedLingShi.RemoveAt(i);
                    continue;
                }
                if (ls == except)
                {
                    stashedLingShi.RemoveAt(i);
                    continue;
                }
                stashedLingShi.RemoveAt(i);
                ls.SetTransparent(false);
                ls.SetTargetScale(basketScale);
                inventoryManager.AddCookedLingShi(ls);
            }
        }

        public void ResetAll()
        {
            if (draggingLingShi != null)
            {
                inventoryManager.ReturnToBasket(draggingLingShi);
            }
            draggingLingShi = null;
            isDragging = false;
            stashedLingShi.Clear();
            ClearPreview();
        }

        private LingShi TryPickUp(Vector2 screenPos)
        {
            Vector3 world = ScreenToWorld(screenPos);
            Collider2D[] hits = Physics2D.OverlapPointAll(world);
            LingShi best = null;
            int bestOrder = int.MinValue;
            foreach (Collider2D hit in hits)
            {
                LingShi ls = hit.GetComponentInParent<LingShi>();
                if (ls == null || !IsPickable(ls))
                {
                    continue;
                }
                SpriteRenderer sr = hit.GetComponentInParent<SpriteRenderer>();
                int order = sr != null ? sr.sortingOrder : 0;
                if (order > bestOrder)
                {
                    best = ls;
                    bestOrder = order;
                }
            }
            return best;
        }

        private static bool IsPickable(LingShi ls)
        {
            switch (ls.GetState())
            {
                case LingShiState.InBasket:
                case LingShiState.Cooked:
                    return true;
                case LingShiState.Placed:
                    return ls.CanPickFromGrid();
                default:
                    return false;
            }
        }

        private bool IsPointerOverBasket(Vector3 world)
        {
            return basketAreaCollider != null && basketAreaCollider.OverlapPoint(world);
        }

        private Vector3 ScreenToWorld(Vector2 screenPos)
        {
            if (cam == null)
            {
                cam = Camera.main;
            }
            float z = cam != null ? Mathf.Abs(cam.transform.position.z) : 10f;
            return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, z));
        }
    }
}
