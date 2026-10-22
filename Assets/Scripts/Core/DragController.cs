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

        public bool IsDragging() { return isDragging; }

        private void Awake()
        {
            cam = Camera.main;
        }

        private void Update()
        {
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

            // ② 网格放置（仅 Cooked）
            if (ls.GetState() == LingShiState.Cooked)
            {
                Vector2Int anchor = gridManager.WorldToGrid(ls.transform.position);
                Hole targetHole = MatchChecker.GetMatchedHole(ls, anchor.x, anchor.y, gridManager);
                if (targetHole != null)
                {
                    gridManager.FillCells(ls, anchor.x, anchor.y);
                    ls.SetTransparent(false);
                    ls.SetTargetScale(1f);
                    StartCoroutine(SnapToCell(ls, anchor));
                    EndDrag();
                    return;
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
            if (draggingLingShi == null || draggingLingShi.GetState() != LingShiState.Cooked)
            {
                ClearPreview();
                return;
            }
            Vector2Int anchor = gridManager.WorldToGrid(draggingLingShi.transform.position);
            List<Vector2Int> cells = MatchChecker.GetMatchedCells(draggingLingShi, anchor.x, anchor.y, gridManager);
            bool valid = MatchChecker.CanPlace(draggingLingShi, anchor.x, anchor.y, gridManager);
            bool wrongHole = !valid && MatchChecker.IsShapeFitsOtherHole(draggingLingShi, anchor.x, anchor.y, gridManager);
            gridVisual.ShowPreview(cells, valid, wrongHole);
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
