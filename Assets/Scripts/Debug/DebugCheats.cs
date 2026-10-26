using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 调试快捷键（仅测试用，正式版本可移除本组件）：
    /// - Q：加灵气（默认 +20，便于测试返还/上限）
    /// - T：开关灵气消耗（关闭后可从容测试摆放）
    /// - 空格：生成一个新洞，并给背篓发放对应的灵石
    /// - K：整个网格变成一个大洞（渲染/性能测试）
    /// - L：重开关卡
    /// - I：重新生成背篓初始灵石
    /// - H：把鼠标所指洞口里已放置的灵石全部取下并放回背篓（纠正误放/死局）
    /// - J：打印所有洞口的形状清单、清单总格数与 CanTile 可解性（排查「这个洞要什么形状、能不能拼」）
    /// </summary>
    public class DebugCheats : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private QiManager qiManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private GridVisual gridVisual;
        [SerializeField] private InventoryManager inventoryManager;
        [Tooltip("按 Q 每次增加的灵气量")]
        [SerializeField] private float qiAddAmount = 20f;

        private void Update()
        {
            if (qiManager != null && Input.GetKeyDown(KeyCode.Q))
            {
                qiManager.AddQi(qiAddAmount);
                Debug.Log($"[Cheats] 灵气 +{qiAddAmount}（当前 {qiManager.GetCurrentQi():F1}/{qiManager.GetMaxQi():F1}）");
            }

            if (qiManager != null && Input.GetKeyDown(KeyCode.T))
            {
                bool enabled = !qiManager.IsConsumptionEnabled();
                qiManager.SetConsumptionEnabled(enabled);
                Debug.Log($"[Cheats] 灵气消耗{(enabled ? "开启" : "关闭")}");
            }

            if (gridManager != null && Input.GetKeyDown(KeyCode.Space))
            {
                Hole hole = gridManager.SpawnHoleForDebug(); // 内部已按「新增格子」发石
                if (hole != null)
                {
                    if (gridVisual != null)
                    {
                        gridVisual.RefreshAll();
                    }
                    Debug.Log($"[Cheats] 新洞 #{hole.GetHoleId()}（{hole.GetRequiredCellCount()} 格）");
                }
                else
                {
                    Debug.Log("[Cheats] 生成新洞失败（网格空间不足）");
                }
            }

            if (gridManager != null && Input.GetKeyDown(KeyCode.K))
            {
                Hole hole = gridManager.DebugFillAllAsHoles();
                if (gridVisual != null)
                {
                    gridVisual.RefreshAll();
                }
                Debug.Log($"[Cheats] 全洞 {hole.GetRequiredCellCount()} 格");
            }

            if (gameManager != null && Input.GetKeyDown(KeyCode.L))
            {
                gameManager.RestartLevel();
                Debug.Log("[Cheats] 重开关卡");
            }

            if (inventoryManager != null && gameManager != null && Input.GetKeyDown(KeyCode.I))
            {
                inventoryManager.GenerateInitialInventory(gameManager.GetLevelConfig(), gridManager);
                Debug.Log("[Cheats] 背篓灵石已重发");
            }

            // H：取下鼠标所指洞口的全部已放置灵石（洞口满但形状不符时用于重摆）
            if (gridManager != null && inventoryManager != null && Input.GetKeyDown(KeyCode.H))
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 world = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y,
                        Mathf.Abs(cam.transform.position.z)));
                    Vector2Int cell = gridManager.WorldToGrid(world);
                    Hole hole = gridManager.GetHoleAt(cell.x, cell.y);
                    if (hole != null)
                    {
                        int count = 0;
                        foreach (LingShi ls in inventoryManager.GetAll())
                        {
                            if (ls == null || ls.GetState() != LingShiState.Placed) continue;
                            var placedCells = ls.GetPlacedCells();
                            if (placedCells.Count == 0) continue;
                            // 用实际放置位置判断属于哪个洞（自由放置下不能依赖 sourceHoleId）
                            var firstCell = placedCells[0];
                            if (gridManager.GetHoleAt(firstCell.x, firstCell.y) != hole) continue;
                            gridManager.UnfillCells(ls);
                            ls.SetState(LingShiState.Cooked);
                            inventoryManager.ReturnToBasket(ls);
                            count++;
                        }
                        Debug.Log($"[Cheats] 已取下洞#{hole.GetHoleId()} 的 {count} 块灵石");
                    }
                    else
                    {
                        Debug.Log("[Cheats] 鼠标下没有洞口");
                    }
                }
            }

            // J：打印所有洞口的形状清单与可解性（不知道某个洞该拼什么形状时用）
            if (Input.GetKeyDown(KeyCode.J))
            {
                LogHoleShapeLists();
            }
        }

        /// <summary>
        /// 打印场上每个洞口的形状清单、清单总格数与 CanTile 可解性。
        /// 用于回答「这个洞到底需要哪些形状、这套形状能不能铺满它」，避免靠预览颜色去猜。
        /// 公开方法，便于在 Play Mode 里用脚本直接调用验证。
        /// </summary>
        public void LogHoleShapeLists()
        {
            if (gridManager == null)
            {
                Debug.Log("[Cheats] gridManager 未接线，无法打印洞口清单");
                return;
            }
            List<Hole> holes = gridManager.GetAllHoles();
            Debug.Log($"[Cheats] ===== 洞口清单诊断（共 {holes.Count} 个洞，■=缺口格 ·=实心）=====");
            foreach (Hole hole in holes)
            {
                List<Vector2Int> cells = hole.GetCells();
                List<List<Vector2Int>> entries = hole.GetRequiredShapeEntries();
                int listCells = 0;
                foreach (List<Vector2Int> entry in entries)
                {
                    listCells += entry.Count;
                }

                string solvable = entries.Count == 0
                    ? "未登记清单，跳过"
                    : (ShapeCutter.CanTile(cells, entries) ? "可解" : "不可解");

                int ownPieces = 0;
                if (inventoryManager != null)
                {
                    foreach (LingShi ls in inventoryManager.GetAll())
                    {
                        if (ls != null && ls.GetSourceHoleId() == hole.GetHoleId())
                        {
                            ownPieces++;
                        }
                    }
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"洞#{hole.GetHoleId()}：{cells.Count} 格 | 已填 {hole.GetNowCellCount()} 格、" +
                              $"已放 {hole.GetPlacedLingShi().Count} 块 | CanTile={solvable} | 存世本洞灵石 {ownPieces} 块");
                AppendShapeDraw(sb, cells, "  ");
                sb.AppendLine($"  清单 {entries.Count} 条 / {listCells} 格：");
                for (int i = 0; i < entries.Count; i++)
                {
                    sb.AppendLine($"    [{i + 1}] {ShapeDescriber.Describe(entries[i])}（{entries[i].Count} 格）");
                    AppendShapeDraw(sb, entries[i], "       ");
                }
                if (entries.Count > 0 && listCells != cells.Count)
                {
                    sb.AppendLine($"  ⚠ 清单格数({listCells}) ≠ 洞格数({cells.Count})，可能被重复切石登记");
                }
                Debug.Log("[Cheats]\n" + sb);
            }
        }

        /// <summary>把一组格子画成字符图（■=形状格，·=包围盒内的空格），便于用眼睛核对形状。</summary>
        private static void AppendShapeDraw(System.Text.StringBuilder sb, List<Vector2Int> cells, string indent)
        {
            if (cells == null || cells.Count == 0)
            {
                sb.AppendLine(indent + "（空）");
                return;
            }
            int minRow = int.MaxValue, maxRow = int.MinValue, minCol = int.MaxValue, maxCol = int.MinValue;
            foreach (Vector2Int cell in cells)
            {
                if (cell.x < minRow) minRow = cell.x;
                if (cell.x > maxRow) maxRow = cell.x;
                if (cell.y < minCol) minCol = cell.y;
                if (cell.y > maxCol) maxCol = cell.y;
            }
            var set = new HashSet<Vector2Int>(cells);
            for (int r = minRow; r <= maxRow; r++)
            {
                sb.Append(indent);
                for (int c = minCol; c <= maxCol; c++)
                {
                    sb.Append(set.Contains(new Vector2Int(r, c)) ? "■" : "·");
                }
                sb.AppendLine();
            }
        }
    }
}
