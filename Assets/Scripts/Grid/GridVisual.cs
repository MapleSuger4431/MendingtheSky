using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 网格视觉：缺口渲染 + 边界特效渲染（阶段 1 范围）。
    /// 预览高亮与闭合动画在阶段 3/5 接入。
    /// 所有 prefab 字段都可为空：为空时用运行时生成的白 Sprite 染色占位，
    /// 保证原型期零美术资源也能跑（对应文档「纯色方块/简单 Sprite」原则）。
    /// </summary>
    public class GridVisual : MonoBehaviour
    {
        [Header("缺口渲染（可为空，空则用占位方块）")]
        [SerializeField] private GameObject holeCellPrefab;
        [SerializeField] private Transform holeCellRoot;
        [SerializeField] private Color fallbackHoleColor = new Color(0.15f, 0.1f, 0.3f, 0.85f);

        [Header("边界特效（可为空，空则用占位细条）")]
        [SerializeField] private GameObject edgeHorizontalPrefab;
        [SerializeField] private GameObject edgeVerticalPrefab;
        [SerializeField] private Transform edgeRoot;
        [SerializeField] private Color fallbackEdgeColor = new Color(0.5f, 0.9f, 1f, 0.9f);
        [SerializeField] private float fallbackEdgeThickness = 0.08f;

        [Header("渲染排序（对应文档：缺口=1，发光=2，边界=3）")]
        [SerializeField] private int holeSortingOrder = 1;
        [SerializeField] private int edgeGlowSortingOrder = 2;
        [SerializeField] private int edgeSortingOrder = 3;
        [SerializeField] private float edgeGlowWidthMultiplier = 2.4f;
        [SerializeField, Range(0f, 1f)] private float edgeGlowAlpha = 0.28f;

        [Header("缺口自动瓦片：16 变体，按「上下左右是否同洞」位掩码选图（1=上 2=下 4=左 8=右）")]
        [SerializeField] private Sprite[] holeCellVariants = new Sprite[16];

        [Header("边界光带：目标厚度占格子的比例 & 两张光条贴图内发光带的占比（实测）")]
        [SerializeField] private float edgeBandThickness = 0.22f;
        [SerializeField] private float edgeBandFractionH = 0.177f;
        [SerializeField] private float edgeBandFractionV = 0.21f;

        [Header("逐洞微色调：用于区分相邻的洞（第 0 个为原色）")]
        [SerializeField] private Color[] holeTints =
        {
            new Color(1f, 1f, 1f),
            new Color(0.80f, 0.92f, 1f),
            new Color(1f, 0.82f, 0.90f),
            new Color(0.82f, 1f, 0.86f)
        };

        [Header("预览高亮（阶段 3）")]
        [SerializeField] private GameObject previewCellPrefab;
        [SerializeField] private Transform previewRoot;
        [SerializeField] private Color previewValidColor = new Color(0.4f, 1f, 0.55f, 0.55f);
        [SerializeField] private Color previewInvalidColor = new Color(1f, 0.35f, 0.35f, 0.55f);
        [SerializeField] private Color previewWrongHoleColor = new Color(1f, 0.75f, 0.25f, 0.55f);
        [SerializeField] private int previewSortingOrder = 4;

        private readonly List<GameObject> previewInstances = new List<GameObject>();

        private GridManager gridManager;
        private readonly Dictionary<Vector2Int, GameObject> holeCellInstances = new Dictionary<Vector2Int, GameObject>();
        private readonly List<GameObject> edgeInstances = new List<GameObject>();
        private Sprite fallbackSprite;

        /// <summary>阶段 4 起签名会扩展为 Init(gridManager, gameManager, levelConfig)。</summary>
        public void Init(GridManager gridManager)
        {
            this.gridManager = gridManager;
            if (holeCellRoot == null)
            {
                holeCellRoot = CreateRoot("HoleCellRoot");
            }
            if (edgeRoot == null)
            {
                edgeRoot = CreateRoot("EdgeRoot");
            }
            if (previewRoot == null)
            {
                previewRoot = CreateRoot("PreviewRoot");
            }
        }

        /// <summary>
        /// 全量重建：清空后按 GridManager 当前状态重建所有缺口与边界。
        /// 原型期洞数量级小，任何变化（新洞/合并）直接全量刷，简单可靠。
        /// </summary>
        public void RefreshAll()
        {
            ClearAll();
            if (gridManager == null)
            {
                return;
            }
            foreach (Hole hole in gridManager.GetAllHoles())
            {
                RefreshHole(hole);
                RebuildBoundaryEdges(hole);
            }
        }

        /// <summary>
        /// 按文档逻辑：state==-1 销毁实例；state==0 或 1 确保实例存在（被灵石遮挡也保留）；
        /// state==2 保留实例等闭合动画（阶段 5 处理）。
        /// </summary>
        public void RefreshHole(Hole hole)
        {
            foreach (Vector2Int cell in hole.GetCells())
            {
                int state = gridManager.GetCell(cell.x, cell.y);
                if (state == GridManager.CellNormal)
                {
                    RemoveHoleCell(cell.x, cell.y);
                }
                else if (!holeCellInstances.ContainsKey(cell))
                {
                    CreateHoleCell(cell.x, cell.y, hole);
                }
            }
        }

        public void ClearAll()
        {
            foreach (GameObject instance in holeCellInstances.Values)
            {
                if (instance != null)
                {
                    DestroyInstance(instance);
                }
            }
            holeCellInstances.Clear();
            foreach (GameObject instance in edgeInstances)
            {
                if (instance != null)
                {
                    DestroyInstance(instance);
                }
            }
            edgeInstances.Clear();
        }

        /// <summary>
        /// 放置预览：isValid=绿色；形状匹配但来源洞不符=橙色；其他无效=红色。
        /// 越界格自动跳过。
        /// </summary>
        public void ShowPreview(List<Vector2Int> cells, bool isValid, bool shapeFitsOtherHole = false)
        {
            ClearPreview();
            if (cells == null || gridManager == null)
            {
                return;
            }
            Color color = isValid
                ? previewValidColor
                : (shapeFitsOtherHole ? previewWrongHoleColor : previewInvalidColor);
            foreach (Vector2Int cell in cells)
            {
                if (!gridManager.IsInside(cell.x, cell.y))
                {
                    continue;
                }
                previewInstances.Add(CreatePreviewCell(cell, color));
            }
        }

        public void ClearPreview()
        {
            foreach (GameObject instance in previewInstances)
            {
                if (instance != null)
                {
                    DestroyInstance(instance);
                }
            }
            previewInstances.Clear();
        }

        private GameObject CreatePreviewCell(Vector2Int cell, Color color)
        {
            Vector3 pos = gridManager.GridToWorld(cell.x, cell.y);
            float cellSize = gridManager.GetCellSize();
            GameObject instance;
            if (previewCellPrefab != null)
            {
                instance = Instantiate(previewCellPrefab, pos, Quaternion.identity, previewRoot);
                Vector2 spriteSize = GetSpriteSize(instance);
                instance.transform.localScale = Vector3.one * (cellSize * 0.95f / Mathf.Max(0.0001f, spriteSize.x));
                SpriteRenderer sr = instance.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = color;
                    sr.sortingOrder = previewSortingOrder;
                }
            }
            else
            {
                instance = new GameObject($"Preview_{cell.x}_{cell.y}");
                instance.transform.SetParent(previewRoot, false);
                instance.transform.position = pos;
                instance.transform.localScale = Vector3.one * (cellSize * 0.9f);
                SpriteRenderer sr = instance.AddComponent<SpriteRenderer>();
                sr.sprite = GetFallbackSprite();
                sr.color = color;
                sr.sortingOrder = previewSortingOrder;
            }
            return instance;
        }

        private GameObject CreateHoleCell(int r, int c, Hole hole)
        {
            Vector3 pos = gridManager.GridToWorld(r, c);
            GameObject instance;
            float cellSize = gridManager.GetCellSize();
            if (holeCellPrefab != null)
            {
                instance = Instantiate(holeCellPrefab, pos, Quaternion.identity, holeCellRoot);
                // prefab 精灵按实际尺寸缩放到格子大小（占满一格）
                Vector2 spriteSize = GetSpriteSize(instance);
                float scale = cellSize * 0.95f / Mathf.Max(0.0001f, spriteSize.x);
                instance.transform.localScale = Vector3.one * scale;

                SpriteRenderer sr = instance.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    Sprite variant = GetHoleVariant(r, c, hole);
                    if (variant != null)
                    {
                        sr.sprite = variant;
                        // 变体尺寸可能与模板不同，重新按变体尺寸缩放；1.01 微重叠消除格间细缝
                        float variantScale = cellSize * 1.01f / Mathf.Max(0.0001f, variant.bounds.size.x);
                        instance.transform.localScale = Vector3.one * variantScale;
                    }
                    sr.color = Color.white; // 洞主体统一底色；逐洞色调只作用于外发光带
                }
            }
            else
            {
                instance = new GameObject($"HoleCell_{r}_{c}");
                instance.transform.SetParent(holeCellRoot, false);
                instance.transform.position = pos;
                instance.transform.localScale = Vector3.one * (cellSize * 0.95f);
                SpriteRenderer sr = instance.AddComponent<SpriteRenderer>();
                sr.sprite = GetFallbackSprite();
                sr.color = fallbackHoleColor * GetHoleTint(hole);
                sr.sortingOrder = holeSortingOrder;
            }
            holeCellInstances[new Vector2Int(r, c)] = instance;
            return instance;
        }

        /// <summary>
        /// 按上下左右是否为「同一个洞」的格子计算位掩码并选变体：
        /// 1=上，2=下，4=左，8=右。内部格（掩码 15）无亮边，外缘格才有。
        /// </summary>
        private Sprite GetHoleVariant(int r, int c, Hole hole)
        {
            if (holeCellVariants == null || holeCellVariants.Length != 16)
            {
                return null;
            }
            int mask = 0;
            if (IsSameHole(r - 1, c, hole)) mask |= 1;
            if (IsSameHole(r + 1, c, hole)) mask |= 2;
            if (IsSameHole(r, c - 1, hole)) mask |= 4;
            if (IsSameHole(r, c + 1, hole)) mask |= 8;
            return holeCellVariants[mask];
        }

        private bool IsSameHole(int r, int c, Hole hole)
        {
            return gridManager.GetHoleAt(r, c) == hole;
        }

        private Color GetHoleTint(Hole hole)
        {
            if (holeTints == null || holeTints.Length == 0)
            {
                return Color.white;
            }
            return holeTints[Mathf.Abs(hole.GetHoleId()) % holeTints.Length];
        }

        private void RemoveHoleCell(int r, int c)
        {
            var key = new Vector2Int(r, c);
            if (holeCellInstances.TryGetValue(key, out GameObject instance))
            {
                holeCellInstances.Remove(key);
                if (instance != null)
                {
                    DestroyInstance(instance);
                }
            }
        }

        /// <summary>
        /// 边界特效方向偏移（按文档）：
        /// Up → (0, +cellSize/2)，Down → (0, -cellSize/2)，
        /// Left → (-cellSize/2, 0)，Right → (+cellSize/2, 0)。
        /// Up/Down 用横条，Left/Right 用竖条。
        /// </summary>
        private void RebuildBoundaryEdges(Hole hole)
        {
            float cellSize = gridManager.GetCellSize();
            foreach (BoundaryEdge edge in hole.GetBoundaryEdges())
            {
                Vector3 basePos = gridManager.GridToWorld(edge.cell.x, edge.cell.y);
                Vector3 pos = basePos + GetEdgeOffset(edge.direction);
                bool horizontal = edge.direction == EdgeDirection.Up || edge.direction == EdgeDirection.Down;
                GameObject prefab = horizontal ? edgeHorizontalPrefab : edgeVerticalPrefab;
                GameObject instance;
                if (prefab != null)
                {
                    instance = Instantiate(prefab, pos, Quaternion.identity, edgeRoot);
                    // 光条贴图是 16:9 大画布，发光带只占中间一部分：
                    // 按「目标带宽 = 格高 × edgeBandThickness」反算缩放，保证实际可见光带够粗
                    Vector2 spriteSize = GetSpriteSize(instance);
                    if (horizontal)
                    {
                        float sx = cellSize * 1.05f / Mathf.Max(0.0001f, spriteSize.x);
                        float sy = cellSize * edgeBandThickness
                                   / Mathf.Max(0.0001f, spriteSize.y * edgeBandFractionH);
                        instance.transform.localScale = new Vector3(sx, sy, 1f);
                    }
                    else
                    {
                        float sx = cellSize * edgeBandThickness
                                   / Mathf.Max(0.0001f, spriteSize.x * edgeBandFractionV);
                        float sy = cellSize * 1.05f / Mathf.Max(0.0001f, spriteSize.y);
                        instance.transform.localScale = new Vector3(sx, sy, 1f);
                    }
                    SpriteRenderer edgeSr = instance.GetComponentInChildren<SpriteRenderer>();
                    if (edgeSr != null)
                    {
                        edgeSr.color = GetHoleTint(hole);
                        edgeSr.sortingOrder = edgeSortingOrder;
                    }

                    // 伪 bloom：同款光带放大 2.4 倍、低透明度垫底，形成柔和外发光
                    GameObject glow = Instantiate(prefab, pos, Quaternion.identity, edgeRoot);
                    Vector3 coreScale = instance.transform.localScale;
                    glow.transform.localScale = horizontal
                        ? new Vector3(coreScale.x, coreScale.y * edgeGlowWidthMultiplier, 1f)
                        : new Vector3(coreScale.x * edgeGlowWidthMultiplier, coreScale.y, 1f);
                    SpriteRenderer glowSr = glow.GetComponentInChildren<SpriteRenderer>();
                    if (glowSr != null)
                    {
                        Color tint = GetHoleTint(hole);
                        glowSr.color = new Color(tint.r, tint.g, tint.b, edgeGlowAlpha);
                        glowSr.sortingOrder = edgeGlowSortingOrder;
                    }
                    edgeInstances.Add(glow);
                }
                else
                {
                    instance = new GameObject($"Edge_{edge.cell.x}_{edge.cell.y}_{edge.direction}");
                    instance.transform.SetParent(edgeRoot, false);
                    instance.transform.position = pos;
                    instance.transform.localScale = horizontal
                        ? new Vector3(cellSize, cellSize * fallbackEdgeThickness, 1f)
                        : new Vector3(cellSize * fallbackEdgeThickness, cellSize, 1f);
                    SpriteRenderer sr = instance.AddComponent<SpriteRenderer>();
                    sr.sprite = GetFallbackSprite();
                    sr.color = fallbackEdgeColor;
                    sr.sortingOrder = edgeSortingOrder;
                }
                edgeInstances.Add(instance);
            }
        }

        private Vector3 GetEdgeOffset(EdgeDirection direction)
        {
            float half = gridManager.GetCellSize() * 0.5f;
            switch (direction)
            {
                case EdgeDirection.Up: return new Vector3(0f, half, 0f);
                case EdgeDirection.Down: return new Vector3(0f, -half, 0f);
                case EdgeDirection.Left: return new Vector3(-half, 0f, 0f);
                case EdgeDirection.Right: return new Vector3(half, 0f, 0f);
                default: return Vector3.zero;
            }
        }

        /// <summary>1x1 白 Sprite（1 世界单位），运行时生成一次复用。</summary>
        private Sprite GetFallbackSprite()
        {
            if (fallbackSprite == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            return fallbackSprite;
        }

        /// <summary>读取实例上 SpriteRenderer 的精灵世界尺寸，用于把 prefab 缩放到格子大小。</summary>
        private static Vector2 GetSpriteSize(GameObject instance)
        {
            SpriteRenderer sr = instance.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                return sr.sprite.bounds.size;
            }
            return Vector2.one;
        }

        private Transform CreateRoot(string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            return root.transform;
        }

        private static void DestroyInstance(GameObject instance)
        {
            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }
    }
}
