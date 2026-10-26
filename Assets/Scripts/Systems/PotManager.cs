using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 锅（熬煮区，设计文档 5.15 + 2.5 熬煮规则）。
    ///
    /// 规则：
    /// - 只接受 InBasket（未煮）状态的灵石；
    /// - 容量有限（LevelConfig.potMaxCount），Cooking 与「煮好未拖走」的 Cooked 都占容量；
    /// - 放入后进入 Cooking，熬煮过程中不可拖拽（CanRotate/CanDragToGrid 均为 false）；
    /// - 熬到 typeConfig.cookTime 后自动变 Cooked（发光贴图），玩家再拖到工作台；
    /// - 不能从锅拖回背篓（拾取 Cooking 状态会被 DragController 拒绝）；
    /// - 灵石以物理方式堆在锅腔内，DragController 负责拾取时的移除登记。
    ///
    /// 驱动方式：由 GameManager 在 Playing 状态下每帧调用 Tick(dt)。
    /// </summary>
    public class PotManager : MonoBehaviour
    {
        [Header("区域与生成")]
        [SerializeField] private Transform root;              // 灵石父节点（通常在 GameManager 下建一个 PotRoot）
        [SerializeField] private Transform potSpawnPoint;     // 锅口生成点（灵石从上方落入锅腔）
        [SerializeField] private Collider2D potAreaCollider;  // 锅区域触发器：判定指针是否在锅上方
        [SerializeField] private float itemScale = 0.7f;      // 锅内灵石显示缩放（与背篓一致）
        [SerializeField] private float spawnJitter = 0.12f;   // 生成点水平随机偏移，避免完全重叠

        [Header("容量（运行时由 LevelConfig.potMaxCount 覆盖）")]
        [SerializeField] private int maxCount = 3;

        /// <summary>锅内的灵石（Cooking 与 Cooked 混合存放，容量按总数计算）。</summary>
        private readonly List<LingShi> potLingShi = new List<LingShi>();
        private readonly System.Random random = new System.Random();

        public int GetCount() { return potLingShi.Count; }
        public int GetMaxCount() { return maxCount; }

        /// <summary>是否还有空位（正在熬 + 煮好未拖走都算占用）。</summary>
        public bool HasSpace() { return potLingShi.Count < maxCount; }

        /// <summary>
        /// 尝试把一块未煮灵石放入锅。
        /// 失败条件：空引用 / 不是 InBasket / 锅已满。
        /// 成功时：登记到列表、切 Cooking 状态、物理落进锅腔。
        /// </summary>
        public bool TryAddLingShi(LingShi ls)
        {
            if (ls == null)
            {
                return false;
            }
            if (ls.GetState() != LingShiState.InBasket)
            {
                return false;
            }
            if (!HasSpace())
            {
                return false;
            }
            potLingShi.Add(ls);
            ls.StartCooking(); // 状态切 Cooking（视觉使用普通贴图），开始累计 cookElapsed
            ls.SetTargetScale(itemScale);
            ls.SetTransparent(false);
            ls.SetPhysicsBodyType(RigidbodyType2D.Dynamic);
            ls.SetSimulated(true);
            SpawnAtPotTop(ls);
            return true;
        }

        /// <summary>
        /// 从锅的登记表移除（玩家把煮好的灵石拖出锅时调用）。
        /// 不销毁对象、不改变状态——调用方负责后续（拖拽/暂存/放回背篓）。
        /// </summary>
        public void RemoveLingShi(LingShi ls)
        {
            potLingShi.Remove(ls);
        }

        /// <summary>查询某块灵石是否登记在锅里（拾取时先移除登记用）。</summary>
        public bool Contains(LingShi ls)
        {
            return potLingShi.Contains(ls);
        }

        /// <summary>返回锅内的全部灵石副本（可解性校验收集未放置灵石时使用）。</summary>
        public List<LingShi> GetAllPieces()
        {
            return new List<LingShi>(potLingShi);
        }

        /// <summary>
        /// 每帧驱动熬煮。只处理 Cooking 状态；到时间自动切 Cooked。
        /// 煮好后仍留在锅里占容量，直到玩家拖走。
        /// </summary>
        public void Tick(float dt)
        {
            for (int i = potLingShi.Count - 1; i >= 0; i--)
            {
                LingShi ls = potLingShi[i];
                if (ls == null)
                {
                    potLingShi.RemoveAt(i);
                    continue;
                }
                if (ls.GetState() == LingShiState.Cooking)
                {
                    ls.UpdateCooking(dt); // 计时达标后内部 SetState(Cooked) 并刷新发光贴图
                }
            }
        }

        /// <summary>获取已煮好的灵石（可拖到工作台）。</summary>
        public List<LingShi> GetCookedLingShi()
        {
            var result = new List<LingShi>();
            foreach (LingShi ls in potLingShi)
            {
                if (ls != null && ls.GetState() == LingShiState.Cooked)
                {
                    result.Add(ls);
                }
            }
            return result;
        }

        /// <summary>获取仍在熬煮的灵石（不可拖拽）。</summary>
        public List<LingShi> GetCookingLingShi()
        {
            var result = new List<LingShi>();
            foreach (LingShi ls in potLingShi)
            {
                if (ls != null && ls.GetState() == LingShiState.Cooking)
                {
                    result.Add(ls);
                }
            }
            return result;
        }

        /// <summary>指针（世界坐标）是否落在锅区域上。DragController 的 OnMouseUp 分支①使用。</summary>
        public bool IsInPotArea(Vector3 worldPosition)
        {
            return potAreaCollider != null && potAreaCollider.OverlapPoint(worldPosition);
        }

        /// <summary>清空锅（重开关卡时调用）：销毁锅内灵石对象并清列表。</summary>
        public void Clear()
        {
            foreach (LingShi ls in potLingShi)
            {
                if (ls == null)
                {
                    continue;
                }
                // EditMode（未播放）下 Unity 不允许 Destroy，必须用 DestroyImmediate，否则单测报错。
                if (Application.isPlaying)
                {
                    Destroy(ls.gameObject);
                }
                else
                {
                    DestroyImmediate(ls.gameObject);
                }
            }
            potLingShi.Clear();
        }

        /// <summary>由 GameManager 在关卡开始时按 LevelConfig.potMaxCount 设置容量。</summary>
        public void SetMaxCount(int value)
        {
            maxCount = Mathf.Max(1, value);
        }

        /// <summary>在锅口上方生成（带少量水平随机偏移），让灵石自然落进锅腔。</summary>
        private void SpawnAtPotTop(LingShi ls)
        {
            if (potSpawnPoint == null)
            {
                return;
            }
            Vector3 pos = potSpawnPoint.position;
            pos.x += ((float)random.NextDouble() - 0.5f) * spawnJitter * 2f;
            ls.transform.position = pos;
            ls.SetVelocity(new Vector2(((float)random.NextDouble() - 0.5f) * 0.3f, 0f));
            if (root != null)
            {
                ls.transform.SetParent(root, true);
            }
        }
    }
}
