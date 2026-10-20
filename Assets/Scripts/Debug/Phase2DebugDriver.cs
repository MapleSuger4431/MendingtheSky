using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 阶段 2 验证驱动：网格 + 洞 + 灵石生成到背篓。
    /// 空格=生成新洞（并发放灵石）、K=全洞测试、L=重开关卡、I=重新生成背篓初始灵石。
    /// </summary>
    public class Phase2DebugDriver : MonoBehaviour
    {
        [SerializeField] private LevelConfig levelConfig;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private GridVisual gridVisual;
        [SerializeField] private InventoryManager inventoryManager;

        private void Start()
        {
            if (levelConfig == null || gridManager == null || gridVisual == null || inventoryManager == null)
            {
                Debug.LogError("[Phase2DebugDriver] levelConfig / gridManager / gridVisual / inventoryManager 需在 Inspector 中赋值");
                enabled = false;
                return;
            }
            RegenerateLevel();
        }

        private void RegenerateLevel()
        {
            gridManager.GenerateLevel(levelConfig);
            gridVisual.Init(gridManager);
            gridVisual.RefreshAll();
            inventoryManager.GenerateInitialInventory(levelConfig, gridManager);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Hole hole = gridManager.SpawnHoleForDebug();
                if (hole != null)
                {
                    gridVisual.RefreshAll();
                    inventoryManager.AddLingShiFromHole(hole, levelConfig);
                    Debug.Log($"[Phase2DebugDriver] 生成新洞 #{hole.GetHoleId()}，{hole.GetRequiredCellCount()} 格，灵石已发放");
                }
                else
                {
                    Debug.Log("[Phase2DebugDriver] 生成失败（网格空间不足或重试耗尽）");
                }
            }
            if (Input.GetKeyDown(KeyCode.K))
            {
                Hole hole = gridManager.DebugFillAllAsHoles();
                gridVisual.RefreshAll();
                Debug.Log($"[Phase2DebugDriver] K 测试：全部 {hole.GetRequiredCellCount()} 格变洞");
            }
            if (Input.GetKeyDown(KeyCode.L))
            {
                RegenerateLevel();
                Debug.Log($"[Phase2DebugDriver] L 测试：重开关卡 {gridManager.GetRows()}x{gridManager.GetCols()}");
            }
            if (Input.GetKeyDown(KeyCode.I))
            {
                inventoryManager.GenerateInitialInventory(levelConfig, gridManager);
                Debug.Log("[Phase2DebugDriver] I 测试：背篓灵石已重新生成");
            }
        }

        private void OnGUI()
        {
            if (gridManager == null || inventoryManager == null)
            {
                return;
            }
            GUILayout.BeginArea(new Rect(10, 10, 340, 200), GUI.skin.box);
            GUILayout.Label($"网格：{gridManager.GetRows()} x {gridManager.GetCols()}，cellSize {gridManager.GetCellSize():F2}");
            GUILayout.Label($"场上洞数：{gridManager.GetUnfilledHoleCount()}，配额：{gridManager.GetGeneratedHoleCount()}");
            GUILayout.Label($"空缺格数：{gridManager.GetUnfilledCellCount()}");
            GUILayout.Label($"背篓灵石：{inventoryManager.GetAll().Count}（未煮 {inventoryManager.GetInBasketCount()} / 已煮 {inventoryManager.GetCookedCount()}）");
            GUILayout.Label("空格=新洞　K=全洞　L=重开　I=重发灵石");
            GUILayout.EndArea();
        }
    }
}
