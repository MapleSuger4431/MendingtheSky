using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 阶段 1 验证驱动（对应文档验证点：开局有洞，手动调 TrySpawnNewHole 能加新洞）。
    /// 用法：场景空物体挂载本脚本 + GridManager + GridVisual，Inspector 拖入 LevelConfig 资产。
    /// 运行：开局自动生成第一批洞；空格 = 手动生成一个新洞。
    /// </summary>
    public class Phase1DebugDriver : MonoBehaviour
    {
        [SerializeField] private LevelConfig levelConfig;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private GridVisual gridVisual;

        private void Start()
        {
            if (levelConfig == null || gridManager == null || gridVisual == null)
            {
                Debug.LogError("[Phase1DebugDriver] levelConfig / gridManager / gridVisual 需在 Inspector 中赋值");
                enabled = false;
                return;
            }
            gridManager.GenerateLevel(levelConfig);
            gridVisual.Init(gridManager);
            gridVisual.RefreshAll();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Hole hole = gridManager.SpawnHoleForDebug();
                if (hole != null)
                {
                    gridVisual.RefreshAll();
                    Debug.Log($"[Phase1DebugDriver] 生成新洞 #{hole.GetHoleId()}，格子数 {hole.GetRequiredCellCount()}");
                }
                else
                {
                    Debug.Log("[Phase1DebugDriver] 生成失败（网格空间不足或重试耗尽）");
                }
            }
            if (Input.GetKeyDown(KeyCode.K))
            {
                Hole hole = gridManager.DebugFillAllAsHoles();
                gridVisual.RefreshAll();
                Debug.Log($"[Phase1DebugDriver] K 测试：全部 {hole.GetRequiredCellCount()} 格变为洞口，边界 {hole.GetBoundaryEdges().Count} 条");
            }
            if (Input.GetKeyDown(KeyCode.L))
            {
                gridManager.GenerateLevel(levelConfig);
                gridVisual.RefreshAll();
                Debug.Log($"[Phase1DebugDriver] L 测试：关卡已重新生成 {gridManager.GetRows()}x{gridManager.GetCols()}");
            }
        }

        private void OnGUI()
        {
            if (gridManager == null)
            {
                return;
            }
            GUILayout.BeginArea(new Rect(10, 10, 320, 160), GUI.skin.box);
            GUILayout.Label($"网格：{gridManager.GetRows()} x {gridManager.GetCols()}");
            GUILayout.Label($"场上洞数：{gridManager.GetUnfilledHoleCount()}");
            GUILayout.Label($"已生成洞配额：{gridManager.GetGeneratedHoleCount()} / {(levelConfig != null && levelConfig.IsEndless() ? "∞" : levelConfig.GetTotalHoles().ToString())}");
            GUILayout.Label($"空缺格数（灵气消耗基数）：{gridManager.GetUnfilledCellCount()}");
            GUILayout.Label("空格 = 生成新洞　K = 全洞测试　L = 重开关卡");
            GUILayout.EndArea();
        }
    }
}
