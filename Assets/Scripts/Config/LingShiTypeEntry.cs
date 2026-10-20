using System;

namespace MendingtheSky
{
    /// <summary>灵石种类条目：种类配置 + 随机权重。</summary>
    [Serializable]
    public class LingShiTypeEntry
    {
        public LingShiTypeConfig typeConfig;
        public float weight = 1f;
    }
}
