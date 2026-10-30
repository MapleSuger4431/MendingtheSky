using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MendingtheSky
{
    /// <summary>
    /// 拖拽控制器（阶段 3）：拾取 → 拖动（A/D 或滚轮旋转）→ 放置/回背篓/暂存。
    /// 拾取方案 A：只检测灵石碰撞体（Physics2D.OverlapPointAll），取排序最上层。
    ///
    /// 放置判定与预览颜色（2026-10-24 用户确认）：
    ///   🟢 绿色 = 可放置：形状覆盖的都是缺口格、且属于同一个洞（同形状灵石可互换）；
    ///   🔴 红色 = 不可放：越界 / 非缺口格 / 跨洞 / 未煮熟。
    /// 不再用“可能导致洞口拼不满”作为拒绝条件（自由放置优先，拼错可点选取下重摆）。
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
        [Tooltip("放置/取下后触发补给账算的最小间隔（秒），避免每帧刷")]
        [SerializeField] private float supplyCheckInterval = 1f;
        private float lastSupplyCheck;
        [Tooltip("锅管理器（阶段 4 接入；未配置时锅分支自动跳过）")]
        [SerializeField] private PotManager potManager;
        [Tooltip("游戏总控（阶段 4 接入；用于暂停/结算时禁用拖拽输入）")]
        [SerializeField] private GameManager gameManager;
        [Tooltip("背篓区域触发器（用于判定指针是否在背篓上）")]
        [SerializeField] private Collider2D basketAreaCollider;

        [Header("输入")]
        [Tooltip("滚轮是否参与旋转（任意方向都算顺时针）")]
        [SerializeField] private bool useMouseScroll = true;

        [Header("旋转按钮（移动端）")]
        [Tooltip("拖拽可旋转灵石时显示的按钮组（顺时针 / 逆时针）；为空则永不显示")]
        [SerializeField] private GameObject rotateControls;
        [SerializeField] private Button rotateClockwiseButton;
        [SerializeField] private Button rotateCounterClockwiseButton;
        [Tooltip("PC / 编辑器上也显示旋转按钮（默认只在移动端显示，这里打开便于调试）")]
        [SerializeField] private bool showRotateControlsOnDesktop;

        [Header("旋转手势（移动端）")]
        [Tooltip("第二根手指滑动多少像素触发一次 90° 旋转；1080 高下 60px 约 1/18 屏")]
        [SerializeField] private float gestureStepPixels = 60f;

        [Header("触摸拖拽")]
        [Tooltip("触摸拖拽时把手上的灵石抬到手指上方的像素距离（0 = 不抬）。鼠标拖拽不受影响")]
        [SerializeField] private float dragLiftPixels = 80f;

        [Header("缩放")]
        [SerializeField] private float basketScale = 0.6f;
        [SerializeField] private float dragScale = 1f;
        [SerializeField] private float snapDuration = 0.15f;

        private LingShi draggingLingShi;
        private readonly List<LingShi> stashedLingShi = new List<LingShi>();
        private bool isDragging;
        private Vector3 dragOffset;
        private Camera cam;
        /// <summary>拖拽那根手指的 fingerId（桌面端为 -1），手势要排除它。</summary>
        private int dragFingerId = -1;
        /// <summary>正在执行旋转手势的第二根手指，-1 表示没有。</summary>
        private int gestureFingerId = -1;
        /// <summary>手势基准的屏幕 y；每触发一次旋转就重置到当前位置。</summary>
        private float gestureLastY;

        public bool IsDragging() { return isDragging; }

        /// <summary>当前正在拖拽的灵石（无则为 null）。调试与 UI 提示用。</summary>
        public LingShi GetDraggingLingShi() { return draggingLingShi; }

        private void Awake()
        {
            cam = Camera.main;
            // 移动端旋转按钮：点击走与键盘同一条 RotateDragging 路径，保证预览/校验一致
            if (rotateClockwiseButton != null)
            {
                rotateClockwiseButton.onClick.AddListener(() => RotateDragging(true));
            }
            if (rotateCounterClockwiseButton != null)
            {
                rotateCounterClockwiseButton.onClick.AddListener(() => RotateDragging(false));
            }
            HideRotateControls();
        }

        private void Update()
        {
            // 阶段 4：只在 GameManager 处于 Playing 时接受拖拽输入
            //（暂停 / 胜利 / 失败 / 未开始时不响应，避免暂停期间还能拖灵石）
            if (gameManager != null && !gameManager.IsPlaying())
            {
                // 暂停 / 结算时按钮也要收起来，否则会浮在面板上还能点
                HideRotateControls();
                return;
            }
            // 触屏和鼠标必须二选一：Unity 会把触摸映射成鼠标事件，两条路径同时跑时
            // Input.mousePosition 会被「当前活动的那根手指」抢走，表现为灵石在两根手指之间来回跳。
            if (Input.touchCount > 0)
            {
                HandleTouchInput();
            }
            else
            {
                HandleMouseInput();
            }

            if (isDragging)
            {
                HandleRotateInput();
                HandleRotateGesture();
            }
        }

        /// <summary>鼠标路径（PC）：拾取前先判断是否点在 UI 上，避免点按钮顺手把石头抓起来。</summary>
        private void HandleMouseInput()
        {
            if (Input.GetMouseButtonDown(1))
            {
                // 右键快速取下已放置的灵石（放回背篓，保持已煮），用于纠正放错的块
                TryEjectPlaced(Input.mousePosition);
            }
            if (Input.GetMouseButtonDown(0))
            {
                if (!IsPointerOverUI(-1))
                {
                    dragFingerId = -1;
                    OnMouseDown(Input.mousePosition);
                }
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
            }
        }

        /// <summary>
        /// 触摸路径：拖拽只跟随「拿起石头的那根手指」。
        /// 其它手指（第二根做旋转手势）无论怎么动都不会影响灵石位置——这是双指抢石头的根因修复。
        /// </summary>
        private void HandleTouchInput()
        {
            if (isDragging)
            {
                if (dragFingerId < 0)
                {
                    return;
                }
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.fingerId != dragFingerId)
                    {
                        continue;
                    }
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        OnMouseUp(touch.position);
                    }
                    else
                    {
                        OnMouseDrag(touch.position);
                    }
                    return;
                }
                // 拖拽手指没出现在本帧触摸列表里（系统取消）：就地放下，避免石头卡在手上
                OnMouseUp(draggingLingShi != null
                    ? (Vector2)cam.WorldToScreenPoint(draggingLingShi.transform.position)
                    : Vector2.zero);
                return;
            }

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began)
                {
                    continue;
                }
                // 点在按钮 / 面板上时不拾取（旋转按钮、暂停按钮都在屏幕内）
                if (IsPointerOverUI(touch.fingerId))
                {
                    continue;
                }
                dragFingerId = touch.fingerId;
                OnMouseDown(touch.position);
                break;
            }
        }

        /// <summary>指针是否落在 UGUI 上：触摸传 fingerId，鼠标传 -1。</summary>
        private static bool IsPointerOverUI(int fingerId)
        {
            EventSystem current = EventSystem.current;
            return current != null && current.IsPointerOverGameObject(fingerId);
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
            AudioManager.Instance?.PlaySfx(SfxId.Lift);
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
            // dragFingerId 由调用方（HandleMouseInput / HandleTouchInput）在拾取前设定：
            // 触摸时必须是真正按下石头的那根手指，不能假设是 GetTouch(0)。
            gestureFingerId = -1;
            dragOffset = ls.transform.position - mouseWorld;
            // 触摸拖拽：按下瞬间就把石头抬到手指上方，别让手指盖住它（鼠标不抬）
            Vector3 lift = ScreenLiftToWorld(CurrentDragLiftPixels());
            if (lift != Vector3.zero)
            {
                ls.transform.position = mouseWorld + dragOffset + lift;
            }
            // 用 Kinematic 而不是 simulated=false：后者会让 Collider 退出物理查询，
            // 导致已放置/暂存的灵石之后无法再被拾取
            ls.SetPhysicsBodyType(RigidbodyType2D.Kinematic);
            ls.SetVelocity(Vector2.zero);
            ls.SetTransparent(false);
            ls.SetTargetScale(dragScale);
            UpdatePreview();
            UpdateRotateControls();
        }

        public void OnMouseDrag(Vector2 screenPos)
        {
            if (draggingLingShi == null)
            {
                return;
            }
            Vector3 world = ScreenToWorld(screenPos);
            draggingLingShi.transform.position = world + dragOffset + ScreenLiftToWorld(CurrentDragLiftPixels());
            UpdatePreview();
        }

        /// <summary>触摸拖拽才抬升；鼠标拖拽（dragFingerId = -1）返回 0，保持原来跟手的表现。</summary>
        private float CurrentDragLiftPixels()
        {
            return dragFingerId >= 0 ? dragLiftPixels : 0f;
        }

        /// <summary>屏幕像素 → 世界单位的向上偏移（正交相机下与位置无关，可直接换算）。</summary>
        private Vector3 ScreenLiftToWorld(float pixels)
        {
            if (pixels <= 0f)
            {
                return Vector3.zero;
            }
            Camera camera = cam != null ? cam : Camera.main;
            if (camera == null)
            {
                return Vector3.zero;
            }
            Vector3 a = camera.WorldToScreenPoint(Vector3.zero);
            Vector3 b = camera.WorldToScreenPoint(Vector3.right);
            float pixelsPerUnit = Mathf.Abs(b.x - a.x);
            if (pixelsPerUnit <= 0.0001f)
            {
                return Vector3.zero;
            }
            return Vector3.up * (pixels / pixelsPerUnit);
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
                    // 2026-10-24 用户反馈：完全按「形状 + 同洞」判定放置（取消来源洞绑定），
                    // 自由放置优先，拼错可点选取下重摆。
                    gridManager.FillCells(ls, anchor.x, anchor.y);
                    AudioManager.Instance?.PlaySfx(SfxId.Place);
                    RequestSupplyCheck();
                    ls.SetTransparent(false);
                    ls.SetTargetScale(1f);
                    StartCoroutine(SnapToCell(ls, anchor));
                    EndDrag();
                    return;
                }
                if (gridManager.IsInside(anchor.x, anchor.y))
                {
                    // 落在网格范围内但放不下（形状不符 / 缺口不匹配）
                    AudioManager.Instance?.PlaySfx(SfxId.Invalid);
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
            dragFingerId = -1;
            gestureFingerId = -1;
            ClearPreview();
            HideRotateControls();
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

        /// <summary>
        /// 拖拽中的旋转输入：A 逆时针、D 顺时针（左右方向直觉，儿童玩家不用记哪个是顺）。
        /// 滚轮不区分方向，任意滚动都算顺时针——滚轮分方向后「上滚」在键盘与滚轮两条路径里
        /// 会给出相反结果，反而更容易误操作（2026-10-28 用户确认）。
        /// </summary>
        private void HandleRotateInput()
        {
            if (draggingLingShi == null || !draggingLingShi.CanRotate())
            {
                return;
            }
            // 键位来自 GameSettings（可在设置面板里改，默认 D / A）
            bool clockwise = Input.GetKeyDown(GameSettings.RotateClockwiseKey);
            bool counterClockwise = Input.GetKeyDown(GameSettings.RotateCounterClockwiseKey);
            if (useMouseScroll && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
            {
                clockwise = true;
            }

            if (clockwise)
            {
                RotateDragging(true);
            }
            else if (counterClockwise)
            {
                RotateDragging(false);
            }
        }

        /// <summary>
        /// 旋转当前拖拽的灵石并刷新预览；供键盘输入与移动端按钮共用。
        /// 未拖拽 / 未煮熟 / 单格形状时安全返回（CanRotate 已含这些判断）。
        /// </summary>
        public void RotateDragging(bool clockwise)
        {
            if (draggingLingShi == null || !draggingLingShi.CanRotate())
            {
                return;
            }
            if (clockwise)
            {
                draggingLingShi.Rotate();
            }
            else
            {
                draggingLingShi.RotateCounterClockwise();
            }
            UpdatePreview();
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
            // 三态预览：绿=可放且本洞清单还有这个形状；黄=可放但清单里没有这个形状；红=不可放
            PreviewHint hint = MatchChecker.GetPreviewHint(draggingLingShi, anchor.x, anchor.y, gridManager);
            gridVisual.ShowPreview(cells, hint);
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
            // 灵石落回背篓的「放石头」音效（拖回篓里、锅满退回都走这里）
            AudioManager.Instance?.PlaySfx(SfxId.Put);
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
            dragFingerId = -1;
            gestureFingerId = -1;
            stashedLingShi.Clear();
            ClearPreview();
            HideRotateControls();
        }

        /// <summary>
        /// 移动端双指旋转手势：第一根手指拖石头，第二根手指上滑逆时针 / 下滑顺时针。
        /// 每滑 gestureStepPixels 触发一次 90°，触发后把基准点重置为当前位置，可以连续转。
        /// 桌面端 touchCount 恒为 0，此方法直接返回，不影响键鼠操作。
        /// </summary>
        private void HandleRotateGesture()
        {
            if (!GameSettings.RotateGestureEnabled || draggingLingShi == null || !draggingLingShi.CanRotate())
            {
                gestureFingerId = -1;
                return;
            }
            // 必须有两根手指：一根按住拖拽，另一根做手势
            if (Input.touchCount < 2)
            {
                gestureFingerId = -1;
                return;
            }

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.fingerId == dragFingerId)
                {
                    continue;
                }
                // 落在按钮 / 面板上的手指不参与旋转手势（否则在按钮上滑动会误触）
                if (IsPointerOverUI(touch.fingerId))
                {
                    if (touch.fingerId == gestureFingerId)
                    {
                        gestureFingerId = -1;
                    }
                    continue;
                }

                if (touch.fingerId != gestureFingerId)
                {
                    // 换了一根手指：只记基准，等它真的滑动再触发，避免一按下就转
                    gestureFingerId = touch.fingerId;
                    gestureLastY = touch.position.y;
                    return;
                }

                float delta = touch.position.y - gestureLastY;
                if (Mathf.Abs(delta) >= Mathf.Max(1f, gestureStepPixels))
                {
                    RotateDragging(IsClockwiseGesture(delta));
                    gestureLastY = touch.position.y;
                }
                return;
            }
        }

        /// <summary>
        /// 手势方向判定：屏幕坐标 y 向上为正，所以上滑（deltaY &gt; 0）是逆时针、下滑是顺时针。
        /// 抽成静态方法便于单测，防止符号写反（写反的话上下滑会完全颠倒）。
        /// </summary>
        public static bool IsClockwiseGesture(float deltaY)
        {
            return deltaY < 0f;
        }

        /// <summary>
        /// 是否允许显示旋转按钮：设置里选「手势」时不显示；默认只在移动端出现，
        /// PC 上靠 showRotateControlsOnDesktop 打开便于调试。
        /// </summary>
        private bool ShouldShowRotateControls()
        {
            return rotateControls != null
                   && GameSettings.RotateButtonsEnabled
                   && (Application.isMobilePlatform || showRotateControlsOnDesktop);
        }

        /// <summary>按当前拖拽状态刷新旋转按钮显隐（只有拖着一块已煮灵石时才出现）。</summary>
        private void UpdateRotateControls()
        {
            if (rotateControls == null)
            {
                return;
            }
            bool show = ShouldShowRotateControls()
                        && draggingLingShi != null
                        && draggingLingShi.CanRotate();
            if (rotateControls.activeSelf != show)
            {
                rotateControls.SetActive(show);
            }
        }

        private void HideRotateControls()
        {
            if (rotateControls != null && rotateControls.activeSelf)
            {
                rotateControls.SetActive(false);
            }
        }

        /// <summary>
        /// 右键快速取下：找到指针下已放置的灵石，从洞口取下并直接放回背篓（保持 Cooked）。
        /// 用于「洞口已满但形状组合不符」时快速纠正，不用重新拖拽。
        /// 公开以便调试脚本直接调用（正常入口是鼠标右键）。
        /// </summary>
        public void TryEjectPlaced(Vector2 screenPos)
        {
            Vector3 world = ScreenToWorld(screenPos);
            Collider2D[] hits = Physics2D.OverlapPointAll(world);
            LingShi target = null;
            int bestOrder = int.MinValue;
            foreach (Collider2D hit in hits)
            {
                LingShi ls = hit.GetComponentInParent<LingShi>();
                if (ls == null || ls.GetState() != LingShiState.Placed)
                {
                    continue;
                }
                SpriteRenderer sr = hit.GetComponentInParent<SpriteRenderer>();
                int order = sr != null ? sr.sortingOrder : 0;
                if (order > bestOrder)
                {
                    target = ls;
                    bestOrder = order;
                }
            }
            if (target == null)
            {
                return;
            }
            gridManager.UnfillCells(target);
            target.SetState(LingShiState.Cooked);
            inventoryManager.ReturnToBasket(target);
            RequestSupplyCheck();
            Debug.Log("[DragController] 右键取下了一块灵石（已放回背篓）");
        }

        /// <summary>
        /// 丢弃一块石头（方案 D 预留出口）：从库存移除并销毁，不返还灵气。
        /// 目前未绑定到玩家输入，等废石观测数据出来再决定是否开放。
        /// </summary>
        public bool TryDiscard(LingShi ls)
        {
            if (ls == null || inventoryManager == null)
            {
                return false;
            }
            if (ls.GetState() == LingShiState.Placed)
            {
                return false; // 已放进洞里的不能直接丢，先取下
            }
            bool ok = inventoryManager.TryDiscard(ls);
            if (ok)
            {
                Debug.Log("[DragController] 丢弃了一块石头");
            }
            return ok;
        }

        /// <summary>节流触发一次补给账算（放置/取下后调用，避免每帧都算）。</summary>
        private void RequestSupplyCheck()
        {
            if (gridManager == null || inventoryManager == null)
            {
                return;
            }
            if (Time.unscaledTime - lastSupplyCheck < supplyCheckInterval)
            {
                return;
            }
            lastSupplyCheck = Time.unscaledTime;
            inventoryManager.EnsureSupply(gridManager.GetLevelConfig());
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
