using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 灵石：由若干方块（blockInstances）按形状拼成。根物体持 Rigidbody2D，方块各带 Collider2D。
    /// 锚点 = 形状 (0,0) 格；方块偏移为 (col*cellSize, -row*cellSize)。
    /// 状态→视觉：InBasket=dust；Cooking/Placed/Locked=normal；Cooked=glow。
    /// </summary>
    public class LingShi : MonoBehaviour
    {
        [Header("方块视觉（由 prefab 预置）")]
        [SerializeField] private GameObject blockPrefab;
        [SerializeField] private Transform blockContainer;
        [Tooltip("填充层相对格子的放大系数（>1 让相邻块贴合无缝）")]
        [SerializeField] private float blockOverlap = 1.03f;
        [Tooltip("共享外轮廓贴图：每格一块、稍大、垫底；多格拼合形成整块轮廓")]
        [SerializeField] private Sprite outlineSprite;
        [SerializeField] private float outlineOverlap = 1.32f;

        /// <summary>每块灵石独立的排序基准（轮廓=base，填充=base+1），由 InventoryManager 分配。</summary>
        private int sortingBase = 5;

        private LingShiTypeConfig typeConfig;
        private LingShiShape shape;
        private LingShiState state = LingShiState.InBasket;
        private float cookElapsed;
        private Vector3 originPosition;
        private readonly List<Vector2Int> placedCells = new List<Vector2Int>();
        private int sourceHoleId = -1;
        private float displayScale = 1f;
        private float targetScale = 1f;
        private float cellSize = 1f;
        private readonly List<GameObject> blockInstances = new List<GameObject>();
        private Rigidbody2D body;

        public void Init(LingShiTypeConfig cfg, LingShiShape shape, Vector3 originPos, float scale, float cellSize)
        {
            typeConfig = cfg;
            this.shape = shape;
            originPosition = originPos;
            displayScale = scale;
            targetScale = scale;
            this.cellSize = cellSize;
            state = LingShiState.InBasket;
            cookElapsed = 0f;
            placedCells.Clear();
            body = GetComponent<Rigidbody2D>();
            transform.position = originPos;
            transform.localScale = Vector3.one * displayScale;
            RebuildVisual();
        }

        private void Update()
        {
            UpdateScale(Time.deltaTime);
        }

        public void Rotate()
        {
            if (!CanRotate())
            {
                return;
            }
            shape.Rotate();
            RebuildVisual();
        }

        public List<Vector2Int> GetCurrentCells() { return shape.GetCurrentCells(); }

        /// <summary>形状的基础格（未旋转）。可解性校验用：求解器会自行尝试 4 种旋转。</summary>
        public List<Vector2Int> GetBaseCells() { return shape != null ? shape.GetBaseCells() : new List<Vector2Int>(); }

        /// <summary>形状的规范化签名（旋转/平移无关），用于洞口形状清单匹配。</summary>
        public string GetShapeSignature() { return ShapeCutter.GetCanonicalSignature(GetBaseCells()); }

        /// <summary>设置本灵石的独立排序基准（轮廓=base，填充=base+1）。</summary>
        public void SetSortingBase(int value)
        {
            sortingBase = value;
            RebuildVisual();
        }
        public int GetRotationSteps() { return shape.GetRotationSteps(); }
        public LingShiTypeConfig GetTypeConfig() { return typeConfig; }
        public LingShiState GetState() { return state; }

        public void SetState(LingShiState newState)
        {
            if (state == newState)
            {
                return;
            }
            state = newState;
            RebuildVisual();
        }

        public void StartCooking()
        {
            cookElapsed = 0f;
            SetState(LingShiState.Cooking);
        }

        public void UpdateCooking(float dt)
        {
            if (state != LingShiState.Cooking)
            {
                return;
            }
            cookElapsed += dt;
            if (IsCooked())
            {
                SetState(LingShiState.Cooked);
            }
        }

        public bool IsCooked()
        {
            if (typeConfig == null)
            {
                return false;
            }
            return cookElapsed >= typeConfig.GetCookTime();
        }

        public float GetCookProgress()
        {
            if (typeConfig == null || typeConfig.GetCookTime() <= 0f)
            {
                return 1f;
            }
            return Mathf.Clamp01(cookElapsed / typeConfig.GetCookTime());
        }

        public void MarkPlaced(List<Vector2Int> cells)
        {
            placedCells.Clear();
            placedCells.AddRange(cells);
            state = LingShiState.Placed;
            RebuildVisual();
        }

        public List<Vector2Int> GetPlacedCells() { return new List<Vector2Int>(placedCells); }

        /// <summary>必需灵石绑定切出它的洞（评审 P0-6）；冗余灵石为 -1，可放任意洞。</summary>
        public void SetSourceHoleId(int id) { sourceHoleId = id; }
        public int GetSourceHoleId() { return sourceHoleId; }

        public void ReturnToOrigin()
        {
            transform.position = originPosition;
        }

        /// <summary>阶段 5 由闭合动画驱动，这里仅占位。</summary>
        public void FadeIntoBackground()
        {
            SetTransparent(true);
        }

        /// <summary>形状旋转是数据驱动（预计算 4 份），视觉无需再对齐。</summary>
        public void AlignVisualToNearest90()
        {
        }

        public void SetTargetScale(float scale)
        {
            targetScale = scale;
        }

        public float GetDisplayScale()
        {
            return displayScale;
        }

        public void UpdateScale(float dt)
        {
            if (Mathf.Approximately(displayScale, targetScale))
            {
                return;
            }
            displayScale = Mathf.MoveTowards(displayScale, targetScale, dt * 3f);
            transform.localScale = Vector3.one * displayScale;
        }

        public void SetTransparent(bool isTransparent)
        {
            SetBlockAlpha(isTransparent ? 0.5f : 1f);
        }

        public void SetPreviewColor(Color color)
        {
            SetBlockColor(color);
        }

        public void ClearPreviewColor()
        {
            SetBlockColor(Color.white);
        }

        public bool CanDragToPot() { return state == LingShiState.InBasket; }
        public bool CanDragToGrid() { return state == LingShiState.Cooked; }
        public bool CanPickFromGrid() { return state == LingShiState.Placed; }
        public bool CanRotate() { return state == LingShiState.Cooked; }

        /// <summary>洞口闭合时调用：锁定并禁用所有方块碰撞器。</summary>
        public void DisableInteraction()
        {
            state = LingShiState.Locked;
            foreach (GameObject block in blockInstances)
            {
                if (block == null)
                {
                    continue;
                }
                foreach (Collider2D collider in block.GetComponentsInChildren<Collider2D>())
                {
                    collider.enabled = false;
                }
            }
        }

        public void SetPhysicsBodyType(RigidbodyType2D type)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }
            if (body != null)
            {
                body.bodyType = type;
            }
        }

        public void SetSimulated(bool simulated)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }
            if (body != null)
            {
                body.simulated = simulated;
            }
        }

        public void SetVelocity(Vector2 velocity)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }
            if (body != null)
            {
                body.velocity = velocity;
            }
        }

        /// <summary>按当前形状与状态重建所有方块（旋转/状态变化时调用）。</summary>
        public void RebuildVisual()
        {
            ClearBlockInstances();
            if (shape == null || blockPrefab == null)
            {
                return;
            }
            if (blockContainer == null)
            {
                blockContainer = transform;
            }
            Sprite fillSprite = GetStateSprite();
            foreach (Vector2Int cell in shape.GetCurrentCells())
            {
                Vector3 localPos = new Vector3(cell.y * cellSize, -cell.x * cellSize, 0f);
                // 先画轮廓（垫底），再画填充（在上），多格拼合后内部轮廓被填充盖住，只剩外缘一圈
                CreateBlock(outlineSprite, localPos, outlineOverlap, sortingBase, Color.white, true);
                CreateBlock(fillSprite, localPos, blockOverlap, sortingBase + 1, Color.white, false);
            }
        }

        private void CreateBlock(Sprite sprite, Vector3 localPos, float overlap, int sortingOrder,
            Color color, bool outline)
        {
            if (sprite == null)
            {
                return;
            }
            GameObject block = Instantiate(blockPrefab, blockContainer);
            block.transform.localPosition = localPos;
            SpriteRenderer sr = block.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = sprite;
                Vector2 size = sprite.bounds.size;
                float maxDim = Mathf.Max(0.0001f, Mathf.Max(size.x, size.y));
                block.transform.localScale = Vector3.one * (cellSize * overlap / maxDim);
                sr.color = color;
                sr.sortingOrder = sortingOrder;
            }
            if (outline)
            {
                foreach (Collider2D collider in block.GetComponentsInChildren<Collider2D>())
                {
                    collider.enabled = false;
                }
            }
            blockInstances.Add(block);
        }

        private Sprite GetStateSprite()
        {
            if (typeConfig == null)
            {
                return null;
            }
            switch (state)
            {
                case LingShiState.InBasket:
                    return typeConfig.GetDustSprite();
                case LingShiState.Cooked:
                    return typeConfig.GetGlowSprite();
                default:
                    return typeConfig.GetNormalSprite();
            }
        }

        private void ClearBlockInstances()
        {
            foreach (GameObject block in blockInstances)
            {
                if (block == null)
                {
                    continue;
                }
                if (Application.isPlaying)
                {
                    Destroy(block);
                }
                else
                {
                    DestroyImmediate(block);
                }
            }
            blockInstances.Clear();
        }

        private void SetBlockAlpha(float alpha)
        {
            foreach (GameObject block in blockInstances)
            {
                if (block == null)
                {
                    continue;
                }
                foreach (SpriteRenderer sr in block.GetComponentsInChildren<SpriteRenderer>())
                {
                    Color c = sr.color;
                    c.a = alpha;
                    sr.color = c;
                }
            }
        }

        private void SetBlockColor(Color color)
        {
            foreach (GameObject block in blockInstances)
            {
                if (block == null)
                {
                    continue;
                }
                foreach (SpriteRenderer sr in block.GetComponentsInChildren<SpriteRenderer>())
                {
                    Color c = color;
                    c.a = sr.color.a;
                    sr.color = c;
                }
            }
        }
    }
}
