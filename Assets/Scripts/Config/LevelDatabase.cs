using UnityEngine;

namespace MendingtheSky
{
    /// <summary>
    /// 关卡数据库。创建方式：Project 窗口右键 → Create → MendingtheSky → Level Database。
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "MendingtheSky/Level Database")]
    public class LevelDatabase : ScriptableObject
    {
        public LevelConfig[] levels;

        public LevelConfig GetLevel(int index)
        {
            if (levels == null || index < 0 || index >= levels.Length)
            {
                return null;
            }
            return levels[index];
        }

        public int GetTotalLevels()
        {
            return levels == null ? 0 : levels.Length;
        }
    }
}
