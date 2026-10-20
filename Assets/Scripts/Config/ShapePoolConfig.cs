using System.Collections.Generic;
using UnityEngine;

namespace MendingtheSky
{
    /// <summary>形状池配置。创建：右键 → Create → MendingtheSky → Shape Pool。</summary>
    [CreateAssetMenu(fileName = "ShapePoolConfig", menuName = "MendingtheSky/Shape Pool")]
    public class ShapePoolConfig : ScriptableObject
    {
        public List<ShapeEntry> shapes = new List<ShapeEntry>();

        public List<ShapeEntry> GetShapes()
        {
            return shapes ?? (shapes = new List<ShapeEntry>());
        }
    }
}
