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
                Hole hole = gridManager.SpawnHoleForDebug();
                if (hole != null)
                {
                    if (gridVisual != null)
                    {
                        gridVisual.RefreshAll();
                    }
                    if (inventoryManager != null && gameManager != null)
                    {
                        inventoryManager.AddLingShiFromHole(hole, gameManager.GetLevelConfig());
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
        }
    }
}
