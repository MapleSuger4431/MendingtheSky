using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>形状池条目：一组格子（以 (0,0) 为锚）+ 权重。建议用 class 语义（struct 含 List 会共享引用）。</summary>
    [System.Serializable]
    public class ShapeEntry
    {
        public List<Vector2Int> cells = new List<Vector2Int>();
        public float weight = 1f;
    }
}
